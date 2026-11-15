// Ported from Lhasa lib/lzs_decoder.c, https://github.com/fragglet/lhasa
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
    using System.IO;

    /// <summary>
    /// Decoder for the -lzs- compression method used by old versions of LArc.
    ///
    /// The input stream consists of commands, each of which is either "output
    /// a literal byte value" or "copy block". A bit at the start of each
    /// command signals which command it is.
    /// </summary>
    internal class LzsDecoder : BlockDecoder
    {
        // Parameters for ring buffer, used for storing history.  This acts
        // as the dictionary for copy operations.
        private const int RING_BUFFER_SIZE = 2048;
        private const int START_OFFSET = 17;

        // Threshold offset.  In the copy operation, the copy length is a 4-bit
        // value, giving a range 0..15.  The threshold offsets this so that it
        // is interpreted as 2..17 - a more useful range.
        private const int THRESHOLD = 2;

        // Size of output buffer.  Must be large enough to hold the results of
        // the maximum copy operation.
        private const int OUTPUT_BUFFER_SIZE = 15 + THRESHOLD;

        private readonly BitStreamReader bitStreamReader;
        private readonly byte[] ringbuf = new byte[RING_BUFFER_SIZE];
        private int ringbufPos;

        public LzsDecoder(Stream input) : base(OUTPUT_BUFFER_SIZE)
        {
            for (var i = 0; i < RING_BUFFER_SIZE; i++)
            {
                ringbuf[i] = (byte)' ';
            }

            ringbufPos = RING_BUFFER_SIZE - START_OFFSET;
            bitStreamReader = new BitStreamReader(input);
        }

        // Add a single byte to the output buffer.
        private void OutputByte(byte[] buf, ref int bufLen, byte b)
        {
            buf[bufLen] = b;
            ++bufLen;

            ringbuf[ringbufPos] = b;
            ringbufPos = (ringbufPos + 1) % RING_BUFFER_SIZE;
        }

        // Output a "block" of data from the specified range in the ring buffer.
        private void OutputBlock(byte[] buf, ref int bufLen, int start, int len)
        {
            for (var i = 0; i < len; ++i)
            {
                OutputByte(buf, ref bufLen, ringbuf[(start + i) % RING_BUFFER_SIZE]);
            }
        }

        // Process a single command from the LZS input stream.
        protected override int ReadBlock(byte[] buf)
        {
            // Start from an empty buffer.
            var result = 0;

            // Each command starts with a bit that signals the type:
            var bit = bitStreamReader.ReadBit();

            if (bit < 0)
            {
                return 0;
            }

            // What type of command is this?
            if (bit != 0)
            {
                var b = bitStreamReader.ReadBits(8);

                if (b < 0)
                {
                    return 0;
                }

                OutputByte(buf, ref result, (byte)b);
            }
            else
            {
                var pos = bitStreamReader.ReadBits(11);
                var len = bitStreamReader.ReadBits(4);

                if (pos < 0 || len < 0)
                {
                    return 0;
                }

                OutputBlock(buf, ref result, pos, len + THRESHOLD);
            }

            return result;
        }
    }
}
