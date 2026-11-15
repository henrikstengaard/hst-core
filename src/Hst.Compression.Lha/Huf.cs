/* ------------------------------------------------------------------------ */
/* LHa for UNIX                                                             */
/*              huf.c -- new static Huffman                                 */
/*                                                                          */
/*      Modified                Nobutaka Watazaki                           */
/*                                                                          */
/*  Ver. 1.14   Source All chagned              1995.01.14  N.Watazaki      */
/*  Ver. 1.14i  Support LH7 & Bug Fixed         2000.10. 6  t.okamoto       */
/* ------------------------------------------------------------------------ */
namespace Hst.Compression.Lha
{
    using System;

    internal class Huf
    {
        /* ------------------------------------------------------------------------ */
        /*                              Encoding                                    */
        /* ------------------------------------------------------------------------ */
        private const int BUFSIZ = 16 * 1024 * 2; /* 65408U; */ /* t.okamoto */

        private static void CountTFreq(Lha lha)
        {
            int i, k, n, count;

            for (i = 0; i < LhConstants.NT; i++)
                lha.t_freq[i] = 0;
            n = LhConstants.NC;
            while (n > 0 && lha.c_len[n - 1] == 0)
                n--;
            i = 0;
            while (i < n)
            {
                k = lha.c_len[i++];
                if (k == 0)
                {
                    count = 1;
                    while (i < n && lha.c_len[i] == 0)
                    {
                        i++;
                        count++;
                    }

                    if (count <= 2)
                        lha.t_freq[0] += (ushort)count;
                    else if (count <= 18)
                        lha.t_freq[1]++;
                    else if (count == 19)
                    {
                        lha.t_freq[0]++;
                        lha.t_freq[1]++;
                    }
                    else
                        lha.t_freq[2]++;
                }
                else
                    lha.t_freq[k + 2]++;
            }
        }

        private static void WritePtLen(Lha lha, int n, byte nbit, int iSpecial)
        {
            int i, k;

            while (n > 0 && lha.pt_len[n - 1] == 0)
                n--;
            lha.bitIo.PutBits(nbit, (ushort)n);
            i = 0;
            while (i < n)
            {
                k = lha.pt_len[i++];
                if (k <= 6)
                    lha.bitIo.PutBits(3, (ushort)k);
                else
                    /* k=7 -> 1110  k=8 -> 11110  k=9 -> 111110 ... */
                    lha.bitIo.PutBits((byte)(k - 3), 0xfffe); /* (short)(USHRT_MAX << 1) */
                if (i == iSpecial)
                {
                    while (i < 6 && lha.pt_len[i] == 0)
                        i++;
                    lha.bitIo.PutBits(2, (ushort)(i - 3));
                }
            }
        }

        private static void WriteCLen(Lha lha)
        {
            int i, k, n, count;

            n = LhConstants.NC;
            while (n > 0 && lha.c_len[n - 1] == 0)
                n--;
            lha.bitIo.PutBits(LhConstants.CBIT, (ushort)n);
            i = 0;
            while (i < n)
            {
                k = lha.c_len[i++];
                if (k == 0)
                {
                    count = 1;
                    while (i < n && lha.c_len[i] == 0)
                    {
                        i++;
                        count++;
                    }

                    if (count <= 2)
                    {
                        for (k = 0; k < count; k++)
                            lha.bitIo.PutCode(lha.pt_len[0], lha.pt_code[0]);
                    }
                    else if (count <= 18)
                    {
                        lha.bitIo.PutCode(lha.pt_len[1], lha.pt_code[1]);
                        lha.bitIo.PutBits(4, (ushort)(count - 3));
                    }
                    else if (count == 19)
                    {
                        lha.bitIo.PutCode(lha.pt_len[0], lha.pt_code[0]);
                        lha.bitIo.PutCode(lha.pt_len[1], lha.pt_code[1]);
                        lha.bitIo.PutBits(4, 15);
                    }
                    else
                    {
                        lha.bitIo.PutCode(lha.pt_len[2], lha.pt_code[2]);
                        lha.bitIo.PutBits(LhConstants.CBIT, (ushort)(count - 20));
                    }
                }
                else
                    lha.bitIo.PutCode(lha.pt_len[k + 2], lha.pt_code[k + 2]);
            }
        }

        private static void EncodeC(Lha lha, int c)
        {
            lha.bitIo.PutCode(lha.c_len[c], lha.c_code[c]);
        }

        private static void EncodeP(Lha lha, ushort p)
        {
            ushort c = 0, q = p;
            while (q != 0)
            {
                q >>= 1;
                c++;
            }

            lha.bitIo.PutCode(lha.pt_len[c], lha.pt_code[c]);
            if (c > 1)
                lha.bitIo.PutBits((byte)(c - 1), p);
        }

