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

        public readonly Queue<LzxEntry> entries;
        public LzxEntry currentEntry { get; set; }
        
        public readonly byte[] read_buffer;
        public readonly byte[] decrunch_buffer;
        public HuffmanTable huffmanTable;
        
        /// <summary>
        /// Pack mode for entry with compressed data
        /// </summary>
        public int pack_mode { get; set; }
        
        /// <summary>
        /// Compressed data size of merged entry
        /// </summary>
        public int pack_size { get; set; }
        
        public int source_pos { get; set; }
        public int source_end { get; set; }
        public int destination_pos { get; set; }
        public int destination_end { get; set; }
        public int pos { get; set; }

        public LzxArchive(Stream stream) : this(stream, LzxOptions.Default)
        {
            this.entries = new Queue<LzxEntry>();
        }

        public LzxArchive(Stream stream, LzxOptions options)
        {
            this.stream = stream;
            this.options = options;
            this.read_buffer = new byte[16384];
            this.decrunch_buffer = new byte[258+65536+258];
            this.source_pos = 0;
            this.source_end = 0;
            this.destination_pos = 0;
            this.destination_end = 0;
            this.pos = 0;
        }
    
        public async Task<IEnumerable<LzxEntry>> Entries()
        {
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
    
        public LzxEntryIterator GetIterator()
        {
            return new LzxEntryIterator(this, new LzxReader(stream, options, true));
        }    
    
        public void Dispose()
        {
            stream?.Dispose();
        }
    }

    public class LzxEntryIterator
    {
        private readonly LzxArchive lzxArchive;
        private readonly LzxReader lzxReader;
        private bool hasExtractedCurrentEntry;
        // private LzxEntry currentEntry;

        public LzxEntryIterator(LzxArchive lzxArchive, LzxReader lzxReader)
        {
            this.lzxArchive = lzxArchive;
            this.lzxReader = lzxReader;
            this.hasExtractedCurrentEntry = false;
            // this.currentEntry = null;
        }
        
        public async Task<LzxEntry> Next()
        {
            if (!hasExtractedCurrentEntry && lzxArchive.entries.Count > 0)
            {
                Extract(null);
            }
            hasExtractedCurrentEntry = false;
            
            // read merged entries (entry pack size = 0) until
            // entry has compressed data (entry pack size > 0)
            if (this.lzxArchive.entries.Count == 0)
            {
                // read merged entries (pack size = 0)
                LzxEntry entry;
                do
                {
                    entry = await lzxReader.Read();

                    if (entry == null)
                    {
                        break;
                    }

                    lzxArchive.entries.Enqueue(entry);
                } while (entry.PackedSize == 0);

                lzxArchive.huffmanTable = new HuffmanTable(0U, -16, 1U);

                if (entry != null)
                {
                    lzxArchive.pack_mode = entry.PackMode;
                    lzxArchive.pack_size = entry.PackedSize;
                    lzxArchive.source_pos = 16384;
                    lzxArchive.source_end = lzxArchive.source_pos - 1024;
                    lzxArchive.destination_end = lzxArchive.destination_pos = 258 + 65536;
                    lzxArchive.pos = lzxArchive.destination_end;
                }
                else
                {
                    lzxArchive.pack_size = 0;
                    lzxArchive.source_pos = 0;
                    lzxArchive.source_end = 0;
                    lzxArchive.destination_pos = 0;
                    lzxArchive.destination_end = 0;
                    lzxArchive.pos = 0;
                }
            }
            
            lzxArchive.currentEntry = lzxArchive.entries.Count == 0
                ? null
                : lzxArchive.entries.Dequeue();

            return lzxArchive.currentEntry;
        }
        
        public void Extract(Stream outputStream)
        {
            switch (lzxArchive.pack_mode)
            {
                case 0:
                    LzxExtract.ExtractStore(lzxArchive, outputStream);
                    break;
                case 2:
                    LzxExtract.ExtractNormal(lzxArchive, outputStream);
                    break;
                default:
                    throw new IOException($"Unknown pack mode {lzxArchive.pack_mode}'");
            }
            
            hasExtractedCurrentEntry = true;
        }
    }
}