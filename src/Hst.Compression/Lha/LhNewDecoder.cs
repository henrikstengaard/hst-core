// Ported from Lhasa lib/lh_new_decoder.c, lh5_decoder.c, lh6_decoder.c, lh7_decoder.c, lhx_decoder.c and
// lk7_decoder.c, https://github.com/fragglet/lhasa
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
    /// Decoder for "new-style" LHA algorithms, used with LHA v2 and onwards
    /// (-lh4-, -lh5-, -lh6-, -lh7-, -lhx- and LHARK's -lh7- renamed to -lk7-).
    /// </summary>
    internal class LhNewDecoder : BlockDecoder
    {
        // Threshold for copying. The first copy code starts from here.
        private const int COPY_THRESHOLD = 3; /* bytes */

        // Number of possible codes in the "temporary table" used to encode the
        // codes table. This is a function of the number of bits used to encode
        // the field that contains the number of temp codes.
        private const int TEMP_CODE_BITS = 5;
        private const int MAX_TEMP_CODES = (1 << TEMP_CODE_BITS) - 1;

        private readonly int ringBufferSize;
        private readonly int offsetBits;
        private readonly int numCodes;
        private readonly int maxOffsetCodes;
        private readonly bool lhark;

        // Input bit stream.
        private readonly BitStreamReader bitStreamReader;

        // Ring buffer of past data.  Used for position-based copies.
        private readonly byte[] ringbuf;
        private int ringbufPos;

        // Number of commands remaining before we start a new block.
        private int blockRemaining;

        // This table is used to encode the temp-tree, which
        // is itself used to encode the code tree.
        private readonly ushort[] tempTree;

        // Table used for the code tree.
        private readonly ushort[] codeTree;

        // Table used to encode the offset tree, used to read offsets
        // into the history buffer.
        private readonly ushort[] offsetTree;

        /// <summary>
        /// Create decoder for -lh4- and -lh5- (16 KiB history ring buffer).
        /// </summary>
        public static LhNewDecoder CreateLh5(Stream input) => new LhNewDecoder(input, 14, 4, 510, false);

        /// <summary>
        /// Create decoder for -lh6- (64 KiB history ring buffer).
        /// </summary>
        public static LhNewDecoder CreateLh6(Stream input) => new LhNewDecoder(input, 16, 5, 510, false);

        /// <summary>
        /// Create decoder for -lh7- (128 KiB history ring buffer).
        /// </summary>
        public static LhNewDecoder CreateLh7(Stream input) => new LhNewDecoder(input, 17, 5, 510, false);

        /// <summary>
        /// Create decoder for -lhx-, Unlha32.dll's original extension. -lhx-'s maximum dictionary size is 2^19,
        /// 2x ring buffer is required.
        /// </summary>
        public static LhNewDecoder CreateLhx(Stream input) => new LhNewDecoder(input, 20, 5, 510, false);

        /// <summary>
        /// Create decoder for -lk7-, AKA LHARK's -lh7-. This algorithm is a modified version of -lh5- that
        /// appeared in Kerwin Medina's LHARK tool named as -lh7-.
        /// </summary>
        public static LhNewDecoder CreateLk7(Stream input) => new LhNewDecoder(input, 16, 6, 289, true);

        private LhNewDecoder(Stream input, int historyBits, int offsetBits, int numCodes, bool lhark)
            : base(1 << historyBits) // At most, a single call to read() might result in a copy of the entire ring buffer.
        {
            ringBufferSize = 1 << historyBits;
            this.offsetBits = offsetBits;
            this.numCodes = numCodes;
            maxOffsetCodes = (1 << offsetBits) - 1;
            this.lhark = lhark;

            // Initialize input stream reader.
            bitStreamReader = new BitStreamReader(input);

            // Initialize data structures.
            ringbuf = new byte[ringBufferSize];
            InitRingBuffer();

            // First read starts the first block.
            blockRemaining = 0;

            // Initialize tree tables to a known state.
            tempTree = new ushort[MAX_TEMP_CODES * 2];
            codeTree = new ushort[numCodes * 2];
            offsetTree = new ushort[maxOffsetCodes * 2];
            TreeDecoder.InitTree(codeTree, numCodes * 2);
            TreeDecoder.InitTree(offsetTree, maxOffsetCodes * 2);
            TreeDecoder.InitTree(tempTree, MAX_TEMP_CODES * 2);
        }

        // Initialize the history ring buffer.
        private void InitRingBuffer()
        {
            for (var i = 0; i < ringBufferSize; i++)
            {
                ringbuf[i] = (byte)' ';
            }

            ringbufPos = 0;
        }

        // Read a length value - this is normally a value in the 0-7 range, but
        // sometimes can be longer.
        private int ReadLengthValue()
        {
            var len = bitStreamReader.ReadBits(3);

            if (len < 0)
            {
                return -1;
            }

            if (len == 7)
            {
                // Read more bits to extend the length until we reach a '0'.
                for (;;)
                {
                    var i = bitStreamReader.ReadBit();

                    if (i < 0)
                    {
                        return -1;
                    }

                    if (i == 0)
                    {
                        break;
                    }

                    ++len;
                }
            }

            return len;
        }

        // Read the values from the input stream that define the temporary table
        // used for encoding the code table.
        private bool ReadTempTable()
        {
            var codeLengths = new byte[MAX_TEMP_CODES];

            // How many codes?
            var n = bitStreamReader.ReadBits(TEMP_CODE_BITS);

            if (n < 0)
            {
                return false;
            }

            // n=0 is a special case, meaning only a single code that
            // is of zero length.
            if (n == 0)
            {
                var code = bitStreamReader.ReadBits(5);

                if (code < 0)
                {
                    return false;
                }

                TreeDecoder.SetTreeSingle(tempTree, code);
                return true;
            }

            // Enforce a hard limit on the number of codes.
            if (n > MAX_TEMP_CODES)
            {
                n = MAX_TEMP_CODES;
            }

            // Read the length of each code.
            for (var i = 0; i < n; ++i)
            {
                var len = ReadLengthValue();

                if (len < 0)
                {
                    return false;
                }

                codeLengths[i] = (byte)len;

                // After the first three lengths, there is a 2-bit
                // field to allow skipping over up to a further three
                // lengths. Not sure of the reason for this ...
                if (i == 2)
                {
                    len = bitStreamReader.ReadBits(2);

                    if (len < 0)
                    {
                        return false;
                    }

                    for (var j = 0; j < len; ++j)
                    {
                        ++i;
                        codeLengths[i] = 0;
                    }
                }
            }

            TreeDecoder.BuildTree(tempTree, MAX_TEMP_CODES * 2, codeLengths, n);

            return true;
        }

        // Code table codes can indicate that a sequence of codes should be
        // skipped over. The number to skip is Huffman-encoded. Given a skip
        // range (0-2), this reads the number of codes to skip over.
        private int ReadSkipCount(int skiprange)
        {
            int result;

            // skiprange=0 => 1 code.
            if (skiprange == 0)
            {
                result = 1;
            }

            // skiprange=1 => 3-18 codes.
            else if (skiprange == 1)
            {
                result = bitStreamReader.ReadBits(4);

                if (result < 0)
                {
                    return -1;
                }

                result += 3;
            }

            // skiprange=2 => 20+ codes.
            else
            {
                result = bitStreamReader.ReadBits(9);

                if (result < 0)
                {
                    return -1;
                }

                result += 20;
            }

            return result;
        }

        private bool ReadCodeTable()
        {
            var codeLengths = new byte[numCodes];

            // How many codes?
            var n = bitStreamReader.ReadBits(9);

            if (n < 0)
            {
                return false;
            }

            // n=0 implies a single code of zero length; all inputs
            // decode to the same code.
            int code;
            if (n == 0)
            {
                code = bitStreamReader.ReadBits(9);

                if (code < 0)
                {
                    return false;
                }

                TreeDecoder.SetTreeSingle(codeTree, code);

                return true;
            }

            if (n > numCodes)
            {
                n = numCodes;
            }

            // Read the length of each code.
            // The lengths are encoded using the temp-table previously read.
            var i = 0;

            while (i < n)
            {
                code = TreeDecoder.ReadFromTree(bitStreamReader, tempTree);

                if (code < 0)
                {
                    return false;
                }

                // The code that was read can have different meanings.
                // If in the range 0-2, it indicates that a number of
                // codes are unused and should be skipped over.
                // Values greater than two represent a frequency count.
                if (code <= 2)
                {
                    var skipCount = ReadSkipCount(code);

                    if (skipCount < 0)
                    {
                        return false;
                    }

                    for (var j = 0; j < skipCount && i < n; ++j)
                    {
                        codeLengths[i] = 0;
                        ++i;
                    }
                }
                else
                {
                    codeLengths[i] = (byte)(code - 2);
                    ++i;
                }
            }

            TreeDecoder.BuildTree(codeTree, numCodes * 2, codeLengths, n);

            return true;
        }

        private bool ReadOffsetTable()
        {
            var codeLengths = new byte[maxOffsetCodes];

            // How many codes?
            var n = bitStreamReader.ReadBits(offsetBits);

            if (n < 0)
            {
                return false;
            }

            // n=0 is a special case, meaning only a single code that
            // is of zero length.
            if (n == 0)
            {
                var code = bitStreamReader.ReadBits(offsetBits);

                if (code < 0)
                {
                    return false;
                }

                TreeDecoder.SetTreeSingle(offsetTree, code);
                return true;
            }

            // Enforce a hard limit on the number of codes.
            if (n > maxOffsetCodes)
            {
                n = maxOffsetCodes;
            }

            // Read the length of each code.
            for (var i = 0; i < n; ++i)
            {
                var len = ReadLengthValue();

                if (len < 0)
                {
                    return false;
                }

                codeLengths[i] = (byte)len;
            }

            TreeDecoder.BuildTree(offsetTree, maxOffsetCodes * 2, codeLengths, n);

            return true;
        }

        // Start reading a new block from the input stream.
        private bool StartNewBlock()
        {
            // Read length of new block (in commands).
            var len = bitStreamReader.ReadBits(16);

            if (len < 0)
            {
                return false;
            }

            blockRemaining = len;

            // Read the temporary decode table, used to encode the codes table.
            if (!ReadTempTable())
            {
                return false;
            }

            // Read the code table; this is encoded *using* the temp table.
            if (!ReadCodeTable())
            {
                return false;
            }

            // Read the offset table.
            if (!ReadOffsetTable())
            {
                return false;
            }

            return true;
        }

        private int LharkReadOffsetCode(int code)
        {
            if (code < 4)
            {
                return code;
            }

            var numLowBits = (code - 2) / 2;
            var lowBits = bitStreamReader.ReadBits(numLowBits);
            if (lowBits < 0)
            {
                return -1;
            }

            return ((2 + code % 2) << numLowBits) + lowBits;
        }

        // Read an offset distance from the input stream.
        // Returns the code, or -1 if an error occurred.
        private int ReadOffsetCode()
        {
            var bits = TreeDecoder.ReadFromTree(bitStreamReader, offsetTree);

            if (bits < 0)
            {
                return -1;
            }

            // The code read indicates the length of the offset in bits.
            //
            // The returned value looks like this:
            //   bits = 0  ->         0
            //   bits = 1  ->         1
            //   bits = 2  ->        1x
            //   bits = 3  ->       1xx
            //   bits = 4  ->      1xxx
            //             etc.
            if (bits == 0)
            {
                return 0;
            }

            if (bits == 1)
            {
                return 1;
            }

            if (lhark)
            {
                return LharkReadOffsetCode(bits);
            }

            var result = bitStreamReader.ReadBits(bits - 1);

            if (result < 0)
            {
                return -1;
            }

            return result + (1 << (bits - 1));
        }

        // Add a byte value to the output stream.
        private void OutputByte(byte[] buf, ref int bufLen, byte b)
        {
            buf[bufLen] = b;
            ++bufLen;

            ringbuf[ringbufPos] = b;
            ringbufPos = (ringbufPos + 1) % ringBufferSize;
        }

        // Copy a block from the history buffer.
        private void CopyFromHistory(byte[] buf, ref int bufLen, int count)
        {
            var offset = ReadOffsetCode();

            if (offset < 0)
            {
                return;
            }

            var start = (uint)(ringbufPos + ringBufferSize - offset - 1);

            for (var i = 0; i < count; ++i)
            {
                OutputByte(buf, ref bufLen, ringbuf[(start + i) % ringBufferSize]);
            }
        }

        private int LharkDecodeCopyCount(int code)
        {
            if (code < 264)
            {
                return code - 256 + COPY_THRESHOLD;
            }

            if (code < 288)
            {
                var numLowBits = (code - 260) / 4;
                var lowBits = bitStreamReader.ReadBits(numLowBits);
                if (lowBits < 0)
                {
                    return -1;
                }

                return ((4 + code % 4) << numLowBits) + lowBits + 3;
            }

            return 514;
        }

        protected override int ReadBlock(byte[] buf)
        {
            // Start of new block?
            while (blockRemaining == 0)
            {
                if (!StartNewBlock())
                {
                    return 0;
                }
            }

            --blockRemaining;

            // Read next command from input stream.
            var result = 0;

            var code = TreeDecoder.ReadFromTree(bitStreamReader, codeTree);

            if (code < 0)
            {
                return 0;
            }

            // The code may be either a literal byte value or a copy command.
            if (code < 256)
            {
                OutputByte(buf, ref result, (byte)code);
            }
            else
            {
                int copyCount;
                if (lhark)
                {
                    copyCount = LharkDecodeCopyCount(code);
                    if (copyCount < 0)
                    {
                        return 0;
                    }
                }
                else
                {
                    copyCount = code - 256 + COPY_THRESHOLD;
                }

                CopyFromHistory(buf, ref result, copyCount);
            }

            return result;
        }
    }
}
