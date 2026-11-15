namespace Hst.Compression.Lha
{
    using System;
    using System.IO;

    /// <summary>
    /// Decodes canonical prefix codes (huffman codes) read most significant bit first. Codes are assigned in order of
    /// code length and then symbol, so a code table is fully defined by the code length of each symbol.
    /// Short codes are decoded with a lookup table, long codes are decoded by code length.
    /// </summary>
    internal sealed class PrefixCodeDecoder
    {
        private const int MAX_CODE_LENGTH = 31;
        private const int LOOKUP_BITS = 10;

        private readonly int[] symbolsByCode;
        private readonly int[] lengthCounts = new int[MAX_CODE_LENGTH + 1];
        private readonly int[] lengthOffsets = new int[MAX_CODE_LENGTH + 2];
        private readonly ushort[] lookupSymbols = new ushort[1 << LOOKUP_BITS];
        private readonly byte[] lookupLengths = new byte[1 << LOOKUP_BITS];
        private int lookupBits;
        private int maxLength;
        private int singleSymbol;

        /// <summary>
        /// Create prefix code decoder.
        /// </summary>
        /// <param name="maxSymbols">Maximum number of symbols in code tables.</param>
        public PrefixCodeDecoder(int maxSymbols)
        {
            symbolsByCode = new int[maxSymbols];
            SetSingle(0);
        }

        /// <summary>
        /// Set code table, which always decodes symbol without reading any bits.
        /// </summary>
        public void SetSingle(int symbol)
        {
            singleSymbol = symbol;
        }

        /// <summary>
        /// Set code table from code lengths of symbols. Code length 0 means symbol is not used.
        /// Unassigned codes of an incomplete code table decode as symbol 0 with the longest code length and a code
        /// table without any used symbols decodes as symbol 0 with a code length of 1.
        /// </summary>
        /// <param name="lengths">Code length of each symbol.</param>
        /// <param name="count">Number of symbols.</param>
        public void Build(byte[] lengths, int count)
        {
            if (count > symbolsByCode.Length)
            {
                throw new IOException("Invalid code table");
            }

            Array.Clear(lengthCounts, 0, lengthCounts.Length);
            maxLength = 1;
            for (var symbol = 0; symbol < count; symbol++)
            {
                int length = lengths[symbol];
                if (length > MAX_CODE_LENGTH)
                {
                    throw new IOException("Invalid code length");
                }

                lengthCounts[length]++;
                if (length > maxLength)
                {
                    maxLength = length;
                }
            }

            lengthCounts[0] = 0;

            // verify codes fit (kraft inequality), more codes than available is invalid
            long available = 1;
            for (var length = 1; length <= maxLength; length++)
            {
                available = (available << 1) - lengthCounts[length];
                if (available < 0)
                {
                    throw new IOException("Invalid code table");
                }
            }

            // order symbols by code length and then symbol, which is the order of their codes
            lengthOffsets[1] = 0;
            for (var length = 1; length <= maxLength; length++)
            {
                lengthOffsets[length + 1] = lengthOffsets[length] + lengthCounts[length];
            }

            for (var symbol = 0; symbol < count; symbol++)
            {
                if (lengths[symbol] > 0)
                {
                    symbolsByCode[lengthOffsets[lengths[symbol]]++] = symbol;
                }
            }

            // fill lookup table with codes not longer than lookup bits
            lookupBits = Math.Min(maxLength, LOOKUP_BITS);
            var lookupSize = 1 << lookupBits;
            var code = 0;
            var index = 0;
            for (var length = 1; length <= lookupBits; length++)
            {
                var span = 1 << (lookupBits - length);
                for (var i = 0; i < lengthCounts[length]; i++)
                {
                    var symbol = (ushort)symbolsByCode[index++];
                    var start = code * span;
                    for (var entry = start; entry < start + span; entry++)
                    {
                        lookupSymbols[entry] = symbol;
                        lookupLengths[entry] = (byte)length;
                    }

                    code++;
                }

                code <<= 1;
            }

            // remaining entries are either unassigned codes (decoded as symbol 0) or prefixes of longer codes
            var first = code >> 1;
            var unassigned = maxLength <= LOOKUP_BITS;
            for (var entry = first; entry < lookupSize; entry++)
            {
                lookupSymbols[entry] = 0;
                lookupLengths[entry] = unassigned ? (byte)maxLength : (byte)0;
            }

            singleSymbol = -1;
        }

        /// <summary>
        /// Decode next symbol.
        /// </summary>
        public int Decode(BitReader reader)
        {
            if (singleSymbol >= 0)
            {
                return singleSymbol;
            }

            var peek = reader.Peek(lookupBits);
            var length = lookupLengths[peek];
            if (length > 0)
            {
                reader.Skip(length);
                return lookupSymbols[peek];
            }

            return DecodeLong(reader);
        }

        private int DecodeLong(BitReader reader)
        {
            var bits = reader.Peek(maxLength);
            var firstCode = 0;
            var index = 0;
            for (var length = 1; length <= maxLength; length++)
            {
                var code = bits >> (maxLength - length);
                var count = lengthCounts[length];
                if (code - firstCode < count)
                {
                    reader.Skip(length);
                    return symbolsByCode[index + code - firstCode];
                }

                index += count;
                firstCode = (firstCode + count) << 1;
            }

            // unassigned code of an incomplete code table
            reader.Skip(maxLength);
            return 0;
        }
    }
}
