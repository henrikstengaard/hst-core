namespace Hst.Compression.Lha
{
    /// <summary>
    /// Result of encoding (compressing) data.
    /// </summary>
    public class EncodeResult
    {
        /// <summary>
        /// CRC-16 of the original data.
        /// </summary>
        public ushort Crc { get; set; }

        /// <summary>
        /// Size of compressed data written.
        /// </summary>
        public long PackedSize { get; set; }

        /// <summary>
        /// Size of original data encoded.
        /// </summary>
        public long OriginalSize { get; set; }

        /// <summary>
        /// True, if data couldn't be compressed to a size smaller than original size.
        /// Encoding is stopped and output must be discarded and data stored instead.
        /// </summary>
        public bool Unpackable { get; set; }
    }
}
