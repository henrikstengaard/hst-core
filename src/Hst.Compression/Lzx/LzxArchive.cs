namespace Hst.Compression.Lzx
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public class LzxArchive : IDisposable
    {
        private readonly Stream stream;
        private readonly LzxOptions options;
        private readonly LzxIterator iterator;
        
        public LzxArchive(Stream stream) : this(stream, LzxOptions.Default)
        {
        }

        public LzxArchive(Stream stream, LzxOptions options)
        {
            this.stream = stream;
            this.options = options;
            this.iterator = new LzxIterator(stream, new LzxReader(stream, options, true));
        }
    
        public async Task<IEnumerable<LzxEntry>> Entries()
        {
            stream.Position = 0;
            var lzxReader = new LzxReader(stream, options, false);
            var entries = new List<LzxEntry>();
            LzxEntry entry;
            do
            {
                entry = await lzxReader.Read();

                if (entry == null)
                {
                    continue;
                }

                entries.Add(entry);
            } while (entry != null);

            return entries;
        }
    
        public async Task<LzxEntry> Next()
        {
            return await this.iterator.Next();
        }    

        public async Task Extract(Stream outputStream)
        {
            await this.iterator.Extract(outputStream);
        }    
        
        public void Dispose()
        {
            stream?.Dispose();
        }
    }
}