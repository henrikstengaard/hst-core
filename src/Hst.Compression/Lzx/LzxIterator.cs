namespace Hst.Compression.Lzx
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    internal class LzxIterator
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
        public async Task<LzxEntry> NextAsync(CancellationToken token = default)
        {
            // iterate over current entry compressed data using seek, if current entry has packed data
            // and was not extracted.
            if (!this.hasExtractedCurrentEntry && this.CurrentEntry != null && 
                this.CurrentEntry.PackedSize > 0)
            {
                stream.Seek(CurrentEntry.PackedSize, SeekOrigin.Current);
            }

            hasExtractedCurrentEntry = false;
            
            // read next merged chunk, if merged chunk is empty
            if (IsMergedChunkEmpty)
            {
                this.mergedChunk = await ReadNextMergedChunk(token);

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
        private async Task<LzxMergedChunk> ReadNextMergedChunk(CancellationToken token)
        {
            var entries = new List<LzxEntry>();

            LzxEntry entry;
            do
            {
                entry = await reader.ReadAsync(token);

                if (entry == null)
                {
                    break;
                }

                entries.Add(entry);
            } while (entry.PackedSize == 0);

            if (entries.Count <= 0)
            {
                return null;
            }
            
            var mergedEntry = entries[entries.Count - 1];
            return new LzxMergedChunk(entries, mergedEntry.PackMode, mergedEntry.PackedSize, stream.Position);
        }
        
        /// <summary>
        /// Extract current entry
        /// </summary>
        /// <param name="outputStream"></param>
        /// <exception cref="IOException"></exception>
        public async Task ExtractAsync(Stream outputStream, CancellationToken token = default)
        {
            switch (this.mergedChunk.PackMode)
            {
                case PackModeEnum.Store:
                    await extractor.ExtractStore(this.CurrentEntry, outputStream, token);
                    break;
                case PackModeEnum.Normal:
                    await extractor.ExtractNormal(this.CurrentEntry, outputStream, token);
                    break;
                default:
                    throw new IOException($"Unknown pack mode {(int)this.mergedChunk.PackMode}'");
            }
            
            hasExtractedCurrentEntry = true;
        }
    }
}