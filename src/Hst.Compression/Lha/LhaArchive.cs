namespace Hst.Compression.Lha
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public class LhaArchive : IDisposable
    {
        private readonly Stream stream;
        private readonly LhaOptions options;

        public LhaArchive(Stream stream) : this(stream, LhaOptions.Default)
        {
        }

        public LhaArchive(Stream stream, LhaOptions options)
        {
            this.stream = stream;
            this.options = options;
        }

        public async Task<IEnumerable<LzHeader>> Entries()
        {
            var lhaReader = new LhaReader(stream, options);
            var entries = new List<LzHeader>();
            LzHeader header;
            do
            {
                header = await lhaReader.Read();

                if (header == null)
                {
                    continue;
                }

                entries.Add(header);
            } while (header != null);

            return entries;
        }

        /// <summary>
        /// Extract entry data to output stream. CRC of extracted data is verified.
        /// </summary>
        /// <param name="header">Header of entry to extract.</param>
        /// <param name="outputStream">Output stream to write extracted data to.</param>
        public void Extract(LzHeader header, Stream outputStream)
        {
            using (var entryStream = OpenEntry(header))
            {
                entryStream.CopyTo(outputStream);
            }
        }

        /// <summary>
        /// Open entry as a stream, which decompresses (extracts) data as it's read.
        /// </summary>
        /// <param name="header">Header of entry to open.</param>
        /// <returns>Entry stream.</returns>
        public LhaEntryStream OpenEntry(LzHeader header)
        {
            return new LhaEntryStream(stream, header);
        }

        public void Dispose()
        {
            stream?.Dispose();
        }
    }
}