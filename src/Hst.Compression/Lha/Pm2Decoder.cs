/***********************************************************
	pm2.c -- extract pmext2 coding
***********************************************************/
/*
  Copyright (c) 1999 Maarten ter Huurne

  Permission is hereby granted, free of charge, to any person
  obtaining a copy of this software and associated documentation files
  (the "Software"), to deal in the Software without restriction,
  including without limitation the rights to use, copy, modify, merge,
  publish, distribute, sublicense, and/or sell copies of the Software,
  and to permit persons to whom the Software is furnished to do so,
  subject to the following conditions:

  The above copyright notice and this permission notice shall be
  included in all copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS
  BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN
  ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
  CONNECTION WITH THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
namespace Hst.Compression.Lha
{
    using System.IO;

    /// <summary>
    /// Decoder for -pm2- method (PMarc2). Combines pm2.c, pm2hist.c and pm2tree.c.
    /// </summary>
    public class Pm2Decoder : IDecoder
    {
        // {decode_c_pm2, decode_p_pm2, decode_start_pm2}
        private const int Offset = 0x100 - 2;

        private static readonly int[] HistoryBits = { 3, 3, 4, 5, 5, 5, 6, 6 };
        private static readonly int[] HistoryBase = { 0, 8, 16, 32, 64, 96, 128, 192 };
        private static readonly int[] RepeatBits = { 3, 3, 5, 6, 7, 0 };
        private static readonly int[] RepeatBase = { 17, 25, 33, 65, 129, 256 };

        private long nextcount;
        private int lastupdate;
        private int dicsiz1;
        private int gettree1;

        // pm2hist.c: circular double-linked list
        private readonly byte[] histPrev = new byte[0x100];
        private readonly byte[] histNext = new byte[0x100];
        private byte lastbyte;

        // pm2tree.c
        private readonly Tree tree1 = new Tree(32);
        private readonly Tree tree2 = new Tree(8);
        private byte tree1bound;
        private byte mindepth;

        private class Tree
        {
            public byte Root;
            public readonly byte[] LeftArr;
            public readonly byte[] RightArr;

            public Tree(int size)
            {
                LeftArr = new byte[size];
                RightArr = new byte[size];
            }
        }

        public void DecodeStart(Lha lha)
        {
            dicsiz1 = (1 << lha.dicbit) - 1;
            lha.bitIo.InitGetBits();
            HistInit();
            nextcount = 0;
            lastupdate = 0;
            lha.bitIo.GetBits(1); /* discard bit */
        }

        public ushort DecodeC(Lha lha)
        {
            /* various admin: */
            while (lastupdate != lha.loc)
            {
                HistUpdate(lha.dtext[lastupdate]);
                lastupdate = (lastupdate + 1) & dicsiz1;
            }

            while (lha.decode_count >= nextcount)
            {
                /* Actually it will never loop, because decode_count doesn't grow that fast.
                   However, this is the way LHA does it.
                   Probably other encoding methods can have repeats larger than 256 bytes.
                   Note: LHA puts this code in decode_p...
                */
                switch (nextcount)
                {
                    case 0x0000:
                        MakeTree1(lha);
                        MakeTree2(lha, 5);
                        nextcount = 0x0400;
                        break;
                    case 0x0400:
                        MakeTree2(lha, 6);
                        nextcount = 0x0800;
                        break;
                    case 0x0800:
                        MakeTree2(lha, 7);
                        nextcount = 0x1000;
                        break;
                    case 0x1000:
                        if (lha.bitIo.GetBits(1) != 0)
                        {
                            MakeTree1(lha);
                        }

                        MakeTree2(lha, 8);
                        nextcount = 0x2000;
                        break;
                    default: /* 0x2000, 0x3000, 0x4000, ... */
                        if (lha.bitIo.GetBits(1) != 0)
                        {
                            MakeTree1(lha);
                            MakeTree2(lha, 8);
                        }

                        nextcount += 0x1000;
                        break;
                }
            }

            gettree1 = TreeGet(lha, tree1); /* value preserved for decode_p */
            if (gettree1 >= 29)
            {
                throw new IOException("Bad table");
            }

            /* direct value (ret <= UCHAR_MAX) */
            if (gettree1 < 8)
            {
                return HistLookup(HistoryBase[gettree1] + lha.bitIo.GetBits(HistoryBits[gettree1]));
            }

            /* repeats: (ret > UCHAR_MAX) */
            if (gettree1 < 23)
            {
                return (ushort)(Offset + 2 + (gettree1 - 8));
            }

            return (ushort)(Offset + RepeatBase[gettree1 - 23] + lha.bitIo.GetBits(RepeatBits[gettree1 - 23]));
        }

        public ushort DecodeP(Lha lha)
        {
            /* gettree1 value preserved from decode_c */
            int nbits, delta;
            if (gettree1 == 8)
            {
                /* 2-byte repeat with offset 0..63 */
                nbits = 6;
                delta = 0;
            }
            else if (gettree1 < 28)
            {
                /* n-byte repeat with offset 0..8191 */
                var gettree2 = TreeGet(lha, tree2);
                if (gettree2 == 0)
                {
                    nbits = 6;
                    delta = 0;
                }
                else
                {
                    /* 1..7 */
                    nbits = 5 + gettree2;
                    delta = 1 << nbits;
                }
            }
            else
            {
                /* 256 bytes repeat with offset 0 */
                nbits = 0;
                delta = 0;
            }

            return (ushort)(delta + lha.bitIo.GetBits(nbits));
        }

        /* pm2hist.c */
        private void HistInit()
        {
            for (var i = 0; i < 0x100; i++)
            {
                histPrev[(0xFF + i) & 0xFF] = (byte)i;
                histNext[(0x01 + i) & 0xFF] = (byte)i;
            }

            histPrev[0x7F] = 0x00;
            histNext[0x00] = 0x7F;
            histPrev[0xDF] = 0x80;
            histNext[0x80] = 0xDF;
            histPrev[0x9F] = 0xE0;
            histNext[0xE0] = 0x9F;
            histPrev[0x1F] = 0xA0;
            histNext[0xA0] = 0x1F;
            histPrev[0xFF] = 0x20;
            histNext[0x20] = 0xFF;
            lastbyte = 0x20;
        }

        private byte HistLookup(int n)
        {
            var direction = histPrev;
            if (n >= 0x80)
            {
                /* Speedup: If you have to process more than half the ring,
                   it's faster to walk the other way around.
                 */
                direction = histNext;
                n = 0x100 - n;
            }

            var i = lastbyte;
            for (; n != 0; n--)
            {
                i = direction[i];
            }

            return i;
        }

        private void HistUpdate(byte data)
        {
            if (data == lastbyte)
            {
                return;
            }

            /* detach from old position */
            var oldNext = histNext[data];
            var oldPrev = histPrev[data];
            histPrev[oldNext] = oldPrev;
            histNext[oldPrev] = oldNext;

            /* attach to new next */
            var newNext = histNext[lastbyte];
            histPrev[newNext] = data;
            histNext[data] = newNext;

            /* attach to new prev */
            histPrev[data] = lastbyte;
            histNext[lastbyte] = data;

            lastbyte = data;
        }

        /* pm2tree.c */
        private void MakeTree1(Lha lha)
        {
            var table1 = new byte[32];

            tree1bound = (byte)lha.bitIo.GetBits(5);
            mindepth = (byte)lha.bitIo.GetBits(3);
            if (mindepth == 0)
            {
                TreeSetSingle(tree1, (byte)(tree1bound - 1));
            }
            else
            {
                var nbits = lha.bitIo.GetBits(3);
                for (var i = 0; i < tree1bound; i++)
                {
                    var x = lha.bitIo.GetBits(nbits);
                    table1[i] = (byte)(x == 0 ? 0 : x - 1 + mindepth);
                }

                TreeRebuild(tree1, tree1bound, mindepth, 31, table1);
            }
        }

        private void MakeTree2(Lha lha, int tree2bound) /* in use: 5 <= tree2bound <= 8 */
        {
            if (tree1bound < 10)
            {
                /* tree1bound=1..8: character only, offset value is no needed. */
                /* tree1bound=9: offset value is not encoded by Huffman tree */
                return;
            }

            if (tree1bound == 29 && mindepth == 0)
            {
                /* the length value is just 256 and offset value is just 0 */
                return;
            }

            /* need to build tree2 for offset value */
            var table2 = new byte[8];
            for (var i = 0; i < tree2bound; i++)
            {
                table2[i] = (byte)lha.bitIo.GetBits(3);
            }

            var index = 0;
            var count = 0;
            for (var i = 0; i < tree2bound; i++)
            {
                if (table2[i] != 0)
                {
                    index = i;
                    count++;
                }
            }

            if (count == 1)
            {
                TreeSetSingle(tree2, (byte)index);
            }
            else if (count > 1)
            {
                TreeRebuild(tree2, (byte)tree2bound, 1, 7, table2);
            }
            // Note: count == 0 is possible!
            //       Excluding that possibility was a bug in version 1.
        }

        private static int TreeGet(Lha lha, Tree t)
        {
            int i = t.Root;
            while (i < 0x80)
            {
                i = lha.bitIo.GetBits(1) == 0 ? t.LeftArr[i] : t.RightArr[i];
            }

            return i & 0x7F;
        }

        private static void TreeSetSingle(Tree t, byte value)
        {
            t.Root = (byte)(128 | value);
        }

        private static void TreeRebuild(Tree t, byte bound, byte mindepth, byte maxdepth, byte[] table)
        {
            var parentarr = new byte[32];
            int i;

            /* validate table */
            var count = new uint[32];
            for (i = 0; i < bound; i++)
            {
                if (table[i] > maxdepth)
                {
                    throw new IOException("Bad table");
                }

                count[table[i]]++;
            }

            var total = 0.0;
            for (i = mindepth; i <= maxdepth; i++)
            {
                // note: long is used as 1 << 31 overflows int, in c it's compared as unsigned int
                var maxLeaves = 1L << i;
                if (count[i] > maxLeaves)
                {
                    throw new IOException("Bad table");
                }

                total += 1.0 / maxLeaves * count[i];
            }

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (total != 1.0)
            {
                /* check the Kraft's inequality */
                throw new IOException("Bad table");
            }

            /* initialize tree */
            t.Root = 0;
            for (i = 0; i < bound; i++)
            {
                t.LeftArr[i] = 0;
                t.RightArr[i] = 0;
                parentarr[i] = 0;
            }

            /* build tree */
            for (i = 0; i < mindepth - 1; i++)
            {
                t.LeftArr[i] = (byte)(i + 1);
                parentarr[i + 1] = (byte)i;
            }

            var curr = mindepth - 1;
            var empty = (int)mindepth;
            for (var d = mindepth; d <= maxdepth; d++)
            {
                for (i = 0; i < bound; i++)
                {
                    if (table[i] != d)
                    {
                        continue;
                    }

                    if (t.LeftArr[curr] == 0)
                    {
                        t.LeftArr[curr] = (byte)(i | 128);
                        continue;
                    }

                    t.RightArr[curr] = (byte)(i | 128);
                    var n = 0;
                    while (t.RightArr[curr] != 0)
                    {
                        if (curr == 0)
                        {
                            /* root? -> done */
                            return;
                        }

                        curr = parentarr[curr];
                        n++;
                    }

                    t.RightArr[curr] = (byte)empty;
                    for (;;)
                    {
                        parentarr[empty] = (byte)curr;
                        curr = empty;
                        empty++;

                        n--;
                        if (n == 0)
                        {
                            break;
                        }

                        t.LeftArr[curr] = (byte)empty;
                    }
                }

                if (t.LeftArr[curr] == 0)
                {
                    t.LeftArr[curr] = (byte)empty;
                }
                else
                {
                    t.RightArr[curr] = (byte)empty;
                }

                parentarr[empty] = (byte)curr;
                curr = empty;
                empty++;
            }

            /* unreachable */
            throw new IOException("Bad table");
        }
    }
}
