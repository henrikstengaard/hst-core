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
        private readonly IteratorStream _iteratorStream;
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
            _iteratorStream = new IteratorStream();
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
                this.MergedChunkEntryIndex <= this.mergedChunk.Entries.Count)
            {
                await Extract(_iteratorStream);
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

        private class IteratorStream : Stream
        {
            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count) => 0;

            public override long Seek(long offset, SeekOrigin origin) => 0;

            public override void SetLength(long value)
            {
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => 0;
            public override long Position { get => 0; set { } }
        }
    }
}