        private static void SendBlock(Lha lha)
        {
            byte flags = 0;
            int i, k, root, pos, size;

            root = MakeTree.Make(lha, LhConstants.NC, lha.c_freq, lha.c_len, lha.c_code);
            size = lha.c_freq[root];
            lha.bitIo.PutBits(16, (ushort)size);
            if (root >= LhConstants.NC)
            {
                CountTFreq(lha);
                root = MakeTree.Make(lha, LhConstants.NT, lha.t_freq, lha.pt_len, lha.pt_code);
                if (root >= LhConstants.NT)
                {
                    WritePtLen(lha, LhConstants.NT, LhConstants.TBIT, 3);
                }
                else
                {
                    lha.bitIo.PutBits(LhConstants.TBIT, 0);
                    lha.bitIo.PutBits(LhConstants.TBIT, (ushort)root);
                }

                WriteCLen(lha);
            }
            else
            {
                lha.bitIo.PutBits(LhConstants.TBIT, 0);
                lha.bitIo.PutBits(LhConstants.TBIT, 0);
                lha.bitIo.PutBits(LhConstants.CBIT, 0);
                lha.bitIo.PutBits(LhConstants.CBIT, (ushort)root);
            }

            root = MakeTree.Make(lha, lha.np, lha.p_freq, lha.pt_len, lha.pt_code);
            if (root >= lha.np)
            {
                WritePtLen(lha, lha.np, (byte)lha.pbit, -1);
            }
            else
            {
                lha.bitIo.PutBits((byte)lha.pbit, 0);
                lha.bitIo.PutBits((byte)lha.pbit, (ushort)root);
            }

            pos = 0;
            for (i = 0; i < size; i++)
            {
                if (i % LhConstants.CHAR_BIT == 0)
                    flags = lha.buf[pos++];
                else
                    flags <<= 1;
                if ((flags & (1 << (LhConstants.CHAR_BIT - 1))) != 0)
                {
                    EncodeC(lha, lha.buf[pos++] + (1 << LhConstants.CHAR_BIT));
                    k = lha.buf[pos++] << LhConstants.CHAR_BIT;
                    k += lha.buf[pos++];
                    EncodeP(lha, (ushort)k);
                }
                else
                    EncodeC(lha, lha.buf[pos++]);

                if (lha.bitIo.unpackable)
                    return;
            }

            for (i = 0; i < LhConstants.NC; i++)
                lha.c_freq[i] = 0;
            for (i = 0; i < lha.np; i++)
                lha.p_freq[i] = 0;
        }

        /// <summary>
        /// lh4, 5, 6, 7
        /// </summary>
        public static void output_st1(Lha lha, uint c, uint p)
        {
            lha.output_mask >>= 1;
            if (lha.output_mask == 0)
            {
                lha.output_mask = 1 << (LhConstants.CHAR_BIT - 1);
                if (lha.output_pos >= lha.bufsiz - 3 * LhConstants.CHAR_BIT)
                {
                    SendBlock(lha);
                    if (lha.bitIo.unpackable)
                        return;
                    lha.output_pos = 0;
                }

                lha.cpos = lha.output_pos++;
                lha.buf[lha.cpos] = 0;
            }

            lha.buf[lha.output_pos++] = (byte)c;
            lha.c_freq[c]++;
            if (c >= (1 << LhConstants.CHAR_BIT))
            {
                lha.buf[lha.cpos] |= (byte)lha.output_mask;
                lha.buf[lha.output_pos++] = (byte)(p >> LhConstants.CHAR_BIT);
                lha.buf[lha.output_pos++] = (byte)p;
                c = 0;
                while (p != 0)
                {
                    p >>= 1;
                    c++;
                }

                lha.p_freq[c]++;
            }
        }

        /// <summary>
        /// lh4, 5, 6, 7
        /// </summary>
        public static void encode_start_st1(Lha lha)
        {
            switch (lha.dicbit)
            {
                case LhConstants.LZHUFF4_DICBIT:
                case LhConstants.LZHUFF5_DICBIT: lha.pbit = 4; lha.np = LhConstants.LZHUFF5_DICBIT + 1; break;
                case LhConstants.LZHUFF6_DICBIT: lha.pbit = 5; lha.np = LhConstants.LZHUFF6_DICBIT + 1; break;
                case LhConstants.LZHUFF7_DICBIT: lha.pbit = 5; lha.np = LhConstants.LZHUFF7_DICBIT + 1; break;
                default:
                    throw new ArgumentException($"Cannot use {(1 << lha.dicbit)} bytes dictionary");
            }

            if (lha.buf == null)
            {
                lha.bufsiz = BUFSIZ;
                lha.buf = new byte[lha.bufsiz];
            }

            for (var i = 0; i < LhConstants.NC; i++)
                lha.c_freq[i] = 0;
            for (var i = 0; i < lha.np; i++)
                lha.p_freq[i] = 0;
            lha.output_pos = lha.output_mask = 0;
            lha.bitIo.InitPutBits();
            lha.crcIo.init_code_cache();
            lha.buf[0] = 0;
        }

        /// <summary>
        /// lh4, 5, 6, 7
        /// </summary>
        public static void encode_end_st1(Lha lha)
        {
            if (!lha.bitIo.unpackable)
            {
                SendBlock(lha);
                lha.bitIo.PutBits(LhConstants.CHAR_BIT - 1, 0); /* flush remaining bits */
            }
        }

    }
}
