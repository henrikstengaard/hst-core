/* ------------------------------------------------------------------------ */
/* LHa for UNIX                                                             */
/*              lha.h, lha_macro.h -- constants                             */
/*                                                                          */
/*      Modified                Nobutaka Watazaki                           */
/*                                                                          */
/*  Ver. 1.14   Source All chagned              1995.01.14  N.Watazaki      */
/* ------------------------------------------------------------------------ */
namespace Hst.Compression.Lha
{
    /// <summary>
    /// Constants used by LHa for UNIX encoders and decoders.
    /// </summary>
    internal static class LhConstants
    {
        public const int LZHUFF0_DICBIT = 0;      /* no compress */
        public const int LZHUFF1_DICBIT = 12;      /* 2^12 =  4KB sliding dictionary */
        public const int LZHUFF2_DICBIT = 13;      /* 2^13 =  8KB sliding dictionary */
        public const int LZHUFF3_DICBIT = 13;      /* 2^13 =  8KB sliding dictionary */
        public const int LZHUFF4_DICBIT = 12;      /* 2^12 =  4KB sliding dictionary */
        public const int LZHUFF5_DICBIT = 13;      /* 2^13 =  8KB sliding dictionary */
        public const int LZHUFF6_DICBIT = 15;      /* 2^15 = 32KB sliding dictionary */
        public const int LZHUFF7_DICBIT = 16;      /* 2^16 = 64KB sliding dictionary */
        public const int LARC_DICBIT = 11;      /* 2^11 =  2KB sliding dictionary */
        public const int LARC5_DICBIT = 12;      /* 2^12 =  4KB sliding dictionary */
        public const int LARC4_DICBIT = 0;      /* no compress */
        public const int PMARC0_DICBIT = 0;      /* no compress */
        public const int PMARC2_DICBIT = 13;      /* 2^13 =  8KB sliding dictionary */
        
//#ifdef SUPPORT_LH7
        public const int MAX_DICBIT = LZHUFF7_DICBIT; /* lh7 use 16bits */
// #endif
// #ifndef SUPPORT_LH7
// #define MAX_DICBIT LZHUFF6_DICBIT /* lh6 use 15bits */
// #endif

        public const int MAX_DICSIZ = (1 << MAX_DICBIT);

        public const byte UCHAR_MAX = (1<<8)-1;
        public const int CHAR_BIT = 8;
        
        /* slide.c */
        public const short MAXMATCH = 256; /* formerly F (not more than UCHAR_MAX + 1) */
        public const byte THRESHOLD = 3;   /* choose optimal value */

        public const byte USHRT_BIT = 16; /* (CHAR_BIT * sizeof(ushort)) */
        public const byte NP = (MAX_DICBIT + 1);
        public const byte NT = (USHRT_BIT + 3);
        public const short NC = UCHAR_MAX + MAXMATCH + 2 - THRESHOLD;

        public const byte PBIT = 5;       /* smallest integer such that (1 << PBIT) > * NP */
        public const byte TBIT = 5;       /* smallest integer such that (1 << TBIT) > * NT */
        public const byte CBIT = 9;       /* smallest integer such that (1 << CBIT) > * NC */ 
        
        /*      #if NT > NP #define NPT NT #else #define NPT NP #endif  */
        public const byte NPT = 0x80;


        public const int BUFFERSIZE = 2048;

        public const int LZHUFF0_METHOD_NUM = 0;
        public const int LZHUFF1_METHOD_NUM = 1;
        public const int LZHUFF2_METHOD_NUM = 2;
        public const int LZHUFF3_METHOD_NUM = 3;
        public const int LZHUFF4_METHOD_NUM = 4;
        public const int LZHUFF5_METHOD_NUM = 5;
        public const int LZHUFF6_METHOD_NUM = 6;
        public const int LZHUFF7_METHOD_NUM = 7;
        public const int LARC_METHOD_NUM = 8;
        public const int LARC5_METHOD_NUM = 9;
        public const int LARC4_METHOD_NUM = 10;
        public const int LZHDIRS_METHOD_NUM = 11;
        public const int PMARC0_METHOD_NUM = 12;
        public const int PMARC2_METHOD_NUM = 13;

        public const int EXTRABITS = 8;               /* >= log2(F-THRESHOLD+258-N1) */
        public const int BUFBITS = 16;              /* >= log2(MAXBUF) */
        public const int LENFIELD = 4;         /* bit size of length field for tree output */

        public const int N_CHAR = 256 + 60 - THRESHOLD + 1;
        public const int TREESIZE_C = N_CHAR * 2;
        public const int TREESIZE_P = 128 * 2;
        public const int TREESIZE = TREESIZE_C + TREESIZE_P;
        public const int ROOT_C = 0;
        public const int ROOT_P = TREESIZE_C;
    }
}
