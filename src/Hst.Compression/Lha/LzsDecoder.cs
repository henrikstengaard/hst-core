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
    /// Decoder for -lzs- method (LArc).
    /// </summary>
    public class LzsDecoder : IDecoder
    {
        // {decode_c_lzs, decode_p_lzs, decode_start_lzs},
        private const int MAGIC0 = 18;

        private int matchpos;

        public ushort DecodeC(Lha lha)
        {
            if (lha.bitIo.GetBits(1) != 0)
            {
                return lha.bitIo.GetBits(8);
            }

            matchpos = lha.bitIo.GetBits(11);
            return (ushort)(lha.bitIo.GetBits(4) + 0x100);
        }

        public ushort DecodeP(Lha lha)
        {
            return (ushort)((lha.loc - matchpos - MAGIC0) & 0x7ff);
        }

        public void DecodeStart(Lha lha)
        {
            lha.bitIo.InitGetBits();
            lha.crcIo.init_code_cache();
            matchpos = 0;
        }
    }
}
