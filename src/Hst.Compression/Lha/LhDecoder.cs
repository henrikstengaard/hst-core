namespace Hst.Compression.Lha
{
    using System;
    using System.IO;

    /// <summary>
    /// Decoder for the static huffman methods -lh4-, -lh5-, -lh6-, -lh7-, -lhx- and -lk7- (LHARK's variant of -lh7-).
    /// Data is a sequence of blocks, each starting with the number of commands in the block followed by three code
    /// tables: a code table used to read code lengths of the main code table, the main code table with literals and
    /// copy lengths, and the offset code table with bit lengths of copy distances. See <see cref="LhEncoder"/> for
    /// the encoding.
    /// </summary>
    internal sealed class LhDecoder : WindowDecoder
    {
        private const int MAIN_SYMBOLS = 510;
        private const int LK7_MAIN_SYMBOLS = 289;
        private const int LENGTH_SYMBOLS = 31;
        private const int MIN_COPY_LENGTH = 3;
        private const int MAX_EXTRA_BITS = 24;

        private readonly BitReader reader;
        private readonly int mainSymbols;
        private readonly int offsetCountBits;
        private readonly bool lk7;
        private readonly PrefixCodeDecoder lengthCodes = new PrefixCodeDecoder(LENGTH_SYMBOLS);
        private readonly PrefixCodeDecoder mainCodes;
        private readonly PrefixCodeDecoder offsetCodes;
        private readonly byte[] lengths = new byte[MAIN_SYMBOLS + 4];
        private int remainingCommands;

        private LhDecoder(Stream input, int windowBits, int offsetCountBits, bool lk7)
            : base(windowBits, (byte)' ')
        {
            reader = new BitReader(input);
            mainSymbols = lk7 ? LK7_MAIN_SYMBOLS : MAIN_SYMBOLS;
            this.offsetCountBits = offsetCountBits;
            this.lk7 = lk7;
            mainCodes = new PrefixCodeDecoder(mainSymbols);
            offsetCodes = new PrefixCodeDecoder((1 << offsetCountBits) - 1);
        }

        /// <summary>
        /// Create decoder for method. Window is large enough for any distance the offset code table of the method
        /// can represent in valid data.
        /// </summary>
        public static LhDecoder Create(Stream input, string method)
        {
            switch (method)
            {
                case LhaMethod.Lh4:
                case LhaMethod.Lh5:
                    return new LhDecoder(input, 14, 4, false);
                case LhaMethod.Lh6:
                    return new LhDecoder(input, 16, 5, false);
                case LhaMethod.Lh7:
                    return new LhDecoder(input, 17, 5, false);
                case LhaMethod.Lhx:
                    return new LhDecoder(input, 20, 5, false);
                case LhaMethod.Lk7:
                    return new LhDecoder(input, 16, 6, true);
                default:
                    throw new NotSupportedException($"Method \"{method}\" is not supported");
            }
        }

        protected override bool DecodeCommand()
        {
            while (remainingCommands == 0)
            {
                remainingCommands = reader.Read(16);
                ReadLengthCodes();
                ReadMainCodes();
                ReadOffsetCodes();
            }

            remainingCommands--;

            var symbol = mainCodes.Decode(reader);
            if (symbol < 256)
            {
                Literal((byte)symbol);
                return true;
            }

            var length = lk7 ? Lk7CopyLength(symbol) : symbol - 256 + MIN_COPY_LENGTH;
            Copy(ReadOffset() + 1, length);
            return true;
        }

        /// <summary>
        /// Read code length as 3 bits, where 7 is extended by 1 for each following 1 bit until a 0 bit.
        /// </summary>
        private int ReadCodeLength()
        {
            var length = reader.Read(3);
            if (length != 7)
            {
                return length;
            }

            while (reader.ReadBit() == 1)
            {
                if (++length > 31)
                {
                    throw new IOException("Invalid code length");
                }
            }

            return length;
        }

        private void ReadLengthCodes()
        {
            var count = reader.Read(5);
            if (count == 0)
            {
                lengthCodes.SetSingle(reader.Read(5));
                return;
            }

            var symbol = 0;
            while (symbol < count)
            {
                lengths[symbol++] = (byte)ReadCodeLength();

                // the three first code lengths are followed by 2 bits with number of zero code lengths after them
                if (symbol != 3)
                {
                    continue;
                }

                var zeros = reader.Read(2);
                for (var i = 0; i < zeros; i++)
                {
                    lengths[symbol++] = 0;
                }
            }

            lengthCodes.Build(lengths, count);
        }

        private void ReadMainCodes()
        {
            var count = reader.Read(9);
            if (count == 0)
            {
                mainCodes.SetSingle(reader.Read(9));
                return;
            }

            count = Math.Min(count, mainSymbols);
            var symbol = 0;
            while (symbol < count)
            {
                // symbols 0 to 2 are runs of unused symbols, others are code length + 2
                var value = lengthCodes.Decode(reader);
                if (value > 2)
                {
                    lengths[symbol++] = (byte)(value - 2);
                    continue;
                }

                int run;
                switch (value)
                {
                    case 0:
                        run = 1;
                        break;
                    case 1:
                        run = 3 + reader.Read(4);
                        break;
                    default:
                        run = 20 + reader.Read(9);
                        break;
                }

                for (var end = Math.Min(symbol + run, count); symbol < end; symbol++)
                {
                    lengths[symbol] = 0;
                }
            }

            mainCodes.Build(lengths, count);
        }

        private void ReadOffsetCodes()
        {
            var count = reader.Read(offsetCountBits);
            if (count == 0)
            {
                offsetCodes.SetSingle(reader.Read(offsetCountBits));
                return;
            }

            for (var symbol = 0; symbol < count; symbol++)
            {
                lengths[symbol] = (byte)ReadCodeLength();
            }

            offsetCodes.Build(lengths, count);
        }

        /// <summary>
        /// Read offset of copy. Offset symbol is the bit length of the offset, where the highest bit is implicit.
        /// </summary>
        private int ReadOffset()
        {
            var bitLength = offsetCodes.Decode(reader);
            if (bitLength <= 1)
            {
                return bitLength;
            }

            if (lk7)
            {
                return Lk7Offset(bitLength);
            }

            var extraBits = bitLength - 1;
            if (extraBits > MAX_EXTRA_BITS)
            {
                throw new IOException("Invalid offset");
            }

            return (1 << extraBits) | reader.Read(extraBits);
        }

        /// <summary>
        /// LHARK offsets use two symbols per bit length with the second highest bit given by the symbol.
        /// </summary>
        private int Lk7Offset(int symbol)
        {
            if (symbol < 4)
            {
                return symbol;
            }

            var extraBits = (symbol - 2) >> 1;
            if (extraBits > MAX_EXTRA_BITS)
            {
                throw new IOException("Invalid offset");
            }

            return ((2 | (symbol & 1)) << extraBits) + reader.Read(extraBits);
        }

        /// <summary>
        /// LHARK copy lengths are direct for the first 8 symbols, then four symbols per bit length with the two
        /// highest bits given by the symbol, and the last symbol is the maximum copy length.
        /// </summary>
        private int Lk7CopyLength(int symbol)
        {
            if (symbol < 264)
            {
                return symbol - 256 + MIN_COPY_LENGTH;
            }

            if (symbol >= 288)
            {
                return 514;
            }

            var extraBits = (symbol - 260) >> 2;
            return ((4 | (symbol & 3)) << extraBits) + reader.Read(extraBits) + MIN_COPY_LENGTH;
        }
    }
}
