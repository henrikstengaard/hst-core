namespace Hst.Compression.Lha.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Hst.Compression.Lha;
    using Xunit;

    /// <summary>
    /// Tests extracting -lh2- and -lh3- archives created by UNLHA32.DLL (from Lhasa test suite) using decoders
    /// based on LHa for UNIX. Extracted data is verified using crc stored in headers.
    /// </summary>
    public class GivenLhaForUnixDecoders
    {
        [Theory]
        [InlineData("unlha32_lh2.lzh", Constants.LZHUFF2_METHOD)]
        [InlineData("unlha32_lh3.lzh", Constants.LZHUFF3_METHOD)]
        public async Task WhenExtractEntriesWithDecodersAddedThenCrcIsValid(string fileName, string expectedMethod)
        {
            // arrange - open lha file with lha for unix decoders added to options
            var path = Path.Combine("TestData", "Lha", fileName);
            await using var stream = File.OpenRead(path);
            var lhaArchive = new LhaArchive(stream, new LhaOptions().AddLhaForUnixDecoders());

            // act - read entries
            var entries = (await lhaArchive.Entries()).ToList();

            // assert - archive contains entry with expected method
            var entry = Assert.Single(entries, x => x.Method == expectedMethod);

            // act - extract entry, crc is verified
            using var output = new MemoryStream();
            lhaArchive.Extract(entry, output);

            // act - read entry stream with small buffer
            await using var entryStream = lhaArchive.OpenEntry(entry);
            using var smallBufferOutput = new MemoryStream();
            var buffer = new byte[7];
            int bytesRead;
            while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                smallBufferOutput.Write(buffer, 0, bytesRead);
            }

            // assert - extracted data is equal
            Assert.Equal(entry.OriginalSize, output.Length);
            Assert.Equal(output.ToArray(), smallBufferOutput.ToArray());
        }

        [Theory]
        [InlineData("unlha32_lh2.lzh")]
        [InlineData("unlha32_lh3.lzh")]
        public async Task WhenExtractEntriesWithoutDecodersAddedThenNotSupportedExceptionIsThrown(string fileName)
        {
            // arrange - open lha file without lha for unix decoders
            var path = Path.Combine("TestData", "Lha", fileName);
            await using var stream = File.OpenRead(path);
            var lhaArchive = new LhaArchive(stream);
            var entry = (await lhaArchive.Entries()).First();

            // act & assert - extract throws not supported exception
            Assert.Throws<NotSupportedException>(() => lhaArchive.Extract(entry, new MemoryStream()));
        }
    }
}
