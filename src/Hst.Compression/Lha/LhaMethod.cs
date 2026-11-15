namespace Hst.Compression.Lha
{
    /// <summary>
    /// Compression methods used in lha archives.
    /// </summary>
    public static class LhaMethod
    {
        /// <summary>
        /// Stored, no compression.
        /// </summary>
        public const string Lh0 = "-lh0-";

        /// <summary>
        /// 4KB sliding dictionary, dynamic huffman.
        /// </summary>
        public const string Lh1 = "-lh1-";

        /// <summary>
        /// 8KB sliding dictionary, dynamic huffman. Not supported.
        /// </summary>
        public const string Lh2 = "-lh2-";

        /// <summary>
        /// 8KB sliding dictionary, static huffman. Not supported.
        /// </summary>
        public const string Lh3 = "-lh3-";

        /// <summary>
        /// 4KB sliding dictionary, static huffman.
        /// </summary>
        public const string Lh4 = "-lh4-";

        /// <summary>
        /// 8KB sliding dictionary, static huffman.
        /// </summary>
        public const string Lh5 = "-lh5-";

        /// <summary>
        /// 32KB sliding dictionary, static huffman.
        /// </summary>
        public const string Lh6 = "-lh6-";

        /// <summary>
        /// 64KB sliding dictionary, static huffman.
        /// </summary>
        public const string Lh7 = "-lh7-";

        /// <summary>
        /// UNLHA32 512KB sliding dictionary, static huffman.
        /// </summary>
        public const string Lhx = "-lhx-";

        /// <summary>
        /// LHARK's -lh7-, which is incompatible with -lh7-. Detected from level 1 headers with os type LHARK.
        /// </summary>
        public const string Lk7 = "-lk7-";

        /// <summary>
        /// Directory or symbolic link, no data.
        /// </summary>
        public const string Lhd = "-lhd-";

        /// <summary>
        /// LArc 2KB sliding dictionary.
        /// </summary>
        public const string Lzs = "-lzs-";

        /// <summary>
        /// LArc stored, no compression.
        /// </summary>
        public const string Lz4 = "-lz4-";

        /// <summary>
        /// LArc 4KB sliding dictionary.
        /// </summary>
        public const string Lz5 = "-lz5-";

        /// <summary>
        /// PMarc stored, no compression.
        /// </summary>
        public const string Pm0 = "-pm0-";

        /// <summary>
        /// PMarc 16KB history.
        /// </summary>
        public const string Pm1 = "-pm1-";

        /// <summary>
        /// PMarc2 8KB sliding dictionary.
        /// </summary>
        public const string Pm2 = "-pm2-";
    }
}
