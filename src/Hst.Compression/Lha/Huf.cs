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

    public class Huf
    {
        // encode parts are skipped

        public static void ReadPtLen(Lha lha, short nn, short nbit, short i_special)
        {
            var c = 0;
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