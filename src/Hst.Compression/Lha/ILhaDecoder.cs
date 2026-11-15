namespace Hst.Compression.Lha
{
    using System.IO;

    /// <summary>
    /// Decoder decompressing data of a lha entry. Used to add support for additional methods via
    /// <see cref="LhaOptions.Decoders"/>.
    /// </summary>
    public interface ILhaDecoder
    {
        /// <summary>
        /// Decode data into buffer.
        /// </summary>
        /// <param name="buffer">Buffer to write decoded data to.</param>
        /// <param name="offset">Offset in buffer to write decoded data to.</param>
        /// <param name="count">Maximum number of bytes to decode.</param>
        /// <returns>Number of bytes decoded, 0 if no more data can be decoded.</returns>
        int Read(byte[] buffer, int offset, int count);
    }

    /// <summary>
    /// Creates a decoder for a lha entry.
    /// </summary>
    /// <param name="packedData">Stream with compressed data of entry, limited to packed size of entry.</param>
    /// <param name="header">Header of entry to decode.</param>
    public delegate ILhaDecoder LhaDecoderFactory(Stream packedData, LzHeader header);
}
