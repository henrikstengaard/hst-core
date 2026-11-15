namespace Hst.Compression.Lha
{
    using System;
    using System.IO;

    /// <summary>
    /// Encoder for static huffman methods -lh4-, -lh5-, -lh6- and -lh7-.
    ///
    /// Data is compressed using a sliding dictionary (LZ77) with hash chains to find matches and lazy matching.
    /// Literals, match lengths and match offsets are collected into blocks, which are written using huffman codes
    /// built for each block in the format read by <see cref="LhNewDecoder"/>:
    /// - 16 bits: number of commands in block.
    /// - Temporary table: Code lengths of huffman code used to write code table code lengths.
    /// - Code table: Code lengths of literals (0-255) and match lengths (256-509).
    /// - Offset table: Code lengths of offset bit lengths.
    /// - Commands: Literal or match length code followed by offset bit length code and offset bits for matches.
    /// </summary>
    internal class LhEncoder
    {
        private const int MIN_MATCH = 3;
        private const int MAX_MATCH = 256;
        private const int NUM_CODES = 256 + MAX_MATCH - MIN_MATCH + 1; /* 510 */
        private const int NUM_TEMP_CODES = 19; /* 3 run length codes + code lengths 1-16 */
        private const int MAX_CODE_LENGTH = 16;
        private const int MAX_BLOCK_COMMANDS = 0x8000;
        private const int HASH_BITS = 15;
        private const int HASH_SIZE = 1 << HASH_BITS;
        private const int HASH_MASK = HASH_SIZE - 1;

        // match length considered good enough to skip lazy evaluation and stop searching
        private const int GOOD_MATCH = 128;

        private readonly int windowSize;
        private readonly int offsetCodes;
        private readonly int offsetTableBits;
        private readonly int maxChain;

        // input buffer holding window history and lookahead
        private readonly byte[] window;
        private long windowStart; // stream position of window[0]
        private int windowEnd; // number of bytes in window

        // hash chains with stream positions, -1 if empty
        private readonly long[] head;
        private readonly long[] prev;

        // commands in current block: values 0-255 are literals, 256-509 are match length codes
        private readonly ushort[] blockCodes;
        private readonly int[] blockOffsets;
        private int blockCommands;
        private readonly int[] codeFrequencies;
        private readonly int[] offsetFrequencies;

        private BitStreamWriter writer;

        private LhEncoder(int dictionaryBits, int offsetTableBits, int maxChain)
        {
            windowSize = 1 << dictionaryBits;
            offsetCodes = dictionaryBits + 1;
            this.offsetTableBits = offsetTableBits;
            this.maxChain = maxChain;

            window = new byte[windowSize * 2 + MAX_MATCH * 2];
            head = new long[HASH_SIZE];
            prev = new long[windowSize];

            blockCodes = new ushort[MAX_BLOCK_COMMANDS];
            blockOffsets = new int[MAX_BLOCK_COMMANDS];
            codeFrequencies = new int[NUM_CODES];
            offsetFrequencies = new int[offsetCodes];
        }

        /// <summary>
        /// Create encoder for method.
        /// </summary>
        public static LhEncoder Create(string method)
        {
            switch (method)
            {
                case Constants.LZHUFF4_METHOD:
                    return new LhEncoder(12, 4, 256);
                case Constants.LZHUFF5_METHOD:
                    return new LhEncoder(13, 4, 256);
                case Constants.LZHUFF6_METHOD:
                    return new LhEncoder(15, 5, 512);
                case Constants.LZHUFF7_METHOD:
                    return new LhEncoder(16, 5, 512);
                default:
                    throw new NotSupportedException($"Compression method \"{method}\" is not supported");
            }
        }

        /// <summary>
        /// Compress size bytes from input to output. Compression stops, if compressed data exceeds limit bytes.
        /// </summary>
        /// <returns>Result with crc and sizes.</returns>
        public LhEncodeResult Encode(Stream input, Stream output, long size, long limit)
        {
            writer = new BitStreamWriter(output, limit);
            windowStart = 0;
            windowEnd = 0;
            blockCommands = 0;
            Array.Clear(codeFrequencies, 0, codeFrequencies.Length);
            Array.Clear(offsetFrequencies, 0, offsetFrequencies.Length);
            for (var i = 0; i < head.Length; i++)
            {
                head[i] = -1;
            }

            ushort crc = 0;
            long remaining = size;
            long pos = 0;

            int RefillWindow()
            {
                // move window down, when there's not enough room for lookahead
                var index = (int)(pos - windowStart);
                if (windowEnd + MAX_MATCH > window.Length && index > windowSize)
                {
                    var shift = index - windowSize;
                    Array.Copy(window, shift, window, 0, windowEnd - shift);
                    windowStart += shift;
                    windowEnd -= shift;
                }

                // fill window with data from input
                while (remaining > 0 && windowEnd < window.Length)
                {
                    var bytesRead = input.Read(window, windowEnd,
                        (int)Math.Min(window.Length - windowEnd, remaining));
                    if (bytesRead == 0)
                    {
                        throw new EndOfStreamException("Unexpected end of data");
                    }

                    crc = Crc16.Update(crc, window, windowEnd, bytesRead);
                    windowEnd += bytesRead;
                    remaining -= bytesRead;
                }

                return windowEnd - (int)(pos - windowStart);
            }

            var available = RefillWindow();
            var pendingLength = 0;
            var pendingOffset = 0;
            var hasPending = false;

            while (available > 0 && !writer.LimitExceeded)
            {
                var length = FindMatch(pos, available, hasPending ? pendingLength : MIN_MATCH - 1, out var distance);

                if (hasPending)
                {
                    if (length > pendingLength)
                    {
                        // match at current position is better, write previous byte as literal
                        AddLiteral(window[pos - 1 - windowStart]);
                        pendingLength = length;
                        pendingOffset = distance;
                    }
                    else
                    {
                        // write pending match starting at previous position
                        AddMatch(pendingLength, pendingOffset);
                        var end = pos - 1 + pendingLength;
                        InsertHash(pos, available);
                        while (++pos < end)
                        {
                            available--;
                            InsertHash(pos, available);
                        }

                        available--;
                        hasPending = false;
                        if (available < MAX_MATCH + 1)
                        {
                            available = RefillWindow();
                        }

                        continue;
                    }
                }
                else if (length >= MIN_MATCH)
                {
                    if (length >= GOOD_MATCH)
                    {
                        // good match, write it without lazy evaluation
                        AddMatch(length, distance);
                        var end = pos + length;
                        do
                        {
                            InsertHash(pos, available);
                            available--;
                        } while (++pos < end);

                        if (available < MAX_MATCH + 1)
                        {
                            available = RefillWindow();
                        }

                        continue;
                    }

                    hasPending = true;
                    pendingLength = length;
                    pendingOffset = distance;
                }
                else
                {
                    AddLiteral(window[pos - windowStart]);
                }

                InsertHash(pos, available);
                pos++;
                available--;

                if (available < MAX_MATCH + 1)
                {
                    available = RefillWindow();
                }
            }

            if (hasPending)
            {
                AddMatch(pendingLength, pendingOffset);
            }

            if (blockCommands > 0)
            {
                WriteBlock();
            }

            writer.Flush();

            return new LhEncodeResult(crc, size, writer.BytesWritten, writer.LimitExceeded);
        }

        private int Hash(int index) =>
            ((window[index] << 10) ^ (window[index + 1] << 5) ^ window[index + 2]) & HASH_MASK;

        private void InsertHash(long pos, int available)
        {
            if (available < MIN_MATCH)
            {
                return;
            }

            var hash = Hash((int)(pos - windowStart));
            prev[pos & (windowSize - 1)] = head[hash];
            head[hash] = pos;
        }

        /// <summary>
        /// Find longest match longer than minimum length for position by following hash chain.
        /// </summary>
        /// <returns>Length of match found or 0, if no match longer than minimum length is found.</returns>
        private int FindMatch(long pos, int available, int minLength, out int distance)
        {
            distance = 0;
            if (available < MIN_MATCH)
            {
                return 0;
            }

            var maxLength = Math.Min(available, MAX_MATCH);
            if (minLength >= maxLength)
            {
                return 0;
            }

            var index = (int)(pos - windowStart);
            var bestLength = minLength;
            var candidate = head[Hash(index)];
            var chain = maxChain;

            while (candidate >= 0 && chain-- > 0)
            {
                var candidateDistance = pos - candidate;

                // positions in chain are overwritten, when they are older than window size
                if (candidateDistance <= 0 || candidateDistance >= windowSize)
                {
                    break;
                }

                var candidateIndex = (int)(candidate - windowStart);
                if (window[candidateIndex + bestLength] == window[index + bestLength] &&
                    window[candidateIndex] == window[index])
                {
                    var length = 0;
                    while (length < maxLength && window[candidateIndex + length] == window[index + length])
                    {
                        length++;
                    }

                    if (length > bestLength)
                    {
                        bestLength = length;
                        distance = (int)candidateDistance;
                        if (length >= maxLength)
                        {
                            break;
                        }
                    }
                }

                var next = prev[candidate & (windowSize - 1)];
                if (next >= candidate)
                {
                    break;
                }

                candidate = next;
            }

            return distance == 0 ? 0 : bestLength;
        }

        private void AddLiteral(byte value)
        {
            blockCodes[blockCommands] = value;
            blockOffsets[blockCommands] = 0;
            codeFrequencies[value]++;
            CommandAdded();
        }

        private void AddMatch(int length, int distance)
        {
            var code = 256 + length - MIN_MATCH;
            var offset = distance - 1;
            blockCodes[blockCommands] = (ushort)code;
            blockOffsets[blockCommands] = offset;
            codeFrequencies[code]++;
            offsetFrequencies[BitLength(offset)]++;
            CommandAdded();
        }

        private void CommandAdded()
        {
            blockCommands++;
            if (blockCommands == MAX_BLOCK_COMMANDS)
            {
                WriteBlock();
            }
        }

        /// <summary>
        /// Number of bits required to represent value, 0 for value 0.
        /// </summary>
        private static int BitLength(int value)
        {
            var bits = 0;
            while (value > 0)
            {
                bits++;
                value >>= 1;
            }

            return bits;
        }

        private void WriteBlock()
        {
            var codeHuffman = HuffmanCode.Build(codeFrequencies, MAX_CODE_LENGTH);
            var offsetHuffman = HuffmanCode.Build(offsetFrequencies, MAX_CODE_LENGTH);

            writer.WriteBits(blockCommands, 16);

            WriteCodeTable(codeHuffman);
            WriteOffsetTable(offsetHuffman);

            for (var i = 0; i < blockCommands; i++)
            {
                int code = blockCodes[i];
                codeHuffman.Write(writer, code);
                if (code < 256)
                {
                    continue;
                }

                var offset = blockOffsets[i];
                var bits = BitLength(offset);
                offsetHuffman.Write(writer, bits);
                if (bits > 1)
                {
                    // most significant bit is implied by bit length
                    writer.WriteBits(offset, bits - 1);
                }
            }

            blockCommands = 0;
            Array.Clear(codeFrequencies, 0, codeFrequencies.Length);
            Array.Clear(offsetFrequencies, 0, offsetFrequencies.Length);
        }

        /// <summary>
        /// Write code length value using 3 bits, values 7 and above are extended with a 1 bit per value above 6
        /// terminated by a 0 bit.
        /// </summary>
        private void WriteLengthValue(int length)
        {
            if (length < 7)
            {
                writer.WriteBits(length, 3);
                return;
            }

            writer.WriteBits(7, 3);
            for (var i = 7; i < length; i++)
            {
                writer.WriteBits(1, 1);
            }

            writer.WriteBits(0, 1);
        }

        /// <summary>
        /// Write code table with code lengths of literals and match lengths. Code lengths are written using temporary
        /// codes: 0 skips 1 code, 1 skips 3-18 codes, 2 skips 20-531 codes and 3-18 are code lengths 1-16.
        /// </summary>
        private void WriteCodeTable(HuffmanCode codeHuffman)
        {
            if (codeHuffman.SingleSymbol >= 0)
            {
                // temporary table isn't used, when code table has a single code
                writer.WriteBits(0, 5);
                writer.WriteBits(0, 5);
                writer.WriteBits(0, 9);
                writer.WriteBits(codeHuffman.SingleSymbol, 9);
                return;
            }

            // convert code lengths to temporary codes with extra bits for skip counts
            var tempCodes = new int[codeHuffman.Count];
            var tempExtra = new int[codeHuffman.Count];
            var tempCount = 0;
            var tempFrequencies = new int[NUM_TEMP_CODES];
            var lengths = codeHuffman.Lengths;

            void AddTemp(int tempCode, int extra)
            {
                tempCodes[tempCount] = tempCode;
                tempExtra[tempCount] = extra;
                tempCount++;
                tempFrequencies[tempCode]++;
            }

            var i = 0;
            while (i < codeHuffman.Count)
            {
                if (lengths[i] != 0)
                {
                    AddTemp(lengths[i] + 2, 0);
                    i++;
                    continue;
                }

                var run = 0;
                while (i + run < codeHuffman.Count && lengths[i + run] == 0)
                {
                    run++;
                }

                i += run;
                while (run > 0)
                {
                    if (run >= 20)
                    {
                        var skip = Math.Min(run, 20 + 511);
                        AddTemp(2, skip - 20);
                        run -= skip;
                    }
                    else if (run == 19)
                    {
                        AddTemp(0, 0);
                        AddTemp(1, 15);
                        run = 0;
                    }
                    else if (run >= 3)
                    {
                        AddTemp(1, run - 3);
                        run = 0;
                    }
                    else
                    {
                        AddTemp(0, 0);
                        run--;
                    }
                }
            }

            var tempHuffman = HuffmanCode.Build(tempFrequencies, MAX_CODE_LENGTH);
            WriteTempTable(tempHuffman);

            writer.WriteBits(codeHuffman.Count, 9);
            for (var t = 0; t < tempCount; t++)
            {
                var tempCode = tempCodes[t];
                tempHuffman.Write(writer, tempCode);
                if (tempCode == 1)
                {
                    writer.WriteBits(tempExtra[t], 4);
                }
                else if (tempCode == 2)
                {
                    writer.WriteBits(tempExtra[t], 9);
                }
            }
        }

        /// <summary>
        /// Write temporary table with code lengths of temporary codes. After the first 3 code lengths, a 2 bit value
        /// skips up to 3 unused codes.
        /// </summary>
        private void WriteTempTable(HuffmanCode tempHuffman)
        {
            if (tempHuffman.SingleSymbol >= 0)
            {
                writer.WriteBits(0, 5);
                writer.WriteBits(tempHuffman.SingleSymbol, 5);
                return;
            }

            var count = tempHuffman.Count;
            var lengths = tempHuffman.Lengths;
            writer.WriteBits(count, 5);

            for (var i = 0; i < count; i++)
            {
                WriteLengthValue(lengths[i]);

                if (i != 2)
                {
                    continue;
                }

                var skip = 0;
                while (skip < 3 && i + 1 + skip < count && lengths[i + 1 + skip] == 0)
                {
                    skip++;
                }

                writer.WriteBits(skip, 2);
                i += skip;
            }
        }

        /// <summary>
        /// Write offset table with code lengths of offset bit lengths.
        /// </summary>
        private void WriteOffsetTable(HuffmanCode offsetHuffman)
        {
            if (offsetHuffman.UsedSymbols <= 1)
            {
                // single code or no matches in block
                writer.WriteBits(0, offsetTableBits);
                writer.WriteBits(Math.Max(offsetHuffman.SingleSymbol, 0), offsetTableBits);
                return;
            }

            writer.WriteBits(offsetHuffman.Count, offsetTableBits);
            for (var i = 0; i < offsetHuffman.Count; i++)
            {
                WriteLengthValue(offsetHuffman.Lengths[i]);
            }
        }
    }

    /// <summary>
    /// Result of encoding data.
    /// </summary>
    internal readonly struct LhEncodeResult
    {
        public ushort Crc { get; }
        public long OriginalSize { get; }
        public long PackedSize { get; }
        public bool LimitExceeded { get; }

        public LhEncodeResult(ushort crc, long originalSize, long packedSize, bool limitExceeded)
        {
            Crc = crc;
            OriginalSize = originalSize;
            PackedSize = packedSize;
            LimitExceeded = limitExceeded;
        }
    }
}
