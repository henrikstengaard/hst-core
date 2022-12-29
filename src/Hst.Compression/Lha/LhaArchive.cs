namespace Hst.Compression.Lha
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    public class LhaArchive : IDisposable
    {
        private readonly Stream stream;
        private readonly Encoding encoding;

        public LhaArchive(Stream stream, Encoding encoding)
        {
            this.stream = stream;
            this.encoding = encoding;
        }

        public async Task<IEnumerable<LzHeader>> Entries()
        {
            var lhaReader = new LhaReader(stream, encoding);
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