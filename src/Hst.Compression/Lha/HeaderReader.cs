namespace Hst.Compression.Lha
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Reads lha headers level 0, 1, 2 and 3.
    /// </summary>
    /// <remarks>
    /// Layout of the fields common to all header levels:
    /// offset 2: method (5 bytes), 7: packed size (4 bytes), 11: unpacked size (4 bytes), 15: time (4 bytes),
    /// 19: attribute, 20: header level.
    /// Level 0 and 1 starts with header size (1 byte) and checksum (1 byte), followed by name length and name at
    /// offset 21, crc and for level 1 os type and size of first extended header.
    /// Level 2 starts with total header size (2 bytes) followed by crc at offset 21, os type at 23 and extended headers
    /// at 24. Level 3 starts with word size 4 (2 bytes) followed by crc at offset 21, os type at 23, total header size
    /// at 24 (4 bytes) and extended headers at 28.
    /// Extended headers are a list of records with size, type and data, terminated by size 0.
    /// </remarks>
    internal class HeaderReader
    {
        private const int PREFIX_SIZE = 22;
        private const int LEVEL0_MIN_SIZE = 20;
        private const int LEVEL1_MIN_SIZE = 25;
        private const int LEVEL2_MIN_SIZE = 26;
        private const int LEVEL3_BASE_SIZE = 32;
        private const int MAX_HEADER_SIZE = 1024 * 1024;

        private const byte EXT_COMMON = 0x00;
        private const byte EXT_FILENAME = 0x01;
        private const byte EXT_PATH = 0x02;
        private const byte EXT_MSDOS_ATTRIBUTE = 0x40;
        private const byte EXT_WINDOWS_TIME = 0x41;
        private const byte EXT_FILE_SIZE = 0x42;
        private const byte EXT_UNIX_MODE = 0x50;
        private const byte EXT_UNIX_GID_UID = 0x51;
        private const byte EXT_UNIX_GROUP = 0x52;
        private const byte EXT_UNIX_USER = 0x53;
        private const byte EXT_UNIX_TIME = 0x54;
        private const byte EXT_OS9 = 0xcc;

        private const ushort DEFAULT_UNIX_MODE = LhaUnixMode.Regular | 0x1b6; /* 0666 */

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly Encoding encoding;

        /// <summary>
        /// Fields read from a header.
        /// </summary>
        private class Fields
        {
            public byte[] Data;
            public int Level;
            public string Method;
            public long PackedSize;
            public long UnpackedSize;
            public DateTime Date;
            public DateTime? WindowsDate;
            public int Attributes;
            public bool HasCrc;
            public ushort Crc;
            public byte OsType;
            public string Directory;
            public string FileName;
            public string LinkTarget;
            public ushort? UnixMode;
            public ushort? Os9Mode;
            public ushort UnixUserId;
            public ushort UnixGroupId;
            public string UnixUserName;
            public string UnixGroupName;
            public int? HeaderCrcOffset;
            public ushort HeaderCrc;
        }

        public HeaderReader(LhaOptions options)
        {
            encoding = options.Encoding;
        }

        /// <summary>
        /// Read header from stream.
        /// </summary>
        /// <param name="stream">Stream positioned at start of header.</param>
        /// <returns>Header read or null, if end of archive is reached.</returns>
        public async Task<LhaEntry> Read(Stream stream)
        {
            var headerOffset = stream.Position;

            var prefix = new byte[PREFIX_SIZE];
            var prefixLength = await ReadFully(stream, prefix, 0, prefix.Length);

            // archive ends, when there's no room for another header or with a 0 byte (level 2 headers are padded to
            // never start with 0). The end may be followed by padding like ctrl-z in pmarc archives.
            if (prefixLength < PREFIX_SIZE || prefix[0] == 0)
            {
                return null;
            }

            var fields = new Fields { Data = prefix, Level = prefix[20] };
            switch (fields.Level)
            {
                case 0:
                case 1:
                    await ReadLevel0Or1(stream, fields);
                    break;
                case 2:
                    await ReadLevel2(stream, fields);
                    break;
                case 3:
                    await ReadLevel3(stream, fields);
                    break;
                default:
                    throw new IOException($"Unknown header level {fields.Level}");
            }

            ApplyQuirks(fields);

            if (fields.HeaderCrcOffset.HasValue)
            {
                VerifyHeaderCrc(fields);
            }

            return CreateEntry(fields, headerOffset);
        }

        private static async Task<int> ReadFully(Stream stream, byte[] buffer, int offset, int count)
        {
            var total = 0;
            while (total < count)
            {
                var read = await stream.ReadAsync(buffer, offset + total, count - total);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            return total;
        }

        /// <summary>
        /// Read more bytes of header and append them to header data.
        /// </summary>
        private static async Task Append(Stream stream, Fields fields, int count)
        {
            if (count < 0 || fields.Data.Length + count > MAX_HEADER_SIZE)
            {
                throw new IOException("Invalid header size");
            }

            var offset = fields.Data.Length;
            var data = fields.Data;
            Array.Resize(ref data, offset + count);
            fields.Data = data;

            if (await ReadFully(stream, data, offset, count) != count)
            {
                throw new IOException("Unexpected end of header");
            }
        }

        private static ushort UInt16(byte[] data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));

        private static uint UInt32(byte[] data, int offset) => UInt16(data, offset) | ((uint)UInt16(data, offset + 2) << 16);

        private static ulong UInt64(byte[] data, int offset) => UInt32(data, offset) | ((ulong)UInt32(data, offset + 4) << 32);

        private static void ReadCommonFields(Fields fields)
        {
            var data = fields.Data;
            fields.Method = Encoding.ASCII.GetString(data, 2, 5);
            fields.PackedSize = UInt32(data, 7);
            fields.UnpackedSize = UInt32(data, 11);
            fields.Attributes = data[19];
        }

        private async Task ReadLevel0Or1(Stream stream, Fields fields)
        {
            int size = fields.Data[0];
            var checksum = fields.Data[1];
            var minSize = fields.Level == 0 ? LEVEL0_MIN_SIZE : LEVEL1_MIN_SIZE;
            if (size < minSize)
            {
                throw new IOException("Invalid header size");
            }

            await Append(stream, fields, size + 2 - fields.Data.Length);

            var data = fields.Data;
            var sum = 0;
            for (var i = 2; i < data.Length; i++)
            {
                sum += data[i];
            }

            if ((sum & 0xff) != checksum)
            {
                throw new IOException("Checksum error");
            }

            ReadCommonFields(fields);
            fields.Date = FromDosTime(UInt32(data, 15));

            int nameLength = data[21];
            if (minSize + nameLength > size)
            {
                throw new IOException("Invalid header size");
            }

            SetLevel0Name(fields, 22, nameLength);

            // crc is missing in level 0 headers created by old versions of LHarc
            var crcOffset = 22 + nameLength;
            fields.HasCrc = size >= crcOffset;
            if (fields.HasCrc)
            {
                fields.Crc = UInt16(data, crcOffset);
            }

            if (fields.Level == 0)
            {
                fields.OsType = LhaOsType.Generic;
                var extendedAreaSize = size - crcOffset;
                if (extendedAreaSize > 0)
                {
                    ReadLevel0ExtendedArea(fields, crcOffset + 2, extendedAreaSize);
                }

                return;
            }

            fields.OsType = data[crcOffset + 2];

            // extended headers follow base header, where packed size includes the size of extended headers
            var extendedHeadersOffset = data.Length - 2;
            int nextSize;
            while ((nextSize = UInt16(fields.Data, fields.Data.Length - 2)) != 0)
            {
                await Append(stream, fields, nextSize);

                if (fields.PackedSize < nextSize || nextSize < 3)
                {
                    throw new IOException("Invalid extended header size");
                }

                fields.PackedSize -= nextSize;
            }

            ReadExtendedHeaders(fields, extendedHeadersOffset, 2);
        }

        private async Task ReadLevel2(Stream stream, Fields fields)
        {
            int size = UInt16(fields.Data, 0);
            if (size < LEVEL2_MIN_SIZE)
            {
                throw new IOException("Invalid header size");
            }

            await Append(stream, fields, size - fields.Data.Length);

            ReadCommonFields(fields);
            fields.Date = FromUnixTime(UInt32(fields.Data, 15));
            fields.HasCrc = true;
            fields.Crc = UInt16(fields.Data, 21);
            fields.OsType = fields.Data[23];

            // LHA for OS-9/68k writes level 2 header size without the size field itself
            if (fields.OsType == LhaOsType.Os68K)
            {
                await Append(stream, fields, 2);
            }

            ReadExtendedHeaders(fields, 24, 2);
        }

        private async Task ReadLevel3(Stream stream, Fields fields)
        {
            if (UInt16(fields.Data, 0) != 4)
            {
                throw new IOException("Invalid header word size");
            }

            await Append(stream, fields, LEVEL3_BASE_SIZE - fields.Data.Length);

            var size = UInt32(fields.Data, 24);
            if (size > MAX_HEADER_SIZE || size < LEVEL3_BASE_SIZE)
            {
                throw new IOException("Invalid header size");
            }

            await Append(stream, fields, (int)size - fields.Data.Length);

            ReadCommonFields(fields);
            fields.Date = FromUnixTime(UInt32(fields.Data, 15));
            fields.HasCrc = true;
            fields.Crc = UInt16(fields.Data, 21);
            fields.OsType = fields.Data[23];

            ReadExtendedHeaders(fields, 28, 4);
        }

        /// <summary>
        /// Set directory and file name from name in level 0 and 1 header using '\' or '/' as path separator.
        /// </summary>
        private void SetLevel0Name(Fields fields, int offset, int length)
        {
            // empty name is used for directories
            if (length == 0)
            {
                return;
            }

            SetPath(fields, encoding.GetString(fields.Data, offset, length).Replace('\\', '/'));
        }

        /// <summary>
        /// Set directory and file name from path, which is split at last path separator.
        /// </summary>
        private static void SetPath(Fields fields, string path)
        {
            var separator = path.LastIndexOf('/');
            if (separator < 0)
            {
                fields.FileName = path;
                return;
            }

            fields.Directory = path.Substring(0, separator + 1);
            fields.FileName = path.Substring(separator + 1);
        }

        /// <summary>
        /// Read extended area of level 0 header, which is used by unix, OS-9/68k and OS-9 versions of lha.
        /// </summary>
        private static void ReadLevel0ExtendedArea(Fields fields, int offset, int length)
        {
            // pmarc stores comments in extended area
            if (fields.Method.StartsWith("-pm", StringComparison.Ordinal))
            {
                return;
            }

            var data = fields.Data;
            switch (data[offset])
            {
                case LhaOsType.Unix:
                case LhaOsType.Os68K:
                    // os type, minor version 0, unix time, then mode, uid and gid at the end. OS-9/68k includes the
                    // time twice, so mode, uid and gid are read from the end.
                    if (length < 12 || data[offset + 1] != 0)
                    {
                        return;
                    }

                    var end = offset + length;
                    fields.OsType = data[offset];
                    fields.Date = FromUnixTime(UInt32(data, offset + 2));
                    fields.UnixMode = UInt16(data, end - 6);
                    fields.UnixUserId = UInt16(data, end - 4);
                    fields.UnixGroupId = UInt16(data, end - 2);
                    break;
                case LhaOsType.Os9:
                    // os type, mode, ... with OS-9 extended header type at offset 9 and mode repeated at offset 17
                    if (length < 22 || data[offset + 9] != EXT_OS9 || data[offset + 1] != data[offset + 17] ||
                        data[offset + 2] != data[offset + 18])
                    {
                        return;
                    }

                    fields.OsType = LhaOsType.Os9;
                    fields.Os9Mode = UInt16(data, offset + 1);
                    break;
            }
        }

        /// <summary>
        /// Read list of extended headers. Each record has size including size field, type and data.
        /// </summary>
        private void ReadExtendedHeaders(Fields fields, int offset, int sizeFieldLength)
        {
            var data = fields.Data;
            while (offset + sizeFieldLength <= data.Length)
            {
                var size = sizeFieldLength == 4 ? UInt32(data, offset) : UInt16(data, offset);
                if (size == 0)
                {
                    break;
                }

                // record must contain size and type and must be followed by size field of next record
                if (size < sizeFieldLength + 1 || offset + size + sizeFieldLength > data.Length)
                {
                    throw new IOException("Invalid extended header size");
                }

                var typeOffset = offset + sizeFieldLength;
                ReadExtendedHeader(fields, data[typeOffset], typeOffset + 1, (int)size - sizeFieldLength - 1);
                offset += (int)size;
            }
        }

        private void ReadExtendedHeader(Fields fields, byte type, int offset, int length)
        {
            var data = fields.Data;
            switch (type)
            {
                case EXT_COMMON when length >= 2:
                    // crc of complete header calculated with crc field set to 0
                    fields.HeaderCrcOffset = offset;
                    fields.HeaderCrc = UInt16(data, offset);
                    break;
                case EXT_FILENAME when length >= 1:
                    // file name can't contain path separators
                    fields.FileName = encoding.GetString(data, offset, length).Replace('/', '_');
                    break;
                case EXT_PATH when length >= 1:
                    fields.Directory = ReadDirectory(data, offset, length);
                    break;
                case EXT_MSDOS_ATTRIBUTE when length >= 2:
                    fields.Attributes = UInt16(data, offset);
                    break;
                case EXT_WINDOWS_TIME when length >= 24:
                    // creation, last modified and last access time as windows file times
                    fields.WindowsDate = FromWindowsTime(UInt64(data, offset + 8));
                    break;
                case EXT_FILE_SIZE when length >= 16:
                    fields.PackedSize = (long)UInt64(data, offset);
                    fields.UnpackedSize = (long)UInt64(data, offset + 8);
                    break;
                case EXT_UNIX_MODE when length >= 2:
                    fields.UnixMode = UInt16(data, offset);
                    break;
                case EXT_UNIX_GID_UID when length >= 4:
                    fields.UnixGroupId = UInt16(data, offset);
                    fields.UnixUserId = UInt16(data, offset + 2);
                    break;
                case EXT_UNIX_GROUP when length >= 1:
                    fields.UnixGroupName = encoding.GetString(data, offset, length).TrimEnd('\0');
                    break;
                case EXT_UNIX_USER when length >= 1:
                    fields.UnixUserName = encoding.GetString(data, offset, length).TrimEnd('\0');
                    break;
                case EXT_UNIX_TIME when length >= 4:
                    fields.Date = FromUnixTime(UInt32(data, offset));
                    break;
                case EXT_OS9 when length >= 12:
                    fields.Os9Mode = UInt16(data, offset + 7);
                    break;
            }
        }

        /// <summary>
        /// Read directory from path extended header with 0xff as path separator. Each directory is decoded
        /// separately, as 0xff isn't a valid character in all encodings. Returned directory ends with '/'.
        /// </summary>
        private string ReadDirectory(byte[] data, int offset, int length)
        {
            var directory = new StringBuilder();
            var start = offset;
            var end = offset + length;
            for (var i = offset; i <= end; i++)
            {
                // a missing separator at the end (written by Amiga LHA 1.22) is treated as present
                if (i < end && data[i] != 0xff)
                {
                    continue;
                }

                if (i == end && start == end)
                {
                    break;
                }

                directory.Append(encoding.GetString(data, start, i - start)).Append('/');
                start = i + 1;
            }

            return directory.ToString();
        }

        /// <summary>
        /// Apply fixes and conversions for archives created by various lha tools.
        /// </summary>
        private static void ApplyQuirks(Fields fields)
        {
            // some Amiga archives store directories as -lh0- without name
            if (fields.OsType == LhaOsType.Amiga && fields.Method == LhaMethod.Lh0 && fields.UnpackedSize == 0 &&
                fields.FileName == null)
            {
                fields.Method = LhaMethod.Lhd;
            }

            // symbolic links are stored as -lhd- with name "link|target", which can be split over directory and
            // file name
            if (fields.Method == LhaMethod.Lhd && fields.UnixMode.HasValue &&
                (fields.UnixMode.Value & LhaUnixMode.TypeMask) == LhaUnixMode.Symlink &&
                (fields.Directory != null || fields.FileName != null))
            {
                var link = string.Concat(fields.Directory, fields.FileName);
                var separator = link.IndexOf('|');
                if (separator < 0)
                {
                    throw new IOException($"Invalid symbolic link \"{link}\"");
                }

                fields.LinkTarget = link.Substring(separator + 1);
                fields.Directory = null;
                fields.FileName = null;
                SetPath(fields, link.Substring(0, separator));
            }

            if (fields.Directory != null)
            {
                fields.Directory = NormalizeDirectory(fields.Directory);
            }

            // unix mode written by OS-9/68k is an OS-9 mode
            if (fields.OsType == LhaOsType.Os68K && fields.UnixMode.HasValue)
            {
                fields.Os9Mode = fields.UnixMode;
            }

            if (fields.Os9Mode.HasValue)
            {
                fields.UnixMode = FromOs9Mode(fields.Os9Mode.Value);
            }

            // LHARK uses -lh7- for its own method, which is incompatible with -lh7-
            if (fields.Level == 1 && fields.OsType == LhaOsType.Lhark && fields.Method == LhaMethod.Lh7)
            {
                fields.Method = LhaMethod.Lk7;
            }
        }

        /// <summary>
        /// Normalize directory by removing empty and "." directories and resolving ".." directories without going
        /// above the top, so a directory can't refer to locations outside the extraction directory.
        /// A leading '/' is preserved.
        /// </summary>
        private static string NormalizeDirectory(string directory)
        {
            var absolute = directory.StartsWith("/", StringComparison.Ordinal);
            var parts = new List<string>();
            foreach (var part in directory.Split('/'))
            {
                switch (part)
                {
                    case "":
                    case ".":
                        break;
                    case "..":
                        if (parts.Count > 0)
                        {
                            parts.RemoveAt(parts.Count - 1);
                        }

                        break;
                    default:
                        parts.Add(part);
                        break;
                }
            }

            var normalized = new StringBuilder(absolute ? "/" : string.Empty);
            foreach (var part in parts)
            {
                normalized.Append(part).Append('/');
            }

            return normalized.ToString();
        }

        /// <summary>
        /// Convert OS-9 mode with owner and public permissions to unix mode, where public permissions are used for
        /// both group and others.
        /// </summary>
        private static ushort FromOs9Mode(ushort os9Mode)
        {
            // OS-9 permission bits are read, write, execute from lowest bit, unix bits are in opposite order
            var owner = ToUnixPermissions(os9Mode & 0x07);
            var others = ToUnixPermissions((os9Mode >> 3) & 0x07);
            var directory = (os9Mode & 0x80) != 0 ? LhaUnixMode.Directory : 0;

            return (ushort)(directory | (owner << 6) | (others << 3) | others);
        }

        private static int ToUnixPermissions(int os9Permissions) =>
            ((os9Permissions & 1) << 2) | (os9Permissions & 2) | ((os9Permissions & 4) >> 2);

        private static void VerifyHeaderCrc(Fields fields)
        {
            var offset = fields.HeaderCrcOffset.Value;
            var data = fields.Data;
            data[offset] = 0;
            data[offset + 1] = 0;

            if (Crc16.Update(0, data, 0, data.Length) != fields.HeaderCrc)
            {
                throw new IOException("Header CRC error");
            }
        }

        private static LhaEntry CreateEntry(Fields fields, long headerOffset)
        {
            return new LhaEntry
            {
                HeaderOffset = headerOffset,
                HeaderSize = fields.Data.Length,
                HeaderLevel = fields.Level,
                HeaderCrc = (short)fields.HeaderCrc,
                Method = fields.Method,
                Name = string.Concat(fields.Directory, fields.FileName).Replace('/', '\\'),
                LinkTarget = fields.LinkTarget?.Replace('/', '\\'),
                PackedSize = fields.PackedSize,
                UnpackedSize = fields.UnpackedSize,
                HasCrc = fields.HasCrc,
                Crc = (short)fields.Crc,
                Attributes = fields.Attributes,
                OsType = fields.OsType,
                // windows time is more precise than ms-dos time in level 0 and 1 headers
                Date = fields.WindowsDate.HasValue && fields.Level < 2 ? fields.WindowsDate.Value : fields.Date,
                UnixMode = fields.UnixMode ?? DEFAULT_UNIX_MODE,
                UnixUserId = fields.UnixUserId,
                UnixGroupId = fields.UnixGroupId,
                UnixUserName = fields.UnixUserName,
                UnixGroupName = fields.UnixGroupName
            };
        }

        /// <summary>
        /// Convert ms-dos date and time. Invalid dates are returned as 1980-01-01.
        /// </summary>
        private static DateTime FromDosTime(uint value)
        {
            var year = 1980 + (int)(value >> 25);
            var month = (int)((value >> 21) & 0x0f);
            var day = (int)((value >> 16) & 0x1f);
            var hour = (int)((value >> 11) & 0x1f);
            var minute = (int)((value >> 5) & 0x3f);
            var second = (int)(value & 0x1f) * 2;

            try
            {
                return new DateTime(year, Math.Max(month, 1), Math.Max(day, 1), hour, minute, second,
                    DateTimeKind.Utc);
            }
            catch (ArgumentOutOfRangeException)
            {
                return new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }
        }

        private static DateTime FromUnixTime(uint value) => UnixEpoch.AddSeconds(value);

        private static DateTime FromWindowsTime(ulong value)
        {
            try
            {
                return DateTime.FromFileTimeUtc((long)value);
            }
            catch (ArgumentOutOfRangeException)
            {
                return UnixEpoch;
            }
        }
    }
}
