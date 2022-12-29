namespace Hst.Compression.Lha
{
    public class Lh3Decoder : IDecoder
    {
        //{decode_c_st0, decode_p_st0, decode_start_st0},
        public ushort DecodeC(Lha lha) => SHuf.decode_c_st0(lha);

        public ushort DecodeP(Lha lha) => SHuf.decode_p_st0(lha);

        public void DecodeStart(Lha lha) => SHuf.decode_start_st0(lha);
    }
}