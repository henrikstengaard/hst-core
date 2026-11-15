// Ported from Lhasa lib/lha_file_header.c and lib/ext_header.c, https://github.com/fragglet/lhasa
//
// Copyright (c) 2011, 2012, Simon Howard
//
// Permission to use, copy, modify, and/or distribute this software
// for any purpose with or without fee is hereby granted, provided
// that the above copyright notice and this permission notice appear
// in all copies.
//
// THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL
// WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE
// AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR
// CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM
// LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT,
// NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN
// CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
//
namespace Hst.Compression.Lha
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Reads lha headers level 0, 1, 2 and 3.
    /// </summary>
    internal class HeaderReader
    {
        private const int COMMON_HEADER_LEN = 22; /* bytes */

        // Minimum length of a level 0 header (with zero-length filename and without crc).
        private const int LEVEL_0_MIN_HEADER_LEN_WITHOUT_CRC = 20; /* bytes */

        // Minimum length of a level 0 header (with zero-length filename).
        private const int LEVEL_0_MIN_HEADER_LEN = 22; /* bytes */

        // Minimum length of a level 1 base header (with zero-length filename).
        private const int LEVEL_1_MIN_HEADER_LEN = 25; /* bytes */

        // Length of a level 2 base header.
        private const int LEVEL_2_HEADER_LEN = 26; /* bytes */

        // Length of a level 3 base header.
        private const int LEVEL_3_HEADER_LEN = 32; /* bytes */

        // Maximum length of a level 3 header (including extended headers).
        private const int LEVEL_3_MAX_HEADER_LEN = 1024 * 1024; /* 1 MB */

        // Length of a level 0 Unix extended area.
        private const int LEVEL_0_UNIX_EXTENDED_LEN = 12; /* bytes */

        // Length of a level 0 OS-9 extended area.
        private const int LEVEL_0_OS9_EXTENDED_LEN = 22; /* bytes */

        // Extended header types:
        private const byte LHA_EXT_HEADER_COMMON = 0x00;
        private const byte LHA_EXT_HEADER_FILENAME = 0x01;
        private const byte LHA_EXT_HEADER_PATH = 0x02;
        private const byte LHA_EXT_HEADER_MSDOS_ATTRIBUTE = 0x40;
        private const byte LHA_EXT_HEADER_WINDOWS_TIMESTAMPS = 0x41;
        private const byte LHA_EXT_HEADER_FILE_SIZES = 0x42;
        private const byte LHA_EXT_HEADER_UNIX_PERMISSION = 0x50;
        private const byte LHA_EXT_HEADER_UNIX_UID_GID = 0x51;
        private const byte LHA_EXT_HEADER_UNIX_GROUP = 0x52;
        private const byte LHA_EXT_HEADER_UNIX_USER = 0x53;
        private const byte LHA_EXT_HEADER_UNIX_TIMESTAMP = 0x54;
        private const byte LHA_EXT_HEADER_OS9 = 0xcc;

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly Encoding encoding;

        /// <summary>
        /// State of header being decoded.
        /// </summary>
        private class FileHeader
        {
            public byte[] RawData = Array.Empty<byte>();
            public int RawDataLen;
            public int HeaderLevel;
            public string CompressMethod;
            public long CompressedLength;
            public long Length;
            public DateTime Timestamp;
            public int Attribute;
            public ushort Crc;
            public bool HasCrc;
            public byte OsType;
            public byte MinorVersion;
            public string Path;
            public string Filename;
            public string SymlinkTarget;
            public bool HasUnixPerms;
            public ushort UnixPerms;
            public bool HasUnixUidGid;
            public ushort UnixUid;
            public ushort UnixGid;
            public bool HasOs9Perms;
            public ushort Os9Perms;
            public bool HasCommonCrc;
            public ushort CommonCrc;
            public bool HasWindowsTimestamps;
            public ulong WinModificationTime;
            public string UnixUsername;
            public string UnixGroup;
        }

        public HeaderReader(LhaOptions options)
        {
            encoding = options.Encoding;
        }

        private static ushort DecodeUint16(byte[] buf, int offset) =>
            (ushort)(buf[offset] | (buf[offset + 1] << 8));

        private static uint DecodeUint32(byte[] buf, int offset) =>
            (uint)(buf[offset] | (buf[offset + 1] << 8) | (buf[offset + 2] << 16) | (buf[offset + 3] << 24));

        private static ulong DecodeUint64(byte[] buf, int offset) =>
            DecodeUint32(buf, offset) | ((ulong)DecodeUint32(buf, offset + 4) << 32);

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

        // Read some more data from the input stream, extending the raw_data
        // array (and the size of the header).
        private static async Task ExtendRawData(FileHeader header, Stream stream, int nbytes)
        {
            if (nbytes < 0 || nbytes > LEVEL_3_MAX_HEADER_LEN)
            {
                throw new IOException("Invalid header size");
            }

            var newRawLen = header.RawDataLen + nbytes;
            if (newRawLen > header.RawData.Length)
            {
                Array.Resize(ref header.RawData, newRawLen);
            }

            if (await ReadFully(stream, header.RawData, header.RawDataLen, nbytes) != nbytes)
            {
                throw new IOException("Unexpected end of header");
            }

            header.RawDataLen = newRawLen;
        }

        /// <summary>
        /// Decode MS-DOS timestamp. Date and time fields are returned as is.
        /// </summary>
        private static DateTime DecodeFtime(uint raw)
        {
            var second = (int)((raw << 1) & 0x3e);
            var minute = (int)((raw >> 5) & 0x3f);
            var hour = (int)((raw >> 11) & 0x1f);
            var day = (int)((raw >> 16) & 0x1f);
            var month = (int)((raw >> 21) & 0xf);
            var year = 1980 + (int)((raw >> 25) & 0x7f);

            try
            {
                return new DateTime(year, month == 0 ? 1 : month, day == 0 ? 1 : day, hour, minute, second,
                    DateTimeKind.Utc);
            }
            catch (ArgumentOutOfRangeException)
            {
                return new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }
        }

        private static DateTime DecodeUnixTime(uint timestamp) => UnixEpoch.AddSeconds(timestamp);

        private static DateTime DecodeWindowsTime(ulong fileTime)
        {
            try
            {
                return DateTime.FromFileTimeUtc((long)fileTime);
            }
            catch (ArgumentOutOfRangeException)
            {
                return UnixEpoch;
            }
        }

        /// <summary>
        /// Given a file header with the filename set, split it into separate
        /// path and filename components, if necessary.
        /// </summary>
        private static void SplitHeaderFilename(FileHeader header)
        {
            // Is there a directory separator in the path?  If so, we need to
            // split into directory name and filename.
            var sep = header.Filename.LastIndexOf('/');

            if (sep >= 0)
            {
                header.Path = header.Filename.Substring(0, sep + 1);
                header.Filename = header.Filename.Substring(sep + 1);
            }
        }

        // Perform checksum of header contents.
        private static bool CheckL0Checksum(byte[] header, int offset, int headerLen, int csum)
        {
            var result = 0;

            for (var i = offset; i < offset + headerLen; ++i)
            {
                result += header[i];
            }

            return (result & 0xff) == csum;
        }

        // Perform full-header CRC check, based on CRC from "common" extended header.
        private static bool CheckCommonCrc(FileHeader header)
        {
            var crc = Crc16.Update(0, header.RawData, 0, header.RawDataLen);

            return crc == header.CommonCrc;
        }

        // Process the OS-9 permissions field and translate into the equivalent
        // Unix permissions.
        private static void Os9ToUnixPermissions(FileHeader header)
        {
            // Translate into equivalent Unix permissions. OS-9 just has
            // owner and public, so double up public for the owner field.
            var or = (header.Os9Perms & 0x01) != 0 ? 1 : 0;
            var ow = (header.Os9Perms & 0x02) != 0 ? 1 : 0;
            var oe = (header.Os9Perms & 0x04) != 0 ? 1 : 0;
            var pr = (header.Os9Perms & 0x08) != 0 ? 1 : 0;
            var pw = (header.Os9Perms & 0x10) != 0 ? 1 : 0;
            var pe = (header.Os9Perms & 0x20) != 0 ? 1 : 0;
            var d = (header.Os9Perms & 0x80) != 0 ? 1 : 0;

            header.HasUnixPerms = true;
            header.UnixPerms = (ushort)((d << 14)
                                        | (or << 8) | (ow << 7) | (oe << 6) // owner
                                        | (pr << 5) | (pw << 4) | (pe << 3) // group
                                        | (pr << 2) | (pw << 1) | (pe << 0)); // everyone
        }

        // Parse a Unix symbolic link. These are stored in the format:
        // filename = symlink|target
        private static void ParseSymlink(FileHeader header)
        {
            // Although the format is always the same, some files have
            // symlink headers where the path is split between the path
            // and filename headers. For example:
            //    path = etc|../../
            //    filename = etc
            var fullpath = string.Concat(header.Path ?? string.Empty, header.Filename ?? string.Empty);

            var p = fullpath.IndexOf('|');

            if (p < 0)
            {
                throw new IOException($"Invalid symbolic link \"{fullpath}\"");
            }

            header.SymlinkTarget = fullpath.Substring(p + 1);

            // Cut the string in half at the separator. Keep the left side
            // as the value for filename.
            header.Path = null;
            header.Filename = fullpath.Substring(0, p);

            // Having joined path and filename together during processing,
            // we now have the opposite problem: header->filename might
            // contain a full path rather than just a filename. Split back
            // into two again.
            SplitHeaderFilename(header);
        }

        // Decode the path field in the header.
        private void ProcessLevel0Path(FileHeader header, int offset, int dataLen)
        {
            // Zero-length filename probably means that this is a directory
            // entry. Leave the filename field as NULL - this makes us
            // consistent with level 2/3 headers.
            if (dataLen == 0)
            {
                return;
            }

            var data = new byte[dataLen];
            Array.Copy(header.RawData, offset, data, 0, dataLen);

            // Convert MS-DOS path separators to Unix path separators.
            for (var i = 0; i < dataLen; ++i)
            {
                if (data[i] == '\\')
                {
                    data[i] = (byte)'/';
                }
            }

            header.Filename = encoding.GetString(data);

            SplitHeaderFilename(header);
        }

        // Decode an extended header.
        private void DecodeExtHeader(FileHeader header, byte num, int offset, int dataLen)
        {
            var raw = header.RawData;

            switch (num)
            {
                // Common header (0x00).
                //
                // This contains a 16-bit CRC of the entire LHA header.
                case LHA_EXT_HEADER_COMMON:
                    if (dataLen < 2)
                    {
                        return;
                    }

                    header.HasCommonCrc = true;
                    header.CommonCrc = DecodeUint16(raw, offset);

                    // There is a catch-22 in calculating the CRC, because the field
                    // containing the CRC is part of the data being CRC'd. The solution
                    // is that the CRC is calculated with the CRC field set to zero.
                    // Therefore, now that the CRC has been read, set the field to
                    // zero in the raw_data array so that the CRC can be calculated
                    // correctly.
                    raw[offset] = 0x00;
                    raw[offset + 1] = 0x00;
                    break;

                // Filename header (0x01).
                //
                // This stores the filename for the file. This is essential on level 2/3
                // headers, as the filename field is no longer part of the standard
                // header.
                case LHA_EXT_HEADER_FILENAME:
                    if (dataLen < 1)
                    {
                        return;
                    }

                    var filename = new byte[dataLen];
                    Array.Copy(raw, offset, filename, 0, dataLen);

                    // Sanitize the filename that was read. It is not allowed to
                    // contain a path separator, which could potentially be used
                    // to do something malicious.
                    for (var i = 0; i < filename.Length; ++i)
                    {
                        if (filename[i] == '/')
                        {
                            filename[i] = (byte)'_';
                        }
                    }

                    header.Filename = encoding.GetString(filename);
                    break;

                // Path header (0x02).
                //
                // This stores the directory path of the file. A value of 0xff is used
                // as the path separator. It is supposed to include a terminating path
                // separator as the last character.
                case LHA_EXT_HEADER_PATH:
                    if (dataLen < 1)
                    {
                        return;
                    }

                    // Amiga LHA v1.22 generates path headers without a path
                    // separator at the end of the string. This is broken (and
                    // was fixed in a later version), but handle it correctly.
                    var addSeparator = raw[offset + dataLen - 1] != 0xff;
                    var path = new byte[addSeparator ? dataLen + 1 : dataLen];
                    Array.Copy(raw, offset, path, 0, dataLen);
                    if (addSeparator)
                    {
                        path[dataLen] = 0xff;
                    }

                    // each path element is decoded separately and joined with '/', as 0xff path separators
                    // aren't valid characters in all encodings
                    var start = 0;
                    var builder = new StringBuilder();
                    for (var i = 0; i < path.Length; ++i)
                    {
                        if (path[i] != 0xff)
                        {
                            continue;
                        }

                        builder.Append(encoding.GetString(path, start, i - start)).Append('/');
                        start = i + 1;
                    }

                    header.Path = builder.ToString();
                    break;

                // MS-DOS attribute header (0x40).
                case LHA_EXT_HEADER_MSDOS_ATTRIBUTE:
                    if (dataLen < 2)
                    {
                        return;
                    }

                    header.Attribute = DecodeUint16(raw, offset);
                    break;

                // Windows timestamp header (0x41).
                //
                // This is a Windows-specific header that stores 64-bit timestamps in
                // Windows FILETIME format. The timestamps have 100ns accuracy, which is
                // much more accurate than the normal Unix time_t format.
                case LHA_EXT_HEADER_WINDOWS_TIMESTAMPS:
                    if (dataLen < 24)
                    {
                        return;
                    }

                    header.HasWindowsTimestamps = true;
                    header.WinModificationTime = DecodeUint64(raw, offset + 8);
                    break;

                // File sizes header (0x42).
                //
                // This contains 64-bit versions of the uncompressed/compressed file size
                // header fields, for storing very long files.
                case LHA_EXT_HEADER_FILE_SIZES:
                    if (dataLen < 16)
                    {
                        return;
                    }

                    header.CompressedLength = (long)DecodeUint64(raw, offset);
                    header.Length = (long)DecodeUint64(raw, offset + 8);
                    break;

                // Unix permissions header (0x50).
                case LHA_EXT_HEADER_UNIX_PERMISSION:
                    if (dataLen < 2)
                    {
                        return;
                    }

                    header.HasUnixPerms = true;
                    header.UnixPerms = DecodeUint16(raw, offset);
                    break;

                // Unix UID/GID header (0x51).
                case LHA_EXT_HEADER_UNIX_UID_GID:
                    if (dataLen < 4)
                    {
                        return;
                    }

                    header.HasUnixUidGid = true;
                    header.UnixGid = DecodeUint16(raw, offset);
                    header.UnixUid = DecodeUint16(raw, offset + 2);
                    break;

                // Unix group header (0x52).
                case LHA_EXT_HEADER_UNIX_GROUP:
                    if (dataLen < 1)
                    {
                        return;
                    }

                    header.UnixGroup = encoding.GetString(raw, offset, dataLen).TrimEnd('\0');
                    break;

                // Unix username header (0x53).
                case LHA_EXT_HEADER_UNIX_USER:
                    if (dataLen < 1)
                    {
                        return;
                    }

                    header.UnixUsername = encoding.GetString(raw, offset, dataLen).TrimEnd('\0');
                    break;

                // Unix timestamp header (0x54).
                //
                // This stores a 32-bit Unix time_t timestamp representing the
                // modification time of the file.
                case LHA_EXT_HEADER_UNIX_TIMESTAMP:
                    if (dataLen < 4)
                    {
                        return;
                    }

                    header.Timestamp = DecodeUnixTime(DecodeUint32(raw, offset));
                    break;

                // OS-9 (6809) header (0xcc)
                //
                // This stores OS-9 filesystem metadata.
                case LHA_EXT_HEADER_OS9:
                    if (dataLen < 12)
                    {
                        return;
                    }

                    header.Os9Perms = DecodeUint16(raw, offset + 7);
                    header.HasOs9Perms = true;
                    break;
            }
        }

        // Starting at the specified offset in the raw_data array, walk
        // through the list of extended headers and parse them.
        private void DecodeExtendedHeaders(FileHeader header, int offset)
        {
            // Level 3 headers use 32-bit length fields; all others use
            // 16-bit fields.
            var fieldSize = header.HeaderLevel == 3 ? 4 : 2;

            long availableLength = header.RawDataLen - offset - fieldSize;

            while (offset <= header.RawDataLen - fieldSize)
            {
                var extHeader = offset + fieldSize;
                long extHeaderLen = fieldSize == 4
                    ? DecodeUint32(header.RawData, offset)
                    : DecodeUint16(header.RawData, offset);

                // Header length zero indicates end of chain. Otherwise, sanity
                // check the header length is valid.
                if (extHeaderLen == 0)
                {
                    break;
                }

                if (extHeaderLen < fieldSize + 1 || extHeaderLen > availableLength)
                {
                    throw new IOException("Invalid extended header size");
                }

                // Process header:
                DecodeExtHeader(header, header.RawData[extHeader], extHeader + 1,
                    (int)extHeaderLen - fieldSize - 1);

                // Advance to next header.
                offset += (int)extHeaderLen;
                availableLength -= extHeaderLen;
            }
        }

        // Read extended headers for a level 1 header, extending the
        // raw_data block to include them.
        private static async Task ReadL1ExtendedHeaders(FileHeader header, Stream stream)
        {
            for (;;)
            {
                // Last two bytes of the header raw data contain the size
                // of the next header.
                var extHeaderLen = DecodeUint16(header.RawData, header.RawDataLen - 2);

                // Last header?
                if (extHeaderLen == 0)
                {
                    break;
                }

                await ExtendRawData(header, stream, extHeaderLen);

                // For backwards compatibility with level 0 headers,
                // the compressed length field is actually "compressed
                // length + length of all extended headers":
                if (header.CompressedLength < extHeaderLen)
                {
                    throw new IOException("Invalid extended header size");
                }

                header.CompressedLength -= extHeaderLen;

                // Must be at least 3 bytes - 1 byte header type
                // + 2 bytes for next header length
                if (extHeaderLen < 3)
                {
                    throw new IOException("Invalid extended header size");
                }
            }
        }

        // Process a level 0 Unix extended area.
        private static void ProcessLevel0UnixArea(FileHeader header, int offset, int dataLen)
        {
            var data = header.RawData;

            // A typical Unix extended area:
            //
            // 00000000  55 00 00 3b 3d 4b 80 81  e8 03 e8 03

            // Sanity check.
            if (dataLen < LEVEL_0_UNIX_EXTENDED_LEN || data[offset + 1] != 0x00)
            {
                return;
            }

            // OS-9/68k generates an extended area that is broadly compatible
            // with the Unix one.

            // Fill in the header fields from the data from the extended area.
            // There's one minor point to note here: OS-9/68k LHA includes the
            // timestamp twice - I have no idea why. In order to support both
            // variants, read the end fields from the end of the extended area.
            header.OsType = data[offset];
            header.MinorVersion = data[offset + 1];
            header.Timestamp = DecodeUnixTime(DecodeUint32(data, offset + 2));

            header.UnixPerms = DecodeUint16(data, offset + dataLen - 6);
            header.UnixUid = DecodeUint16(data, offset + dataLen - 4);
            header.UnixGid = DecodeUint16(data, offset + dataLen - 2);

            header.HasUnixPerms = true;
            header.HasUnixUidGid = true;
        }

        // Process a level 0 OS-9 extended area.
        private static void ProcessLevel0Os9Area(FileHeader header, int offset, int dataLen)
        {
            var data = header.RawData;

            // A typical OS-9 extended area:
            //
            // 00000000  39 13 00 00 c3 16 00 0f  00 cc 18 07 09 03 01 16
            // 00000010  00 13 00 00 00 00

            // Sanity checks:
            if (dataLen < LEVEL_0_OS9_EXTENDED_LEN
                || data[offset + 9] != 0xcc || data[offset + 1] != data[offset + 17] ||
                data[offset + 2] != data[offset + 18])
            {
                return;
            }

            // The contents resemble the contents of the OS-9 extended header.
            // We just want the permissions field.
            header.OsType = Constants.EXTEND_OS9;
            header.Os9Perms = DecodeUint16(data, offset + 1);
            header.HasOs9Perms = true;
        }

        // Handling for level 0 extended areas.
        private static void ProcessLevel0ExtendedArea(FileHeader header, int offset, int dataLen)
        {
            // PMarc archives can include comments that are stored in the
            // extended area. It is possible that this could conflict with
            // the logic below, so specifically exclude them.
            if (header.CompressMethod.StartsWith("-pm", StringComparison.Ordinal))
            {
                return;
            }

            // Different tools include different extended areas. Try to
            // identify which tool generated this one, based on the first
            // byte.
            switch (header.RawData[offset])
            {
                case Constants.EXTEND_UNIX:
                case Constants.EXTEND_OS68K:
                    ProcessLevel0UnixArea(header, offset, dataLen);
                    break;

                case Constants.EXTEND_OS9:
                    ProcessLevel0Os9Area(header, offset, dataLen);
                    break;
            }
        }

        // Decode a level 0 or 1 header.
        private async Task DecodeLevel0Header(FileHeader header, Stream stream)
        {
            var headerLen = header.RawData[0];
            var headerCsum = header.RawData[1];

            // Sanity check header length.  This is the minimum header length
            // for a header that has a zero-length path.
            var minLen = header.HeaderLevel == 0 ? LEVEL_0_MIN_HEADER_LEN_WITHOUT_CRC : LEVEL_1_MIN_HEADER_LEN;

            if (headerLen < minLen)
            {
                throw new IOException("Invalid header size");
            }

            // We only have a partial header so far. Read the full header.
            await ExtendRawData(header, stream, headerLen + 2 - header.RawDataLen);

            // Checksum the header.
            if (!CheckL0Checksum(header.RawData, 2, header.RawDataLen - 2, headerCsum))
            {
                throw new IOException("Checksum error");
            }

            // Compression method:
            header.CompressMethod = Encoding.ASCII.GetString(header.RawData, 2, 5);

            // File lengths:
            header.CompressedLength = DecodeUint32(header.RawData, 7);
            header.Length = DecodeUint32(header.RawData, 11);

            // Timestamp:
            header.Timestamp = DecodeFtime(DecodeUint32(header.RawData, 15));

            header.Attribute = header.RawData[19];

            // Read path.  Check path length field - is the header long enough
            // to hold this full path?
            var pathLen = header.RawData[21];

            if (minLen + pathLen > headerLen)
            {
                throw new IOException("Invalid header size");
            }

            // OS type?
            header.OsType = header.HeaderLevel == 0 ? Constants.EXTEND_GENERIC : header.RawData[24 + pathLen];

            // Read filename field:
            ProcessLevel0Path(header, 22, pathLen);

            // CRC field. Old LHarc level 0 headers doesn't have a crc field.
            header.HasCrc = headerLen >= LEVEL_0_MIN_HEADER_LEN + pathLen;
            if (header.HasCrc)
            {
                header.Crc = DecodeUint16(header.RawData, 22 + pathLen);
            }

            // Level 0 headers can contain extended data through different schemes
            // to the extended header system used in level 1+.
            if (header.HeaderLevel == 0 && headerLen > LEVEL_0_MIN_HEADER_LEN + pathLen)
            {
                ProcessLevel0ExtendedArea(header, LEVEL_0_MIN_HEADER_LEN + 2 + pathLen,
                    headerLen - LEVEL_0_MIN_HEADER_LEN - pathLen);
            }
        }

        private async Task DecodeLevel1Header(FileHeader header, Stream stream)
        {
            await DecodeLevel0Header(header, stream);

            // Level 1 headers can have extended headers, so parse them.
            var extHeaderStart = header.RawDataLen - 2;

            await ReadL1ExtendedHeaders(header, stream);
            DecodeExtendedHeaders(header, extHeaderStart);
        }

        private async Task DecodeLevel2Header(FileHeader header, Stream stream)
        {
            var headerLen = DecodeUint16(header.RawData, 0);

            if (headerLen < LEVEL_2_HEADER_LEN)
            {
                throw new IOException("Invalid header size");
            }

            // Read the full header.
            await ExtendRawData(header, stream, headerLen - header.RawDataLen);

            // Compression method:
            header.CompressMethod = Encoding.ASCII.GetString(header.RawData, 2, 5);

            // File lengths:
            header.CompressedLength = DecodeUint32(header.RawData, 7);
            header.Length = DecodeUint32(header.RawData, 11);

            // Timestamp. Unlike level 0/1, this is a Unix-style timestamp.
            header.Timestamp = DecodeUnixTime(DecodeUint32(header.RawData, 15));

            header.Attribute = header.RawData[19];

            // CRC.
            header.HasCrc = true;
            header.Crc = DecodeUint16(header.RawData, 21);

            // OS type:
            header.OsType = header.RawData[23];

            // LHA for OS-9/68k generates broken level 2 archives: the header
            // length field is the length of the remainder of the header, not
            // the complete header length. As a result it's two bytes too
            // short. We can use the OS type field to detect these archives
            // and compensate.
            if (header.OsType == Constants.EXTEND_OS68K)
            {
                await ExtendRawData(header, stream, 2);
            }

            DecodeExtendedHeaders(header, 24);
        }

        private async Task DecodeLevel3Header(FileHeader header, Stream stream)
        {
            // The first field at the start of a level 3 header is supposed to
            // indicate word size, with the idea being that the header format
            // can be extended beyond 32-bit words in the future. In practise,
            // nothing supports anything other than 32-bit (4 bytes), and neither
            // do we.
            if (DecodeUint16(header.RawData, 0) != 4)
            {
                throw new IOException("Invalid header word size");
            }

            // Read the full header.
            await ExtendRawData(header, stream, LEVEL_3_HEADER_LEN - header.RawDataLen);

            // Read the header length field (including extended headers), and
            // extend to this full length. Because this is a 32-bit value,
            // we must place a sensible limit on the amount of data that will
            // be read, to avoid possibly allocating gigabytes of memory.
            var headerLen = DecodeUint32(header.RawData, 24);

            if (headerLen > LEVEL_3_MAX_HEADER_LEN || headerLen < header.RawDataLen)
            {
                throw new IOException("Invalid header size");
            }

            await ExtendRawData(header, stream, (int)headerLen - header.RawDataLen);

            // Compression method:
            header.CompressMethod = Encoding.ASCII.GetString(header.RawData, 2, 5);

            // File lengths:
            header.CompressedLength = DecodeUint32(header.RawData, 7);
            header.Length = DecodeUint32(header.RawData, 11);

            // Unix-style timestamp.
            header.Timestamp = DecodeUnixTime(DecodeUint32(header.RawData, 15));

            header.Attribute = header.RawData[19];

            // CRC.
            header.HasCrc = true;
            header.Crc = DecodeUint16(header.RawData, 21);

            // OS type:
            header.OsType = header.RawData[23];

            DecodeExtendedHeaders(header, 28);
        }

        // "Collapse" a path down, by removing all instances of "." and ".."
        // paths. This is to protect against malicious archives that might include
        // ".." in a path to break out of the extract directory.
        private static string CollapsePath(string filename)
        {
            var prefix = string.Empty;

            // If the path starts with a /, it is an absolute path; skip over
            // that first character and don't remove it.
            if (filename.StartsWith("/", StringComparison.Ordinal))
            {
                prefix = "/";
                filename = filename.Substring(1);
            }

            var result = new StringBuilder();
            var currpath = 0;

            for (var r = 0; r < filename.Length; ++r)
            {
                result.Append(filename[r]);

                // Each time a new path separator is found, examine the
                // path that was just written.
                if (filename[r] != '/')
                {
                    continue;
                }

                var currpathLen = result.Length - currpath - 1;

                // Empty path (//) or current directory (.)?
                if (currpathLen == 0 || (currpathLen == 1 && result[currpath] == '.'))
                {
                    result.Length = currpath;
                }
                // Parent directory (..)?
                else if (currpathLen == 2 && result[currpath] == '.' && result[currpath + 1] == '.')
                {
                    // Walk back up by one directory. Don't go
                    // past the start of the string.
                    if (currpath == 0)
                    {
                        result.Length = 0;
                    }
                    else
                    {
                        var w = currpath - 1;
                        while (w > 0)
                        {
                            if (result[w - 1] == '/')
                            {
                                break;
                            }

                            --w;
                        }

                        result.Length = w;
                        currpath = w;
                    }
                }
                // Save for next time we start a new path.
                else
                {
                    currpath = result.Length;
                }
            }

            return string.Concat(prefix, result.ToString());
        }

        /// <summary>
        /// Read header from stream.
        /// </summary>
        /// <param name="stream">Stream positioned at start of header.</param>
        /// <returns>Header read or null, if end of archive is reached.</returns>
        public async Task<LzHeader> Read(Stream stream)
        {
            var headerOffset = stream.Position;

            // We cannot decode the file header until we identify the
            // header level (as different header level formats are
            // decoded in different ways. The header level field is
            // located at byte offset 20 within the header, so we
            // must read the first 21 bytes to read it (actually this
            // reads one byte more, so that we get the filename length
            // byte for level 1 headers as well).
            var header = new FileHeader
            {
                RawData = new byte[COMMON_HEADER_LEN],
                RawDataLen = COMMON_HEADER_LEN
            };

            var bytesRead = await ReadFully(stream, header.RawData, 0, COMMON_HEADER_LEN);

            // End of archive is reached, when there's not enough data for a header or first byte is 0
            // (level 2 headers are padded to never start with 0). End of archive mark might be followed by padding
            // like 0x1a (ctrl-z) in pmarc archives.
            if (bytesRead < COMMON_HEADER_LEN || header.RawData[0] == 0)
            {
                return null;
            }

            // Identify header level, and decode header depending on
            // the value encountered.
            header.HeaderLevel = header.RawData[20];

            switch (header.HeaderLevel)
            {
                case 0:
                    await DecodeLevel0Header(header, stream);
                    break;
                case 1:
                    await DecodeLevel1Header(header, stream);
                    break;
                case 2:
                    await DecodeLevel2Header(header, stream);
                    break;
                case 3:
                    await DecodeLevel3Header(header, stream);
                    break;
                default:
                    throw new IOException($"Unknown header level {header.HeaderLevel}");
            }

            // Some Amiga archives have directory entries mistakenly encoded
            // as -lh0- rather than -lhd-. These look like regular files
            // without a filename, which is obviously incorrect. So we catch
            // this case and fix the compress_method field.
            if (header.OsType == Constants.EXTEND_AMIGA
                && header.CompressMethod == Constants.LZHUFF0_METHOD
                && header.Length == 0 && header.Filename == null)
            {
                header.CompressMethod = Constants.LZHDIRS_METHOD;
            }

            // Symlinks are stored using the same compression method
            // field string (-lhd-) as directories.
            if (header.CompressMethod == Constants.LZHDIRS_METHOD
                && header.HasUnixPerms
                && (header.Path != null || header.Filename != null)
                && (header.UnixPerms & Constants.UNIX_FILE_TYPEMASK) == Constants.UNIX_FILE_SYMLINK)
            {
                ParseSymlink(header);
            }

            // Collapse special directory paths to ensure the path is clean.
            if (header.Path != null)
            {
                header.Path = CollapsePath(header.Path);
            }

            // Is this header generated by OS-9/68k LHA? If so, any Unix
            // permissions are actually OS-9 permissions.
            if (header.OsType == Constants.EXTEND_OS68K && header.HasUnixPerms)
            {
                header.Os9Perms = header.UnixPerms;
                header.HasOs9Perms = true;
            }

            // If OS-9 permissions were read, translate into Unix permissions.
            if (header.HasOs9Perms)
            {
                Os9ToUnixPermissions(header);
            }

            // Was the "common" extended header read, which contains a CRC of
            // the full header? If so, perform a CRC check now.
            if (header.HasCommonCrc && !CheckCommonCrc(header))
            {
                throw new IOException("Header CRC error");
            }

            // The DOS LHARK tool has its own -lh7- format that is incompatible
            // with the -lh7- that everyone else uses. As a workaround, we detect
            // and rename the compression method to -lk7- so as to be able to
            // distinguish between the two formats.
            if (header.HeaderLevel == 1 && header.OsType == Constants.EXTEND_LHARK
                && header.CompressMethod == Constants.LZHUFF7_METHOD)
            {
                header.CompressMethod = Constants.LHARK7_METHOD;
            }

            // Windows timestamp is used for level 0 and 1 headers, which have ms-dos timestamps
            var timestamp = header.HasWindowsTimestamps && header.HeaderLevel < 2
                ? DecodeWindowsTime(header.WinModificationTime)
                : header.Timestamp;

            return new LzHeader
            {
                HeaderOffset = headerOffset,
                HeaderSize = header.RawDataLen,
                SizeFieldLength = header.HeaderLevel == 3 ? 4 : 2,
                HeaderLevel = header.HeaderLevel,
                Method = header.CompressMethod,
                PackedSize = header.CompressedLength,
                OriginalSize = header.Length,
                Attribute = header.Attribute,
                Name = string.Concat(header.Path ?? string.Empty, header.Filename ?? string.Empty)
                    .Replace('/', '\\'),
                RealName = header.SymlinkTarget?.Replace('/', '\\'),
                HasCrc = header.HasCrc,
                Crc = (short)header.Crc,
                HeaderCrc = (short)header.CommonCrc,
                ExtendType = header.OsType,
                MinorVersion = header.MinorVersion,
                UnixLastModifiedStamp = timestamp,
                UnixMode = header.HasUnixPerms
                    ? header.UnixPerms
                    : (ushort)(Constants.UNIX_FILE_REGULAR | Constants.UNIX_RW_RW_RW),
                UnixUid = header.UnixUid,
                UnixGid = header.UnixGid,
                UnixUserName = header.UnixUsername,
                UnixGroupName = header.UnixGroup
            };
        }
    }
}
