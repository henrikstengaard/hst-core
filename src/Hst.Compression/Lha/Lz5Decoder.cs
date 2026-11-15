// Ported from Lhasa lib/lz5_decoder.c, https://github.com/fragglet/lhasa
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
    /// Decoder for the -lz5- compression method used by LArc.
    ///
    /// This processes "runs" of eight commands, each of which is either
    /// "output a character" or "copy block".  The result of that run
    /// is written into the output buffer.
    /// </summary>
    internal class Lz5Decoder : BlockDecoder
    {
        // Parameters for ring buffer, used for storing history.  This acts
        // as the dictionary for copy operations.
        private const int RING_BUFFER_SIZE = 4096;
        private const int START_OFFSET = 18;

        // Threshold offset.  In the copy operation, the copy length is a 4-bit
        // value, giving a range 0..15.  The threshold offsets this so that it
        // is interpreted as 3..18 - a more useful range.
        private const int THRESHOLD = 3;

        // Size of output buffer.  Must be large enough to hold the results of
        // a complete "run" (see below).
        private const int OUTPUT_BUFFER_SIZE = (15 + THRESHOLD) * 8;

        private readonly Stream input;
        private readonly byte[] ringbuf = new byte[RING_BUFFER_SIZE];
        private readonly byte[] cmd = new byte[2];
        private int ringbufPos;

        public Lz5Decoder(Stream input) : base(OUTPUT_BUFFER_SIZE)
        {
            this.input = input;
            FillInitial();
            ringbufPos = RING_BUFFER_SIZE - START_OFFSET;
        }

        private void FillInitial()
        {
            var p = 0;

            // For each byte value, the history buffer includes a run of 13
            // bytes all with that value. This is useful eg. for text files
            // that include a long run like this (eg. ===========).
            for (var i = 0; i < 256; ++i)
            {
                for (var j = 0; j < 13; ++j)
                {
                    ringbuf[p++] = (byte)i;
                }
            }

            // Next we include all byte values ascending and descending.
            for (var i = 0; i < 256; ++i)
            {
                ringbuf[p++] = (byte)i;
            }

            for (var i = 0; i < 256; ++i)
            {
                ringbuf[p++] = (byte)(255 - i);
            }

            // Block of zeros, and then ASCII space characters. I think these are
            // towards the end of the range because they're most likely to be
            // useful and therefore last to get overwritten?
            for (var i = 0; i < 128; ++i)
            {
                ringbuf[p++] = 0;
            }

            for (var i = 0; i < 110; ++i)
            {
                ringbuf[p++] = (byte)' ';
            }

            // Final 18 characters are all zeros, probably because of START_OFFSET.
            for (var i = 0; i < 18; ++i)
            {
                ringbuf[p++] = 0;
            }
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

        // Process a "run" of LZ5-compressed data (a control byte followed by
        // eight "commands").
        protected override int ReadBlock(byte[] buf)
        {
            // Start from an empty buffer.
            var result = 0;

            // Read the bitmap byte first.
            if (input.Read(cmd, 0, 1) == 0)
            {
                return 0;
            }

            var bitmap = cmd[0];

            // Each bit in the bitmap is a command.
            // If the bit is set, it is an "output byte" command.
            // If it is not set, it is a "copy block" command.
            for (var bit = 0; bit < 8; ++bit)
            {
                if ((bitmap & (1 << bit)) != 0)
                {
                    if (input.Read(cmd, 0, 1) == 0)
                    {
                        break;
                    }

                    OutputByte(buf, ref result, cmd[0]);
                }
                else
                {
                    if (input.Read(cmd, 0, 2) == 0)
                    {
                        break;
                    }

                    var seqstart = ((cmd[1] & 0xf0) << 4) | cmd[0];
                    var seqlen = (cmd[1] & 0x0f) + THRESHOLD;

                    OutputBlock(buf, ref result, seqstart, seqlen);
                }
            }

            return result;
        }
    }
}
