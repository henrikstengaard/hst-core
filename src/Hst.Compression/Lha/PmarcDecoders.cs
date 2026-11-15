namespace Hst.Compression.Lha
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Move to front list of byte values used by PMarc methods, where literals are encoded as index of the byte value
    /// in the list. Every decoded byte is moved to the front of the list, so recently used byte values get small
    /// indexes.
    /// </summary>
    internal sealed class MoveToFrontList
    {
        private readonly byte[] values = new byte[256];

        public MoveToFrontList()
        {
            // initial order: printable ascii, control characters, 0xa0-0xdf, 0x80-0x9f and 0xe0-0xff
            var index = 0;
            foreach (var (first, last) in new[] { (0x20, 0x7f), (0x00, 0x1f), (0xa0, 0xdf), (0x80, 0x9f), (0xe0, 0xff) })
            {
                for (var value = first; value <= last; value++)
                {
                    values[index++] = (byte)value;
                }
            }
        }

        public byte this[int index] => values[index];

        public void MoveToFront(byte value)
        {
            if (values[0] == value)
            {
                return;
            }

            var index = Array.IndexOf(values, value);
            Array.Copy(values, 0, values, 1, index);
            values[0] = value;
        }
    }

    /// <summary>
    /// Decoder for the PMarc -pm2- method with an 8 KiB window. A code table with up to 29 symbols encodes literal
    /// ranges (symbols 0 to 7) and copies (symbols 8 and up). Code tables are read at start of data and after 1, 2, 4
    /// and then every 4 KiB of decoded data.
    /// </summary>
    internal sealed class Pm2Decoder : WindowDecoder
    {
        private const int MAX_SYMBOLS = 29;

        // literal ranges in move to front list (base, bits)
        private static readonly (int Base, int Bits)[] LiteralRanges =
        {
            (0, 3), (8, 3), (16, 4), (32, 5), (64, 5), (96, 5), (128, 6), (192, 6)
        };

        // copy length ranges for copy symbols 15 and up (base, bits)
        private static readonly (int Base, int Bits)[] CopyLengthRanges =
        {
            (17, 3), (25, 3), (33, 5), (65, 6), (129, 7), (256, 0)
        };

        private readonly BitReader reader;
        private readonly MoveToFrontList recentValues = new MoveToFrontList();
        private readonly PrefixCodeDecoder codes = new PrefixCodeDecoder(MAX_SYMBOLS);
        private readonly PrefixCodeDecoder offsetCodes = new PrefixCodeDecoder(8);
        private readonly byte[] lengths = new byte[MAX_SYMBOLS];
        private bool started;
        private bool useOffsetCodes;
        private int tablesRead;
        private int untilNextTables;
        private bool readTables;

        public Pm2Decoder(Stream input) : base(13, (byte)' ', true)
        {
            reader = new BitReader(input);
        }

        protected override void OnOutput(byte value)
        {
            recentValues.MoveToFront(value);

            if (--untilNextTables != 0)
            {
                return;
            }

            // tables are read before next command
            readTables = true;
            untilNextTables = tablesRead == 1 ? 1024 : tablesRead == 2 ? 2048 : 4096;
        }

        private void ReadTables()
        {
            switch (tablesRead)
            {
                case 0:
                    ReadCodes();
                    ReadOffsetCodes(5);
                    untilNextTables = 1024;
                    break;
                case 1:
                    ReadOffsetCodes(6);
                    break;
                case 2:
                    ReadOffsetCodes(7);
                    break;
                case 3:
                    if (reader.ReadBit() == 1)
                    {
                        ReadCodes();
                    }

                    ReadOffsetCodes(8);
                    break;
                default:
                    if (reader.ReadBit() == 1)
                    {
                        ReadCodes();
                        ReadOffsetCodes(8);
                    }

                    break;
            }

            if (tablesRead < 4)
            {
                tablesRead++;
            }
        }

        private void ReadCodes()
        {
            var count = reader.Read(5);
            var minLength = reader.Read(3);
            if (count > MAX_SYMBOLS)
            {
                throw new IOException("Invalid code table");
            }

            useOffsetCodes = count >= 10 && !(count == MAX_SYMBOLS && minLength == 0);

            if (minLength == 0)
            {
                if (count == 0)
                {
                    throw new IOException("Invalid code table");
                }

                codes.SetSingle(count - 1);
                return;
            }

            // code lengths are stored as 0 for unused or minimum length + value - 1
            var lengthBits = reader.Read(3);
            for (var symbol = 0; symbol < count; symbol++)
            {
                var value = reader.Read(lengthBits);
                lengths[symbol] = value == 0 ? (byte)0 : (byte)(minLength + value - 1);
            }

            codes.Build(lengths, count);
        }

        private void ReadOffsetCodes(int count)
        {
            if (!useOffsetCodes)
            {
                return;
            }

            var used = 0;
            var lastUsed = 0;
            for (var symbol = 0; symbol < count; symbol++)
            {
                lengths[symbol] = (byte)reader.Read(3);
                if (lengths[symbol] != 0)
                {
                    used++;
                    lastUsed = symbol;
                }
            }

            if (used == 1)
            {
                offsetCodes.SetSingle(lastUsed);
                return;
            }

            offsetCodes.Build(lengths, count);
        }

        protected override bool DecodeCommand()
        {
            if (!started)
            {
                // first bit is unused
                started = true;
                reader.ReadBit();
                ReadTables();
            }
            else if (readTables)
            {
                readTables = false;
                ReadTables();
            }

            var symbol = codes.Decode(reader);
            if (symbol < LiteralRanges.Length)
            {
                var range = LiteralRanges[symbol];
                Literal(recentValues[range.Base + reader.Read(range.Bits)]);
                return true;
            }

            var copySymbol = symbol - LiteralRanges.Length;
            int length;
            if (copySymbol < 15)
            {
                length = copySymbol + 2;
            }
            else
            {
                var range = CopyLengthRanges[copySymbol - 15];
                length = range.Base + reader.Read(range.Bits);
            }

            Copy(ReadOffset(copySymbol) + 1, length);
            return true;
        }

        private int ReadOffset(int copySymbol)
        {
            // copies of 22 bytes or more have offset 0
            if (copySymbol >= 20)
            {
                return 0;
            }

            // copies of 2 bytes have a 6 bit offset, others use offset code table for the bit length of offset
            var bitLength = copySymbol == 0 ? 0 : offsetCodes.Decode(reader);
            if (bitLength == 0)
            {
                return reader.Read(6);
            }

            var bits = bitLength + 5;
            return (1 << bits) + reader.Read(bits);
        }
    }

    /// <summary>
    /// Decoder for the PMarc -pm1- method with a 16 KiB window. Data starts with 5 bits selecting one of a set of fixed
    /// trees used to decode literal ranges. Commands are either copies or a block of literals followed by a copy.
    /// Some copy distance ranges are only available, when enough data is decoded to use them.
    /// Compressed data is padded with zero bits, as data can be read past end of compressed data.
    /// </summary>
    internal sealed class Pm1Decoder : WindowDecoder
    {
        private const int MAX_LITERAL_BLOCK = 216;

        // trees selecting literal range a to f, written with nested brackets for nodes with 0 and 1 branches
        private static readonly string[] LiteralTreeDefinitions =
        {
            "((((a b) c) d) (e f))", "(((a b) (c f)) (d e))", "(((a b) c) (d (e f)))", "((a (b c)) (d (e f)))",
            "((a (b d)) (c (e f)))", "((a (b (e f))) (c d))", "((a b) ((c d) (e f)))", "((a b) ((c (e f)) d))",
            "((a b) (c (d (e f))))", "(a (((b f) c) (d e)))", "(a (((b (e f)) c) d))", "(a (((b c) d) (e f)))",
            "(a ((b (c f)) (d e)))", "(a ((b c) (d (e f))))", "(a ((b (d (e f))) c))", "(a (b ((c d) (e f))))",
            "(a (b (c (d (e f)))))", "(((d e) c) (d e))", "((a (b e)) (c d))", "((a b) (c (d e)))",
            "(a (((b e) c) d))", "(a ((b c) (d e)))", "(a ((b (d e)) c))", "(a (b (c (d e))))",
            "(((a b) c) d)", "((a (b d)) c)", "((a b) (c d))", "(a ((b d) c))",
            "(a (b (c d)))", "(a (b c))", "(a b)", "a"
        };

        private static readonly int[][] LiteralTrees = ParseTrees(LiteralTreeDefinitions);

        // literal ranges in move to front list (base, bits)
        private static readonly (int Base, int Bits)[] LiteralRanges =
        {
            (0, 4), (16, 4), (32, 5), (64, 6), (128, 6), (192, 6)
        };

        // copy distance ranges (base, maximum bits), where ranges 3 to 5 use fewer bits until enough data is decoded
        private static readonly (int Base, int Bits)[] DistanceRanges =
        {
            (0, 6), (64, 8), (0, 6), (64, 9), (576, 11), (2624, 13)
        };

        private readonly BitReader reader;
        private readonly MoveToFrontList recentValues = new MoveToFrontList();
        private int[] literalTree;
        private int decodedBytes;
        private int remainingLiterals;
        private bool copyAfterLiterals;

        public Pm1Decoder(Stream input) : base(14, 0, true)
        {
            reader = new BitReader(input, true);
        }

        /// <summary>
        /// Parse tree definitions into arrays with 2 entries per node starting with root node, where an entry is either
        /// index of child node or a leaf with the complement of the literal range. A tree with only a leaf is an array
        /// with the leaf.
        /// </summary>
        private static int[][] ParseTrees(IEnumerable<string> definitions)
        {
            var trees = new List<int[]>();
            foreach (var definition in definitions)
            {
                var nodes = new List<int>();
                var position = 0;
                var root = ParseNode(definition, ref position, nodes);
                trees.Add(root < 0 ? new[] { root } : nodes.ToArray());
            }

            return trees.ToArray();
        }

        private static int ParseNode(string definition, ref int position, List<int> nodes)
        {
            while (definition[position] == ' ')
            {
                position++;
            }

            if (definition[position] != '(')
            {
                return ~(definition[position++] - 'a');
            }

            position++;
            var node = nodes.Count / 2;
            nodes.Add(0);
            nodes.Add(0);
            nodes[node * 2] = ParseNode(definition, ref position, nodes);
            nodes[node * 2 + 1] = ParseNode(definition, ref position, nodes);
            while (definition[position] != ')')
            {
                position++;
            }

            position++;
            return node;
        }

        protected override void OnOutput(byte value)
        {
            recentValues.MoveToFront(value);
            decodedBytes++;
        }

        protected override bool DecodeCommand()
        {
            if (literalTree == null)
            {
                literalTree = LiteralTrees[reader.Read(5)];
            }

            if (remainingLiterals > 0)
            {
                remainingLiterals--;
                Literal(ReadLiteral());
                return true;
            }

            if (copyAfterLiterals)
            {
                copyAfterLiterals = false;
                ReadCopy();
                return true;
            }

            if (reader.ReadBit() == 0)
            {
                ReadCopy();
                return true;
            }

            // block of literals is followed by a copy, unless block has maximum length
            var count = ReadLiteralCount();
            remainingLiterals = count - 1;
            copyAfterLiterals = count != MAX_LITERAL_BLOCK;
            Literal(ReadLiteral());
            return true;
        }

        private byte ReadLiteral()
        {
            var range = LiteralRanges[ReadLiteralRange()];
            return recentValues[range.Base + reader.Read(range.Bits)];
        }

        private int ReadLiteralRange()
        {
            // tree with a single leaf doesn't read any bits
            if (literalTree.Length == 1)
            {
                return ~literalTree[0];
            }

            var entry = literalTree[reader.ReadBit()];
            while (entry >= 0)
            {
                entry = literalTree[entry * 2 + reader.ReadBit()];
            }

            return ~entry;
        }

        private int ReadLiteralCount()
        {
            var value = reader.Read(2);
            if (value < 3)
            {
                return value + 1;
            }

            value = reader.Read(3);
            if (value < 7)
            {
                return value + 4;
            }

            value = reader.Read(4);
            if (value < 14)
            {
                return value + 11;
            }

            return value == 14 ? reader.Read(6) + 25 : reader.Read(7) + 89;
        }

        /// <summary>
        /// Read bit, if enough data is decoded, otherwise return default value without reading.
        /// </summary>
        private int ReadBitAfter(int decoded, int defaultValue) =>
            decodedBytes >= decoded ? reader.ReadBit() : defaultValue;

        private int ReadDistanceRange()
        {
            if (reader.ReadBit() == 0)
            {
                return ReadBitAfter(576, 0) == 1 ? 4 : ReadBitAfter(64, 0);
            }

            if (ReadBitAfter(64, 1) == 0)
            {
                return 3;
            }

            return ReadBitAfter(2624, 1) == 1 ? 2 : 5;
        }

        private int ReadCopyLength()
        {
            var value = reader.Read(2);
            if (value < 3)
            {
                return value + 3;
            }

            value = reader.Read(3);
            switch (value)
            {
                case 5:
                    return reader.Read(2) + 11;
                case 6:
                    return reader.Read(3) + 15;
                case 7:
                    break;
                default:
                    return value + 6;
            }

            value = reader.Read(6);
            if (value < 62)
            {
                return value + 23;
            }

            return value == 62 ? reader.Read(5) + 85 : reader.Read(7) + 117;
        }

        private void ReadCopy()
        {
            var rangeIndex = ReadDistanceRange();

            // ranges 0 and 1 are copies of 2 bytes
            var length = rangeIndex < 2 ? 2 : ReadCopyLength();

            // ranges 3 to 5 use at least 8 bits and only enough bits to reach start of decoded data
            var range = DistanceRanges[rangeIndex];
            var bits = range.Bits;
            if (rangeIndex >= 3)
            {
                bits = 8;
                while (bits < range.Bits && decodedBytes >= range.Base + (1 << bits))
                {
                    bits++;
                }
            }

            var distance = range.Base + reader.Read(bits);
            if (distance >= decodedBytes)
            {
                throw new IOException("Invalid copy distance");
            }

            Copy(distance + 1, length);
        }
    }
}
