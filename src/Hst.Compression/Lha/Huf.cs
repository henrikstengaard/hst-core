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

            for (i = 0; i < Constants.NT; i++)
                lha.t_freq[i] = 0;
            n = Constants.NC;
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

            n = Constants.NC;
            while (n > 0 && lha.c_len[n - 1] == 0)
                n--;
            lha.bitIo.PutBits(Constants.CBIT, (ushort)n);
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
                        lha.bitIo.PutBits(Constants.CBIT, (ushort)(count - 20));
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

            root = MakeTree.Make(lha, Constants.NC, lha.c_freq, lha.c_len, lha.c_code);
            size = lha.c_freq[root];
            lha.bitIo.PutBits(16, (ushort)size);
            if (root >= Constants.NC)
            {
                CountTFreq(lha);
                root = MakeTree.Make(lha, Constants.NT, lha.t_freq, lha.pt_len, lha.pt_code);
                if (root >= Constants.NT)
                {
                    WritePtLen(lha, Constants.NT, Constants.TBIT, 3);
                }
                else
                {
                    lha.bitIo.PutBits(Constants.TBIT, 0);
                    lha.bitIo.PutBits(Constants.TBIT, (ushort)root);
                }

                WriteCLen(lha);
            }
            else
            {
                lha.bitIo.PutBits(Constants.TBIT, 0);
                lha.bitIo.PutBits(Constants.TBIT, 0);
                lha.bitIo.PutBits(Constants.CBIT, 0);
                lha.bitIo.PutBits(Constants.CBIT, (ushort)root);
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
                if (i % Constants.CHAR_BIT == 0)
                    flags = lha.buf[pos++];
                else
                    flags <<= 1;
                if ((flags & (1 << (Constants.CHAR_BIT - 1))) != 0)
                {
                    EncodeC(lha, lha.buf[pos++] + (1 << Constants.CHAR_BIT));
                    k = lha.buf[pos++] << Constants.CHAR_BIT;
                    k += lha.buf[pos++];
                    EncodeP(lha, (ushort)k);
                }
                else
                    EncodeC(lha, lha.buf[pos++]);

                if (lha.bitIo.unpackable)
                    return;
            }

            for (i = 0; i < Constants.NC; i++)
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
                lha.output_mask = 1 << (Constants.CHAR_BIT - 1);
                if (lha.output_pos >= lha.bufsiz - 3 * Constants.CHAR_BIT)
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
            if (c >= (1 << Constants.CHAR_BIT))
            {
                lha.buf[lha.cpos] |= (byte)lha.output_mask;
                lha.buf[lha.output_pos++] = (byte)(p >> Constants.CHAR_BIT);
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
                case Constants.LZHUFF4_DICBIT:
                case Constants.LZHUFF5_DICBIT: lha.pbit = 4; lha.np = Constants.LZHUFF5_DICBIT + 1; break;
                case Constants.LZHUFF6_DICBIT: lha.pbit = 5; lha.np = Constants.LZHUFF6_DICBIT + 1; break;
                case Constants.LZHUFF7_DICBIT: lha.pbit = 5; lha.np = Constants.LZHUFF7_DICBIT + 1; break;
                default:
                    throw new ArgumentException($"Cannot use {(1 << lha.dicbit)} bytes dictionary");
            }

            if (lha.buf == null)
            {
                lha.bufsiz = BUFSIZ;
                lha.buf = new byte[lha.bufsiz];
            }

            for (var i = 0; i < Constants.NC; i++)
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
                lha.bitIo.PutBits(Constants.CHAR_BIT - 1, 0); /* flush remaining bits */
            }
        }

        /* ------------------------------------------------------------------------ */
        /*                              Decoding                                    */
        /* ------------------------------------------------------------------------ */
        public static void ReadPtLen(Lha lha, short nn, short nbit, short i_special)
        {
            int c;
            var n = lha.bitIo.GetBits(nbit);
            if (n == 0)
            {
                c = lha.bitIo.GetBits(nbit);
                for (var i = 0; i < nn; i++)
                {
                    lha.pt_len[i] = 0;
                }

                for (var i = 0; i < 256; i++)
                {
                    lha.pt_table[i] = (ushort)c;
                }
            }
            else
            {
                var i = 0;
                while (i < Math.Min(n, Constants.NPT))
                {
                    c = lha.bitIo.PeekBits(3);
                    if (c != 7)
                        lha.bitIo.FillBuf(3);
                    else
                    {
                        ushort mask = 1 << (16 - 4);
                        while ((mask & lha.bitIo.bitbuf) != 0)
                        {
                            mask >>= 1;
                            c++;
                        }

                        lha.bitIo.FillBuf(c - 3);
                    }

                    lha.pt_len[i++] = (byte)c;
                    if (i == i_special)
                    {
                        c = lha.bitIo.GetBits(2);
                        while (--c >= 0 && i < Constants.NPT)
                        {
                            lha.pt_len[i++] = 0;
                        }
                    }
                }

                while (i < nn)
                {
                    lha.pt_len[i++] = 0;
                }
                MakeTbl.MakeTable(nn, lha.pt_len, 8, lha.pt_table, lha.left, lha.right);
            }
        }
        
        public static void ReadCLen(Lha lha)
        {
            short i, c, n;

            n = (short)lha.bitIo.GetBits(Constants.CBIT);
            if (n == 0) {
                c = (short)lha.bitIo.GetBits(Constants.CBIT);
                for (i = 0; i < Constants.NC; i++)
                    lha.c_len[i] = 0;
                for (i = 0; i < 4096; i++)
                    lha.c_table[i] = (ushort)c;
            } else {
                i = 0;
                while (i < Math.Min(n, Constants.NC)) {
                    c = (short)lha.pt_table[lha.bitIo.PeekBits(8)];
                    if (c >= Constants.NT) {
                        ushort mask = 1 << (16 - 9);
                        do {
                            if ((lha.bitIo.bitbuf & mask) != 0)
                                c = (short)lha.right[c];
                            else
                                c = (short)lha.left[c];
                            mask >>= 1;
                        } while (c >= Constants.NT && (mask != 0 || c != lha.left[c])); /* CVE-2006-4338 */
                    }
                    lha.bitIo.FillBuf(lha.pt_len[c]);
                    if (c <= 2) {
                        if (c == 0)
                            c = 1;
                        else if (c == 1)
                            c = (short)(lha.bitIo.GetBits(4) + 3);
                        else
                            c = (short)(lha.bitIo.GetBits(Constants.CBIT) + 20);
                        while (--c >= 0)
                            lha.c_len[i++] = 0;
                    }
                    else
                    {
                        lha.c_len[i++] = (byte)(c - 2);
                    }
                }

                while (i < Constants.NC)
                {
                    lha.c_len[i++] = 0;
                }
                MakeTbl.MakeTable(Constants.NC, lha.c_len, 12, lha.c_table, lha.left, lha.right);
            }
        }
        
        /// <summary>
        /// lh4, 5, 6, 7
        /// </summary>
        /// <returns></returns>
        public static ushort decode_c_st1(Lha lha)
        {
            ushort j = 0;
            ushort mask = 0;

            if (lha.blocksize == 0)
            {
                lha.blocksize = (ushort)lha.bitIo.GetBits(16);
                ReadPtLen(lha, Constants.NT, Constants.TBIT, 3);
                ReadCLen(lha);
                ReadPtLen(lha, (short)lha.np, (short)lha.pbit, -1);
            }

            lha.blocksize--;
            j = lha.c_table[lha.bitIo.PeekBits(12)];
            if (j < Constants.NC)
                lha.bitIo.FillBuf(lha.c_len[j]);
            else
            {
                lha.bitIo.FillBuf(12);
                mask = 1 << (16 - 1);
                do
                {
                    if ((lha.bitIo.bitbuf & mask) != 0)
                        j = lha.right[j];
                    else
                        j = lha.left[j];
                    mask >>= 1;
                } while (j >= Constants.NC && (mask != 0 || j != lha.left[j])); /* CVE-2006-4338 */

                lha.bitIo.FillBuf(lha.c_len[j] - 12);
            }

            return j;
        }
        
        /* ------------------------------------------------------------------------ */
        /*  */
        /// <summary>
        /// lh4, 5, 6, 7
        /// </summary>
        /// <returns></returns>
        public static ushort decode_p_st1(Lha lha)
        {
            ushort j, mask;

            j = lha.pt_table[lha.bitIo.PeekBits(8)];
            if (j < lha.np)
                lha.bitIo.FillBuf(lha.pt_len[j]);
            else {
                lha.bitIo.FillBuf(8);
                mask = 1 << (16 - 1);
                do {
                    if ((lha.bitIo.bitbuf & mask) != 0)
                        j = lha.right[j];
                    else
                        j = lha.left[j];
                    mask >>= 1;
                } while (j >= lha.np && (mask != 0 || j != lha.left[j])); /* CVE-2006-4338 */
                lha.bitIo.FillBuf(lha.pt_len[j] - 8);
            }
            if (j != 0)
                j = (ushort)((1 << (j - 1)) + lha.bitIo.GetBits(j - 1));
            return j;
        }

        
        /* ------------------------------------------------------------------------ */
        /* lh4, 5, 6, 7 */
        public static void decode_start_st1(Lha lha)
        {
            switch (lha.dicbit)
            {
                case Constants.LZHUFF4_DICBIT:
                case Constants.LZHUFF5_DICBIT: lha.pbit = 4; lha.np = Constants.LZHUFF5_DICBIT + 1; break;
                case Constants.LZHUFF6_DICBIT: lha.pbit = 5; lha.np = Constants.LZHUFF6_DICBIT + 1; break;
                case Constants.LZHUFF7_DICBIT: lha.pbit = 5; lha.np = Constants.LZHUFF7_DICBIT + 1; break;
                default:
                    throw new Exception($"Cannot use {(1 << lha.dicbit)} bytes dictionary");
            }

            lha.bitIo.InitGetBits();
            lha.crcIo.init_code_cache();
            lha.blocksize = 0;
        }
    }
}