namespace Hst.Compression.Lha
{
    public interface IDecoder
    {
        // https://github.com/jca02266/lha/blob/master/src/slide.c
        /* lh4 */
        //{decode_c_st1, decode_p_st1, decode_start_st1},
        
        ushort DecodeC(Lha huf);
        ushort DecodeP(Lha huf);
        void DecodeStart(Lha huf);
    }
}