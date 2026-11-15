namespace Hst.Compression.Lha
{
    using System;
    using System.IO;

    /// <summary>
    /// Base class for decoders of lz77 based methods. A decoder decodes commands, each producing either a literal byte
    /// or a copy of previously decoded bytes from a sliding window. Decoded bytes are written directly to the buffer
    /// being read into, a copy not fitting into the buffer continues on next read.
    /// </summary>
    internal abstract class WindowDecoder : ILhaDecoder
    {
        private readonly byte[] window;
        private readonly int windowMask;
        private readonly bool observeOutput;
        private int windowPosition;
        private int copySource;
        private int copyRemaining;

        private byte[] output;
        private int outputOffset;
        private int outputEnd;

        /// <summary>
        /// Create window decoder.
        /// </summary>
        /// <param name="windowBits">Size of window as power of 2.</param>
        /// <param name="initialValue">Initial value of all bytes in window.</param>
        /// <param name="observeOutput">Call <see cref="OnOutput"/> for every decoded byte.</param>
        protected WindowDecoder(int windowBits, byte initialValue, bool observeOutput = false)
        {
            window = new byte[1 << windowBits];
            windowMask = window.Length - 1;
            this.observeOutput = observeOutput;

            if (initialValue != 0)
            {
                for (var i = 0; i < window.Length; i++)
                {
                    window[i] = initialValue;
                }
            }
        }

        /// <summary>
        /// Window with previously decoded bytes.
        /// </summary>
        protected byte[] Window => window;

        /// <summary>
        /// Position in window, where next decoded byte is written.
        /// </summary>
        protected int WindowPosition
        {
            get => windowPosition;
            set => windowPosition = value & windowMask;
        }

        /// <summary>
        /// Decode next command, which must call either <see cref="Literal"/> or <see cref="Copy"/> once.
        /// </summary>
        /// <returns>True if a command was decoded, false if end of data is reached.</returns>
        protected abstract bool DecodeCommand();

        /// <summary>
        /// Called for every decoded byte, if enabled.
        /// </summary>
        protected virtual void OnOutput(byte value)
        {
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            output = buffer;
            outputOffset = offset;
            outputEnd = offset + count;

            try
            {
                while (outputOffset < outputEnd)
                {
                    if (copyRemaining > 0)
                    {
                        ContinueCopy();
                        continue;
                    }

                    if (!DecodeCommand())
                    {
                        break;
                    }
                }

                return outputOffset - offset;
            }
            finally
            {
                output = null;
            }
        }

        /// <summary>
        /// Output a literal byte.
        /// </summary>
        protected void Literal(byte value)
        {
            window[windowPosition] = value;
            windowPosition = (windowPosition + 1) & windowMask;
            output[outputOffset++] = value;

            if (observeOutput)
            {
                OnOutput(value);
            }
        }

        /// <summary>
        /// Output a copy of bytes previously decoded.
        /// </summary>
        /// <param name="distance">Distance back from current position to copy from, 1 is the last decoded byte.</param>
        /// <param name="length">Number of bytes to copy.</param>
        protected void Copy(int distance, int length) => CopyFromWindow(windowPosition - distance, length);

        /// <summary>
        /// Output a copy of bytes from an absolute position in window.
        /// </summary>
        /// <param name="position">Position in window to copy from.</param>
        /// <param name="length">Number of bytes to copy.</param>
        protected void CopyFromWindow(int position, int length)
        {
            if (length < 0)
            {
                throw new IOException("Invalid compressed data");
            }

            copySource = position & windowMask;
            copyRemaining = length;
            ContinueCopy();
        }

        private void ContinueCopy()
        {
            var count = Math.Min(copyRemaining, outputEnd - outputOffset);
            copyRemaining -= count;

            for (var i = 0; i < count; i++)
            {
                var value = window[copySource];
                copySource = (copySource + 1) & windowMask;
                window[windowPosition] = value;
                windowPosition = (windowPosition + 1) & windowMask;
                output[outputOffset++] = value;

                if (observeOutput)
                {
                    OnOutput(value);
                }
            }
        }
    }

    /// <summary>
    /// Decoder for data stored without compression (-lh0-, -lz4-, -pm0- and -lhd-).
    /// </summary>
    internal sealed class StoredDecoder : ILhaDecoder
    {
        private readonly Stream packedData;

        public StoredDecoder(Stream packedData)
        {
            this.packedData = packedData;
        }

        public int Read(byte[] buffer, int offset, int count) => packedData.Read(buffer, offset, count);
    }
}
