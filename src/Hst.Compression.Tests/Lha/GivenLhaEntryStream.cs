namespace Hst.Compression.Tests.Lha
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Compression.Lha;
    using Xunit;

    public class GivenLhaEntryStream
    {
        [Fact]
        public void WhenExtractUncompressedLh0DataThenBytesAreEqual()
        {
            // arrange - test data to uncompress
            const string testData = "This is a test\n";

            // arrange - lh0 uncompressed entry (bytes extracted from header end to next header)
            var header = new LzHeader
            {
                Method = Constants.LZHUFF0_METHOD,
                Name = "test.txt",
                OriginalSize = 15,
                PackedSize = 15,
                HasCrc = true,
                Crc = 17248
            };
            var input = new MemoryStream(Encoding.ASCII.GetBytes(testData));

            // act - extract lh0 uncompressed data
            using var entryStream = new LhaEntryStream(input, header, 0, LhaOptions.Default);
            using var output = new MemoryStream();
            entryStream.CopyTo(output);

            // assert - compare uncompressed with expected bytes
            Assert.Equal(Encoding.ASCII.GetBytes(testData), output.ToArray());
        }

        [Fact]
        public async Task WhenExtractCompressedLh5DataThenBytesAreEqual()
        {
            // arrange - lh5 compressed entry (bytes extracted from header end to next header)
            var header = new LzHeader
            {
                Method = Constants.LZHUFF5_METHOD,
                Name = "test1.info",
                OriginalSize = 900,
                PackedSize = 435,
                HasCrc = true,
                Crc = 11704
            };
            var lh5CompressedBytes =
                await File.ReadAllBytesAsync(Path.Combine("TestData", "Lha", "test1.info.lh5.bin"));
            await using var input = new MemoryStream(lh5CompressedBytes);

            // act - extract lh5 compressed data
            await using var entryStream = new LhaEntryStream(input, header, 0, LhaOptions.Default);
            await using var output = new MemoryStream();
            await entryStream.CopyToAsync(output);

            // assert - compare uncompressed with expected bytes
            var expectedBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "Lha", "test1.info"));
            Assert.Equal(expectedBytes, output.ToArray());
        }

        [Fact]
        public void WhenExtractWithInvalidCrcThenExceptionIsThrown()
        {
            // arrange - lh0 entry with invalid crc
            var data = Encoding.ASCII.GetBytes("This is a test\n");
            var header = new LzHeader
            {
                Method = Constants.LZHUFF0_METHOD,
                Name = "test.txt",
                OriginalSize = data.Length,
                PackedSize = data.Length,
                HasCrc = true,
                Crc = 1234
            };

            // act & assert - reading entry throws crc error
            using var entryStream = new LhaEntryStream(new MemoryStream(data), header, 0, LhaOptions.Default);
            var exception = Assert.Throws<IOException>(() => entryStream.CopyTo(new MemoryStream()));
            Assert.Contains("CRC error", exception.Message);
        }

        [Fact]
        public void WhenExtractMethodWithDecoderAddedToOptionsThenDecoderIsUsed()
        {
            // arrange - entry with custom method and decoder adding 1 to each byte
            var data = new byte[] { 1, 2, 3, 4 };
            var header = new LzHeader
            {
                Method = "-xx1-",
                Name = "test.bin",
                OriginalSize = data.Length,
                PackedSize = data.Length,
                HasCrc = false
            };
            var options = new LhaOptions();
            options.Decoders.Add("-xx1-", (packedData, _) => new AddOneDecoder(packedData));

            // act - extract entry
            using var entryStream = new LhaEntryStream(new MemoryStream(data), header, 0, options);
            using var output = new MemoryStream();
            entryStream.CopyTo(output);

            // assert - decoder from options was used
            Assert.Equal(new byte[] { 2, 3, 4, 5 }, output.ToArray());
        }

        [Theory]
        [InlineData(Constants.LZHUFF2_METHOD)]
        [InlineData(Constants.LZHUFF3_METHOD)]
        public void WhenExtractLh2OrLh3ThenNotSupportedExceptionIsThrown(string method)
        {
            var header = new LzHeader
            {
                Method = method,
                Name = "test.bin",
                OriginalSize = 10,
                PackedSize = 10
            };

            var exception = Assert.Throws<NotSupportedException>(() =>
                new LhaEntryStream(new MemoryStream(new byte[10]), header, 0, LhaOptions.Default));
            Assert.Contains("is not supported", exception.Message);
        }

        [Fact]
        public void WhenExtractUnknownMethodThenNotSupportedExceptionIsThrown()
        {
            var header = new LzHeader
            {
                Method = "-zz9-",
                Name = "test.bin",
                OriginalSize = 10,
                PackedSize = 10
            };

            Assert.Throws<NotSupportedException>(() =>
                new LhaEntryStream(new MemoryStream(new byte[10]), header, 0, LhaOptions.Default));
        }

        private class AddOneDecoder : ILhaDecoder
        {
            private readonly Stream packedData;

            public AddOneDecoder(Stream packedData) => this.packedData = packedData;

            public int Read(byte[] buffer, int offset, int count)
            {
                var read = packedData.Read(buffer, offset, count);
                for (var i = offset; i < offset + read; i++)
                {
                    buffer[i]++;
                }

                return read;
            }
        }
    }
}
