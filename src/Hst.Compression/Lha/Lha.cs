/* ------------------------------------------------------------------------ */
/* LHa for UNIX    Archiver Driver                                          */
/*                                                                          */
/*      Modified                Nobutaka Watazaki                           */
/*                                                                          */
/*  Ver. 1.14   Soruce All chagned              1995.01.14  N.Watazaki      */
/*  Ver. 1.14i  Modified and bug fixed          2000.10.06  t.okamoto       */
/* ------------------------------------------------------------------------ */
/*
    Included...
        lharc.h     interface.h     slidehuf.h
*/
namespace Hst.Compression.Lha
{
    public class Lha
    {
        public readonly int dicbit;
        public const int N1 = 286;             /* alphabet size */
        public const int N2 = 2 * N1 - 1;    /* # of nodes in Huffman tree */        
        public const int NP = 8 * 1024 / 64;
        public const int NP2 = NP * 2 - 1;
            
        public readonly int[][] FixedTable = {
            new [] {3, 0x01, 0x04, 0x0c, 0x18, 0x30, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},   /* old compatible */
            new [] {2, 0x01, 0x01, 0x03, 0x06, 0x0D, 0x1F, 0x4E, 0, 0, 0, 0, 0, 0, 0, 0}    /* 8K buf */
        };        
        
        public readonly BitIo bitIo;
        public readonly CrcIo crcIo;
            
        // unsigned short left[2 * NC - 1], right[2 * NC - 1];
        public readonly ushort[] left;
        public readonly ushort[] right;

        // unsigned short c_code[NC];      /* encode */
        // unsigned short pt_code[NPT];    /* encode */
        public readonly ushort[] c_code; /* encode */
        public readonly ushort[] pt_code; /* encode */

        // unsigned short c_table[4096];   /* decode */
        // unsigned short pt_table[256];   /* decode */
        public readonly ushort[] c_table; /* decode */
        public readonly ushort[] pt_table; /* decode */
            
        // unsigned short c_freq[2 * NC - 1]; /* encode */
        // unsigned short p_freq[2 * NP - 1]; /* encode */
        // unsigned short t_freq[2 * NT - 1]; /* encode */
        public readonly ushort[] c_freq; /* encode */
        public readonly ushort[] p_freq; /* encode */
        public readonly ushort[] t_freq; /* encode */

        // unsigned char  c_len[NC];
        // unsigned char  pt_len[NPT];
        public readonly byte[] c_len;
        public readonly byte[] pt_len;

        // static unsigned char *buf;      /* encode */
        // static unsigned int bufsiz;     /* encode */
        // static unsigned short blocksize; /* decode */
        // static unsigned short output_pos, output_mask; /* encode */
        public byte buf;      /* encode */
        public uint bufsiz;     /* encode */
        public ushort blocksize; /* decode */
        //static ushort output_pos, output_mask; /* encode */

        // static int pbit;
        // static int np;
        public int pbit;
        public int np;
        
        public int decode_count;
        
        public uint n_max;
        public ushort maxmatch;
        
        public readonly short[] child;
        public readonly short[] parent;
        public readonly short[] block;
        public readonly short[] edge;
        public readonly short[] stock;
        public readonly short[] s_node;   /* Changed N.Watazaki */ /*  node[..] -> s_node[..] */

        public readonly ushort[] freq;    
        
        public ushort total_p;
        public int avail, n1;
        public int      most_p, nn;
        public uint nextcount;
        
        public Lha(int dicbit, BitIo bitIo, CrcIo crcIo)
        {
            this.dicbit = dicbit;
            this.bitIo = bitIo;
            this.crcIo = crcIo;
                
            left = new ushort[2 * Constants.NC - 1];
            right = new ushort[2 * Constants.NC - 1];

            c_code = new ushort[Constants.NC];
            pt_code = new ushort[Constants.NPT];

            c_table = new ushort[4096];
            pt_table = new ushort[256];

            c_freq = new ushort[2 * Constants.NC - 1];
            p_freq = new ushort[2 * Constants.NP - 1];
            t_freq = new ushort[2 * Constants.NT - 1];
                
            c_len = new byte[Constants.NC];
            pt_len = new byte[Constants.NPT];

            child = new short[Constants.TREESIZE];
            parent = new short[Constants.TREESIZE];
            block = new short[Constants.TREESIZE];
            edge = new short[Constants.TREESIZE];
            stock = new short[Constants.TREESIZE];
            s_node = new short[Constants.TREESIZE / 2];
            
            freq = new ushort[Constants.TREESIZE];
        }
        
    }
}