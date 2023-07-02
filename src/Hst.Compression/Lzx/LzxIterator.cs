namespace Hst.Compression.Lzx
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public class LzxIterator
    {
        private readonly Stream stream;
        private readonly LzxReader reader;
        private readonly LzxExtractor extractor; 
        private LzxMergedChunk mergedChunk;
        private int MergedChunkEntryIndex { get; set; }
        private bool hasExtractedCurrentEntry;
        private LzxEntry CurrentEntry { get; set; }
        
        public LzxIterator(Stream stream, LzxReader lzxReader)
        {
            this.stream = stream;
            this.reader = lzxReader;
            this.extractor = new LzxExtractor(this.stream);
            this.mergedChunk = null;
            this.MergedChunkEntryIndex = 0;
            this.hasExtractedCurrentEntry = false;
            this.CurrentEntry = null;
        }
        
        /// <summary>
        /// Read next entry
        /// </summary>
        /// <returns>Entry</returns>
        public async Task<LzxEntry> Next()
        {
            // iterate over current entry compressed data by extracting compressed entry data to null,
            // if current entry was not extracted.
            // required as offsets for individual entries compressed data is not seekable and has to be iterated over
            if (!this.hasExtractedCurrentEntry && this.mergedChunk != null && 
                this.MergedChunkEntryIndex < this.mergedChunk.Entries.Count)
            {
                await Extract(null);
            }

            hasExtractedCurrentEntry = false;
            
            // read next merged chunk, if merged chunk is empty
            if (IsMergedChunkEmpty)
            {
                this.mergedChunk = await ReadNextMergedChunk();

                this.MergedChunkEntryIndex = 0;
                
                // reset extractor for next merged chunk
                extractor.Reset(this.mergedChunk);
            }
            
            // set current entry to next entry in merged chunk
            CurrentEntry = this.mergedChunk == null || this.MergedChunkEntryIndex >= this.mergedChunk.Entries.Count
                ? null
                : this.mergedChunk.Entries[this.MergedChunkEntryIndex++];

            return CurrentEntry;
        }

        private bool IsMergedChunkEmpty =>
            this.mergedChunk == null || this.MergedChunkEntryIndex >= this.mergedChunk.Entries.Count;

        /// <summary>
        /// Read next merged chunk of entries (read entries until packed size is greater than 0)
        /// </summary>
        /// <returns></returns>
        /// <exception cref="IOException"></exception>
        private async Task<LzxMergedChunk> ReadNextMergedChunk()
        {
            var entries = new List<LzxEntry>();

            LzxEntry entry;
            do
            {
                entry = await reader.Read();

                if (entry == null)
                {
                    break;
                }

                entries.Add(entry);
            } while (entry.PackedSize == 0);

            if (entry == null || entries.Count == 0)
            {
                return null;
            }

            return new LzxMergedChunk(entries, entry.PackMode, entry.PackedSize, stream.Position);
        }
        
        /// <summary>
        /// Extract current entry
        /// </summary>
        /// <param name="outputStream"></param>
        /// <exception cref="IOException"></exception>
        public async Task Extract(Stream outputStream)
        {
            switch (this.mergedChunk.PackMode)
            {
                case PackModeEnum.Store:
                    await extractor.ExtractStore(this.CurrentEntry, outputStream);
                    break;
                case PackModeEnum.Normal:
                    await extractor.ExtractNormal(this.CurrentEntry, outputStream);
                    break;
                default:
                    throw new IOException($"Unknown pack mode {(int)this.mergedChunk.PackMode}'");
            }
            
            hasExtractedCurrentEntry = true;
        }
    }
}