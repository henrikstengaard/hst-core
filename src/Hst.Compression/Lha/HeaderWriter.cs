namespace Hst.Compression.Lha
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Writes lha headers level 0, 1 and 2.
    ///
    /// Level 0 and 1 headers start with header size (1 byte) and checksum (1 byte) followed by method (5 bytes),
    /// packed size (4 bytes), original size (4 bytes), ms-dos timestamp (4 bytes), attribute (1 byte),
    /// header level (1 byte), name length (1 byte), name and crc (2 bytes).
    /// Level 0 headers can have a unix extended area. Level 1 headers continue with os type (1 byte) and
    /// extended headers.
    ///
    /// Level 2 headers start with header size (2 bytes) followed by method (5 bytes), packed size (4 bytes),
    /// original size (4 bytes), unix timestamp (4 bytes), reserved (1 byte), header level (1 byte), crc (2 bytes),
    /// os type (1 byte) and extended headers.
    ///
    /// Extended headers are preceded by their size (2 bytes) and contain type (1 byte) and data.
    /// </summary>
    internal class HeaderWriter
    {
        private const byte EXT_COMMON = 0x00;
        private const byte EXT_FILENAME = 0x01;
        private const byte EXT_PATH = 0x02;
        private const byte EXT_MSDOS_ATTRIBUTE = 0x40;

        // attribute field of level 1 and 2 headers is reserved and must be 0x20
        private const byte RESERVED_ATTRIBUTE = 0x20;
        private const byte EXT_UNIX_PERMISSION = 0x50;
        private const byte EXT_UNIX_UID_GID = 0x51;
        private const byte EXT_UNIX_TIMESTAMP = 0x54;

        private const int MAX_LEVEL_0_1_HEADER_SIZE = 255;
        private const int MAX_LEVEL_1_BASE_NAME_LENGTH = MAX_LEVEL_0_1_HEADER_SIZE - 25;

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly Encoding encoding;

        /// <summary>
        /// Create header writer. Unix timestamp, permissions, uid and gid are written for headers with operating
        /// system type unix. Headers with operating system type Amiga store attributes (Amiga protection bits) in the
        /// attribute field of level 1 and 2 headers too, like Amiga LhA.
        /// </summary>
        /// <param name="encoding">Encoding of names.</param>
        public HeaderWriter(Encoding encoding)
        {
            this.encoding = encoding;
        }

        private static bool HasUnixExtensions(LhaEntry header) => header.OsType == LhaOsType.Unix;

        private static byte BaseAttribute(LhaEntry header) =>
            header.OsType == LhaOsType.Amiga ? (byte)header.Attributes : RESERVED_ATTRIBUTE;

        /// <summary>
        /// Create bytes of header.
        /// </summary>
        public byte[] Write(LhaEntry header)
        {
            switch (header.HeaderLevel)
            {
                case 0:
                    return WriteLevel0(header);
                case 1:
                    return WriteLevel1(header);
                case 2:
                    return WriteLevel2(header);
                default:
                    throw new ArgumentException($"Header level {header.HeaderLevel} is not supported",
                        nameof(header));
            }
        }

        private static void WriteUInt16(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteUInt32(byte[] buffer, int offset, long value)
        {
            if (value < 0 || value > uint.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Value doesn't fit in header field");
            }

            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static uint ToDosTime(DateTime date)
        {
            if (date.Year < 1980)
            {
                date = new DateTime(1980, 1, 1);
            }
            else if (date.Year > 2107)
            {
                date = new DateTime(2107, 12, 31, 23, 59, 58);
            }

            return (uint)((date.Year - 1980) << 25 | date.Month << 21 | date.Day << 16 | date.Hour << 11 |
                          date.Minute << 5 | date.Second >> 1);
        }

        private static uint ToUnixTime(DateTime date)
        {
            var utc = date.Kind == DateTimeKind.Local ? date.ToUniversalTime() : date;
            var seconds = (long)(utc - UnixEpoch).TotalSeconds;
            return seconds < 0 ? 0 : seconds > uint.MaxValue ? uint.MaxValue : (uint)seconds;
        }

        /// <summary>
        /// Split name into directory path including last separator and filename. A comment after a zero character
        /// stays in the filename, also when it contains separators.
        /// </summary>
        private static void SplitName(string name, out string path, out string filename)
        {
            var commentIndex = name.IndexOf('\0');
            var pathAndName = commentIndex >= 0 ? name.Substring(0, commentIndex) : name;
            var separator = pathAndName.LastIndexOf('\\');
            path = separator >= 0 ? name.Substring(0, separator + 1) : string.Empty;
            filename = separator >= 0 ? name.Substring(separator + 1) : name;
        }

        /// <summary>
        /// Encode directory path using 0xff as separator.
        /// </summary>
        private byte[] EncodePath(string path)
        {
            var bytes = new List<byte>();
            foreach (var part in path.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
            {
                bytes.AddRange(encoding.GetBytes(part));
                bytes.Add(0xff);
            }

            return bytes.ToArray();
        }

        private static void WriteCommonFields(byte[] buffer, LhaEntry header, long packedSize, uint timestamp)
        {
            if (header.Method == null || header.Method.Length != 5)
            {
                throw new ArgumentException($"Invalid method \"{header.Method}\"", nameof(header));
            }

            Encoding.ASCII.GetBytes(header.Method, 0, 5, buffer, 2);
            WriteUInt32(buffer, 7, packedSize);
            WriteUInt32(buffer, 11, header.UnpackedSize);
            WriteUInt32(buffer, 15, timestamp);
            buffer[20] = (byte)header.HeaderLevel;
        }

        private static byte Checksum(byte[] buffer, int offset, int count)
        {
            var sum = 0;
            for (var i = offset; i < offset + count; i++)
            {
                sum += buffer[i];
            }

            return (byte)sum;
        }

        private byte[] WriteLevel0(LhaEntry header)
        {
            var name = encoding.GetBytes(header.Name);
            var unixExtensions = HasUnixExtensions(header);
            var extendedAreaSize = unixExtensions ? 12 : 0;
            var headerSize = 22 + name.Length + extendedAreaSize;

            if (headerSize > MAX_LEVEL_0_1_HEADER_SIZE)
            {
                throw new ArgumentException($"Name \"{header.Name}\" is too long for header level 0",
                    nameof(header));
            }

            var buffer = new byte[headerSize + 2];
            buffer[0] = (byte)headerSize;
            WriteCommonFields(buffer, header, header.PackedSize, ToDosTime(header.Date));
            buffer[19] = (byte)header.Attributes;
            buffer[21] = (byte)name.Length;
            Array.Copy(name, 0, buffer, 22, name.Length);

            var offset = 22 + name.Length;
            WriteUInt16(buffer, offset, (ushort)header.Crc);
            offset += 2;

            if (unixExtensions)
            {
                buffer[offset] = LhaOsType.Unix;
                buffer[offset + 1] = 0; // minor version
                WriteUInt32(buffer, offset + 2, ToUnixTime(header.Date));
                WriteUInt16(buffer, offset + 6, header.UnixMode);
                WriteUInt16(buffer, offset + 8, header.UnixUserId);
                WriteUInt16(buffer, offset + 10, header.UnixGroupId);
            }

            buffer[1] = Checksum(buffer, 2, headerSize);

            return buffer;
        }

        private List<(byte Type, byte[] Data)> CreateExtendedHeaders(LhaEntry header, string path,
            byte[] filename, bool unixTimestamp)
        {
            var extendedHeaders = new List<(byte, byte[])>();

            if (filename != null && filename.Length > 0)
            {
                extendedHeaders.Add((EXT_FILENAME, filename));
            }

            if (path.Length > 0)
            {
                extendedHeaders.Add((EXT_PATH, EncodePath(path)));
            }

            if (header.Attributes != RESERVED_ATTRIBUTE)
            {
                var attribute = new byte[2];
                WriteUInt16(attribute, 0, header.Attributes);
                extendedHeaders.Add((EXT_MSDOS_ATTRIBUTE, attribute));
            }

            if (HasUnixExtensions(header))
            {
                var permission = new byte[2];
                WriteUInt16(permission, 0, header.UnixMode);
                extendedHeaders.Add((EXT_UNIX_PERMISSION, permission));

                var uidGid = new byte[4];
                WriteUInt16(uidGid, 0, header.UnixGroupId);
                WriteUInt16(uidGid, 2, header.UnixUserId);
                extendedHeaders.Add((EXT_UNIX_UID_GID, uidGid));

                if (unixTimestamp)
                {
                    var timestamp = new byte[4];
                    WriteUInt32(timestamp, 0, ToUnixTime(header.Date));
                    extendedHeaders.Add((EXT_UNIX_TIMESTAMP, timestamp));
                }
            }

            return extendedHeaders;
        }

        /// <summary>
        /// Write extended headers each followed by size of next extended header. Size of first extended header is
        /// written at offset - 2.
        /// </summary>
        private static int WriteExtendedHeaders(byte[] buffer, int offset, List<(byte Type, byte[] Data)> headers)
        {
            WriteUInt16(buffer, offset - 2, headers.Count > 0 ? 3 + headers[0].Data.Length : 0);

            for (var i = 0; i < headers.Count; i++)
            {
                buffer[offset] = headers[i].Type;
                Array.Copy(headers[i].Data, 0, buffer, offset + 1, headers[i].Data.Length);
                offset += 1 + headers[i].Data.Length;
                WriteUInt16(buffer, offset, i + 1 < headers.Count ? 3 + headers[i + 1].Data.Length : 0);
                offset += 2;
            }

            return offset;
        }

        private static int ExtendedHeadersSize(List<(byte Type, byte[] Data)> headers)
        {
            var size = 0;
            foreach (var extendedHeader in headers)
            {
                size += 3 + extendedHeader.Data.Length;
            }

            return size;
        }

        private byte[] WriteLevel1(LhaEntry header)
        {
            SplitName(header.Name, out var path, out var filename);
            var filenameBytes = encoding.GetBytes(filename);

            // filename is stored in base header, if it fits. Otherwise it's stored in an extended header
            var baseName = filenameBytes.Length <= MAX_LEVEL_1_BASE_NAME_LENGTH ? filenameBytes : Array.Empty<byte>();
            var extendedHeaders = CreateExtendedHeaders(header, path,
                baseName.Length == 0 ? filenameBytes : null, true);

            var headerSize = 25 + baseName.Length;
            var extendedHeadersSize = ExtendedHeadersSize(extendedHeaders);
            var buffer = new byte[headerSize + 2 + extendedHeadersSize];

            buffer[0] = (byte)headerSize;

            // packed size includes extended headers for compatibility with level 0
            WriteCommonFields(buffer, header, header.PackedSize + extendedHeadersSize,
                ToDosTime(header.Date));
            buffer[19] = BaseAttribute(header);
            buffer[21] = (byte)baseName.Length;
            Array.Copy(baseName, 0, buffer, 22, baseName.Length);

            var offset = 22 + baseName.Length;
            WriteUInt16(buffer, offset, (ushort)header.Crc);
            buffer[offset + 2] = (byte)header.OsType;

            WriteExtendedHeaders(buffer, headerSize + 2, extendedHeaders);
            buffer[1] = Checksum(buffer, 2, headerSize);

            return buffer;
        }

        private byte[] WriteLevel2(LhaEntry header)
        {
            SplitName(header.Name, out var path, out var filename);

            var extendedHeaders = CreateExtendedHeaders(header, path, encoding.GetBytes(filename), false);

            // common header with crc of header is first
            extendedHeaders.Insert(0, (EXT_COMMON, new byte[2]));

            var headerSize = 26 + ExtendedHeadersSize(extendedHeaders);

            // first byte of header must not be 0, as it marks end of archive
            if ((headerSize & 0xff) == 0)
            {
                headerSize++;
            }

            if (headerSize > ushort.MaxValue)
            {
                throw new ArgumentException($"Header for \"{header.Name}\" is too large", nameof(header));
            }

            var buffer = new byte[headerSize];
            WriteUInt16(buffer, 0, headerSize);
            WriteCommonFields(buffer, header, header.PackedSize, ToUnixTime(header.Date));
            buffer[19] = BaseAttribute(header);
            WriteUInt16(buffer, 21, (ushort)header.Crc);
            buffer[23] = (byte)header.OsType;

            WriteExtendedHeaders(buffer, 26, extendedHeaders);

            // crc of header is calculated with crc field set to 0, common header data is at offset 27
            var headerCrc = Crc16.Update(0, buffer, 0, buffer.Length);
            WriteUInt16(buffer, 27, headerCrc);
            header.HeaderCrc = (short)headerCrc;

            return buffer;
        }

        /// <summary>
        /// Write header to stream.
        /// </summary>
        /// <returns>Size of header written.</returns>
        public int Write(LhaEntry header, Stream stream)
        {
            var bytes = Write(header);
            stream.Write(bytes, 0, bytes.Length);
            return bytes.Length;
        }
    }
}
