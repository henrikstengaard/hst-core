namespace Hst.Compression.Lzx
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public class LzxArchive : IDisposable
    {
        public readonly Stream stream;
        private readonly LzxOptions options;

        // public readonly byte[] read_buffer;
        // public readonly byte[] decrunch_buffer;
        //
        // public int source_pos { get; set; }
        // public int source_end { get; set; }
        // public int destination_pos { get; set; }
        // public int destination_end { get; set; }

        public LzxArchive(Stream stream) : this(stream, LzxOptions.Default)
        {
        }

        public LzxArchive(Stream stream, LzxOptions options)
        {
            this.stream = stream;
            this.options = options;
            // this.read_buffer = new byte[16384];
            // this.decrunch_buffer = new byte[258+65536+258];
            // this.source_pos = 0;
            // this.source_end = 0;
            // this.destination_pos = 0;
            // this.destination_end = 0;
        }
    
        public async Task<IEnumerable<LzxEntry>> Entries()
        {
            var lzxReader = new LzxReader(stream, options);
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
    
        public void Extract(LzxEntry entry, Stream outputStream)
        {
            stream.Seek(entry.Offset, SeekOrigin.Begin);
            
            //LhExt.ExtractOne(stream, outputStream, header);
        }    
    
        public void Dispose()
        {
            stream?.Dispose();
        }
    }
}