namespace Hst.Compression.Lha
{
    /// <summary>
    /// Encoder used by sliding dictionary to output letters and matches.
    /// </summary>
    internal interface IEncoder
    {
        // https://github.com/jca02266/lha/blob/master/src/slide.c
        /* lh1 */
        //{output_dyn, encode_start_fix, encode_end_dyn},
        /* lh4, 5, 6, 7 */
        //{output_st1, encode_start_st1, encode_end_st1}

        void Output(Lha lha, uint c, uint p);
        void EncodeStart(Lha lha);
        void EncodeEnd(Lha lha);
    }

    /// <summary>
    /// Encoder for lh1 method using dynamic huffman.
    /// </summary>
    internal class Lh1Encoder : IEncoder
    {
        public void Output(Lha lha, uint c, uint p) => DHuf.output_dyn(lha, c, p);

        public void EncodeStart(Lha lha) => SHuf.encode_start_fix(lha);

        public void EncodeEnd(Lha lha) => DHuf.encode_end_dyn(lha);
    }

    /// <summary>
    /// Encoder for lh4, lh5, lh6 and lh7 methods using static huffman.
    /// </summary>
    internal class Lh5Encoder : IEncoder
    {
        public void Output(Lha lha, uint c, uint p) => Huf.output_st1(lha, c, p);

        public void EncodeStart(Lha lha) => Huf.encode_start_st1(lha);

        public void EncodeEnd(Lha lha) => Huf.encode_end_st1(lha);
    }
}
