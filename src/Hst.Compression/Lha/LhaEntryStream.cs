namespace Hst.Compression.Lha
{
    using System;
    using System.IO;

    /// <summary>
    /// Read-only stream decompressing (extracting) a lha entry on demand as it's read.
    /// Supports methods: -lh0-, -lh1-, -lh4-, -lh5-, -lh6-, -lh7-, -lhx-, -lk7-, -lzs-, -lz4-, -lz5-, -lhd-, -pm0-,
    /// -pm1- and -pm2-. Additional methods can be supported by adding decoders to <see cref="LhaOptions.Decoders"/>.
    /// </summary>
    /// <remarks>
    /// If the archive stream is seekable, the archive stream position is restored before each read, so multiple
    /// entry streams can be read interleaved from the same archive stream.
    /// If the archive stream is not seekable, it must be positioned at the start of the entry data and is read
    /// sequentially.
    /// The CRC of the extracted data is verified, when the end of the entry is reached.
    /// </remarks>
    public class LhaEntryStream : Stream
    {
        private readonly LzHeader header;
        private readonly ILhaDecoder decoder;
        private ushort crc;
        private long position;
        private bool verified;

        /// <summary>
        /// Create entry stream for lha entry in archive stream.
        /// </summary>
        /// <param name="stream">Archive stream entry header was read from.</param>
        /// <param name="header">Header of entry to read.</param>
        public LhaEntryStream(Stream stream, LzHeader header) : this(stream, header, LhaOptions.Default)
        {
        }

        /// <summary>
        /// Create entry stream for lha entry in archive stream.
        /// </summary>
        /// <param name="stream">Archive stream entry header was read from.</param>
        /// <param name="header">Header of entry to read.</param>
        /// <param name="options">Options with additional decoders.</param>
        public LhaEntryStream(Stream stream, LzHeader header, LhaOptions options)
            : this(stream, header, header == null ? 0 : header.HeaderOffset + header.HeaderSize, options)
        {
        }

        /// <summary>
        /// Create entry stream for lha entry data starting at data offset.
        /// </summary>
        /// <param name="stream">Stream to read entry data from.</param>
        /// <param name="header">Header of entry to read.</param>
        /// <param name="dataOffset">Offset in stream where entry data starts. Ignored for non-seekable streams.</param>
        /// <param name="options">Options with additional decoders.</param>
        internal LhaEntryStream(Stream stream, LzHeader header, long dataOffset, LhaOptions options)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            if (!stream.CanRead)
            {
                throw new ArgumentException("Stream must be readable", nameof(stream));
            }

            this.header = header;
            var packedData = new PackedDataStream(stream, dataOffset, header.PackedSize);
            decoder = LhaDecoders.Create(packedData, header, options);
            crc = 0;
            position = 0;
            verified = false;
        }

        /// <summary>
        /// Header of entry.
        /// </summary>
        public LzHeader Header => header;

        /// <summary>
        /// CRC-16 of data read so far.
        /// </summary>
        public ushort Crc => crc;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            // When we reach the end of the stream, we must truncate the
            // decompressed data at exactly the right point (original size),
            // or we may read a few extra false byte(s) by mistake.
            var remaining = header.OriginalSize - position;
            if (count > remaining)
            {
                count = (int)remaining;
            }

            if (count == 0)
            {
                Verify();
                return 0;
            }

            var bytesRead = decoder.Read(buffer, offset, count);
            if (bytesRead == 0)
            {
                throw new IOException(
                    $"Unexpected end of compressed data for \"{header.Name}\" at {position} of {header.OriginalSize} bytes");
            }

            crc = Crc16.Update(crc, buffer, offset, bytesRead);
            position += bytesRead;

            if (position >= header.OriginalSize)
            {
                Verify();
            }

            return bytesRead;
        }

        private void Verify()
        {
            if (verified)
            {
                return;
            }

            verified = true;

            if (header.HasCrc && crc != (ushort)header.Crc)
            {
                throw new IOException($"CRC error: \"{header.Name}\"");
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => header.OriginalSize;

        public override long Position
        {
            get => position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
