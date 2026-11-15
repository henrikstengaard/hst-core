namespace Hst.Compression.Lha
{
    using System;
    using System.IO;

    /// <summary>
    /// Read-only stream with compressed data of a lha entry, limited to packed size of entry.
    /// If archive stream is seekable, position is restored before each read, so multiple entries can be read
    /// interleaved from the same archive stream. If archive stream is not seekable, it must be positioned at start of
    /// entry data and is read sequentially.
    /// Reads fill buffer completely unless end of compressed data is reached.
    /// </summary>
    internal class PackedDataStream : Stream
    {
        private readonly Stream stream;
        private readonly long offset;
        private readonly long length;
        private long position;

        public PackedDataStream(Stream stream, long offset, long length)
        {
            this.stream = stream;
            this.offset = offset;
            this.length = length;
            position = 0;
        }

        public override int Read(byte[] buffer, int bufferOffset, int count)
        {
            var remaining = length - position;
            if (count > remaining)
            {
                count = (int)remaining;
            }

            if (count <= 0)
            {
                return 0;
            }

            if (stream.CanSeek)
            {
                stream.Seek(offset + position, SeekOrigin.Begin);
            }

            var total = 0;
            while (total < count)
            {
                var read = stream.Read(buffer, bufferOffset + total, count - total);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            position += total;
            return total;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;

        public override long Position
        {
            get => position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override long Seek(long seekOffset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int bufferOffset, int count) => throw new NotSupportedException();
    }
}
