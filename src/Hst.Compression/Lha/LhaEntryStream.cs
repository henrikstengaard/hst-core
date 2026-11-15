namespace Hst.Compression.Lha
{
    using System;
    using System.IO;

    /// <summary>
    /// Read-only stream decompressing (extracting) a lha entry on demand as it's read.
    /// Supports all lha methods: lh0, lh1, lh2, lh3, lh4, lh5, lh6, lh7, lzs, lz4, lz5, lhd, pm0 and pm2.
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
        private readonly Stream stream;
        private readonly LzHeader header;
        private readonly bool verifyCrc;
        private readonly CrcIo crcIo;
        private readonly bool isStored;
        private readonly byte[] storedBuffer;

        // sliding dictionary decode state, see slide.c decode
        private readonly IDecoder decoder;
        private readonly Lha lha;
        private readonly BitIo bitIo;
        private readonly byte[] dtext;
        private readonly int dicsiz1;
        private readonly int adjust;
        private int loc;
        private int matchRemaining;
        private int matchPos;

        private long streamPosition;
        private long position;
        private bool verified;

        /// <summary>
        /// Create entry stream for lha entry in archive stream.
        /// </summary>
        /// <param name="stream">Archive stream entry header was read from.</param>
        /// <param name="header">Header of entry to read.</param>
        /// <param name="extractBrokenArchive">Extract archives created by old LHa for UNIX with a bug.</param>
        public LhaEntryStream(Stream stream, LzHeader header, bool extractBrokenArchive = false)
            : this(stream, header, header.HeaderOffset + header.HeaderSize, extractBrokenArchive, true)
        {
        }

        /// <summary>
        /// Create entry stream for lha entry data starting at data offset.
        /// </summary>
        /// <param name="stream">Stream to read entry data from.</param>
        /// <param name="header">Header of entry to read.</param>
        /// <param name="dataOffset">Offset in stream where entry data starts. Ignored for non-seekable streams.</param>
        /// <param name="extractBrokenArchive">Extract archives created by old LHa for UNIX with a bug.</param>
        /// <param name="verifyCrc">Verify CRC and throw exception, when end of entry is reached.</param>
        internal LhaEntryStream(Stream stream, LzHeader header, long dataOffset, bool extractBrokenArchive,
            bool verifyCrc)
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

            LhExt.EnsureSupportedMethod(header);

            this.stream = stream;
            this.header = header;
            this.verifyCrc = verifyCrc;
            crcIo = new CrcIo();
            crcIo.InitializeCrc();
            streamPosition = dataOffset;
            position = 0;
            verified = false;

            var method = Extract.GetMethodNum(header.Method);
            var dicBit = Extract.GetDicBit(header.Method);

            if (dicBit == 0)
            {
                /* LZHUFF0_DICBIT or LARC4_DICBIT or PMARC0_DICBIT or LZHDIRS */
                isStored = true;
                storedBuffer = new byte[Constants.BUFFERSIZE];
                return;
            }

            SeekStream();

            var dicsiz = 1 << dicBit;
            dtext = new byte[dicsiz];
            for (var i = 0; i < dicsiz; i++)
            {
                /* LHa for UNIX (autoconf) had a fatal bug since version
                   1.14i-ac20030713 (slide.c revision 1.20).

                   This bug is possible to make a broken archive, proper LHA
                   cannot extract it (probably it report CRC error).

                   If the option "--extract-broken-archive" specified, extract
                   the broken archive made by old LHa for UNIX. */
                dtext[i] = (byte)(extractBrokenArchive ? 0 : ' ');
            }

            decoder = Slide.GetDecoder(method);
            bitIo = new BitIo(stream, (int)header.OriginalSize, (int)header.PackedSize);
            lha = new Lha(dicBit, bitIo, crcIo)
            {
                dtext = dtext,
                loc = 0,
                decode_count = 0
            };

            decoder.DecodeStart(lha);
            UpdateStreamPosition();

            dicsiz1 = dicsiz - 1;
            adjust = 256 - Constants.THRESHOLD;
            if (method == Constants.LARC_METHOD_NUM || method == Constants.PMARC2_METHOD_NUM)
            {
                adjust = 256 - 2;
            }

            loc = 0;
            matchRemaining = 0;
            matchPos = 0;
        }

        /// <summary>
        /// Header of entry.
        /// </summary>
        public LzHeader Header => header;

        /// <summary>
        /// CRC-16 of data read so far.
        /// </summary>
        public ushort Crc => (ushort)crcIo.crc;

        /// <summary>
        /// Number of compressed bytes read from archive stream so far.
        /// </summary>
        public long ReadSize => isStored ? position : header.PackedSize - bitIo.compsize;

        private void SeekStream()
        {
            // restore archive stream position, so other reads of archive stream doesn't affect entry stream
            if (stream.CanSeek)
            {
                stream.Seek(streamPosition, SeekOrigin.Begin);
            }
        }

        private void UpdateStreamPosition()
        {
            if (stream.CanSeek)
            {
                streamPosition = stream.Position;
            }
        }

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

            SeekStream();

            var bytesRead = isStored ? ReadStored(buffer, offset, count) : ReadDecoded(buffer, offset, count);

            UpdateStreamPosition();
            position += bytesRead;

            if (position >= header.OriginalSize)
            {
                Verify();
            }

            return bytesRead;
        }

        private int ReadStored(byte[] buffer, int offset, int count)
        {
            var bytesRead = 0;
            while (bytesRead < count)
            {
                var size = Math.Min(count - bytesRead, storedBuffer.Length);
                var read = stream.Read(storedBuffer, 0, size);
                if (read == 0)
                {
                    throw new IOException("file read error");
                }

                Array.Copy(storedBuffer, 0, buffer, offset + bytesRead, read);
                crcIo.calccrc(storedBuffer, 0, read);
                bytesRead += read;
            }

            return bytesRead;
        }

        private int ReadDecoded(byte[] buffer, int offset, int count)
        {
            var bytesRead = 0;
            while (bytesRead < count)
            {
                byte c;
                if (matchRemaining > 0)
                {
                    c = dtext[matchPos++ & dicsiz1];
                    matchRemaining--;
                }
                else
                {
                    // decoders lzs, lz5, pm2 and lh2 uses current dictionary position and decoded bytes count
                    lha.loc = loc;
                    lha.decode_count = position + bytesRead;

                    var code = decoder.DecodeC(lha);
                    if (code < 256)
                    {
                        c = (byte)code;
                    }
                    else
                    {
                        var matchLength = code - adjust;
                        var matchOffset = decoder.DecodeP(lha) + 1;
                        matchPos = (loc - matchOffset) & dicsiz1;
                        c = dtext[matchPos++ & dicsiz1];
                        matchRemaining = matchLength - 1;
                    }
                }

                dtext[loc++] = c;
                if (loc > dicsiz1)
                {
                    loc = 0;
                }

                crcIo.UPDATE_CRC(c);
                buffer[offset + bytesRead++] = c;
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

            if (!verifyCrc)
            {
                return;
            }

            if (isStored && header.PackedSize != position)
            {
                throw new IOException($"Read size {position} doesnt match packed size {header.PackedSize}");
            }

            if (header.HasCrc && (ushort)crcIo.crc != (ushort)header.Crc)
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
