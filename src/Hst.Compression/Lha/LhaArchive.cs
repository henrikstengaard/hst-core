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

        public void Extract(LzHeader header, Stream outputStream)
        {
            stream.Seek(header.HeaderOffset + header.HeaderSize, SeekOrigin.Begin);
            
            LhExt.ExtractOne(stream, outputStream, header);
        }

        public void Dispose()
        {
            stream?.Dispose();
        }
    }
}