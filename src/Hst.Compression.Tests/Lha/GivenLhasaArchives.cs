namespace Hst.Compression.Tests.Lha
{
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Compression.Lha;
    using Xunit;

    /// <summary>
    /// Tests extracting archives from Lhasa test suite created by various lha tools and covering all methods.
    /// Extracted data is verified using crc stored in headers.
    /// </summary>
    public class GivenLhasaArchives
    {
        [Theory]
        [InlineData("generated_lzs.lzs", LhaMethod.Lzs)]
        [InlineData("larc333_lz4.lzs", LhaMethod.Lz4)]
        [InlineData("larc333_lz5.lzs", LhaMethod.Lz5)]
        [InlineData("larc333_subdir.lzs", LhaMethod.Lz4)]
        [InlineData("pmarc2_pm0.pma", LhaMethod.Pm0)]
        [InlineData("pmarc2_pm2.pma", LhaMethod.Pm2)]
        [InlineData("pmarc2_long.pma", LhaMethod.Pm2)]
        [InlineData("lharc113_lh1.lzh", LhaMethod.Lh1)]
        [InlineData("lha_amiga_122_lh1.lzh", LhaMethod.Lh1)]
        [InlineData("lha_amiga_122_lh4.lzh", LhaMethod.Lh4)]
        [InlineData("lha_unix114i_h0_subdir.lzh", LhaMethod.Lhd)]
        [InlineData("lha_unix114i_h1_subdir.lzh", LhaMethod.Lhd)]
        [InlineData("lha_unix114i_h2_subdir.lzh", LhaMethod.Lhd)]
        [InlineData("unlha32_h2_lhx.lzh", LhaMethod.Lhx)]
        [InlineData("lhark04d_lh7.lzh", LhaMethod.Lk7)]
        [InlineData("pmarc124_pm1.pma", LhaMethod.Pm1)]
        [InlineData("pmarc124_mtcd.pma", LhaMethod.Pm1)]
        public async Task WhenExtractEntriesThenCrcIsValidAndEntryStreamBytesAreEqual(string fileName,
            string expectedMethod)
        {
            // arrange - open lha file
            var path = Path.Combine("TestData", "Lha", "lhasa", fileName);
            await using var stream = File.OpenRead(path);
            var lhaArchive = new LhaArchive(stream);

            // act - read entries
            var entries = (await lhaArchive.EntriesAsync()).ToList();

            // assert - archive contains entry with expected method
            Assert.NotEmpty(entries);
            Assert.Contains(entries, x => x.Method == expectedMethod);

            foreach (var entry in entries)
            {
                // act - extract entry, crc is verified
                using var expectedStream = new MemoryStream();
                await lhaArchive.ExtractAsync(entry, expectedStream);
                Assert.Equal(entry.UnpackedSize, expectedStream.Length);

                // act - read entry stream with small buffer to read across decode boundaries
                await using var entryStream = await lhaArchive.OpenEntryAsync(entry);
                using var actualStream = new MemoryStream();
                var buffer = new byte[7];
                int bytesRead;
                while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    actualStream.Write(buffer, 0, bytesRead);
                }

                // assert - entry stream bytes are equal to extracted bytes
                Assert.Equal(expectedStream.ToArray(), actualStream.ToArray());
            }
        }

        [Theory]
        [InlineData("lha_unix114i_h1_symlink.lzh")]
        [InlineData("lha_unix114i_h2_symlink.lzh")]
        public async Task WhenReadSymbolicLinkEntryThenNameAndLinkTargetAreSplit(string fileName)
        {
            // arrange - open lha file
            var path = Path.Combine("TestData", "Lha", "lhasa", fileName);
            await using var stream = File.OpenRead(path);
            var lhaArchive = new LhaArchive(stream);

            // act - read entries
            var entries = (await lhaArchive.EntriesAsync()).ToList();

            // assert - symbolic link entry has name and real name
            var entry = Assert.Single(entries);
            Assert.Equal(LhaMethod.Lhd, entry.Method);
            Assert.Equal(LhaUnixMode.Symlink, entry.UnixMode & LhaUnixMode.Symlink);
            Assert.False(string.IsNullOrEmpty(entry.Name));
            Assert.False(string.IsNullOrEmpty(entry.LinkTarget));

            // act - extract symbolic link entry, which has no data
            using var output = new MemoryStream();
            await lhaArchive.ExtractAsync(entry, output);
            Assert.Equal(0, output.Length);
        }
    }
}
