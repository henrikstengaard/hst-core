/* ------------------------------------------------------------------------ */
/* LHa for UNIX                                                             */
/*              larc.c -- extra *.lzs                                       */
/*                                                                          */
/*      Modified                Nobutaka Watazaki                           */
/*                                                                          */
/*  Ver. 1.14   Source All chagned              1995.01.14  N.Watazaki      */
/* ------------------------------------------------------------------------ */
namespace Hst.Compression.Lha
{
    /// <summary>
    /// Decoder for -lz5- method (LArc).
    /// </summary>
    public class Lz5Decoder : IDecoder
    {
        // {decode_c_lz5, decode_p_lz5, decode_start_lz5},
        private const int MAGIC5 = 19;

        private int flag;
        private int flagcnt;
        private int matchpos;

        public ushort DecodeC(Lha lha)
        {
            if (flagcnt == 0)
            {
                flagcnt = 8;
                flag = lha.bitIo.GetC();
            }

            flagcnt--;
            var c = lha.bitIo.GetC();
            if ((flag & 1) == 0)
            {
                matchpos = c;
                c = lha.bitIo.GetC();
                matchpos += (c & 0xf0) << 4;
                c &= 0x0f;
                c += 0x100;
            }

            flag >>= 1;
            return (ushort)c;
        }

        public ushort DecodeP(Lha lha)
        {
            return (ushort)((lha.loc - matchpos - MAGIC5) & 0xfff);
        }

        public void DecodeStart(Lha lha)
        {
            var dtext = lha.dtext;

            flag = 0;
            flagcnt = 0;
            matchpos = 0;

            for (var i = 0; i < 256; i++)
            {
                for (var j = 0; j < 13; j++)
                {
                    dtext[i * 13 + 18 + j] = (byte)i;
                }
            }

            for (var i = 0; i < 256; i++)
            {
                dtext[256 * 13 + 18 + i] = (byte)i;
            }

            for (var i = 0; i < 256; i++)
            {
                dtext[256 * 13 + 256 + 18 + i] = (byte)(255 - i);
            }

            for (var i = 0; i < 128; i++)
            {
                dtext[256 * 13 + 512 + 18 + i] = 0;
            }

            for (var i = 0; i < 128 - 18; i++)
            {
                dtext[256 * 13 + 512 + 128 + 18 + i] = (byte)' ';
            }
        }
    }
}
