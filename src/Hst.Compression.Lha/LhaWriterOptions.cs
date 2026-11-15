namespace Hst.Compression.Lha
{
    using System.Text;

    /// <summary>
    /// Options for writing lha archives.
    /// </summary>
    public class LhaWriterOptions
    {
        /// <summary>
        /// Encoding used for entry names. Default is UTF-8.
        /// </summary>
        public Encoding Encoding { get; set; }

        /// <summary>
        /// Header level used for entries, supports 0, 1 and 2. Default is 2.
        /// </summary>
        public int HeaderLevel { get; set; }

        /// <summary>
        /// Compression method used for files, supports "-lh0-", "-lh1-", "-lh4-", "-lh5-", "-lh6-" and "-lh7-".
        /// Default is "-lh5-".
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Write generic headers without unix extended headers (unix permissions, uid and gid).
        /// Default is false.
        /// </summary>
        public bool GenericFormat { get; set; }

        public LhaWriterOptions()
        {
            Encoding = Encoding.UTF8;
            HeaderLevel = 2;
            Method = Constants.LZHUFF5_METHOD;
            GenericFormat = false;
        }

        public static LhaWriterOptions Default => new LhaWriterOptions();
    }
}
