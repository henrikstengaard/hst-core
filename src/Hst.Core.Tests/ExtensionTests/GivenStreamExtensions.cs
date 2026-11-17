namespace Hst.Core.Tests.ExtensionTests
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Extensions;
    using Xunit;

    public class GivenStreamExtensions
    {
        /// <summary>
        /// Stream returning at most one byte per read, like network or buffered streams can return partial reads.
        /// </summary>
        private class OneBytePerReadStream : MemoryStream
        {
            public OneBytePerReadStream(byte[] data) : base(data)
            {
            }

            public override int Read(byte[] buffer, int offset, int count) =>
                base.Read(buffer, offset, Math.Min(count, 1));
        }

        [Fact]
        public async Task WhenReadingBytesFromStreamWithPartialReadsThenAllBytesAreRead()
        {
            var stream = new OneBytePerReadStream(new byte[] { 1, 2, 3, 4, 5 });

            var bytes = await stream.ReadBytes(4);

            Assert.Equal(new byte[] { 1, 2, 3, 4 }, bytes);
        }

        [Fact]
        public async Task WhenReadingMoreBytesThanAvailableThenRemainingBytesAreReturned()
        {
            var stream = new MemoryStream(new byte[] { 1, 2, 3 });

            var bytes = await stream.ReadBytes(4);

            Assert.Equal(new byte[] { 1, 2, 3 }, bytes);
        }

        [Fact]
        public async Task WhenReadingBytesAtEndOfStreamThenEmptyArrayIsReturned()
        {
            var stream = new MemoryStream(Array.Empty<byte>());

            var bytes = await stream.ReadBytes(4);

            Assert.Empty(bytes);
        }

        [Fact]
        public async Task WhenReadingBigEndianValuesFromStreamWithPartialReadsThenValuesAreRead()
        {
            var stream = new OneBytePerReadStream(new byte[]
                { 0x12, 0x34, 0x56, 0x78, 0x9a, 0xbc, 0xff, 0xfe, 0x80, 0x00, 0x00, 0x01 });

            Assert.Equal(0x12345678U, await stream.ReadBigEndianUInt32());
            Assert.Equal((ushort)0x9abc, await stream.ReadBigEndianUInt16());
            Assert.Equal((short)-2, await stream.ReadBigEndianInt16());
            Assert.Equal(unchecked((int)0x80000001), await stream.ReadBigEndianInt32());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        public async Task WhenReadingBigEndianUInt32PastEndOfStreamThenEndOfStreamExceptionIsThrown(int length)
        {
            var stream = new MemoryStream(new byte[length]);

            await Assert.ThrowsAsync<EndOfStreamException>(() => stream.ReadBigEndianUInt32());
        }

        [Fact]
        public async Task WhenReadingBigEndianValuesPastEndOfStreamThenEndOfStreamExceptionIsThrown()
        {
            await Assert.ThrowsAsync<EndOfStreamException>(() => new MemoryStream(new byte[1]).ReadBigEndianInt16());
            await Assert.ThrowsAsync<EndOfStreamException>(() => new MemoryStream(new byte[1]).ReadBigEndianUInt16());
            await Assert.ThrowsAsync<EndOfStreamException>(() => new MemoryStream(new byte[3]).ReadBigEndianInt32());
        }

        [Fact]
        public async Task WhenReadingPastEndOfStreamThenExceptionIsAnIOException()
        {
            // callers handling end of stream by catching io exception
            var exception = await Record.ExceptionAsync(() => new MemoryStream(new byte[2]).ReadBigEndianUInt32());

            Assert.IsAssignableFrom<IOException>(exception);
        }

        private static readonly byte[] VersionPattern = Encoding.ASCII.GetBytes("$VER");

        private static MemoryStream CreateStream(int length, params (int Offset, string Text)[] parts)
        {
            var data = new byte[length];
            foreach (var (offset, text) in parts)
            {
                Encoding.ASCII.GetBytes(text).CopyTo(data, offset);
            }

            return new MemoryStream(data);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(100)]
        [InlineData(32764)]
        [InlineData(32765)]
        [InlineData(32766)]
        [InlineData(32767)]
        [InlineData(32768)]
        [InlineData(33000)]
        [InlineData(65534)]
        [InlineData(69996)]
        public async Task WhenFindingPatternThenPositionOfPatternIsReturned(int offset)
        {
            var stream = CreateStream(70000, (offset, "$VER"));

            Assert.Equal(offset, await stream.Find(VersionPattern));
        }

        [Fact]
        public async Task WhenFindingPatternAfterPartialMatchAtChunkBoundaryThenPositionOfPatternIsReturned()
        {
            var stream = CreateStream(40000, (32766, "$V"), (33000, "$VER"));

            Assert.Equal(33000, await stream.Find(VersionPattern));
        }

        [Fact]
        public async Task WhenFindingPatternWithOverlappingPrefixThenPositionOfPatternIsReturned()
        {
            var stream = CreateStream(10, (0, "$$VER"));

            Assert.Equal(1, await stream.Find(VersionPattern));
        }

        [Fact]
        public async Task WhenFindingPatternFromNonZeroPositionThenStreamPositionOfPatternIsReturned()
        {
            var stream = CreateStream(40000, (5, "$VER"), (33000, "$VER"));
            stream.Position = 10;

            Assert.Equal(33000, await stream.Find(VersionPattern));
        }

        [Fact]
        public async Task WhenFindingPatternInStreamWithPartialReadsThenPositionOfPatternIsReturned()
        {
            var stream = new OneBytePerReadStream(Encoding.ASCII.GetBytes("abc$VE$VERxyz"));

            Assert.Equal(6, await stream.Find(VersionPattern));
        }

        [Theory]
        [InlineData(0, "")]
        [InlineData(10, "$")]
        [InlineData(10, "$V")]
        [InlineData(10, "$VE")]
        [InlineData(32768, "$VE")]
        [InlineData(40000, "$VE")]
        public async Task WhenFindingPatternNotPresentThenMinusOneIsReturned(int length, string end)
        {
            // stream ending with start of pattern
            var stream = CreateStream(length, (length - end.Length, end));

            Assert.Equal(-1, await stream.Find(VersionPattern));
        }

        [Fact]
        public async Task WhenFindingEmptyOrNullPatternThenExceptionIsThrown()
        {
            var stream = new MemoryStream(new byte[10]);

            await Assert.ThrowsAsync<ArgumentException>(() => stream.Find(Array.Empty<byte>()));
            await Assert.ThrowsAsync<ArgumentNullException>(() => stream.Find(null));
        }
    }
}
