namespace Hst.Compression.Lzx
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    public class LzxReader
    {
        private const int InfoHeaderSize = 10;
        private const int ArchiveHeaderSize = 31;

        private readonly Stream stream;
        private readonly LzxOptions lzxOptions;
        private readonly bool extract;
        private bool isFirst;

        public LzxReader(Stream stream, LzxOptions lzxOptions, bool extract)
        {
            this.stream = stream;
            this.lzxOptions = lzxOptions;
            this.extract = extract;
            this.isFirst = true;
        }

        /// <summary>
        /// Read next entry
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public async Task<LzxEntry> Read()
        {
            // read info header, if first entry
            if (isFirst)
            {
                var infoHeaderBytes = new byte[InfoHeaderSize];
                if (await stream.ReadAsync(infoHeaderBytes, 0, infoHeaderBytes.Length) != infoHeaderBytes.Length)
                {
                    throw new IOException("Failed to info header");
                }

                // throw exception, if info header doesn't start with "LZX"
                if (infoHeaderBytes[0] != 76 || infoHeaderBytes[1] != 90 || infoHeaderBytes[2] != 88)
                {
                    throw new IOException("Invalid LZX header id");
                }

                isFirst = false;
            }

            var entryOffset = stream.Position;
            
            var archiveHeaderBytes = new byte[ArchiveHeaderSize];
            var bytesRead = await stream.ReadAsync(archiveHeaderBytes, 0, ArchiveHeaderSize);

            // end of archive
            var endOfArchive = bytesRead == 0;
            if (endOfArchive)
            {
                return null;
            }

            if (bytesRead != ArchiveHeaderSize)
            {
                throw new IOException("Failed to read archive header");
            }

            var headerCrc = (archiveHeaderBytes[29] << 24) + (archiveHeaderBytes[28] << 16) + (archiveHeaderBytes[27] << 8) +
                      archiveHeaderBytes[26];
            archiveHeaderBytes[29] = 0; /* Must set the field to 0 before calculating the crc */
            archiveHeaderBytes[28] = 0;
            archiveHeaderBytes[27] = 0;
            archiveHeaderBytes[26] = 0;
            var sum = CrcHelper.crc_calc(archiveHeaderBytes, 0, ArchiveHeaderSize, 0);

            // read filename
            var filenameLength = archiveHeaderBytes[30]; /* filename length */
            var headerFilenameBytes = new byte[filenameLength];
            if (await stream.ReadAsync(headerFilenameBytes, 0, filenameLength) != filenameLength)
            {
                throw new IOException("Failed to read header filename");
            }

            // read comment
            sum = CrcHelper.crc_calc(headerFilenameBytes, 0, filenameLength, sum);
            var commentLength = archiveHeaderBytes[14]; /* comment length */
            var headerCommentBytes = new byte[commentLength];
            if (await stream.ReadAsync(headerCommentBytes, 0, commentLength) != commentLength)
            {
                throw new IOException("Failed to read header comment");
            }

            sum = CrcHelper.crc_calc(headerCommentBytes, 0, commentLength, sum);

            var filename = lzxOptions.Encoding.GetString(headerFilenameBytes);
            var comment = lzxOptions.Encoding.GetString(headerCommentBytes);

            if (sum != headerCrc)
            {
                throw new IOException($"Failed to read header, crc {sum} expected {headerCrc}");
            }

            var attributes = (AttributesEnum)archiveHeaderBytes[0]; /* file protection modes */
            var unpackSize = (archiveHeaderBytes[5] << 24) + (archiveHeaderBytes[4] << 16) +
                              (archiveHeaderBytes[3] << 8) + archiveHeaderBytes[2]; /* unpack size */
            var packSize = (archiveHeaderBytes[9] << 24) + (archiveHeaderBytes[8] << 16) +
                            (archiveHeaderBytes[7] << 8) + archiveHeaderBytes[6]; /* packed size */
            var packMode = archiveHeaderBytes[11]; /* pack mode */
            var dataCrc = (archiveHeaderBytes[25] << 24) + (archiveHeaderBytes[24] << 16) +
                          (archiveHeaderBytes[23] << 8) + archiveHeaderBytes[22]; /* data crc */
            var date = (archiveHeaderBytes[18] << 24) + (archiveHeaderBytes[19] << 16) + (archiveHeaderBytes[20] << 8) +
                       archiveHeaderBytes[21]; /* date */
            var year = ((date >> 17) & 63) + 1970;
            var month = ((date >> 23) & 15) + 1;
            var day = (date >> 27) & 31;
            var hour = (date >> 12) & 31;
            var minute = (date >> 6) & 63;
            var second = date & 63;

            var dataOffset = stream.Position;
            if (!extract && packSize > 0) /* seek past the packed data */
            {
                if (stream.Seek(packSize, SeekOrigin.Current) == 0)
                {
                    throw new IOException("Seek pack size failed");
                }
            }

            var isMergedEntry = (archiveHeaderBytes[12] & 1) == 1 && packSize > 0;
            
            return new LzxEntry
            {
                EntryOffset = entryOffset,
                DataOffset = dataOffset,
                DataCrc = dataCrc,
                Name = filename,
                Comment = comment,
                Date = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local),
                PackMode = (PackModeEnum)packMode,
                PackedSize = packSize,
                UnpackedSize = unpackSize,
                Attributes = attributes,
                IsMergedEntry = isMergedEntry
            };
        }
    }
}