/* ---------------------------------------------------------------------- */
/* LHa for UNIX                                                             */
/*              dhuf.c -- Dynamic Hufffman routine                          */
/*                                                                          */
/*      Modified                H.Yoshizaki                                 */
/*                                                                          */
/*  Ver. 1.14   Source All chagned              1995.01.14  N.Watazaki      */
/* ------------------------------------------------------------------------ */
namespace Hst.Compression.Lha
{
    public static class DHuf
    {
    /* ------------------------------------------------------------------------ */
        public static void start_c_dyn(Lha lha)
        {
            int             i, j, f;

            lha.n1 = (int)(lha.n_max >= 256 + lha.maxmatch - Constants.THRESHOLD + 1 ? 512 : lha.n_max - 1);
            for (i = 0; i < Constants.TREESIZE_C; i++)
            {
                lha.stock[i] = (short)i;
                lha.block[i] = 0;
            }
            for (i = 0, j = (int)(lha.n_max * 2 - 2); i < lha.n_max; i++, j--)
            {
                lha.freq[j] = 1;
                lha.child[j] = (short)~i;
                lha.s_node[i] = (short)j;
                lha.block[j] = 1;
            }
            lha.avail = 2;
            lha.edge[1] = (short)(lha.n_max - 1);
            i = (int)lha.n_max * 2 - 2;
            while (j >= 0)
            {
                f = lha.freq[j] = (ushort)(lha.freq[i] + lha.freq[i - 1]);
                lha.child[j] = (short)i;
                lha.parent[i] = lha.parent[i - 1] = (short)j;
                if (f == lha.freq[j + 1])
                {
                    lha.edge[lha.block[j] = lha.block[j + 1]] = (short)j;
                }
                else
                {
                    lha.edge[lha.block[j] = lha.stock[lha.avail++]] = (short)j;
                }
                i -= 2;
                j--;
            }
        }

    /* ------------------------------------------------------------------------ */
        public static void start_p_dyn(Lha lha)
        {
            lha.freq[Constants.ROOT_P] = 1;
            lha.child[Constants.ROOT_P] = ~Constants.N_CHAR;
            lha.s_node[Constants.N_CHAR] = Constants.ROOT_P;
            lha.edge[lha.block[Constants.ROOT_P] = lha.stock[lha.avail++]] = Constants.ROOT_P;
            lha.most_p = Constants.ROOT_P;
            lha.total_p = 0;
            lha.nn = 1 << lha.dicbit;
            lha.nextcount = 64;
        }    
    /* lh2 */
        public static void decode_start_dyn(Lha lha)
        {
            lha.n_max = 286;
            lha.maxmatch = (ushort)Constants.MAXMATCH;
            lha.bitIo.InitGetBits();
            lha.crcIo.init_code_cache();
            start_c_dyn(lha);
            start_p_dyn(lha);
        }
        
    /* ------------------------------------------------------------------------ */
    public static void reconst(Lha lha, int start, int end)
    {
        int             i, j, k, l, b = 0;
        uint    f, g;

        for (i = j = start; i < end; i++)
        {
            if ((k = lha.child[i]) < 0)
            {
                lha.freq[j] = (ushort)((lha.freq[i] + 1) / 2);
                lha.child[j] = (short)k;
                j++;
            }
            if (lha.edge[b = lha.block[i]] == i)
            {
                lha.stock[--lha.avail] = (short)b;
            }
        }
        j--;
        i = end - 1;
        l = end - 2;
        while (i >= start)
        {
            while (i >= l)
            {
                lha.freq[i] = lha.freq[j];
                lha.child[i] = lha.child[j];
                i--;
                j--;
            }
            f = (ushort)(lha.freq[l] + lha.freq[l + 1]);
            for (k = start; f < lha.freq[k]; k++)
            {
            }

            while (j >= k)
            {
                lha.freq[i] = lha.freq[j];
                lha.child[i] = lha.child[j];
                i--;
                j--;
            }
            lha.freq[i] = (ushort)f;
            lha.child[i] = (short)(l + 1);
            i--;
            l -= 2;
        }
        f = 0;
        for (i = start; i < end; i++)
        {
            if ((j = lha.child[i]) < 0)
                lha.s_node[~j] = (short)i;
            else
                lha.parent[j] = lha.parent[j - 1] = (short)i;
            if ((g = lha.freq[i]) == f)
            {
                lha.block[i] = (short)b;
            }
            else
            {
                lha.edge[b = lha.block[i] = lha.stock[lha.avail++]] = (short)i;
                f = g;
            }
        }
    }

