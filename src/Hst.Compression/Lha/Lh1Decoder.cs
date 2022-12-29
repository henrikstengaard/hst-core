namespace Hst.Compression.Lha
{
    public class Lh1Decoder : IDecoder
    {
        // {decode_c_dyn, decode_p_st0, decode_start_fix},
        public ushort DecodeC(Lha lha) => DHuf.decode_c_dyn(lha);

        public ushort DecodeP(Lha lha) => SHuf.decode_p_st0(lha);

        public void DecodeStart(Lha lha) => SHuf.decode_start_fix(lha);
    }
}