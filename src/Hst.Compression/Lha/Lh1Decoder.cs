// Ported from Lhasa lib/lh1_decoder.c, https://github.com/fragglet/lhasa
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
    /// Decoder for -lh1- method using an adaptive huffman tree.
    /// </summary>
    internal class Lh1Decoder : BlockDecoder
    {
        // Size of the ring buffer used to hold history:
        private const int RING_BUFFER_SIZE = 4096; /* bytes */

        // When this limit is reached, the code tree is reordered.
        private const int TREE_REORDER_LIMIT = 32 * 1024; /* 32 kB */

        // Number of codes ('byte' codes + 'copy' codes):
        private const int NUM_CODES = 314;

        // Number of nodes in the code tree.
        private const int NUM_TREE_NODES = NUM_CODES * 2 - 1;

        // Number of possible offsets:
        private const int NUM_OFFSETS = 64;

        // Minimum length of the offset top bits:
        private const int MIN_OFFSET_LENGTH = 3; /* bits */

        // Threshold for copying. The first copy code starts from here.
        private const int COPY_THRESHOLD = 3; /* bytes */

        // Required size of the output buffer.  At most, a single call to read()
        // might result in a copy of the entire ring buffer.
        private const int OUTPUT_BUFFER_SIZE = RING_BUFFER_SIZE;

        private struct Node
        {
            // If true, this node is a leaf node.
            public bool Leaf;

            // If this is a leaf node, child_index is the code represented by
            // this node. Otherwise, nodes[child_index] and nodes[child_index-1]
            // are the children of this node.
            public ushort ChildIndex;

            // Index of the parent node of this node.
            public ushort Parent;

            // Frequency count for this node - number of times that it has
            // received a hit.
            public ushort Freq;

            // Group that this node belongs to.
            public ushort Group;
        }

        // Frequency distribution used to calculate the offset codes.
        private static readonly int[] OffsetFdist =
        {
            1, // 3 bits
            3, // 4 bits
            8, // 5 bits
            12, // 6 bits
            24, // 7 bits
            16, // 8 bits
        };

        // Input bit stream.
        private readonly BitStreamReader bitStreamReader;

        // Ring buffer of past data.  Used for position-based copies.
        private readonly byte[] ringbuf = new byte[RING_BUFFER_SIZE];
        private int ringbufPos;

        // Array of tree nodes. nodes[0] is the root node.  The array
        // is maintained in order by frequency.
        private readonly Node[] nodes = new Node[NUM_TREE_NODES];

        // Indices of leaf nodes of the tree (map from code to leaf
        // node index)
        private readonly ushort[] leafNodes = new ushort[NUM_CODES];

        // Groups list.  Every node belongs to a group.  All nodes within
        // a group have the same frequency. There can be at most
        // NUM_TREE_NODES groups (one for each node). num_groups is used
        // to allocate and deallocate groups as needed.
        private readonly ushort[] groups = new ushort[NUM_TREE_NODES];
        private int numGroups;

        // Index of the "leader" of a group within the nodes[] array.
        // The leader is the left-most node within a span of nodes with
        // the same frequency.
        private readonly ushort[] groupLeader = new ushort[NUM_TREE_NODES];

        // Offset lookup table.  Maps from a byte value (sequence of next
        // 8 bits from input stream) to an offset value.
        private readonly byte[] offsetLookup = new byte[256];

        // Length of offsets, in bits.
        private readonly byte[] offsetLengths = new byte[NUM_OFFSETS];

        public Lh1Decoder(Stream input) : base(OUTPUT_BUFFER_SIZE)
        {
            // Initialize input stream reader.
            bitStreamReader = new BitStreamReader(input);

            // Initialize data structures.
            InitGroups();
            InitTree();
            InitOffsetTable();
            InitRingBuffer();
        }

        // Allocate a group from the free groups array.
        private ushort AllocGroup()
        {
            var result = groups[numGroups];
            ++numGroups;

            return result;
        }

        // Free a group that is no longer in use.
        private void FreeGroup(ushort group)
        {
            --numGroups;
            groups[numGroups] = group;
        }

        // Initialize groups array.
        private void InitGroups()
        {
            for (var i = 0; i < NUM_TREE_NODES; ++i)
            {
                groups[i] = (ushort)i;
            }

            numGroups = 0;
        }

        // Initialize the tree with its basic initial configuration.
        private void InitTree()
        {
            // Leaf nodes are placed at the end of the table.  Start by
            // initializing these, and working backwards.
            var nodeIndex = NUM_TREE_NODES - 1;
            var leafGroup = AllocGroup();

            for (var i = 0; i < NUM_CODES; ++i)
            {
                nodes[nodeIndex].Leaf = true;
                nodes[nodeIndex].ChildIndex = (ushort)i;
                nodes[nodeIndex].Freq = 1;
                nodes[nodeIndex].Group = leafGroup;
                groupLeader[leafGroup] = (ushort)nodeIndex;
                leafNodes[i] = (ushort)nodeIndex;
                --nodeIndex;
            }

            // Now build up the intermediate nodes, up to the root.  Each
            // node gets two nodes as children.
            var child = NUM_TREE_NODES - 1;

            while (nodeIndex >= 0)
            {
                nodes[nodeIndex].Leaf = false;

                // Set child pointer and update the parent pointers of the
                // children.
                nodes[nodeIndex].ChildIndex = (ushort)child;
                nodes[child].Parent = (ushort)nodeIndex;
                nodes[child - 1].Parent = (ushort)nodeIndex;

                // The node's frequency is equal to the sum of the frequencies
                // of its children.
                nodes[nodeIndex].Freq = (ushort)(nodes[child].Freq + nodes[child - 1].Freq);

                // Is the frequency the same as the last node we processed?
                // if so, we are in the same group. If not, we must
                // allocate a new group.  Either way, this node is now the
                // leader of its group.
                if (nodes[nodeIndex].Freq == nodes[nodeIndex + 1].Freq)
                {
                    nodes[nodeIndex].Group = nodes[nodeIndex + 1].Group;
                }
                else
                {
                    nodes[nodeIndex].Group = AllocGroup();
                }

                groupLeader[nodes[nodeIndex].Group] = (ushort)nodeIndex;

                // Process next node.
                --nodeIndex;
                child -= 2;
            }
        }

        // Fill in a range of values in the offset_lookup table, which have
        // the bits from 'code' as the high bits, and the low bits can be
        // any values in the range from 'mask'.  Set these values to point
        // to 'offset'.
        private void FillOffsetRange(byte code, int mask, int offset)
        {
            // Set offset lookup table to map from all possible input values
            // that fit within the mask to the target offset.
            for (var i = 0; (i & ~mask) == 0; ++i)
            {
                offsetLookup[code | i] = (byte)offset;
            }
        }

        // Calculate the values for the offset_lookup and offset_lengths
        // tables.
        private void InitOffsetTable()
        {
            byte code = 0;
            byte offset = 0;

            // Iterate through each entry in the frequency distribution table,
            // filling in codes in the lookup table as we go.
            for (var i = 0; i < OffsetFdist.Length; ++i)
            {
                // offset_fdist[0] is the number of codes of length
                // MIN_OFFSET_LENGTH bits, increasing as we go. As the
                // code increases in length, we must iterate progressively
                // lower bits in the code (moving right - extending the
                // code to be 1 bit longer).
                var len = i + MIN_OFFSET_LENGTH;
                var iterbit = (byte)(1 << (8 - len));

                for (var j = 0; j < OffsetFdist[i]; ++j)
                {
                    // Store lookup values for this offset in the
                    // lookup table, and save the code length.
                    // (iterbit - 1) turns into a mask for the lower
                    // bits that are not part of the code.
                    FillOffsetRange(code, (byte)(iterbit - 1), offset);
                    offsetLengths[offset] = (byte)len;

                    // Iterate to next code.
                    code = (byte)(code + iterbit);
                    ++offset;
                }
            }
        }

        // Initialize the history ring buffer.
        private void InitRingBuffer()
        {
            for (var i = 0; i < RING_BUFFER_SIZE; i++)
            {
                ringbuf[i] = (byte)' ';
            }

            ringbufPos = 0;
        }

        // Make the given node the leader of its group: swap it with the current
        // leader so that it is in the left-most position.  Returns the new index
        // of the node.
        private ushort MakeGroupLeader(ushort nodeIndex)
        {
            var group = nodes[nodeIndex].Group;
            var leaderIndex = groupLeader[group];

            // Already the leader?  If so, there is nothing to do.
            if (leaderIndex == nodeIndex)
            {
                return nodeIndex;
            }

            // Swap leaf and child indices in the two nodes:
            var tmpLeaf = nodes[leaderIndex].Leaf;
            nodes[leaderIndex].Leaf = nodes[nodeIndex].Leaf;
            nodes[nodeIndex].Leaf = tmpLeaf;

            var tmpChild = nodes[leaderIndex].ChildIndex;
            nodes[leaderIndex].ChildIndex = nodes[nodeIndex].ChildIndex;
            nodes[nodeIndex].ChildIndex = tmpChild;

            if (nodes[nodeIndex].Leaf)
            {
                leafNodes[nodes[nodeIndex].ChildIndex] = nodeIndex;
            }
            else
            {
                nodes[nodes[nodeIndex].ChildIndex].Parent = nodeIndex;
                nodes[nodes[nodeIndex].ChildIndex - 1].Parent = nodeIndex;
            }

            if (nodes[leaderIndex].Leaf)
            {
                leafNodes[nodes[leaderIndex].ChildIndex] = leaderIndex;
            }
            else
            {
                nodes[nodes[leaderIndex].ChildIndex].Parent = leaderIndex;
                nodes[nodes[leaderIndex].ChildIndex - 1].Parent = leaderIndex;
            }

            return leaderIndex;
        }

        // Increase the frequency count for a node, rearranging groups as
        // appropriate.
        private void IncrementNodeFreq(ushort nodeIndex)
        {
            var otherIndex = nodeIndex - 1;

            ++nodes[nodeIndex].Freq;

            // If the node is part of a group containing other nodes, it
            // must leave the group.
            if (nodeIndex < NUM_TREE_NODES - 1 && nodes[nodeIndex].Group == nodes[nodeIndex + 1].Group)
            {
                // Next node in the group now becomes the leader.
                ++groupLeader[nodes[nodeIndex].Group];

                // The node must now either join the group to its
                // left, or start a new group.
                if (nodes[nodeIndex].Freq == nodes[otherIndex].Freq)
                {
                    nodes[nodeIndex].Group = nodes[otherIndex].Group;
                }
                else
                {
                    nodes[nodeIndex].Group = AllocGroup();
                    groupLeader[nodes[nodeIndex].Group] = nodeIndex;
                }
            }
            else
            {
                // The node is in a group of its own (single-node
                // group).  It might need to join the group of the
                // node on its left if it has the same frequency.
                if (nodes[nodeIndex].Freq == nodes[otherIndex].Freq)
                {
                    FreeGroup(nodes[nodeIndex].Group);
                    nodes[nodeIndex].Group = nodes[otherIndex].Group;
                }
            }
        }

        // Reconstruct the code huffman tree to be more evenly distributed.
        // Invoked periodically as data is processed.
        private void ReconstructTree()
        {
            // Gather all leaf nodes at the start of the table.
            var leaf = 0;

            for (var i = 0; i < NUM_TREE_NODES; ++i)
            {
                if (nodes[i].Leaf)
                {
                    nodes[leaf].Leaf = true;
                    nodes[leaf].ChildIndex = nodes[i].ChildIndex;

                    // Frequency of the nodes in the new tree is halved,
                    // this acts as a running average each time the
                    // tree is reconstructed.
                    nodes[leaf].Freq = (ushort)((nodes[i].Freq + 1) / 2);

                    ++leaf;
                }
            }

            // The leaf nodes are now all at the start of the table.  Now
            // reconstruct the tree, starting from the end of the table and
            // working backwards, inserting branch nodes between the leaf
            // nodes.  Each branch node inherits the sum of the frequencies
            // of its children, and must be placed to maintain the ordering
            // within the table by decreasing frequency.
            leaf = NUM_CODES - 1;
            var child = NUM_TREE_NODES - 1;
            var index = NUM_TREE_NODES - 1;

            while (index >= 0)
            {
                // Before we can add a new branch node, we need at least
                // two nodes to use as children.  If we don't have this
                // then we need to copy some from the leaves.
                while (child - index < 2)
                {
                    nodes[index] = nodes[leaf];
                    leafNodes[nodes[leaf].ChildIndex] = (ushort)index;

                    --index;
                    --leaf;
                }

                // Now that we have at least two nodes to take as children
                // of the new branch node, we can calculate the branch
                // node's frequency.
                var freq = nodes[child].Freq + nodes[child - 1].Freq;

                // Now copy more leaf nodes until the correct place to
                // insert the new branch node presents itself.
                while (leaf >= 0 && freq >= nodes[leaf].Freq)
                {
                    nodes[index] = nodes[leaf];
                    leafNodes[nodes[leaf].ChildIndex] = (ushort)index;

                    --index;
                    --leaf;
                }

                // The new branch node can now be inserted.
                nodes[index].Leaf = false;
                nodes[index].Freq = (ushort)freq;
                nodes[index].ChildIndex = (ushort)child;

                nodes[child].Parent = (ushort)index;
                nodes[child - 1].Parent = (ushort)index;

                --index;

                // Process the next pair of children.
                child -= 2;
            }

            // Reconstruct the group data.  Start by resetting group data.
            InitGroups();

            // Assign a group to the first node.
            var group = AllocGroup();
            nodes[0].Group = group;
            groupLeader[group] = 0;

            // Assign a group number to each node, nodes having the same
            // group if the have the same frequency, and allocating new
            // groups when a new frequency is found.
            for (var i = 1; i < NUM_TREE_NODES; ++i)
            {
                if (nodes[i].Freq == nodes[i - 1].Freq)
                {
                    nodes[i].Group = nodes[i - 1].Group;
                }
                else
                {
                    group = AllocGroup();
                    nodes[i].Group = group;

                    // First node with a particular frequency is leader.
                    groupLeader[group] = (ushort)i;
                }
            }
        }

        // Increment the counter for the specific code, reordering the tree as
        // necessary.
        private void IncrementForCode(ushort code)
        {
            // When the limit is reached, we must reorder the code tree
            // to better match the code frequencies:
            if (nodes[0].Freq >= TREE_REORDER_LIMIT)
            {
                ReconstructTree();
            }

            ++nodes[0].Freq;

            // Dynamically adjust the tree.  Start from the leaf node of
            // the tree and walk back up, rearranging nodes to the root.
            var nodeIndex = leafNodes[code];

            while (nodeIndex != 0)
            {
                // Shift the node to the left side of its group,
                // and bump the frequency count.
                nodeIndex = MakeGroupLeader(nodeIndex);

                IncrementNodeFreq(nodeIndex);

                // Iterate up to the parent node.
                nodeIndex = nodes[nodeIndex].Parent;
            }
        }

        // Read a code from the input stream.
        private bool ReadCode(out ushort result)
        {
            result = 0;

            // Start from the root node, and traverse down until a leaf is
            // reached.
            var nodeIndex = 0;

            while (!nodes[nodeIndex].Leaf)
            {
                var bit = bitStreamReader.ReadBit();

                if (bit < 0)
                {
                    return false;
                }

                // Choose one of the two children depending on the
                // bit that was read.
                nodeIndex = nodes[nodeIndex].ChildIndex - bit;
            }

            result = nodes[nodeIndex].ChildIndex;
            IncrementForCode(result);

            return true;
        }

        // Read an offset code from the input stream.
        private bool ReadOffset(out int result)
        {
            result = 0;

            // The offset can be up to 8 bits long, but is likely not
            // that long. Use the lookup table to find the offset
            // and its length.
            var future = bitStreamReader.PeekBits(8);

            if (future < 0)
            {
                return false;
            }

            var offset = offsetLookup[future];

            // Skip past the offset bits and also read the following
            // lower-order bits.
            bitStreamReader.ReadBits(offsetLengths[offset]);

            var offset2 = bitStreamReader.ReadBits(6);

            if (offset2 < 0)
            {
                return false;
            }

            result = (offset << 6) | offset2;

            return true;
        }

        private void OutputByte(byte[] buf, ref int bufLen, byte b)
        {
            buf[bufLen] = b;
            ++bufLen;

            ringbuf[ringbufPos] = b;
            ringbufPos = (ringbufPos + 1) % RING_BUFFER_SIZE;
        }

        protected override int ReadBlock(byte[] buf)
        {
            var result = 0;

            // Read the next code from the input stream.
            if (!ReadCode(out var code))
            {
                return 0;
            }

            // The code either indicates a single byte to be output, or
            // it indicates that a block should be copied from the ring
            // buffer as it is a repeat of a sequence earlier in the
            // stream.
            if (code < 0x100)
            {
                OutputByte(buf, ref result, (byte)code);
            }
            else
            {
                // Read the offset into the history at which to start
                // copying.
                if (!ReadOffset(out var offset))
                {
                    return 0;
                }

                var count = code - 0x100 + COPY_THRESHOLD;
                var start = ringbufPos - offset + RING_BUFFER_SIZE - 1;

                // Copy from history into output buffer:
                for (var i = 0; i < count; ++i)
                {
                    var pos = (start + i) % RING_BUFFER_SIZE;

                    OutputByte(buf, ref result, ringbuf[pos]);
                }
            }

            return result;
        }
    }
}
