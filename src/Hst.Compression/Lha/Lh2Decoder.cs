namespace Hst.Compression.Lha
{
    public class Lh2Decoder : IDecoder
    {
        //{decode_c_dyn, decode_p_dyn, decode_start_dyn},
        public ushort DecodeC(Lha lha)
        {
            return DHuf.decode_c_dyn(lha);
        }

        public ushort DecodeP(Lha lha)
        {
            return DHuf.decode_p_dyn(lha);
        }

        public void DecodeStart(Lha lha)
        {
            DHuf.decode_start_dyn(lha);
        }
    }
}