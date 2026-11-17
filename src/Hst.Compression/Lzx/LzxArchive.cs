namespace Hst.Compression.Lzx
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Lzx archive for listing, opening and extracting entries.
    /// </summary>
    public class LzxArchive : IDisposable
    {
        private const int CopyBufferSize = 81920;

        private readonly Stream stream;
        private readonly LzxOptions options;
        private readonly bool leaveOpen;
        private readonly LzxIterator iterator;
        private bool hasCurrentEntry;
        private bool disposed;

        /// <summary>
        /// Create lzx archive reading from stream. Stream is disposed, when archive is disposed.
        /// </summary>
        /// <param name="stream">Stream to read archive from.</param>
        public LzxArchive(Stream stream) : this(stream, LzxOptions.Default)
        {
        }

        /// <summary>
        /// Create lzx archive reading from stream.
        /// </summary>
        /// <param name="stream">Stream to read archive from. Must be readable and seekable.</param>
        /// <param name="options">Options for reading archive.</param>
        /// <param name="leaveOpen">Leave stream open, when archive is disposed.</param>
        public LzxArchive(Stream stream, LzxOptions options, bool leaveOpen = false)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            this.options = options ?? LzxOptions.Default;
            this.leaveOpen = leaveOpen;
            iterator = new LzxIterator(stream, new LzxReader(stream, this.options, true));
            hasCurrentEntry = false;
        }

        /// <summary>
        /// Read entries in archive. Doesn't affect iterating entries with <see cref="NextAsync"/>.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Entries in archive.</returns>
        public async Task<IEnumerable<LzxEntry>> EntriesAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();

            // read entries and restore stream position, so iterating entries with next isn't affected
            var streamPosition = stream.Position;
            try
            {
                stream.Position = 0;
                var lzxReader = new LzxReader(stream, options, false);
                var entries = new List<LzxEntry>();
                LzxEntry entry;
                while ((entry = await lzxReader.ReadAsync(token).ConfigureAwait(false)) != null)
                {
                    entries.Add(entry);
                }

                return entries;
            }
            finally
            {
                stream.Position = streamPosition;
            }
        }

        /// <summary>
        /// Iterate to next entry in archive. Iterating entries and extracting them with
        /// <see cref="ExtractCurrentAsync"/> is the fastest way to extract all entries, as data is read sequentially.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Next entry or null, if end of archive is reached.</returns>
        public async Task<LzxEntry> NextAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();

            var entry = await iterator.NextAsync(token).ConfigureAwait(false);
            hasCurrentEntry = entry != null;
            return entry;
        }

        /// <summary>
        /// Extract current entry returned by <see cref="NextAsync"/> to output stream.
        /// </summary>
        /// <param name="outputStream">Output stream to write extracted data to.</param>
        /// <param name="token">Cancellation token.</param>
        /// <exception cref="InvalidOperationException">No current entry, <see cref="NextAsync"/> hasn't been called
        /// or end of archive is reached.</exception>
        public async Task ExtractCurrentAsync(Stream outputStream, CancellationToken token = default)
        {
            ThrowIfDisposed();

            if (outputStream == null)
            {
                throw new ArgumentNullException(nameof(outputStream));
            }

            if (!hasCurrentEntry)
            {
                throw new InvalidOperationException("No current entry to extract, call NextAsync first");
            }

            await iterator.ExtractAsync(outputStream, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Extract entry data to output stream. CRC of extracted data is verified.
        /// Doesn't affect iterating entries with <see cref="NextAsync"/>.
        /// </summary>
        /// <param name="entry">Entry to extract.</param>
        /// <param name="outputStream">Output stream to write extracted data to.</param>
        /// <param name="token">Cancellation token.</param>
        /// <exception cref="ArgumentException">Entry doesn't exist in archive.</exception>
        public async Task ExtractAsync(LzxEntry entry, Stream outputStream, CancellationToken token = default)
        {
            if (outputStream == null)
            {
                throw new ArgumentNullException(nameof(outputStream));
            }

            var streamPosition = stream.Position;
            try
            {
                using (var entryStream = await OpenEntryAsync(entry, token).ConfigureAwait(false))
                {
                    await entryStream.CopyToAsync(outputStream, CopyBufferSize, token).ConfigureAwait(false);
                }
            }
            finally
            {
                stream.Position = streamPosition;
            }
        }

        /// <summary>
        /// Open entry as a stream, which decompresses entry data as it's read.
        /// </summary>
        /// <param name="entry">Entry to open.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Entry stream.</returns>
        /// <exception cref="ArgumentException">Entry doesn't exist in archive.</exception>
        public async Task<LzxEntryStream> OpenEntryAsync(LzxEntry entry, CancellationToken token = default)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var entries = (await EntriesAsync(token).ConfigureAwait(false)).ToList();

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

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(LzxArchive));
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            if (!leaveOpen)
            {
                stream.Dispose();
            }
        }
    }
}
