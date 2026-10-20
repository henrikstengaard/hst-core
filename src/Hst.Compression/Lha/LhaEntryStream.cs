namespace Hst.Compression.Lha
{
    using System;
    using System.IO;

    /// <summary>
    /// Read-only stream decompressing a lha entry on demand as it's read.
    /// </summary>
    public class LhaEntryStream : Stream
    {
        private readonly Stream stream;
        private readonly LzHeader header;
        private readonly CrcIo crcIo;
        private readonly bool isStored;
        private readonly byte[] storedBuffer;

        // sliding dictionary decode state, see Slide.Decode
        private readonly IDecoder decoder;
        private readonly Lha lha;
        private readonly BitIo bitIo;
        private readonly byte[] dtext;
        private readonly long dicsiz1;
        private readonly int adjust;
        private int loc;
        private int matchRemaining;
        private uint matchPos;

        private long streamPosition;
        private long position;
        private bool verified;

        public LhaEntryStream(Stream stream, LzHeader header, bool extractBrokenArchive = false)
        {
            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new ArgumentException("Stream must be readable and seekable", nameof(stream));
            }

            LhExt.EnsureSupportedMethod(header);

            this.stream = stream;
            this.header = header;
            crcIo = new CrcIo();
            crcIo.InitializeCrc();
            streamPosition = header.HeaderOffset + header.HeaderSize;
            position = 0;
            verified = false;

            var method = Extract.GetMethodNum(header.Method);
            var dicBit = Extract.GetDicBit(header.Method);

            if (dicBit == 0)
            {
                /* LZHUFF0_DICBIT or LARC4_DICBIT or PMARC0_DICBIT*/
                isStored = true;
                storedBuffer = new byte[Constants.BUFFERSIZE];
                return;
            }

            this.stream.Seek(streamPosition, SeekOrigin.Begin);

            decoder = Slide.GetDecoder(method);
            bitIo = new BitIo(stream, (int)header.OriginalSize, (int)header.PackedSize);
            lha = new Lha(dicBit, bitIo, crcIo);

            var dicsiz = 1L << dicBit;
            dtext = new byte[dicsiz];
            for (var i = 0; i < dicsiz; i++)
            {
                dtext[i] = (byte)(extractBrokenArchive ? 0 : ' ');
            }

            decoder.DecodeStart(lha);
            streamPosition = this.stream.Position;

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

            // restore archive stream position, so other reads of archive stream doesn't affect entry stream
            stream.Seek(streamPosition, SeekOrigin.Begin);

            var bytesRead = isStored ? ReadStored(buffer, offset, count) : ReadDecoded(buffer, offset, count);

            streamPosition = stream.Position;
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
                crcIo.calccrc(storedBuffer, (uint)read);
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
                    var code = decoder.DecodeC(lha);
                    if (code < 256)
                    {
                        c = (byte)code;
                    }
                    else
                    {
                        var matchLength = code - adjust;
                        var matchOffset = (uint)decoder.DecodeP(lha) + 1;
                        matchPos = (uint)((loc - matchOffset) & dicsiz1);
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

            var readSize = isStored ? position : header.PackedSize - bitIo.compsize;
            if (header.PackedSize != readSize)
            {
                throw new IOException($"Read size {readSize} doesnt match packed size {header.PackedSize}");
            }

            if (header.HasCrc && (short)crcIo.crc != header.Crc)
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
