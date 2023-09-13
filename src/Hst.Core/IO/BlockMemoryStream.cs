using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

namespace Hst.Core.IO
{
    /// <summary>
    /// Block memory stream is used to represent a large stream using blocks of a given size to contain only the blocks that has actual data written to them. Otherwise blocks will contain zero bytes.
    /// </summary>
    public class BlockMemoryStream : Stream
    {
        private readonly int blockSize;
        private readonly IDictionary<long, byte[]> blocks;
        private long length;
        private long position;

        public readonly ReadOnlyDictionary<long, byte[]> Blocks;

        public BlockMemoryStream(int blockSize = 512)
        {
            if (blockSize % 512 != 0)
            {
                throw new ArgumentException("Block size must be dividable by 512", nameof(blockSize));
            }

            this.blockSize = blockSize;
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
            var blockOffset = position / blockSize;
            var blockPosition = (int)(position % blockSize);

            var bytesRead = 0;
            int blockLength;
            for (var i = offset; i < Math.Min(buffer.Length, offset + count) && i < length - position; i += blockLength)
            {
                // calculate block length
                blockLength = position + blockSize < length ? blockSize - blockPosition : (int)(length - position);

                // calculate read length
                var readLength = i + blockLength < offset + count ? blockLength : offset + count - i;
                
                if (!blocks.ContainsKey(blockOffset))
                {
                    // increase position by read length
                    position += readLength;
                    
                    break;
                }

                // get existing block bytes or create new
                var blockBytes = blocks.TryGetValue(blockOffset, out var block) ? block : new byte[blockSize];

                // copy from block bytes to buffer
                Array.Copy(blockBytes, blockPosition, buffer, i, readLength);
                
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
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var blockOffset = (position + offset) / blockSize;
            var blockPosition = (int)((position + offset) % blockSize);

            int blockLength;
            for (var i = offset; i < Math.Min(buffer.Length, offset + count); i += blockLength)
            {
                // get existing block bytes or create new
                var blockBytes = blocks.TryGetValue(blockOffset, out var block) ? block : new byte[blockSize];

                // calculate block length
                blockLength = blockSize - blockPosition;

                // copy block length from buffer to block bytes
                var bufferLength = i + blockLength < offset + count ? blockLength : offset + count - i;
                Array.Copy(buffer, i, blockBytes, blockPosition, bufferLength);
                
                // update block bytes
                blocks[blockOffset] = blockBytes;
                
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
    }
}