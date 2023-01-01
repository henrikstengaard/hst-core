namespace Hst.Compression.Lha
{
    using System.IO;
    using System.Threading.Tasks;

    public class LhaReader
    {
        private readonly Stream stream;
        private readonly Header header;
        private LzHeader current;

        public LhaReader(Stream stream, LhaOptions options)
        {
            this.stream = stream;
            this.header = new Header(options);
            this.current = null;
        }

        public async Task<LzHeader> Read()
        {
            if (current != null)
            {
                stream.Seek(current.HeaderOffset + current.HeaderSize + current.PackedSize, SeekOrigin.Begin);
            }

            current = await this.header.GetHeader(stream);
            return current;
        }
    }
}