    /* ------------------------------------------------------------------------ */
    public static int swap_inc(Lha lha, int p)
    {
        int             b, q, r, s;
        var adjust = false;

        b = lha.block[p];
        if ((q = lha.edge[b]) != p) {   /* swap for leader */
            r = lha.child[p];
            s = lha.child[q];
            lha.child[p] = (short)s;
            lha.child[q] = (short)r;
            if (r >= 0)
                lha.parent[r] = lha.parent[r - 1] = (short)q;
            else
                lha.s_node[~r] = (short)q;
            if (s >= 0)
                lha.parent[s] = lha.parent[s - 1] = (short)p;
            else
                lha.s_node[~s] = (short)p;
            p = q;
            adjust = true;
        }

        if (adjust || b == lha.block[p + 1])
        {
            lha.edge[b]++;
            if (++lha.freq[p] == lha.freq[p - 1])
            {
                lha.block[p] = lha.block[p - 1];
            }
            else
            {
                lha.edge[lha.block[p] = lha.stock[lha.avail++]] = (short)p;    /* create block */
            }
        }
        else if (++lha.freq[p] == lha.freq[p - 1])
        {
            lha.stock[--lha.avail] = (short)b; /* delete block */
            lha.block[p] = lha.block[p - 1];
        }
        return lha.parent[p];
    }    

    /* ------------------------------------------------------------------------ */
        public static void update_c(Lha lha, int p)
        {
            // int             p;
            int             q;

            if (lha.freq[Constants.ROOT_C] == 0x8000)
            {
                reconst(lha, 0, (int)(lha.n_max * 2 - 1));
            }
            lha.freq[Constants.ROOT_C]++;
            q = lha.s_node[p];
            do
            {
                q = swap_inc(lha, q);
            } while (q != Constants.ROOT_C);
        }

    /* ------------------------------------------------------------------------ */
        public static void update_p(Lha lha, int p)
        {
            int             q;

            if (lha.total_p == 0x8000)
            {
                reconst(lha, Constants.ROOT_P, lha.most_p + 1);
                lha.total_p = lha.freq[Constants.ROOT_P];
                lha.freq[Constants.ROOT_P] = 0xffff;
            }
            q = lha.s_node[p + Constants.N_CHAR];
            while (q != Constants.ROOT_P)
            {
                q = swap_inc(lha, q);
            }
            lha.total_p++;
        }
        
    /* ------------------------------------------------------------------------ */
        public static void make_new_node(Lha lha, int p)
        {
            int             q, r;

            r = lha.most_p + 1;
            q = r + 1;
            lha.s_node[~(lha.child[r] = lha.child[lha.most_p])] = (short)r;
            lha.child[q] = (short)~(p + Constants.N_CHAR);
            lha.child[lha.most_p] = (short)q;
            lha.freq[r] = lha.freq[lha.most_p];
            lha.freq[q] = 0;
            lha.block[r] = lha.block[lha.most_p];
            if (lha.most_p == Constants.ROOT_P)
            {
                lha.freq[Constants.ROOT_P] = 0xffff;
                lha.edge[lha.block[Constants.ROOT_P]]++;
            }
            lha.parent[r] = lha.parent[q] = (short)lha.most_p;
            lha.most_p = q;
            lha.edge[lha.block[q] = lha.stock[lha.avail++]] = lha.s_node[p + Constants.N_CHAR] = (short)lha.most_p; 
            update_p(lha, p);
        }    
        
    /* lh1, 2 */
        public static ushort decode_c_dyn(Lha lha)
        {
            int             c;
            short           buf, cnt;

            c = lha.child[Constants.ROOT_C];
            buf = (short)lha.bitIo.bitbuf;
            cnt = 0;
            do {
                c = lha.child[c - (buf < 0 ? 1 : 0)];
                buf <<= 1;
                if (++cnt == 16) {
                    lha.bitIo.FillBuf(16);
                    buf = (short)lha.bitIo.bitbuf;
                    cnt = 0;
                }
            } while (c > 0);
            lha.bitIo.FillBuf(cnt);
            c = ~c;
            update_c(lha, c);
            if (c == lha.n1)
                c += lha.bitIo.GetBits(8);
            return (ushort)c;
        }    
        
        /* lh2 */
        public static ushort decode_p_dyn(Lha lha)
        {
            int             c;
            short           buf, cnt;

            while (lha.decode_count > lha.nextcount)
            {
                make_new_node(lha, (int)(lha.nextcount / 64));
                if ((lha.nextcount += 64) >= lha.nn)
                    lha.nextcount = 0xffffffff;
            }
            c = lha.child[Constants.ROOT_P];
            buf = (short)lha.bitIo.bitbuf;
            cnt = 0;
            while (c > 0) {
                c = lha.child[c - (buf < 0 ? 1 : 0)];
                buf <<= 1;
                if (++cnt == 16)
                {
                    lha.bitIo.FillBuf(16);
                    buf = (short)lha.bitIo.bitbuf;
                    cnt = 0;
                }
            }
            lha.bitIo.FillBuf(cnt);
            c = (~c) - Constants.N_CHAR;
            update_p(lha, c);

            return (ushort)((c << 6) + lha.bitIo.GetBits(6));
        }    
    }
}