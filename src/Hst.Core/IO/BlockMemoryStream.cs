using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Hst.Core.IO
{
    /// <summary>
    /// Block memory stream is used to represent a large stream using blocks of a given size to contain only the blocks that has actual data written to them. Otherwise blocks will contain zero bytes.
    /// </summary>
    public class BlockMemoryStream : Stream
    {
        private readonly int blockSize;
        private readonly bool skipZeroBytes;
        private readonly IDictionary<long, byte[]> blocks;
        private long length;
        private long position;

        public readonly ReadOnlyDictionary<long, byte[]> Blocks;

        public BlockMemoryStream(int blockSize = 512, bool skipZeroBytes = true)
        {
            if (blockSize % 512 != 0)
            {
                throw new ArgumentException("Block size must be dividable by 512", nameof(blockSize));
            }

            this.blockSize = blockSize;
            this.skipZeroBytes = skipZeroBytes;
            blocks = new Dictionary<long, byte[]>();
            Blocks = new ReadOnlyDictionary<long, byte[]>(blocks);
            length = 0;
            position = 0;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var blockPosition = (int)(position % blockSize);
            var blockOffset = position - blockPosition;

            var bytesRead = 0;
            int blockLength;
            for (var bufferOffset = offset;
                 bufferOffset < offset + count && position < length;
                 bufferOffset += blockLength)
            {
                // calculate block length
                blockLength = position + blockSize <= length ? blockSize - blockPosition : (int)(length - position);

                // calculate read length
                var readLength = bufferOffset + blockLength < offset + count
                    ? blockLength
                    : offset + count - bufferOffset;

                if (blocks.ContainsKey(blockOffset))
                {
                    // get existing block bytes or create new
                    var blockBytes = blocks.TryGetValue(blockOffset, out var block) ? block : new byte[blockSize];

                    // copy from block bytes to buffer
                    Array.Copy(blockBytes, blockPosition, buffer, bufferOffset, readLength);
                }
                else
                {
                    // block doesn't exist, fill with zeros
                    var end = bufferOffset + readLength - 1;
                    for (var start = bufferOffset; start < bufferOffset + readLength && start <= end; start++, end--)
                    {
                        buffer[start] = 0;
                        buffer[end] = 0;
                    }
                }

                // increase position by buffer length
                position += readLength;

                // increase block offset by block size and reset block position
                blockOffset += blockSize;
                blockPosition = 0;

                // increase bytes read by read length
                bytesRead += readLength;
            }

            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.End:
                    position = length;
                    break;
                case SeekOrigin.Begin:
                    position = offset;
                    break;
                case SeekOrigin.Current:
                    position += offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported origin '{origin}'");
            }

            return position;
        }

        public override void SetLength(long value)
        {
            length = value;

            foreach (var offset in blocks.Keys.ToList().Where(offset => offset >= value))
            {
                blocks.Remove(offset);
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var blockPosition = (int)(position % blockSize);
            var blockOffset = position - blockPosition;

            int blockLength;
            for (var bufferOffset = offset; bufferOffset < offset + count; bufferOffset += blockLength)
            {
                // calculate block length
                blockLength = blockSize - blockPosition;

                // copy block length from buffer to block bytes
                var bufferLength = bufferOffset + blockLength < offset + count
                    ? blockLength
                    : offset + count - bufferOffset;

                UpdateBlock(blockOffset, blockPosition, buffer, bufferOffset, bufferLength);

                // increase position by buffer length
                position += bufferLength;

                // increase block offset by block size and reset block position
                blockOffset += blockSize;
                blockPosition = 0;
            }

            if (position > length)
            {
                length = position;
            }
        }

        private void UpdateBlock(long blockOffset, int blockPosition, byte[] buffer, int bufferOffset, int bufferLength)
        {
            var blockExists = blocks.ContainsKey(blockOffset);

            var bufferIsZeroBytes = IsZeroBytes(buffer, bufferOffset, bufferLength);

            if (skipZeroBytes && !blockExists && bufferIsZeroBytes)
            {
                return;
            }

            // get existing block bytes or create new for block offset
            var blockBytes = blocks.TryGetValue(blockOffset, out var block) ? block : new byte[blockSize];

            // copy bytes from buffer to block bytes
            Array.Copy(buffer, bufferOffset, blockBytes, blockPosition, bufferLength);

            // examine if block bytes is all zero bytes after copy
            var blockIsZeroBytes = IsZeroBytes(blockBytes, 0, blockBytes.Length);

            // remote block bytes, if skip zero bytes, block exists and contain all zero bytes
            if (skipZeroBytes && blockExists && blockIsZeroBytes)
            {
                blocks.Remove(blockOffset);
                return;
            }

            // update block bytes at block offset
            blocks[blockOffset] = blockBytes;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => length;

        public override long Position
        {
            get => position;
            set => position = value;
        }

        public async Task WriteTo(Stream stream)
        {
            stream.SetLength(length);
            foreach (var block in blocks)
            {
                stream.Seek(block.Key, SeekOrigin.Begin);
                await stream.WriteAsync(block.Value, 0, block.Value.Length);
            }
        }

        private static bool IsZeroBytes(byte[] data, int offset, int count)
        {
            var endPosition = count - 1;
            for (var startPosition = 0;
                 startPosition < count && startPosition <= endPosition;
                 startPosition++, endPosition--)
            {
                if (data[offset + startPosition] != 0 || data[offset + endPosition] != 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}