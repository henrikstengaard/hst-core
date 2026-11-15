// Ported from Lhasa lib/pma_common.c, https://github.com/fragglet/lhasa
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
    /// Entry in a variable length table used by PMarc decoders.
    /// </summary>
    internal struct VariableLengthTable
    {
        public readonly int Offset;
        public readonly int Bits;

        public VariableLengthTable(int offset, int bits)
        {
            Offset = offset;
            Bits = bits;
        }

        // Read a variable length code, given the header bits already read.
        // Returns the decoded value, or -1 for error.
        public static int Decode(BitStreamReader reader, VariableLengthTable[] table, int header)
        {
            var value = reader.ReadBits(table[header].Bits);

            if (value < 0)
            {
                return -1;
            }

            return table[header].Offset + value;
        }
    }

    /// <summary>
    /// History linked list used by PMarc decoders. In the decode stream, codes representing
    /// characters are not the character itself, but the number of
    /// nodes to count back in time in the linked list. Every time
    /// a character is output, it is moved to the front of the linked
    /// list. The entry point index into the list is the last output
    /// character, given by history_head;
    /// </summary>
    internal class HistoryLinkedList
    {
        private readonly byte[] prev = new byte[256];
        private readonly byte[] next = new byte[256];
        private byte historyHead;

        public HistoryLinkedList()
        {
            // History buffer is initialized to a linear chain to
            // start off with:
            for (var i = 0; i < 256; ++i)
            {
                prev[i] = (byte)(i + 1);
                next[i] = (byte)(i - 1);
            }

            // The chain is cut into groups and initially arranged so
            // that the ASCII characters are closest to the start of
            // the chain. This is followed by ASCII control characters,
            // then various other groups.
            historyHead = 0x20;

            prev[0x7f] = 0x00; // 0x20 ... 0x7f -> 0x00
            next[0x00] = 0x7f;

            prev[0x1f] = 0xa0; // 0x00 ... 0x1f -> 0xa0
            next[0xa0] = 0x1f;

            prev[0xdf] = 0x80; // 0xa0 ... 0xdf -> 0x80
            next[0x80] = 0xdf;

            prev[0x9f] = 0xe0; // 0x80 ... 0x9f -> 0xe0
            next[0xe0] = 0x9f;

            prev[0xff] = 0x20; // 0xe0 ... 0xff -> 0x20
            next[0x20] = 0xff;
        }

        // Look up an entry in the history list, returning the code found.
        public byte Find(byte count)
        {
            // Start from the last outputted byte.
            var code = historyHead;

            // Walk along the history chain until we reach the desired
            // node.  If we will have to walk more than half the chain,
            // go the other way around.
            if (count < 128)
            {
                for (var i = 0; i < count; ++i)
                {
                    code = prev[code];
                }
            }
            else
            {
                for (var i = 0; i < 256 - count; ++i)
                {
                    code = next[code];
                }
            }

            return code;
        }

        // Update history list, by moving the specified byte to the head
        // of the queue.
        public void Update(byte b)
        {
            // No update necessary?
            if (historyHead == b)
            {
                return;
            }

            // Unhook the entry from its current position:
            prev[next[b]] = prev[b];
            next[prev[b]] = next[b];

            // Hook in between the old head and old_head->next:
            prev[b] = historyHead;
            next[b] = next[historyHead];

            prev[next[historyHead]] = b;
            next[historyHead] = b;

            // 'b' is now the head of the queue:
            historyHead = b;
        }
    }
}
