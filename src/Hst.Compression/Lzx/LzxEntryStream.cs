namespace Hst.Compression.Lzx
{
    using System;
    using System.IO;

    /// <summary>
    /// Read-only stream decompressing a lzx entry on demand as it's read.
    /// </summary>
    public class LzxEntryStream : Stream
    {
        private readonly Stream stream;
        private readonly LzxEntry entry;
        private readonly PackModeEnum packMode;
        private readonly LzxExtractor extractor;

        // size of entries preceding entry in merged chunk, which must be skipped before reading entry data
        private long skipSize;

        private long streamPosition;
        private long position;
        private int dataCrc;
        private bool verified;

        public LzxEntryStream(Stream stream, LzxMergedChunk mergedChunk, LzxEntry entry)
        {
            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new ArgumentException("Stream must be readable and seekable", nameof(stream));
            }

            if (mergedChunk.PackMode != PackModeEnum.Store && mergedChunk.PackMode != PackModeEnum.Normal)
            {
                throw new IOException($"Unknown pack mode {(int)mergedChunk.PackMode}'");
            }

            this.stream = stream;
            this.entry = entry;
            packMode = mergedChunk.PackMode;
            extractor = new LzxExtractor(stream);
            extractor.Reset(mergedChunk);

            skipSize = 0;
            foreach (var mergedEntry in mergedChunk.Entries)
            {
                if (mergedEntry.EntryOffset == entry.EntryOffset)
                {
                    break;
                }

                skipSize += mergedEntry.UnpackedSize;
            }

            streamPosition = mergedChunk.CompressedDataOffset;
            position = 0;
            dataCrc = 0;
            verified = false;
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

            var remaining = entry.UnpackedSize - position;
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

            Skip();

            var bytesRead = 0;
            while (bytesRead < count)
            {
                var read = ReadData(buffer, offset + bytesRead, count - bytesRead);
                if (read == 0)
                {
                    break;
                }

                bytesRead += read;
            }

            streamPosition = stream.Position;

            dataCrc = CrcHelper.crc_calc(buffer, offset, bytesRead, dataCrc);
            position += bytesRead;

            if (bytesRead == 0 || position >= entry.UnpackedSize)
            {
                Verify();
            }

            return bytesRead;
        }

        private int ReadData(byte[] buffer, int offset, int count)
        {
            return packMode == PackModeEnum.Store
                ? extractor.ReadStore(buffer, offset, count)
                : extractor.ReadNormal(buffer, offset, count);
        }

        /// <summary>
        /// Skip data of entries preceding entry in merged chunk
        /// </summary>
        private void Skip()
        {
            if (skipSize <= 0)
            {
                return;
            }

            if (packMode == PackModeEnum.Store)
            {
                stream.Seek(skipSize, SeekOrigin.Current);
                skipSize = 0;
                return;
            }

            var skipBuffer = new byte[16384];
            while (skipSize > 0)
            {
                var read = extractor.ReadNormal(skipBuffer, 0, (int)Math.Min(skipSize, skipBuffer.Length));
                if (read == 0)
                {
                    throw new IOException("Failed to read data");
                }

                skipSize -= read;
            }
        }

        private void Verify()
        {
            if (verified)
            {
                return;
            }

            verified = true;

            if (position != entry.UnpackedSize)
            {
                throw new IOException($"Read size {position} doesnt match unpacked size {entry.UnpackedSize}");
            }

            if (dataCrc != entry.DataCrc)
            {
                throw new IOException("Crc error");
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => entry.UnpackedSize;

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
