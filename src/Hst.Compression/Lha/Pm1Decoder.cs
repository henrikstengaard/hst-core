// Ported from Lhasa lib/pm1_decoder.c, https://github.com/fragglet/lhasa
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
    /// Decoder for -pm1- compressed files.
    ///
    /// This was difficult to put together. I can't find any versions of
    /// PMarc that will generate -pm1- encoded files (only -pm2-); however,
    /// the extraction tool, PMext, will extract them. I have therefore been
    /// able to reverse engineer the format and write a decoder. I am
    /// indebted to Alwin Henseler for publishing the Z80 assembly source to
    /// his UNPMA10 tool, which was apparently decompiled from the original
    /// PMarc and includes the -pm1- decoding code.
    /// </summary>
    internal class Pm1Decoder : BlockDecoder
    {
        // Size of the ring buffer used to hold the history.
        private const int RING_BUFFER_SIZE = 16384;

        // Maximum length of a command representing a block of bytes:
        private const int MAX_BYTE_BLOCK_LEN = 216;

        // Maximum number of bytes that can be copied by a single copy command.
        private const int MAX_COPY_BLOCK_LEN = 244;

        // Output buffer length. A single call to lha_pm1_read can perform one
        // byte block output followed by a copy command.
        private const int OUTPUT_BUFFER_SIZE = MAX_BYTE_BLOCK_LEN + MAX_COPY_BLOCK_LEN;

        // Table used to decode distance into history buffer to copy data.
        private static readonly VariableLengthTable[] CopyRanges =
        {
            new VariableLengthTable(0, 6), //    0 +  (1 << 6) =    64
            new VariableLengthTable(64, 8), //   64 +  (1 << 8) =   320
            new VariableLengthTable(0, 6), //    0 +  (1 << 6) =    64
            new VariableLengthTable(64, 9), //   64 +  (1 << 9) =   576
            new VariableLengthTable(576, 11), //  576 + (1 << 11) =  2624
            new VariableLengthTable(2624, 13), // 2624 + (1 << 13) = 10816

            // The above table entries are used after a certain number of
            // bytes have been decoded.
            // Early in the stream, some of the copy ranges are more limited
            // in their range, so that fewer bits are needed. The above
            // table entries are redirected to these entries instead.

            // Table entry #3 (64):
            new VariableLengthTable(64, 8), // < 320 bytes

            // Table entry #4 (576):
            new VariableLengthTable(576, 8), // < 832 bytes
            new VariableLengthTable(576, 9), // < 1088 bytes
            new VariableLengthTable(576, 10), // < 1600 bytes

            // Table entry #5 (2624):
            new VariableLengthTable(2624, 8), // < 2880 bytes
            new VariableLengthTable(2624, 9), // < 3136 bytes
            new VariableLengthTable(2624, 10), // < 3648 bytes
            new VariableLengthTable(2624, 11), // < 4672 bytes
            new VariableLengthTable(2624, 12), // < 6720 bytes
        };

        // Table used to decode byte values.
        private static readonly VariableLengthTable[] ByteRanges =
        {
            new VariableLengthTable(0, 4), //   0 + (1 << 4) = 16
            new VariableLengthTable(16, 4), //  16 + (1 << 4) = 32
            new VariableLengthTable(32, 5), //  32 + (1 << 5) = 64
            new VariableLengthTable(64, 6), //  64 + (1 << 6) = 128
            new VariableLengthTable(128, 6), // 128 + (1 << 6) = 191
            new VariableLengthTable(192, 6), // 192 + (1 << 6) = 255
        };

        // This table is a list of trees to decode indices into byte_ranges.
        // Each line is actually a mini binary tree, starting with the first
        // byte as the root node. Each nybble of the byte is one of the two
        // branches: either a leaf value (a-f) or an offset to the child node.
        // Expanded representation is shown in comments below.
        private static readonly byte[][] ByteDecodeTrees =
        {
            new byte[] { 0x12, 0x2d, 0xef, 0x1c, 0xab }, // ((((a b) c) d) (e f))
            new byte[] { 0x12, 0x23, 0xde, 0xab, 0xcf }, // (((a b) (c f)) (d e))
            new byte[] { 0x12, 0x2c, 0xd2, 0xab, 0xef }, // (((a b) c) (d (e f)))
            new byte[] { 0x12, 0xa2, 0xd2, 0xbc, 0xef }, // ((a (b c)) (d (e f)))
            new byte[] { 0x12, 0xa2, 0xc2, 0xbd, 0xef }, // ((a (b d)) (c (e f)))
            new byte[] { 0x12, 0xa2, 0xcd, 0xb1, 0xef }, // ((a (b (e f))) (c d))
            new byte[] { 0x12, 0xab, 0x12, 0xcd, 0xef }, // ((a b) ((c d) (e f)))
            new byte[] { 0x12, 0xab, 0x1d, 0xc1, 0xef }, // ((a b) ((c (e f)) d))
            new byte[] { 0x12, 0xab, 0xc1, 0xd1, 0xef }, // ((a b) (c (d (e f))))
            new byte[] { 0xa1, 0x12, 0x2c, 0xde, 0xbf }, // (a (((b f) c) (d e)))
            new byte[] { 0xa1, 0x1d, 0x1c, 0xb1, 0xef }, // (a (((b (e f)) c) d))
            new byte[] { 0xa1, 0x12, 0x2d, 0xef, 0xbc }, // (a (((b c) d) (e f)))
            new byte[] { 0xa1, 0x12, 0xb2, 0xde, 0xcf }, // (a ((b (c f)) (d e)))
            new byte[] { 0xa1, 0x12, 0xbc, 0xd1, 0xef }, // (a ((b c) (d (e f))))
            new byte[] { 0xa1, 0x1c, 0xb1, 0xd1, 0xef }, // (a ((b (d (e f))) c))
            new byte[] { 0xa1, 0xb1, 0x12, 0xcd, 0xef }, // (a (b ((c d) (e f))))
            new byte[] { 0xa1, 0xb1, 0xc1, 0xd1, 0xef }, // (a (b (c (d (e f)))))
            new byte[] { 0x12, 0x1c, 0xde, 0xab, 0x00 }, // (((d e) c) (d e)) <- BROKEN!
            new byte[] { 0x12, 0xa2, 0xcd, 0xbe, 0x00 }, // ((a (b e)) (c d))
            new byte[] { 0x12, 0xab, 0xc1, 0xde, 0x00 }, // ((a b) (c (d e)))
            new byte[] { 0xa1, 0x1d, 0x1c, 0xbe, 0x00 }, // (a (((b e) c) d))
            new byte[] { 0xa1, 0x12, 0xbc, 0xde, 0x00 }, // (a ((b c) (d e)))
            new byte[] { 0xa1, 0x1c, 0xb1, 0xde, 0x00 }, // (a ((b (d e)) c))
            new byte[] { 0xa1, 0xb1, 0xc1, 0xde, 0x00 }, // (a (b (c (d e))))
            new byte[] { 0x1d, 0x1c, 0xab, 0x00, 0x00 }, // (((a b) c) d)
            new byte[] { 0x1c, 0xa1, 0xbd, 0x00, 0x00 }, // ((a (b d)) c)
            new byte[] { 0x12, 0xab, 0xcd, 0x00, 0x00 }, // ((a b) (c d))
            new byte[] { 0xa1, 0x1c, 0xbd, 0x00, 0x00 }, // (a ((b d) c))
            new byte[] { 0xa1, 0xb1, 0xcd, 0x00, 0x00 }, // (a (b (c d)))
            new byte[] { 0xa1, 0xbc, 0x00, 0x00, 0x00 }, // (a (b c))
            new byte[] { 0xab, 0x00, 0x00, 0x00, 0x00 }, // (a b)
            new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 }, // -- special entry: 0, no tree
        };

        private readonly BitStreamReader bitStreamReader;

        // Position in output stream, in bytes.
        private int outputStreamPos;

        // Pointer to the entry in byte_decode_table used to decode
        // byte value indices.
        private byte[] byteDecodeTree;

        // History ring buffer.
        private readonly byte[] ringbuf = new byte[RING_BUFFER_SIZE];
        private int ringbufPos;

        // History linked list, for adaptively encoding byte values.
        private readonly HistoryLinkedList historyList = new HistoryLinkedList();

        public Pm1Decoder(Stream input) : base(OUTPUT_BUFFER_SIZE)
        {
            // Unlike other decoders, the bitstream reader fills zero bytes,
            // when the end of the compressed data is reached.
            bitStreamReader = new BitStreamReader(input, true);

            outputStreamPos = 0;
            byteDecodeTree = null;
            ringbufPos = 0;
        }

        // Read the 5-bit header from the start of the input stream. This
        // specifies the table entry to use for byte decodes.
        private bool ReadStartHeader()
        {
            var index = bitStreamReader.ReadBits(5);

            if (index < 0)
            {
                return false;
            }

            byteDecodeTree = ByteDecodeTrees[index];

            return true;
        }

        // Function called when a new byte is outputted, to update the
        // appropriate data structures.
        private void OutputtedByte(byte b)
        {
            // Add to history ring buffer.
            ringbuf[ringbufPos] = b;
            ringbufPos = (ringbufPos + 1) % RING_BUFFER_SIZE;

            // Other updates: history linked list, output stream position:
            historyList.Update(b);
            ++outputStreamPos;
        }

        // Decode a count of the number of bytes to copy in a copy command.
        // Returns -1 for failure.
        private int ReadCopyByteCount()
        {
            // This is a form of static huffman encoding that uses less bits
            // to encode short copy amounts (again).

            // Value in the range 3..5?
            // Length values start at 3: if it was 2, a different copy
            // range would have been used and this function would not
            // have been called.
            var x = bitStreamReader.ReadBits(2);

            if (x < 0)
            {
                return -1;
            }

            if (x < 3)
            {
                return x + 3;
            }

            // Value in range 6..10?
            x = bitStreamReader.ReadBits(3);

            if (x < 0)
            {
                return -1;
            }

            if (x < 5)
            {
                return x + 6;
            }

            // Value in range 11..14?
            if (x == 5)
            {
                x = bitStreamReader.ReadBits(2);

                return x < 0 ? -1 : x + 11;
            }

            // Value in range 15..22?
            if (x == 6)
            {
                x = bitStreamReader.ReadBits(3);

                return x < 0 ? -1 : x + 15;
            }

            // else x == 7...
            x = bitStreamReader.ReadBits(6);

            if (x < 0)
            {
                return -1;
            }

            if (x < 62)
            {
                return x + 23;
            }

            // Value in range 85..116?
            if (x == 62)
            {
                x = bitStreamReader.ReadBits(5);

                return x < 0 ? -1 : x + 85;
            }

            // Value in range 117..244?
            x = bitStreamReader.ReadBits(7);

            return x < 0 ? -1 : x + 117;
        }

        // Read a single bit from the input stream, but only once the specified
        // point is reached in the output stream. Before that point is reached,
        // return the value of 'def' instead. Returns -1 for error.
        private int ReadBitAfterThreshold(int threshold, int def)
        {
            return outputStreamPos >= threshold ? bitStreamReader.ReadBit() : def;
        }

        // Read the range index for the copy type used when performing a copy command.
        private int ReadCopyTypeRange()
        {
            // This is another static huffman tree, but the path grows as
            // more data is decoded. The progression is as follows:
            //  1. Initially, only '0' and '2' can be returned.
            //  2. After 64 bytes, '1' and '3' can be returned as well.
            //  3. After 576 bytes, '4' can be returned.
            //  4. After 2624 bytes, '5' can be returned.
            var x = bitStreamReader.ReadBit();

            if (x < 0)
            {
                return -1;
            }

            if (x == 0)
            {
                x = ReadBitAfterThreshold(576, 0);

                if (x < 0)
                {
                    return -1;
                }

                if (x != 0)
                {
                    return 4;
                }

                // Return either 0 or 1.
                return ReadBitAfterThreshold(64, 0);
            }

            x = ReadBitAfterThreshold(64, 1);

            if (x < 0)
            {
                return -1;
            }

            if (x == 0)
            {
                return 3;
            }

            x = ReadBitAfterThreshold(2624, 1);

            if (x < 0)
            {
                return -1;
            }

            return x != 0 ? 2 : 5;
        }

        // Read a copy command from the input stream and copy from history.
        // Returns 0 for failure.
        private int ReadCopyCommand(byte[] buf, int bufOffset)
        {
            int count;

            var rangeIndex = ReadCopyTypeRange();

            if (rangeIndex < 0)
            {
                return 0;
            }

            // The first two entries in the copy_ranges table are used as
            // a shorthand to copy two bytes. Otherwise, decode the number
            // of bytes to copy.
            if (rangeIndex < 2)
            {
                count = 2;
            }
            else
            {
                count = ReadCopyByteCount();

                if (count < 0)
                {
                    return 0;
                }
            }

            // The 'range_index' variable is an index into the copy_ranges
            // array. As a special-case hack, early in the output stream
            // some history ranges are inaccessible, so fewer bits can be
            // used. Redirect range_index to special entries to do this.
            if (rangeIndex == 3)
            {
                if (outputStreamPos < 320)
                {
                    rangeIndex = 6;
                }
            }
            else if (rangeIndex == 4)
            {
                if (outputStreamPos < 832)
                {
                    rangeIndex = 7;
                }
                else if (outputStreamPos < 1088)
                {
                    rangeIndex = 8;
                }
                else if (outputStreamPos < 1600)
                {
                    rangeIndex = 9;
                }
            }
            else if (rangeIndex == 5)
            {
                if (outputStreamPos < 2880)
                {
                    rangeIndex = 10;
                }
                else if (outputStreamPos < 3136)
                {
                    rangeIndex = 11;
                }
                else if (outputStreamPos < 3648)
                {
                    rangeIndex = 12;
                }
                else if (outputStreamPos < 4672)
                {
                    rangeIndex = 13;
                }
                else if (outputStreamPos < 6720)
                {
                    rangeIndex = 14;
                }
            }

            // Calculate the number of bytes back into the history buffer
            // to read.
            var historyDistance = VariableLengthTable.Decode(bitStreamReader, CopyRanges, rangeIndex);

            if (historyDistance < 0 || historyDistance >= outputStreamPos)
            {
                return 0;
            }

            // Copy from the ring buffer.
            var copyIndex = (ringbufPos + RING_BUFFER_SIZE - historyDistance - 1) % RING_BUFFER_SIZE;

            for (var i = 0; i < count; ++i)
            {
                buf[bufOffset + i] = ringbuf[copyIndex];
                OutputtedByte(ringbuf[copyIndex]);
                copyIndex = (copyIndex + 1) % RING_BUFFER_SIZE;
            }

            return count;
        }

        // Read the index into the byte decode table, using the byte_decode_tree
        // set at the start of the stream. Returns -1 for failure.
        private int ReadByteDecodeIndex()
        {
            var ptr = 0;

            if (byteDecodeTree[0] == 0)
            {
                return 0;
            }

            // Walk down the tree, reading a bit at each node to determine
            // which path to take.
            for (;;)
            {
                int child;
                var bit = bitStreamReader.ReadBit();

                if (bit < 0)
                {
                    return -1;
                }

                if (bit == 0)
                {
                    child = (byteDecodeTree[ptr] >> 4) & 0x0f;
                }
                else
                {
                    child = byteDecodeTree[ptr] & 0x0f;
                }

                // Reached a leaf node?
                if (child >= 10)
                {
                    return child - 10;
                }

                ptr += child;
            }
        }

        // Read a single byte value from the input stream.
        // Returns -1 for failure.
        private int ReadByte()
        {
            // Read the index into the byte_ranges table to use.
            var index = ReadByteDecodeIndex();

            if (index < 0)
            {
                return -1;
            }

            // Decode value using byte_ranges table. This is actually
            // a distance to walk along the history linked list - it
            // is static huffman encoding, so that recently used byte
            // values use fewer bits.
            var count = VariableLengthTable.Decode(bitStreamReader, ByteRanges, index);

            if (count < 0)
            {
                return -1;
            }

            // Walk through the history linked list to get the actual
            // value.
            return historyList.Find((byte)count);
        }

        // Read the length of a block of bytes.
        private int ReadByteBlockCount()
        {
            // This is a form of static huffman coding, where smaller
            // lengths are encoded using shorter bit sequences.

            // Value in the range 1..3?
            var x = bitStreamReader.ReadBits(2);

            if (x < 0)
            {
                return 0;
            }

            if (x < 3)
            {
                return x + 1;
            }

            // Value in the range 4..10?
            x = bitStreamReader.ReadBits(3);

            if (x < 0)
            {
                return 0;
            }

            if (x < 7)
            {
                return x + 4;
            }

            // Value in the range 11..25?
            x = bitStreamReader.ReadBits(4);

            if (x < 0)
            {
                return 0;
            }

            if (x < 14)
            {
                return x + 11;
            }

            if (x == 14)
            {
                // Value in the range 25-88:
                x = bitStreamReader.ReadBits(6);

                return x < 0 ? 0 : x + 25;
            }

            // x = 15
            // Value in the range 89-216:
            x = bitStreamReader.ReadBits(7);

            return x < 0 ? 0 : x + 89;
        }

        // Read a block of bytes from the input stream.
        // Returns 0 for failure.
        private int ReadByteBlock(byte[] buf)
        {
            // How many bytes to decode?
            var blockLen = ReadByteBlockCount();

            if (blockLen == 0)
            {
                return 0;
            }

            // Decode the byte values and add them to the output buffer.
            for (var i = 0; i < blockLen; ++i)
            {
                var byteval = ReadByte();

                if (byteval < 0)
                {
                    return 0;
                }

                buf[i] = (byte)byteval;
                OutputtedByte((byte)byteval);
            }

            var result = blockLen;

            // Because this is a block of bytes, it can be assumed that the
            // block ended for a copy command. The one exception is that if
            // the maximum block length was reached, the block may have
            // ended just because it could not be any larger.
            if (result == MAX_BYTE_BLOCK_LEN)
            {
                return result;
            }

            var result2 = ReadCopyCommand(buf, result);

            if (result2 == 0)
            {
                return 0;
            }

            return result + result2;
        }

        protected override int ReadBlock(byte[] buf)
        {
            // Start of input stream? Read the header.
            if (byteDecodeTree == null && !ReadStartHeader())
            {
                return 0;
            }

            // Read what type of command this is.
            var commandType = bitStreamReader.ReadBit();

            return commandType == 0 ? ReadCopyCommand(buf, 0) : ReadByteBlock(buf);
        }
    }
}
