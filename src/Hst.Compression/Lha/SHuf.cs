/* ------------------------------------------------------------------------ */
/* LHa for UNIX                                                             */
/*              shuf.c -- extract static Huffman coding                     */
/*                                                                          */
/*      Modified                Nobutaka Watazaki                           */
/*                                                                          */
/*  Ver. 1.14   Source All chagned              1995.01.14  N.Watazaki      */
/* ------------------------------------------------------------------------ */
namespace Hst.Compression.Lha
{
    /// <summary>
    /// extract static Huffman coding
    /// </summary>
    public static class SHuf
    {
    /* lh3 */
        public static void decode_start_st0(Lha lha)
        {
            lha.n_max = 286;
            lha.maxmatch = (ushort)Constants.MAXMATCH;
            lha.bitIo.InitGetBits();
            lha.crcIo.init_code_cache();
            lha.np = 1 << (Constants.LZHUFF3_DICBIT - 6);
        }
        
        /// <summary>
        /// lh1
        /// </summary>
        /// <param name="lha"></param>
        public static void decode_start_fix(Lha lha)
        {
            lha.n_max = 314;
            lha.maxmatch = 60;
            lha.bitIo.InitGetBits();
            lha.crcIo.init_code_cache();
            lha.np = 1 << (Constants.LZHUFF1_DICBIT - 6);
            start_c_dyn(lha);
            ready_made(lha, 0);
            MakeTbl.MakeTable((short)lha.np, lha.pt_len, 8, lha.pt_table, lha.left, lha.right);
        }
     
    /* lh3 */
        public static ushort decode_c_st0(Lha lha)
        {
            int             i, j;
            //ushort blocksize = 0;

            if (lha.blocksize == 0)
            {
                /* read block head */
                lha.blocksize = (ushort)lha.bitIo.GetBits(Constants.BUFBITS);   /* read block blocksize */
                read_tree_c(lha);
                if (lha.bitIo.GetBits(1) != 0)
                {
                    read_tree_p(lha);
                }
                else
                {
                    ready_made(lha, 1);
                }
                MakeTbl.MakeTable(Lha.NP, lha.pt_len, 8, lha.pt_table, lha.left, lha.right);
            }
            lha.blocksize--;
            j = lha.c_table[lha.bitIo.PeekBits(12)];
            if (j < Lha.N1)
                lha.bitIo.FillBuf(lha.c_len[j]);
            else {
                lha.bitIo.FillBuf(12);
                i = lha.bitIo.bitbuf;
                do {
                    if ((short) i < 0)
                        j = lha.right[j];
                    else
                        j = lha.left[j];
                    i <<= 1;
                } while (j >= Lha.N1);
                lha.bitIo.FillBuf(lha.c_len[j] - 12);
            }

            if (j == Lha.N1 - 1)
            {
                j += lha.bitIo.GetBits(Constants.EXTRABITS);
            }
            return (ushort)j;
        }

    /* lh1, 3 */
        public static ushort decode_p_st0(Lha lha)
        {
            int             i, j;

            j = lha.pt_table[lha.bitIo.PeekBits(8)];
            if (j < lha.np)
            {
                lha.bitIo.FillBuf(lha.pt_len[j]);
            }
            else {
                lha.bitIo.FillBuf(8);
                i = lha.bitIo.bitbuf;
                do {
                    if ((short) i < 0)
                        j = lha.right[j];
                    else
                        j = lha.left[j];
                    i <<= 1;
                } while (j >= lha.np);
                lha.bitIo.FillBuf(lha.pt_len[j] - 8);
            }
            return (ushort)((j << 6) + lha.bitIo.GetBits(6));
        }    
        
        public static void start_c_dyn(Lha lha)
        {
            short i, j, f;

            lha.n1 = (int)(lha.n_max >= 256 + lha.maxmatch - Constants.THRESHOLD + 1 ? 512 : lha.n_max - 1);
            for (i = 0; i < Constants.TREESIZE_C; i++) {
                lha.stock[i] = i;
                lha.block[i] = 0;
            }
            for (i = 0, j = (short)(lha.n_max * 2 - 2); i < lha.n_max; i++, j--)
            {
                lha.freq[j] = 1;
                lha.child[j] = (short)~i;
                lha.s_node[i] = j;
                lha.block[j] = 1;
            }
            lha.avail = 2;
            lha.edge[1] = (short)(lha.n_max - 1);
            i = (short)(lha.n_max * 2 - 2);
            while (j >= 0)
            {
                lha.freq[j] = (ushort)(lha.freq[i] + lha.freq[i - 1]);
                f = (short)lha.freq[j];
                lha.child[j] = i;
                lha.parent[i] = lha.parent[i - 1] = j;
                if (f == lha.freq[j + 1])
                {
                    lha.edge[lha.block[j] = lha.block[j + 1]] = j;
                }
                else
                {
                    lha.edge[lha.block[j] = lha.stock[lha.avail++]] = j;
                }
                i -= 2;
                j--;
            }
        }
        
        public static void ready_made(Lha lha, int method)
        {
            // int             i, j;
            // unsigned int    code, weight;
            // int            *tbl;
            int i, j;
            uint code, weight;

            // tbl = fixed[method];
            var tbl = 0;
            j = lha.FixedTable[method][tbl++];
            weight = (uint)(1 << (16 - j));
            code = 0;
            for (i = 0; i < lha.np; i++) {
                while (lha.FixedTable[method][tbl] == i) {
                    j++;
                    tbl++;
                    weight >>= 1;
                }
                lha.pt_len[i] = (byte)j;
                lha.pt_code[i] = (ushort)code;
                code += weight;
            }
        }
        
        public static void read_tree_c(Lha lha)
        {
            /* read tree from file */
            int i, c;

            i = 0;
            while (i < Lha.N1) {
                if (lha.bitIo.GetBits(1) != 0)
                    lha.c_len[i] = (byte)(lha.bitIo.GetBits(Constants.LENFIELD) + 1);
                else
                    lha.c_len[i] = 0;
                if (++i == 3 && lha.c_len[0] == 1 && lha.c_len[1] == 1 && lha.c_len[2] == 1)
                {
                    c = lha.bitIo.GetBits(Constants.CBIT);
                    for (i = 0; i < Lha.N1; i++)
                        lha.c_len[i] = 0;
                    for (i = 0; i < 4096; i++)
                        lha.c_table[i] = (byte)c;
                    return;
                }
            }
            MakeTbl.MakeTable(Lha.N1, lha.c_len, 12, lha.c_table, lha.left, lha.right);
        }
            
        /* ------------------------------------------------------------------------ */
        public static void read_tree_p(Lha lha)
        {
            /* read tree from file */
            int i, c;

            i = 0;
            while (i < Lha.NP)
            {
                lha.pt_len[i] = (byte)lha.bitIo.GetBits(Constants.LENFIELD);
                if (++i == 3 && lha.pt_len[0] == 1 && lha.pt_len[1] == 1 && lha.pt_len[2] == 1) {
                    c = lha.bitIo.GetBits(Constants.LZHUFF3_DICBIT - 6);
                    for (i = 0; i < Lha.NP; i++)
                        lha.pt_len[i] = 0;
                    for (i = 0; i < 256; i++)
                        lha.pt_table[i] = (ushort)c;
                    return;
                }
            }
        }    
    }
}