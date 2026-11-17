namespace Hst.Core.Tests.ExtensionTests
{
    using System;
    using System.IO;
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
    }
}
