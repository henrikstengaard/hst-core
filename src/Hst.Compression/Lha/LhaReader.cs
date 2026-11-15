namespace Hst.Compression.Lha
{
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Reads headers of entries in a lha archive.
    /// </summary>
    public class LhaReader
    {
        private readonly Stream stream;
        private readonly HeaderReader headerReader;
        private LzHeader current;

        public LhaReader(Stream stream, LhaOptions options)
        {
            this.stream = stream;
            headerReader = new HeaderReader(options ?? LhaOptions.Default);
            current = null;
        }

        /// <summary>
        /// Read header of next entry.
        /// </summary>
        /// <returns>Header of next entry or null, if end of archive is reached.</returns>
        public async Task<LzHeader> Read()
        {
            if (current != null)
            {
                stream.Seek(current.HeaderOffset + current.HeaderSize + current.PackedSize, SeekOrigin.Begin);
            }

            current = await headerReader.Read(stream);
            return current;
        }
    }
}
