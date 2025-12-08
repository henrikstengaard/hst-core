namespace Hst.Core.IO
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// Cached stream divides a stream into blocks and caches each block for reads and writes
    /// </summary>
    public class CachedStream : Stream
    {
        private readonly Stream stream;
        private readonly int blockSize;
        private readonly int blocksLimit;
        private readonly IDictionary<long, CachedBlock> cachedBlocks;
        private readonly IDictionary<long, CachedBlock> updatedCachedBlocks;
        private readonly System.Timers.Timer flushTimer;
        private readonly bool flushTimerEnabled;
        private long position;

        private readonly SemaphoreSlim semaphoreLock = new SemaphoreSlim(1, 1);

        public IEnumerable<long> CachedBlockOffsets => cachedBlocks.Keys;

        public CachedStream(Stream stream, int blockSize, int blocksLimit, TimeSpan flushInterval)
        {
            this.stream = stream;
            this.blockSize = blockSize;
            this.blocksLimit = blocksLimit;
            this.cachedBlocks = new Dictionary<long, CachedBlock>();
            this.updatedCachedBlocks = new Dictionary<long, CachedBlock>();
            flushTimerEnabled = flushInterval.TotalMilliseconds > 0;
            flushTimer = new System.Timers.Timer();
            flushTimer.Enabled = false;
            if (flushTimerEnabled)
            {
                flushTimer.Interval = flushInterval.TotalMilliseconds;
            }
            flushTimer.Elapsed += (sender, args) => Flush();
            this.position = 0;
        }

        public CachedStream(Stream stream, int blockSize, int blocksLimit)
            : this(stream, blockSize, blocksLimit, TimeSpan.Zero)
        {
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                flushTimer.Stop();
                Flush();
            }
        }

        public override void Flush()
        {
            semaphoreLock.Wait();
            try
            {
                WriteCachedBlocks();
                PurgeCachedBlocks();
                stream.Flush();
            }
            finally
            {
                semaphoreLock.Release();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var startOffset = position - (int)(position % blockSize);
            var positionInBlock = (int)(position - startOffset);
            var blocks = Convert.ToInt32(Math.Ceiling(((double)positionInBlock + count) / blockSize));
            
            var cachedBlocksWithBuffer = FetchCachedBlocks(startOffset, blocks).ToList();

            var bytesRead = 0;
            foreach (var cachedBlock in cachedBlocksWithBuffer)
            {
                var bytesFromBlock = positionInBlock + (count - bytesRead) >= cachedBlock.Length
                    ? cachedBlock.Length - positionInBlock
                    : count - bytesRead;
                
                Array.Copy(cachedBlock.Data, positionInBlock, buffer, bytesRead, bytesFromBlock);

                positionInBlock = 0;
                bytesRead += bytesFromBlock;
                cachedBlock.ReadCount++;
            }

            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin != SeekOrigin.Begin)
            {
                throw new ArgumentOutOfRangeException(nameof(origin));
            }

            return position = offset;
        }

        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var startOffset = position - (int)(position % blockSize);
            var positionInBlock = (int)(position - startOffset);
            // var blocks = 1 + (positionInBlock + count > blockSize ? (positionInBlock + count) / blockSize : 0);
            var blocks = Convert.ToInt32(Math.Ceiling(((double)positionInBlock + count) / blockSize));

            var cachedBlocksToUpdate = FetchCachedBlocks(startOffset, blocks).ToList();

            var bytesWritten = 0;
            foreach (var cachedBlock in cachedBlocksToUpdate)
            {
                var bytesToBlock = positionInBlock + (count - bytesWritten) >= blockSize
                    ? blockSize - positionInBlock
                    : count - bytesWritten;
                
                Array.Copy(buffer, offset + bytesWritten, cachedBlock.Data, positionInBlock, bytesToBlock);

                if (cachedBlock.Length < blockSize)
                {
                    cachedBlock.Length = positionInBlock + bytesToBlock;
                }

                positionInBlock = 0;
                bytesWritten += bytesToBlock;
                position += bytesToBlock;
                cachedBlock.WriteCount++;

                if (updatedCachedBlocks.ContainsKey(cachedBlock.Offset))
                {
                    continue;
                }

                updatedCachedBlocks.Add(cachedBlock.Offset, cachedBlock);
            }

            if (!flushTimerEnabled)
            {
                return;
            }
            flushTimer.Enabled = true;
        }

        public override bool CanRead => stream.CanRead;
        public override bool CanSeek => stream.CanSeek;
        public override bool CanWrite => stream.CanWrite;
        public override long Length => stream.Length;

        public override long Position
        {
            get => position;
            set => position = value;
        }

        private IEnumerable<CachedBlock> FetchCachedBlocks(long startPosition, int blocks)
        {
            for (var i = 0; i < blocks; i++)
            {
                var offset = startPosition + (i * blockSize);
                if (cachedBlocks.ContainsKey(offset))
                {
                    yield return cachedBlocks[offset];
                    continue;
                }

                // write and purge cached blocks, if number of cached blocks + 1 new is larger then blocks limit
                if (cachedBlocks.Count + 1 > blocksLimit)
                {
                    Flush();
                }
                
                stream.Seek(offset, SeekOrigin.Begin);
                var buffer = new byte[blockSize];
                var bytesRead = stream.Read(buffer, 0, buffer.Length);

                var cachedBlock = new CachedBlock
                {
                    Offset = offset,
                    Data = buffer,
                    Length = bytesRead,
                    ReadCount = 0,
                    WriteCount = 0
                };

                cachedBlocks.Add(offset, cachedBlock);

                yield return cachedBlock;
            }
        }

        private void WriteCachedBlocks()
        {
            if (updatedCachedBlocks.Count == 0)
            {
                return;
            }
            
            foreach (var cachedBlock in updatedCachedBlocks.Values.ToList())
            {
                stream.Seek(cachedBlock.Offset, SeekOrigin.Begin);

                stream.Write(cachedBlock.Data, 0, cachedBlock.Length);

                cachedBlock.WriteCount = 0;
            }

            updatedCachedBlocks.Clear();
            
            if (!flushTimerEnabled)
            {
                return;
            }
            flushTimer.Enabled = false;
        }

        private void PurgeCachedBlocks()
        {
            var purgeThreshold = blocksLimit / 2;
            
            // return, if number cached blocks is smaller than or equal to purge threshold
            if (cachedBlocks.Count <= purgeThreshold)
            {
                return;
            }

            var cachedBlocksOrderedByReadCount = cachedBlocks.Values.OrderBy(x => x.ReadCount).ToList();

            for (var i = 0; i < cachedBlocksOrderedByReadCount.Count; i++)
            {
                var cachedBlock = cachedBlocksOrderedByReadCount[i];
            
                // remove cached block, if less than purge threshold
                if (i < purgeThreshold)
                {
                    cachedBlocks.Remove(cachedBlock.Offset);
                    continue;
                }

                if (cachedBlock.ReadCount > 0)
                {
                    cachedBlock.ReadCount /= 2;
                }
            
                if (cachedBlock.WriteCount > 0)
                {
                    cachedBlock.WriteCount /= 2;
                }
            }
        }
    }
}