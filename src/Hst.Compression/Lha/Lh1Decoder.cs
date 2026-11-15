namespace Hst.Compression.Lha
{
    using System;
    using System.IO;

    /// <summary>
    /// Decoder for the -lh1- method, the LZHUF algorithm with a 4 KiB window. Literals and copy lengths are encoded
    /// with an adaptive huffman code, which is updated after each symbol. The upper 6 bits of copy offsets are encoded
    /// with a fixed prefix code followed by the lower 6 bits.
    /// </summary>
    /// <remarks>
    /// The adaptive huffman tree is kept in arrays ordered by increasing frequency with the root as last node.
    /// Children of an internal node are stored next to each other, where the first child is the one with the lower
    /// frequency.
    /// </remarks>
    internal sealed class Lh1Decoder : WindowDecoder
    {
        private const int WINDOW_BITS = 12;
        private const int MIN_COPY_LENGTH = 3;
        private const int MAX_COPY_LENGTH = 60;
        private const int SYMBOLS = 256 + MAX_COPY_LENGTH - MIN_COPY_LENGTH + 1;
        private const int NODES = SYMBOLS * 2 - 1;
        private const int ROOT = NODES - 1;
        private const int REBUILD_FREQUENCY = 0x8000;

        // number of upper offset codes for each code length from 3 to 8 bits
        private static readonly int[] OffsetCodeLengthCounts = { 1, 3, 8, 12, 24, 16 };

        private readonly BitReader reader;
        private readonly PrefixCodeDecoder offsetCodes = new PrefixCodeDecoder(64);

        // frequency of each node, with a sentinel after last node
        private readonly int[] frequencies = new int[NODES + 1];

        // first child of each internal node, leaves have NODES + symbol
        private readonly int[] children = new int[NODES];

        // parent of each node followed by parent of each symbol leaf
        private readonly int[] parents = new int[NODES + SYMBOLS];

        public Lh1Decoder(Stream input) : base(WINDOW_BITS, (byte)' ')
        {
            reader = new BitReader(input);

            var offsetCodeLengths = new byte[64];
            var symbol = 0;
            for (var i = 0; i < OffsetCodeLengthCounts.Length; i++)
            {
                for (var j = 0; j < OffsetCodeLengthCounts[i]; j++)
                {
                    offsetCodeLengths[symbol++] = (byte)(i + 3);
                }
            }

            offsetCodes.Build(offsetCodeLengths, offsetCodeLengths.Length);

            InitializeTree();
        }

        private void InitializeTree()
        {
            // leaves with frequency 1 for all symbols
            for (var symbol = 0; symbol < SYMBOLS; symbol++)
            {
                frequencies[symbol] = 1;
                children[symbol] = NODES + symbol;
                parents[NODES + symbol] = symbol;
            }

            // internal nodes joining pairs of nodes
            for (int node = SYMBOLS, child = 0; node < NODES; node++, child += 2)
            {
                frequencies[node] = frequencies[child] + frequencies[child + 1];
                children[node] = child;
                parents[child] = node;
                parents[child + 1] = node;
            }

            frequencies[NODES] = int.MaxValue;
            parents[ROOT] = 0;
        }

        /// <summary>
        /// Rebuild tree with halved frequencies.
        /// </summary>
        private void RebuildTree()
        {
            // collect leaves in order with halved frequencies
            var leaves = 0;
            for (var node = 0; node < NODES; node++)
            {
                if (children[node] < NODES)
                {
                    continue;
                }

                frequencies[leaves] = (frequencies[node] + 1) / 2;
                children[leaves] = children[node];
                leaves++;
            }

            // join pairs of nodes in order, inserting each new internal node after nodes with lower or equal frequency
            for (int node = SYMBOLS, child = 0; node < NODES; node++, child += 2)
            {
                var frequency = frequencies[child] + frequencies[child + 1];
                var position = node;
                while (frequencies[position - 1] > frequency)
                {
                    position--;
                }

                Array.Copy(frequencies, position, frequencies, position + 1, node - position);
                Array.Copy(children, position, children, position + 1, node - position);
                frequencies[position] = frequency;
                children[position] = child;
            }

            for (var node = 0; node < NODES; node++)
            {
                var child = children[node];
                parents[child] = node;
                if (child < NODES)
                {
                    parents[child + 1] = node;
                }
            }
        }

        /// <summary>
        /// Increment frequency of symbol and its ancestors. A node, which gets a higher frequency than following
        /// nodes, is swapped with the last node with its previous frequency to keep nodes ordered by frequency.
        /// </summary>
        private void Update(int symbol)
        {
            if (frequencies[ROOT] == REBUILD_FREQUENCY)
            {
                RebuildTree();
            }

            var node = parents[NODES + symbol];
            do
            {
                var frequency = ++frequencies[node];
                if (frequency > frequencies[node + 1])
                {
                    var swap = node + 1;
                    while (frequency > frequencies[swap + 1])
                    {
                        swap++;
                    }

                    frequencies[node] = frequencies[swap];
                    frequencies[swap] = frequency;

                    var child = children[node];
                    var swapChild = children[swap];
                    children[node] = swapChild;
                    children[swap] = child;
                    SetParent(child, swap);
                    SetParent(swapChild, node);

                    node = swap;
                }

                node = parents[node];
            } while (node != 0);
        }

        private void SetParent(int child, int parent)
        {
            parents[child] = parent;
            if (child < NODES)
            {
                parents[child + 1] = parent;
            }
        }

        private int DecodeSymbol()
        {
            var node = children[ROOT];
            while (node < NODES)
            {
                node = children[node + reader.ReadBit()];
            }

            var symbol = node - NODES;
            Update(symbol);
            return symbol;
        }

        protected override bool DecodeCommand()
        {
            var symbol = DecodeSymbol();
            if (symbol < 256)
            {
                Literal((byte)symbol);
                return true;
            }

            var offset = (offsetCodes.Decode(reader) << 6) | reader.Read(6);
            Copy(offset + 1, symbol - 256 + MIN_COPY_LENGTH);
            return true;
        }
    }
}
