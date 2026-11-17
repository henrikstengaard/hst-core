namespace Hst.Compression.Lha
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Lha archive for listing, opening and extracting entries.
    /// </summary>
    public class LhaArchive : IDisposable
    {
        private const int CopyBufferSize = 81920;

        private readonly Stream stream;
        private readonly LhaOptions options;
        private readonly bool leaveOpen;
        private readonly long startPosition;
        private bool disposed;

        /// <summary>
        /// Create lha archive reading from stream. Stream is disposed, when archive is disposed.
        /// </summary>
        /// <param name="stream">Stream to read archive from.</param>
        public LhaArchive(Stream stream) : this(stream, LhaOptions.Default)
        {
        }

        /// <summary>
        /// Create lha archive reading from stream.
        /// </summary>
        /// <param name="stream">Stream to read archive from. Archive is read from current position of stream.</param>
        /// <param name="options">Options for reading archive.</param>
        /// <param name="leaveOpen">Leave stream open, when archive is disposed.</param>
        public LhaArchive(Stream stream, LhaOptions options, bool leaveOpen = false)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            this.options = options ?? LhaOptions.Default;
            this.leaveOpen = leaveOpen;
            startPosition = stream.CanSeek ? stream.Position : 0;
        }

        /// <summary>
        /// Read entries in archive.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Entries in archive.</returns>
        public async Task<IEnumerable<LhaEntry>> EntriesAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();

            if (stream.CanSeek)
            {
                stream.Seek(startPosition, SeekOrigin.Begin);
            }

            var lhaReader = new LhaReader(stream, options);
            var entries = new List<LhaEntry>();
            LhaEntry entry;
            while ((entry = await lhaReader.ReadAsync(token).ConfigureAwait(false)) != null)
            {
                entries.Add(entry);
            }

            return entries;
        }

        /// <summary>
        /// Extract entry data to output stream. CRC of extracted data is verified.
        /// </summary>
        /// <param name="entry">Entry to extract.</param>
        /// <param name="outputStream">Output stream to write extracted data to.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task ExtractAsync(LhaEntry entry, Stream outputStream, CancellationToken token = default)
        {
            if (outputStream == null)
            {
                throw new ArgumentNullException(nameof(outputStream));
            }

            using (var entryStream = await OpenEntryAsync(entry, token).ConfigureAwait(false))
            {
                await entryStream.CopyToAsync(outputStream, CopyBufferSize, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Open entry as a stream, which decompresses (extracts) data as it's read.
        /// </summary>
        /// <param name="entry">Entry to open.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Entry stream.</returns>
        public Task<LhaEntryStream> OpenEntryAsync(LhaEntry entry, CancellationToken token = default)
        {
            ThrowIfDisposed();
            token.ThrowIfCancellationRequested();

            return Task.FromResult(new LhaEntryStream(stream, entry, options));
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(LhaArchive));
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
