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
        private bool isFirst;

        public LzxReader(Stream stream, LzxOptions lzxOptions)
        {
            this.stream = stream;
            this.lzxOptions = lzxOptions;
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

            // actual = fread(archive_header, 1, 31, in_file);
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

            //var sum = 0; /* reset CRC */
            var headerCrc = (archiveHeaderBytes[29] << 24) + (archiveHeaderBytes[28] << 16) + (archiveHeaderBytes[27] << 8) +
                      archiveHeaderBytes[26];
            archiveHeaderBytes[29] = 0; /* Must set the field to 0 before calculating the crc */
            archiveHeaderBytes[28] = 0;
            archiveHeaderBytes[27] = 0;
            archiveHeaderBytes[26] = 0;
            var sum = CrcHelper.crc_calc(archiveHeaderBytes, 0, ArchiveHeaderSize, 0);

            // READ FILENAME
            var filenameLength = archiveHeaderBytes[30]; /* filename length */
            // actual = fread(header_filename, 1, temp, in_file);
            var headerFilenameBytes = new byte[filenameLength];
            if (await stream.ReadAsync(headerFilenameBytes, 0, filenameLength) != filenameLength)
            {
                throw new IOException("Failed to read header filename");
            }

            // READ COMMENT
            //headerFilenameBytes[filenameLength] = 0;
            sum = CrcHelper.crc_calc(headerFilenameBytes, 0, filenameLength, sum);
            var commentLength = archiveHeaderBytes[14]; /* comment length */
            var headerCommentBytes = new byte[commentLength];
            // actual = fread(header_comment, 1, filenameLength, in_file);
            if (await stream.ReadAsync(headerCommentBytes, 0, commentLength) != commentLength)
            {
                throw new IOException("Failed to read header comment");
            }

            //headerCommentBytes[commentLength] = 0;
            sum = CrcHelper.crc_calc(headerCommentBytes, 0, commentLength, sum);

            var filename = lzxOptions.Encoding.GetString(headerFilenameBytes);
            var comment = lzxOptions.Encoding.GetString(headerCommentBytes);

            if (sum != headerCrc)
            {
                throw new IOException($"Failed to read header, crc {sum} expected {headerCrc}");
            }

            var attributes = archiveHeaderBytes[0]; /* file protection modes */
            var unpack_size = (archiveHeaderBytes[5] << 24) + (archiveHeaderBytes[4] << 16) +
                              (archiveHeaderBytes[3] << 8) + archiveHeaderBytes[2]; /* unpack size */
            var pack_size = (archiveHeaderBytes[9] << 24) + (archiveHeaderBytes[8] << 16) +
                            (archiveHeaderBytes[7] << 8) + archiveHeaderBytes[6]; /* packed size */
            var pack_mode = archiveHeaderBytes[11]; /* pack mode */
            var dataCrc = (archiveHeaderBytes[25] << 24) + (archiveHeaderBytes[24] << 16) +
                          (archiveHeaderBytes[23] << 8) + archiveHeaderBytes[22]; /* data crc */
            var date = (archiveHeaderBytes[18] << 24) + (archiveHeaderBytes[19] << 16) + (archiveHeaderBytes[20] << 8) +
                       archiveHeaderBytes[21]; /* date */
            var year = ((date >> 17) & 63) + 1970;
            var month = (date >> 23) & 15;
            var day = (date >> 27) & 31;
            var hour = (date >> 12) & 31;
            var minute = (date >> 6) & 63;
            var second = date & 63;

            // total_pack += pack_size;
            // total_unpack += unpack_size;
            // total_files++;
            // merge_size += unpack_size;

            // printf("%8ld ", unpack_size);
            // if(archive_header[12] & 1)
            //  printf("     n/a ");
            // else
            //  printf("%8ld ", pack_size);
            // printf("%02ld:%02ld:%02ld ", hour, minute, second);
            // printf("%2ld-%s-%4ld ", day, month_str[month], year);
            // printf("%c%c%c%c%c%c%c%c ",
            //  (attributes & 32) ? 'h' : '-',
            //  (attributes & 64) ? 's' : '-',
            //  (attributes & 128) ? 'p' : '-',
            //  (attributes & 16) ? 'a' : '-',
            //  (attributes & 1) ? 'r' : '-',
            //  (attributes & 2) ? 'w' : '-',
            //  (attributes & 8) ? 'e' : '-',
            //  (attributes & 4) ? 'd' : '-');
            // printf("\"%s\"\n", header_filename);
            // if(header_comment[0])
            //  printf(": \"%s\"\n", header_comment);
            // if((archive_header[12] & 1) && pack_size)
            // {
            //  printf("%8ld %8ld Merged\n", merge_size, pack_size);
            // }

            var offset = stream.Position;
            if (pack_size > 0) /* seek past the packed data */
            {
                //merge_size = 0;
                if (stream.Seek(pack_size, SeekOrigin.Current) == 0)
                {
                    throw new IOException("Seek pack size failed");
                }
                // if(!fseek(in_file, pack_size, SEEK_CUR))
                // {
                //  abort = 0; /* continue */
                // }
                // else
                //  perror("FSeek()");
            }
            // else
            //  abort = 0; /* continue */

            return new LzxEntry
            {
                Offset = offset,
                DataCrc = dataCrc,
                Name = filename,
                Comment = comment,
                Date = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local),
                PackMode = pack_mode,
                PackedSize = pack_size,
                UnpackedSize = unpack_size,
                Attributes = attributes
            };
        }
    }
}