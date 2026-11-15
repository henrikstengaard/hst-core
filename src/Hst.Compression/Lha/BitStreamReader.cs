// Ported from Lhasa lib/bit_stream_reader.c, https://github.com/fragglet/lhasa
//
// Copyright (c) 2011, 2012, Simon Howard
//
// Permission to use, copy, modify, and/or distribute this software
// for any purpose with or without fee is hereby granted, provided
// that the above copyright notice and this permission notice appear
// in all copies.
//
// THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL
// WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE
// AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR
// CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM
// LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT,
// NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN
// CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
//
namespace Hst.Compression.Lha
{
    using System;
    using System.IO;

    /// <summary>
    /// Reads bits from an input stream.
    /// </summary>
    internal class BitStreamReader
    {
        private readonly Stream input;
        private readonly bool zeroFillAtEnd;
        private readonly byte[] buf = new byte[4];

        // Bits from the input stream that are waiting to be read.
        private uint bitBuffer;
        private int bits;

        /// <summary>
        /// Create bit stream reader.
        /// </summary>
        /// <param name="input">Stream to read from.</param>
        /// <param name="zeroFillAtEnd">Read zero bytes, when end of stream is reached (used by -pm1-).</param>
        public BitStreamReader(Stream input, bool zeroFillAtEnd = false)
        {
            this.input = input;
            this.zeroFillAtEnd = zeroFillAtEnd;
            bits = 0;
            bitBuffer = 0;
        }

        /// <summary>
        /// Read bytes from input stream, equivalent of decoder callback.
        /// </summary>
        public int ReadBytes(byte[] buffer, int count)
        {
            var bytes = input.Read(buffer, 0, count);

            // There seem to be archive files that actually depend on the
            // ability to read "beyond" the length of the compressed data.
            if (bytes == 0 && zeroFillAtEnd)
            {
                Array.Clear(buffer, 0, count);
                bytes = count;
            }

            return bytes;
        }

        // Return the next n bits waiting to be read from the input stream,
        // without removing any.  Returns -1 for failure.
        public int PeekBits(int n)
        {
            if (n == 0)
            {
                return 0;
            }

            // If there are not enough bits in the buffer to satisfy this
            // request, we need to fill up the buffer with more bits.
            while (bits < n)
            {
                // Maximum number of bytes we can fill?
                var fillBytes = (32 - bits) / 8;

                // Read from input and fill bit_buffer.
                var bytes = ReadBytes(buf, fillBytes);

                // End of file?
                if (bytes == 0)
                {
                    return -1;
                }

                for (var i = 0; i < bytes; i++)
                {
                    bitBuffer |= (uint)buf[i] << (24 - bits);
                    bits += 8;
                }
            }

            return (int)(bitBuffer >> (32 - n));
        }

        // Read bits from the input stream.
        // Returns -1 for failure.
        public int ReadBits(int n)
        {
            var result = PeekBits(n);

            if (result >= 0 && n > 0)
            {
                bitBuffer <<= n;
                bits -= n;
            }

            return result;
        }

        // Read a bit from the input stream.
        // Returns -1 for failure.
        public int ReadBit()
        {
            return ReadBits(1);
        }
    }
}
