// Ported from Lhasa lib/lha_decoder.c, https://github.com/fragglet/lhasa
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

    /// <summary>
    /// Base class for decoders, which decode a block of data at a time into an output buffer.
    /// </summary>
    internal abstract class BlockDecoder : ILhaDecoder
    {
        private readonly byte[] outbuf;
        private int outbufPos;
        private int outbufLen;
        private bool decoderFailed;

        /// <summary>
        /// Create block decoder.
        /// </summary>
        /// <param name="maxRead">Maximum number of bytes decoded by a single call to ReadBlock.</param>
        protected BlockDecoder(int maxRead)
        {
            outbuf = new byte[maxRead];
            outbufPos = 0;
            outbufLen = 0;
            decoderFailed = false;
        }

        /// <summary>
        /// Decode next block of data.
        /// </summary>
        /// <param name="buf">Buffer to write decoded data to, starting at offset 0.</param>
        /// <returns>Number of bytes decoded, 0 for failure or end of data.</returns>
        protected abstract int ReadBlock(byte[] buf);

        public int Read(byte[] buffer, int offset, int count)
        {
            // Try to fill up the buffer that has been passed with as much
            // data as possible. Each call to read() will fill up outbuf
            // with some data; this is then copied into buf, with some
            // data left at the end for the next call.
            var filled = 0;
            while (filled < count)
            {
                // Try to empty out some of the output buffer first.
                var bytes = outbufLen - outbufPos;
                if (count - filled < bytes)
                {
                    bytes = count - filled;
                }

                Array.Copy(outbuf, outbufPos, buffer, offset + filled, bytes);
                outbufPos += bytes;
                filled += bytes;

                // If we previously encountered a failure reading from
                // the decoder, don't try to call the read function again.
                if (decoderFailed)
                {
                    break;
                }

                // If outbuf is now empty, we can process another run to
                // re-fill it.
                if (outbufPos >= outbufLen)
                {
                    outbufLen = ReadBlock(outbuf);
                    outbufPos = 0;
                }

                // No more data to be read?
                if (outbufLen == 0)
                {
                    decoderFailed = true;
                    break;
                }
            }

            return filled;
        }
    }

    /// <summary>
    /// Decoder for data stored without compression (-lh0-, -lz4-, -pm0-).
    /// </summary>
    internal class NullDecoder : ILhaDecoder
    {
        private readonly System.IO.Stream packedData;

        public NullDecoder(System.IO.Stream packedData)
        {
            this.packedData = packedData;
        }

        public int Read(byte[] buffer, int offset, int count) => packedData.Read(buffer, offset, count);
    }
}
