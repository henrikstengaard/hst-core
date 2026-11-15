// Ported from Lhasa lib/tree_decode.c, https://github.com/fragglet/lhasa
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
    /// <summary>
    /// Common tree decoding code.
    ///
    /// A recurring feature used by the different LHA algorithms is to
    /// encode a set of codes, which have varying bit lengths. This is
    /// implemented using a binary tree, stored inside an array of
    /// elements.
    /// </summary>
    internal static class TreeDecoder
    {
        // Upper bit is set in a node value to indicate a leaf.
        private const ushort TREE_NODE_LEAF = 0x8000;

        // Structure used to hold data needed to build the tree.
        private class TreeBuildData
        {
            // The tree data and its size (must not be exceeded)
            public ushort[] Tree;
            public int TreeLen;

            // Counter used to allocate entries from the tree.
            // Every time a new node is allocated, this increase by 2.
            public int TreeAllocated;

            // The next tree entry.
            // As entries are allocated sequentially, the range from
            // next_entry..tree_allocated-1 constitutes the indices into
            // the tree that are available to be filled in. By the
            // end of the tree build, next_entry should = tree_allocated.
            public int NextEntry;
        }

        // Initialize all elements of the given tree to a good initial state.
        public static void InitTree(ushort[] tree, int treeLen)
        {
            for (var i = 0; i < treeLen; ++i)
            {
                tree[i] = TREE_NODE_LEAF;
            }
        }

        // Set tree to always decode to a single code.
        public static void SetTreeSingle(ushort[] tree, int code)
        {
            tree[0] = (ushort)(code | TREE_NODE_LEAF);
        }

        // "Expand" the list of queue entries. This generates a new child
        // node at each of the entries currently in the queue, adding the
        // children of those nodes into the queue to replace them.
        // The effect of this is to add an extra level to the tree, and
        // to increase the tree depth of the indices in the queue.
        private static void ExpandQueue(TreeBuildData build)
        {
            // Sanity check that there is enough space in the tree for
            // all the new nodes.
            var newNodes = (build.TreeAllocated - build.NextEntry) * 2;

            if (build.TreeAllocated + newNodes > build.TreeLen)
            {
                return;
            }

            // Go through all entries currently in the allocated range, and
            // allocate a subnode for each.
            var endOffset = build.TreeAllocated;

            while (build.NextEntry < endOffset)
            {
                build.Tree[build.NextEntry] = (ushort)build.TreeAllocated;
                build.TreeAllocated += 2;
                ++build.NextEntry;
            }
        }

        // Read the next entry from the queue of entries waiting to be used.
        private static int ReadNextEntry(TreeBuildData build)
        {
            // Sanity check.
            if (build.NextEntry >= build.TreeAllocated)
            {
                return 0;
            }

            var result = build.NextEntry;
            ++build.NextEntry;

            return result;
        }

        // Add all codes to the tree that have the specified length.
        // Returns true if there are any entries in code_lengths[] still
        // waiting to be added to the tree.
        private static bool AddCodesWithLength(TreeBuildData build, byte[] codeLengths, int numCodeLengths,
            int codeLen)
        {
            var codesRemaining = false;

            for (var i = 0; i < numCodeLengths; ++i)
            {
                // Does this code belong at this depth in the tree?
                if (codeLengths[i] == codeLen)
                {
                    var node = ReadNextEntry(build);

                    build.Tree[node] = (ushort)(i | TREE_NODE_LEAF);
                }

                // More work to be done after this pass?
                else if (codeLengths[i] > codeLen)
                {
                    codesRemaining = true;
                }
            }

            return codesRemaining;
        }

        // Build a tree, given the specified array of codes indicating the
        // required depth within the tree at which each code should be
        // located.
        public static void BuildTree(ushort[] tree, int treeLen, byte[] codeLengths, int numCodeLengths)
        {
            var build = new TreeBuildData
            {
                Tree = tree,
                TreeLen = treeLen,
                // Start with a single entry in the queue - the root node
                // pointer.
                NextEntry = 0,
                // We always have the root ...
                TreeAllocated = 1
            };

            // Iterate over each possible code length.
            // Note: code_len == 0 is deliberately skipped over, as 0
            // indicates "not used".
            var codeLen = 0;

            do
            {
                // Advance to the next code length by allocating extra
                // nodes to the tree - the slots waiting in the queue
                // will now be one level deeper in the tree (and the
                // codes 1 bit longer).
                ExpandQueue(build);
                ++codeLen;

                // Add all codes that have this length.
            } while (AddCodesWithLength(build, codeLengths, numCodeLengths, codeLen));
        }

        // Read bits from the input stream, traversing the specified tree
        // from the root node until we reach a leaf.  The leaf value is
        // returned or -1 for failure.
        public static int ReadFromTree(BitStreamReader reader, ushort[] tree)
        {
            // Start from root.
            var code = tree[0];

            while ((code & TREE_NODE_LEAF) == 0)
            {
                var bit = reader.ReadBit();

                if (bit < 0)
                {
                    return -1;
                }

                code = tree[code + bit];
            }

            // Mask off leaf bit to get the plain code.
            return code & ~TREE_NODE_LEAF;
        }
    }
}
