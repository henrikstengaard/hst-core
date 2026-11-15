/* ------------------------------------------------------------------------ */
/* LHa for UNIX                                                             */
/*              maketree.c -- make Huffman tree                             */
/*                                                                          */
/*      Modified                Nobutaka Watazaki                           */
/*                                                                          */
/*  Ver. 1.14   Source All chagned              1995.01.14  N.Watazaki      */
/* ------------------------------------------------------------------------ */
namespace Hst.Compression.Lha
{
    internal static class MakeTree
    {
        private static void MakeCode(int nchar, byte[] bitlen, ushort[] code, ushort[] leafNum)
        {
            var weight = new ushort[17]; /* 0x10000ul >> bitlen */
            var start = new ushort[17]; /* start code */
            ushort total = 0;

            for (var i = 1; i <= 16; i++)
            {
                start[i] = total;
                weight[i] = (ushort)(1 << (16 - i));
                total += (ushort)(weight[i] * leafNum[i]);
            }

            for (var c = 0; c < nchar; c++)
            {
                var i = bitlen[c];
                code[c] = start[i];
                start[i] += weight[i];
            }
        }

        private static void CountLeaf(Lha lha, int node, int nchar, ushort[] leafNum, int depth) /* call with node = root */
        {
            if (node < nchar)
            {
                leafNum[depth < 16 ? depth : 16]++;
            }
            else
            {
                CountLeaf(lha, lha.left[node], nchar, leafNum, depth + 1);
                CountLeaf(lha, lha.right[node], nchar, leafNum, depth + 1);
            }
        }

        private static void MakeLen(byte[] bitlen, ushort[] sort, ushort[] leafNum)
        {
            uint cum = 0;
            for (var i = 16; i > 0; i--)
            {
                cum += (uint)(leafNum[i] << (16 - i));
            }

            cum &= 0xffff;

            /* adjust len */
            if (cum != 0)
            {
                leafNum[16] -= (ushort)cum; /* always leaf_num[16] > cum */
                do
                {
                    for (var i = 15; i > 0; i--)
                    {
                        if (leafNum[i] != 0)
                        {
                            leafNum[i]--;
                            leafNum[i + 1] += 2;
                            break;
                        }
                    }
                } while (--cum != 0);
            }

            /* make len */
            var sortPos = 0;
            for (var i = 16; i > 0; i--)
            {
                int k = leafNum[i];
                while (k > 0)
                {
                    bitlen[sort[sortPos++]] = (byte)i;
                    k--;
                }
            }
        }

        /* priority queue; send i-th entry down heap */
        private static void DownHeap(int i, short[] heap, int heapsize, ushort[] freq)
        {
            int j;

            var k = heap[i];
            while ((j = 2 * i) <= heapsize)
            {
                if (j < heapsize && freq[heap[j]] > freq[heap[j + 1]])
                {
                    j++;
                }

                if (freq[k] <= freq[heap[j]])
                {
                    break;
                }

                heap[i] = heap[j];
                i = j;
            }

            heap[i] = k;
        }

        /// <summary>
        /// make tree, calculate bitlen[], return root
        /// </summary>
        public static short Make(Lha lha, int nchar, ushort[] freq, byte[] bitlen, ushort[] code)
        {
            short root;
            var heap = new short[Constants.NC + 1]; /* NC >= nchar */

            var avail = (short)nchar;
            var heapsize = 0;
            heap[1] = 0;
            for (short i = 0; i < nchar; i++)
            {
                bitlen[i] = 0;
                if (freq[i] != 0)
                {
                    heap[++heapsize] = i;
                }
            }

            if (heapsize < 2)
            {
                code[heap[1]] = 0;
                return heap[1];
            }

            /* make priority queue */
            for (var i = heapsize / 2; i >= 1; i--)
            {
                DownHeap(i, heap, heapsize, freq);
            }

            /* make huffman tree */
            var sort = code;
            var sortPos = 0;
            do
            {
                /* while queue has at least two entries */
                var i = heap[1]; /* take out least-freq entry */
                if (i < nchar)
                {
                    sort[sortPos++] = (ushort)i;
                }

                heap[1] = heap[heapsize--];
                DownHeap(1, heap, heapsize, freq);
                var j = heap[1]; /* next least-freq entry */
                if (j < nchar)
                {
                    sort[sortPos++] = (ushort)j;
                }

                root = avail++; /* generate new node */
                freq[root] = (ushort)(freq[i] + freq[j]);
                heap[1] = root;
                DownHeap(1, heap, heapsize, freq); /* put into queue */
                lha.left[root] = (ushort)i;
                lha.right[root] = (ushort)j;
            } while (heapsize > 1);

            /* make leaf_num */
            var leafNum = new ushort[17];
            CountLeaf(lha, root, nchar, leafNum, 0);

            /* make bitlen */
            MakeLen(bitlen, code, leafNum);

            /* make code table */
            MakeCode(nchar, bitlen, code, leafNum);

            return root;
        }
    }
}
