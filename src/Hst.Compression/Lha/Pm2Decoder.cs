// Ported from Lhasa lib/pm2_decoder.c, https://github.com/fragglet/lhasa
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
    /// Decoder for PMarc -pm2- compression format.  PMarc is a variant
    /// of LHA commonly used on the MSX computer architecture.
    /// </summary>
    internal class Pm2Decoder : BlockDecoder
    {
        // Size of the ring buffer (in bytes) used to store past history
        // for copies.
        private const int RING_BUFFER_SIZE = 8192;

        // Maximum number of bytes that might be placed in the output buffer
        // from a single call to lha_pm2_decoder_read (largest copy size).
        private const int OUTPUT_BUFFER_SIZE = 256;

        // Number of tree elements in the code tree.
        private const int CODE_TREE_ELEMENTS = 65;

        // Number of tree elements in the offset tree.
        private const int OFFSET_TREE_ELEMENTS = 17;

        private enum Pm2RebuildState
        {
            Unbuilt, // At start of stream
            Build1, // After 1KiB
            Build2, // After 2KiB
            Build3, // After 4KiB
            Continuing, // 8KiB onwards...
        }

        // Decode table for history value. Characters that appeared recently in
        // the history are more likely than ones that appeared a long time ago,
        // so the history value is huffman coded so that small values require
        // fewer bits. The history value is then used to search within the
        // history linked list to get the actual character.
        private static readonly VariableLengthTable[] HistoryDecode =
        {
            new VariableLengthTable(0, 3), //   0 + (1 << 3) =   8
            new VariableLengthTable(8, 3), //   8 + (1 << 3) =  16
            new VariableLengthTable(16, 4), //  16 + (1 << 4) =  32
            new VariableLengthTable(32, 5), //  32 + (1 << 5) =  64
            new VariableLengthTable(64, 5), //  64 + (1 << 5) =  96
            new VariableLengthTable(96, 5), //  96 + (1 << 5) = 128
            new VariableLengthTable(128, 6), // 128 + (1 << 6) = 192
            new VariableLengthTable(192, 6), // 192 + (1 << 6) = 256
        };

        // Decode table for copies. As with history_decode[], small copies
        // are more common, and require fewer bits.
        private static readonly VariableLengthTable[] CopyDecode =
        {
            new VariableLengthTable(17, 3), //  17 + (1 << 3) =  25
            new VariableLengthTable(25, 3), //  25 + (1 << 3) =  33
            new VariableLengthTable(33, 5), //  33 + (1 << 5) =  65
            new VariableLengthTable(65, 6), //  65 + (1 << 6) = 129
            new VariableLengthTable(129, 7), // 129 + (1 << 7) = 256
            new VariableLengthTable(256, 0), // 256 (unique value)
        };

        private readonly BitStreamReader bitStreamReader;

        // State of decode tree.
        private Pm2RebuildState treeState;

        // Number of bytes until we initiate a tree rebuild.
        private int treeRebuildRemaining;

        // History ring buffer, for copies:
        private readonly byte[] ringbuf = new byte[RING_BUFFER_SIZE];
        private int ringbufPos;

        // History linked list, for adaptively encoding byte values.
        private readonly HistoryLinkedList historyList = new HistoryLinkedList();

        // Array representing the huffman tree used for representing
        // code values. A given node of the tree has children
        // code_tree[n] and code_tree[n + 1].  code_tree[0] is the
        // root node.
        private readonly ushort[] codeTree = new ushort[CODE_TREE_ELEMENTS];

        // If false, we don't need an offset tree:
        private bool needOffsetTree;

        // Array representing huffman tree used to look up offsets.
        // Same format as code_tree[].
        private readonly ushort[] offsetTree = new ushort[OFFSET_TREE_ELEMENTS];

        public Pm2Decoder(Stream input) : base(OUTPUT_BUFFER_SIZE)
        {
            bitStreamReader = new BitStreamReader(input);

            // Tree has not been built yet.  It needs to be built on
            // the first call to read().
            treeState = Pm2RebuildState.Unbuilt;
            treeRebuildRemaining = 0;

            // Initialize ring buffer contents.
            for (var i = 0; i < RING_BUFFER_SIZE; i++)
            {
                ringbuf[i] = (byte)' ';
            }

            ringbufPos = 0;

            // Initialize the lookup trees to a known state.
            TreeDecoder.InitTree(codeTree, CODE_TREE_ELEMENTS);
            TreeDecoder.InitTree(offsetTree, OFFSET_TREE_ELEMENTS);
        }

        // Read the list of code lengths to use for the code tree and construct
        // the code_tree structure.
        private bool ReadCodeTree()
        {
            var codeLengths = new byte[31];

            // Read the number of codes in the tree.
            var numCodes = bitStreamReader.ReadBits(5);

            // Read min_code_length, which is used as an offset.
            var minCodeLength = bitStreamReader.ReadBits(3);

            if (minCodeLength < 0 || numCodes < 0)
            {
                return false;
            }

            // Code values > 28 can potentially lead to an overflow of the
            // copy_decode table (GHSA-j2m3-h278-rrg9). We prevent this by
            // checking there are no more than 29 codes (the 28/29
            // difference here is not an error; 0-28 is 29 codes).
            if (numCodes > 29)
            {
                return false;
            }

            // Store flag variable indicating whether we want to read
            // the offset tree as well.
            needOffsetTree = numCodes >= 10 && !(numCodes == 29 && minCodeLength == 0);

            // Minimum length of zero means a tree containing a single code.
            if (minCodeLength == 0)
            {
                TreeDecoder.SetTreeSingle(codeTree, numCodes - 1);
                return true;
            }

            // How many bits are used to represent each table entry?
            var lengthBits = bitStreamReader.ReadBits(3);

            if (lengthBits < 0)
            {
                return false;
            }

            // Read table of code lengths:
            for (var i = 0; i < numCodes; ++i)
            {
                // Read a table entry.  A value of zero represents an
                // unused code.  Otherwise the value represents
                // an offset from the minimum length (previously read).
                var val = bitStreamReader.ReadBits(lengthBits);

                if (val < 0)
                {
                    return false;
                }

                codeLengths[i] = val == 0 ? (byte)0 : (byte)(minCodeLength + val - 1);
            }

            // Build the tree.
            TreeDecoder.BuildTree(codeTree, CODE_TREE_ELEMENTS, codeLengths, numCodes);

            return true;
        }

        // Read the code lengths for the offset tree and construct the offset
        // tree lookup table.
        private bool ReadOffsetTree(int numOffsets)
        {
            var offsetLengths = new byte[8];

            if (!needOffsetTree)
            {
                return true;
            }

            // Read 'num_offsets' 3-bit length values.  For each offset
            // value 'off', offset_lengths[off] is the length of the
            // code that will represent 'off', or 0 if it will not
            // appear within the tree.
            var numCodes = 0;
            var singleOffset = 0;

            for (var off = 0; off < numOffsets; ++off)
            {
                var len = bitStreamReader.ReadBits(3);

                if (len < 0)
                {
                    return false;
                }

                offsetLengths[off] = (byte)len;

                // Track how many actual codes were in the tree.
                if (len != 0)
                {
                    singleOffset = off;
                    ++numCodes;
                }
            }

            // If there was a single code, this is a single node tree.
            if (numCodes == 1)
            {
                TreeDecoder.SetTreeSingle(offsetTree, singleOffset);
                return true;
            }

            // Build the tree.
            TreeDecoder.BuildTree(offsetTree, OFFSET_TREE_ELEMENTS, offsetLengths, numOffsets);

            return true;
        }

        // Rebuild the decode trees used to compress data.  This is called when
        // decoder->tree_rebuild_remaining reaches zero.
        private void RebuildTree()
        {
            switch (treeState)
            {
                // Initial tree build, from start of stream:
                case Pm2RebuildState.Unbuilt:
                    ReadCodeTree();
                    ReadOffsetTree(5);
                    treeState = Pm2RebuildState.Build1;
                    treeRebuildRemaining = 1024;
                    break;

                // Tree rebuild after 1KiB of data has been read:
                case Pm2RebuildState.Build1:
                    ReadOffsetTree(6);
                    treeState = Pm2RebuildState.Build2;
                    treeRebuildRemaining = 1024;
                    break;

                // Tree rebuild after 2KiB of data has been read:
                case Pm2RebuildState.Build2:
                    ReadOffsetTree(7);
                    treeState = Pm2RebuildState.Build3;
                    treeRebuildRemaining = 2048;
                    break;

                // Tree rebuild after 4KiB of data has been read:
                case Pm2RebuildState.Build3:
                    if (bitStreamReader.ReadBit() == 1)
                    {
                        ReadCodeTree();
                    }

                    ReadOffsetTree(8);
                    treeState = Pm2RebuildState.Continuing;
                    treeRebuildRemaining = 4096;
                    break;

                // Tree rebuild after 8KiB of data has been read,
                // and every 4KiB after that:
                case Pm2RebuildState.Continuing:
                    if (bitStreamReader.ReadBit() == 1)
                    {
                        ReadCodeTree();
                        ReadOffsetTree(8);
                    }

                    treeRebuildRemaining = 4096;
                    break;
            }
        }

        private void OutputByte(byte[] buf, ref int bufLen, byte b)
        {
            // Add to history ring buffer.
            ringbuf[ringbufPos] = b;
            ringbufPos = (ringbufPos + 1) % RING_BUFFER_SIZE;

            // Add to output buffer.
            buf[bufLen] = b;
            ++bufLen;

            // Update history chain.
            historyList.Update(b);

            // Count down until it is time to perform a rebuild of the
            // lookup trees.
            --treeRebuildRemaining;

            if (treeRebuildRemaining == 0)
            {
                RebuildTree();
            }
        }

        // Read a single byte from the input stream and add it to the output
        // buffer.
        private void ReadSingleByte(int code, byte[] buf, ref int bufLen)
        {
            var offset = VariableLengthTable.Decode(bitStreamReader, HistoryDecode, code);

            if (offset < 0)
            {
                return;
            }

            var b = historyList.Find((byte)offset);
            OutputByte(buf, ref bufLen, b);
        }

        // Calculate how many bytes from history to copy:
        private int HistoryGetCount(int code)
        {
            // How many bytes to copy?  A small value represents the
            // literal number of bytes to copy; larger values are a header
            // for a variable length value to be decoded.
            if (code < 15)
            {
                return code + 2;
            }

            return VariableLengthTable.Decode(bitStreamReader, CopyDecode, code - 15);
        }

        // Calculate the offset within history at which to start copying:
        private int HistoryGetOffset(int code)
        {
            int bits;
            var result = 0;

            // Calculate number of bits to read.

            // Code of zero indicates a simple 6-bit value giving the offset.
            if (code == 0)
            {
                bits = 6;
            }

            // Mid-range encoded offset value.
            // Read a code using the offset tree, indicating the length
            // of the offset value to follow.  The code indicates the
            // number of bits (values 0-7 = 6-13 bits).
            else if (code < 20)
            {
                var val = TreeDecoder.ReadFromTree(bitStreamReader, offsetTree);

                if (val < 0)
                {
                    return -1;
                }

                if (val == 0)
                {
                    bits = 6;
                }
                else
                {
                    bits = val + 5;
                    result = 1 << bits;
                }
            }

            // Large copy values start from offset zero.
            else
            {
                return 0;
            }

            // Read a number of bits representing the offset value.  The
            // number of length of this value is variable, and is calculated
            // above.
            var value = bitStreamReader.ReadBits(bits);

            if (value < 0)
            {
                return -1;
            }

            result += value;

            return result;
        }

        private void CopyFromHistory(int code, byte[] buf, ref int bufLen)
        {
            // Read number of bytes to copy and offset within history to copy
            // from.
            var toCopy = HistoryGetCount(code);
            var offset = HistoryGetOffset(code);

            if (toCopy < 0 || offset < 0)
            {
                return;
            }

            // Sanity check to prevent the potential for buffer overflow.
            if (toCopy > OUTPUT_BUFFER_SIZE)
            {
                return;
            }

            // Perform copy.
            var start = (uint)(ringbufPos + RING_BUFFER_SIZE - 1 - offset);

            for (var i = 0; i < toCopy; ++i)
            {
                var pos = (start + (uint)i) % RING_BUFFER_SIZE;

                OutputByte(buf, ref bufLen, ringbuf[pos]);
            }
        }

        // Decode data and store it into buf[], returning the number of
        // bytes decoded.
        protected override int ReadBlock(byte[] buf)
        {
            // On first pass through, build initial lookup trees.
            if (treeState == Pm2RebuildState.Unbuilt)
            {
                // First bit in stream is discarded?
                bitStreamReader.ReadBit();
                RebuildTree();
            }

            var result = 0;

            var code = TreeDecoder.ReadFromTree(bitStreamReader, codeTree);

            if (code < 0)
            {
                return 0;
            }

            if (code < 8)
            {
                ReadSingleByte(code, buf, ref result);
            }
            else
            {
                CopyFromHistory(code - 8, buf, ref result);
            }

            return result;
        }
    }
}
