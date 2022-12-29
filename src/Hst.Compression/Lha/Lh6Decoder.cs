namespace Hst.Compression.Lha
{
    public class Lh6Decoder : IDecoder
    {
        // {decode_c_st1, decode_p_st1, decode_start_st1},
        public ushort DecodeC(Lha lha) => Huf.decode_c_st1(lha);

        public ushort DecodeP(Lha lha) => Huf.decode_p_st1(lha);

        public void DecodeStart(Lha lha) => Huf.decode_start_st1(lha);
    }
}