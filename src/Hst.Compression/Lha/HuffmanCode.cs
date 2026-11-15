namespace Hst.Compression.Lha
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Builds length limited canonical huffman codes from symbol frequencies. Codes are assigned in order of
    /// code length and symbol, matching how decoders build trees from code lengths.
    /// </summary>
    internal class HuffmanCode
    {
        /// <summary>
        /// Code length for each symbol, 0 if symbol is not used.
        /// </summary>
        public byte[] Lengths { get; }

        /// <summary>
        /// Code for each symbol.
        /// </summary>
        public ushort[] Codes { get; }

        /// <summary>
        /// Number of symbols used.
        /// </summary>
        public int UsedSymbols { get; }

        /// <summary>
        /// Symbol used, if only one symbol is used. Otherwise -1.
        /// </summary>
        public int SingleSymbol { get; }

        /// <summary>
        /// Number of code lengths to store, which is last used symbol + 1.
        /// </summary>
        public int Count { get; }

        private HuffmanCode(byte[] lengths, ushort[] codes, int usedSymbols, int singleSymbol, int count)
        {
            Lengths = lengths;
            Codes = codes;
            UsedSymbols = usedSymbols;
            SingleSymbol = singleSymbol;
            Count = count;
        }

        /// <summary>
        /// Build huffman code from frequencies with code lengths limited to max length.
        /// </summary>
        public static HuffmanCode Build(int[] frequencies, int maxLength)
        {
            var symbols = frequencies.Length;
            var lengths = new byte[symbols];
            var used = 0;
            var single = -1;
            var count = 0;

            for (var i = 0; i < symbols; i++)
            {
                if (frequencies[i] <= 0)
                {
                    continue;
                }

                used++;
                single = i;
                count = i + 1;
            }

            if (used != 1)
            {
                single = -1;
            }

            if (used >= 2)
            {
                var scaled = (int[])frequencies.Clone();
                while (!TryBuildLengths(scaled, lengths, maxLength))
                {
                    // flatten frequencies until longest code fits within max length
                    for (var i = 0; i < symbols; i++)
                    {
                        if (scaled[i] > 0)
                        {
                            scaled[i] = (scaled[i] >> 1) | 1;
                        }
                    }
                }
            }

            return new HuffmanCode(lengths, CreateCanonicalCodes(lengths, maxLength), used, single, count);
        }

        /// <summary>
        /// Build huffman code lengths using a min heap of nodes. Returns false, if longest code exceeds max length.
        /// </summary>
        private static bool TryBuildLengths(int[] frequencies, byte[] lengths, int maxLength)
        {
            var symbols = frequencies.Length;

            // nodes 0..symbols-1 are leaves, nodes from symbols are internal nodes
            var parent = new int[symbols * 2];
            var weight = new long[symbols * 2];
            var heap = new SortedSet<(long Weight, int Order, int Node)>();
            var order = 0;

            for (var i = 0; i < symbols; i++)
            {
                if (frequencies[i] <= 0)
                {
                    continue;
                }

                weight[i] = frequencies[i];
                heap.Add((weight[i], order++, i));
            }

            var next = symbols;
            while (heap.Count > 1)
            {
                var a = heap.Min;
                heap.Remove(a);
                var b = heap.Min;
                heap.Remove(b);

                weight[next] = a.Weight + b.Weight;
                parent[a.Node] = next;
                parent[b.Node] = next;
                heap.Add((weight[next], order++, next));
                next++;
            }

            var root = heap.Min.Node;

            // depth of each internal node, computed from root as parents are created before children are visited
            var depth = new int[symbols * 2];
            depth[root] = 0;
            for (var node = root - 1; node >= symbols; node--)
            {
                depth[node] = depth[parent[node]] + 1;
            }

            for (var i = 0; i < symbols; i++)
            {
                if (frequencies[i] <= 0)
                {
                    lengths[i] = 0;
                    continue;
                }

                var length = depth[parent[i]] + 1;
                if (length > maxLength)
                {
                    return false;
                }

                lengths[i] = (byte)length;
            }

            return true;
        }

        /// <summary>
        /// Create canonical codes from code lengths: shorter codes come first and codes with equal lengths are
        /// assigned in order of symbol.
        /// </summary>
        private static ushort[] CreateCanonicalCodes(byte[] lengths, int maxLength)
        {
            var lengthCounts = new int[maxLength + 1];
            foreach (var length in lengths)
            {
                if (length > 0)
                {
                    lengthCounts[length]++;
                }
            }

            var nextCode = new int[maxLength + 2];
            var code = 0;
            for (var length = 1; length <= maxLength; length++)
            {
                code = (code + lengthCounts[length - 1]) << 1;
                nextCode[length] = code;
            }

            var codes = new ushort[lengths.Length];
            for (var i = 0; i < lengths.Length; i++)
            {
                if (lengths[i] > 0)
                {
                    codes[i] = (ushort)nextCode[lengths[i]]++;
                }
            }

            return codes;
        }

        /// <summary>
        /// Write code for symbol.
        /// </summary>
        public void Write(BitStreamWriter writer, int symbol)
        {
            if (SingleSymbol >= 0)
            {
                // single symbol trees decode without reading any bits
                return;
            }

            if (Lengths[symbol] == 0)
            {
                throw new InvalidOperationException($"Symbol {symbol} has no code");
            }

            writer.WriteBits(Codes[symbol], Lengths[symbol]);
        }
    }
}
