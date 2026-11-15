namespace Hst.Compression.Lha
{
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Options for reading and extracting lha archives.
    /// </summary>
    public class LhaOptions
    {
        /// <summary>
        /// Encoding used for entry names. Default is UTF-8.
        /// </summary>
        public Encoding Encoding { get; set; }

        /// <summary>
        /// Additional decoders by method, e.g. "-lh2-". Decoders added here take precedence over built-in decoders.
        /// </summary>
        public IDictionary<string, LhaDecoderFactory> Decoders { get; }

        public LhaOptions()
        {
            Encoding = Encoding.UTF8;
            Decoders = new Dictionary<string, LhaDecoderFactory>();
        }

        public static LhaOptions Default => new LhaOptions();

        /// <summary>
        /// Options for Amiga lha archives
        /// </summary>
        public static LhaOptions AmigaLhaOptions =>
            new LhaOptions
            {
                Encoding = Encoding.GetEncoding("ISO-8859-1"),
            };
    }
}
