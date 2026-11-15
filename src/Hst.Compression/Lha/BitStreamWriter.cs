namespace Hst.Compression.Lha
{
    using System;
    using System.IO;

    /// <summary>
    /// Writes bits most significant bit first to a stream. Writing stops, when a limit of bytes is reached, which is
    /// used to detect data that can't be compressed without writing more bytes than the limit.
    /// </summary>
    internal class BitStreamWriter
    {
        private readonly Stream stream;
        private readonly long limit;
        private readonly byte[] buffer;
        private int bufferLength;
        private uint bits;
        private int bitCount;

        /// <summary>
        /// Number of bytes written.
        /// </summary>
        public long BytesWritten { get; private set; }

        /// <summary>
        /// True, if limit of bytes is exceeded. Bytes exceeding limit are not written to stream.
        /// </summary>
        public bool LimitExceeded { get; private set; }

        public BitStreamWriter(Stream stream, long limit)
        {
            this.stream = stream;
            this.limit = limit;
            buffer = new byte[8192];
        }

        /// <summary>
        /// Write value using count bits (0-16).
        /// </summary>
        public void WriteBits(int value, int count)
        {
            if (count == 0)
            {
                return;
            }

            bits = (bits << count) | (uint)(value & ((1 << count) - 1));
            bitCount += count;

            while (bitCount >= 8)
            {
                bitCount -= 8;
                WriteByte((byte)(bits >> bitCount));
            }
        }

        private void WriteByte(byte value)
        {
            if (LimitExceeded)
            {
                return;
            }

            if (BytesWritten >= limit)
            {
                LimitExceeded = true;
                return;
            }

            buffer[bufferLength++] = value;
            BytesWritten++;

            if (bufferLength == buffer.Length)
            {
                FlushBuffer();
            }
        }

        private void FlushBuffer()
        {
            if (bufferLength == 0)
            {
                return;
            }

            stream.Write(buffer, 0, bufferLength);
            bufferLength = 0;
        }

        /// <summary>
        /// Write remaining bits padded with zero bits to a whole byte and flush buffered bytes to stream.
        /// </summary>
        public void Flush()
        {
            if (bitCount > 0)
            {
                WriteBits(0, 8 - bitCount);
            }

            FlushBuffer();
        }
    }
}
