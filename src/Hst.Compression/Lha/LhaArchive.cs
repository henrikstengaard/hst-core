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
            this.options = options ?? LhaOptions.Default;
        }

        public async Task<IEnumerable<LhaEntry>> Entries()
        {
            var lhaReader = new LhaReader(stream, options);
            var entries = new List<LhaEntry>();
            LhaEntry entry;
            do
            {
                entry = await lhaReader.Read();

                if (entry == null)
                {
                    continue;
                }

                entries.Add(entry);
            } while (entry != null);

            return entries;
        }

        /// <summary>
        /// Extract entry data to output stream. CRC of extracted data is verified.
        /// </summary>
        /// <param name="entry">Entry to extract.</param>
        /// <param name="outputStream">Output stream to write extracted data to.</param>
        public void Extract(LhaEntry entry, Stream outputStream)
        {
            using (var entryStream = OpenEntry(entry))
            {
                entryStream.CopyTo(outputStream);
            }
        }

        /// <summary>
        /// Open entry as a stream, which decompresses (extracts) data as it's read.
        /// </summary>
        /// <param name="entry">Entry to open.</param>
        /// <returns>Entry stream.</returns>
        public LhaEntryStream OpenEntry(LhaEntry entry)
        {
            return new LhaEntryStream(stream, entry, options);
        }

        public void Dispose()
        {
            stream?.Dispose();
        }
    }
}