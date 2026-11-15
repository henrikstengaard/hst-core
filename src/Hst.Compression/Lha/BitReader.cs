namespace Hst.Compression.Lha
{
    using System.IO;

    /// <summary>
    /// Reads bits most significant bit first from compressed data. Up to 31 bits can be peeked or read at a time.
    /// </summary>
    internal sealed class BitReader
    {
        private readonly Stream input;
        private readonly bool zeroPadding;
        private readonly byte[] buffer = new byte[4096];
        private int bufferLength;
        private int bufferOffset;
        private bool endOfInput;

        // bits waiting to be consumed, left aligned
        private ulong cache;
        private int cachedBits;

        // number of bits at the end of cache, which are padding added after end of input
        private int paddingBits;

        /// <summary>
        /// Create bit reader.
        /// </summary>
        /// <param name="input">Stream with compressed data.</param>
        /// <param name="zeroPadding">Allow reading zero bits after end of input, otherwise an exception is thrown,
        /// when bits after end of input are consumed.</param>
        public BitReader(Stream input, bool zeroPadding = false)
        {
            this.input = input;
            this.zeroPadding = zeroPadding;
        }

        private int NextByte()
        {
            if (bufferOffset >= bufferLength)
            {
                if (endOfInput)
                {
                    return -1;
                }

                bufferLength = input.Read(buffer, 0, buffer.Length);
                bufferOffset = 0;
                if (bufferLength <= 0)
                {
                    bufferLength = 0;
                    endOfInput = true;
                    return -1;
                }
            }

            return buffer[bufferOffset++];
        }

        private void FillCache()
        {
            while (cachedBits <= 56)
            {
                var value = NextByte();
                if (value < 0)
                {
                    // pad with zeros, peeking past end of input is allowed
                    value = 0;
                    paddingBits += 8;
                }

                cache |= (ulong)value << (56 - cachedBits);
                cachedBits += 8;
            }
        }

        /// <summary>
        /// Peek bits without consuming them.
        /// </summary>
        /// <param name="count">Number of bits to peek, 0 to 31.</param>
        public int Peek(int count)
        {
            if (count == 0)
            {
                return 0;
            }

            if (cachedBits < count)
            {
                FillCache();
            }

            return (int)(cache >> (64 - count));
        }

        /// <summary>
        /// Consume bits.
        /// </summary>
        /// <param name="count">Number of bits to consume, 0 to 31.</param>
        public void Skip(int count)
        {
            if (cachedBits < count)
            {
                FillCache();
            }

            cache <<= count;
            cachedBits -= count;

            if (cachedBits >= paddingBits)
            {
                return;
            }

            if (!zeroPadding)
            {
                throw new IOException("Unexpected end of compressed data");
            }

            paddingBits = cachedBits;
        }

        /// <summary>
        /// Read bits.
        /// </summary>
        /// <param name="count">Number of bits to read, 0 to 31.</param>
        public int Read(int count)
        {
            var value = Peek(count);
            Skip(count);
            return value;
        }

        /// <summary>
        /// Read a single bit.
        /// </summary>
        public int ReadBit() => Read(1);
    }
}
