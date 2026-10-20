namespace Hst.Compression.Lzx
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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

        /// <summary>
        /// Open entry as a stream, which decompresses entry data as it's read
        /// </summary>
        /// <param name="entry">Entry to open</param>
        /// <returns>Entry stream</returns>
        /// <exception cref="ArgumentException">Entry doesn't exist in archive</exception>
        public async Task<LzxEntryStream> OpenEntry(LzxEntry entry)
        {
            // read entries and restore stream position, so iterating entries with next isn't affected
            var streamPosition = stream.Position;
            var entries = (await Entries()).ToList();
            stream.Position = streamPosition;

            // find merged chunk containing entry (entries until packed size is greater than 0)
            var mergedChunkEntries = new List<LzxEntry>();
            var containsEntry = false;
            foreach (var archiveEntry in entries)
            {
                mergedChunkEntries.Add(archiveEntry);
                containsEntry |= archiveEntry.EntryOffset == entry.EntryOffset;

                if (archiveEntry.PackedSize == 0 && archiveEntry != entries[entries.Count - 1])
                {
                    continue;
                }

                if (containsEntry)
                {
                    var mergedEntry = mergedChunkEntries[mergedChunkEntries.Count - 1];
                    return new LzxEntryStream(stream, new LzxMergedChunk(mergedChunkEntries, mergedEntry.PackMode,
                        mergedEntry.PackedSize, mergedEntry.DataOffset), entry);
                }

                mergedChunkEntries = new List<LzxEntry>();
            }

            throw new ArgumentException($"Entry '{entry.Name}' at offset {entry.EntryOffset} doesn't exist in archive",
                nameof(entry));
        }
        
        public void Dispose()
        {
            stream?.Dispose();
        }
    }
}