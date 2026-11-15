namespace Hst.Compression.Lha
{
    /// <summary>
    /// Extensions for lha options to add decoders based on LHa for UNIX.
    /// </summary>
    public static class LhaOptionsExtensions
    {
        /// <summary>
        /// Add decoders for -lh2- and -lh3- methods based on LHa for UNIX to options.
        /// </summary>
        /// <param name="options">Options to add decoders to.</param>
        /// <returns>Options with decoders added.</returns>
        public static LhaOptions AddLhaForUnixDecoders(this LhaOptions options)
        {
            options.Decoders[Constants.LZHUFF2_METHOD] = SlideDecoder.CreateLh2;
            options.Decoders[Constants.LZHUFF3_METHOD] = SlideDecoder.CreateLh3;
            return options;
        }
    }
}
