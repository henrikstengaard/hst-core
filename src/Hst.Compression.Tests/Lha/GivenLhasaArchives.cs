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
        [InlineData("generated_lzs.lzs", Constants.LARC_METHOD)]
        [InlineData("larc333_lz4.lzs", Constants.LARC4_METHOD)]
        [InlineData("larc333_lz5.lzs", Constants.LARC5_METHOD)]
        [InlineData("larc333_subdir.lzs", Constants.LARC4_METHOD)]
        [InlineData("pmarc2_pm0.pma", Constants.PMARC0_METHOD)]
        [InlineData("pmarc2_pm2.pma", Constants.PMARC2_METHOD)]
        [InlineData("pmarc2_long.pma", Constants.PMARC2_METHOD)]
        [InlineData("lharc113_lh1.lzh", Constants.LZHUFF1_METHOD)]
        [InlineData("lha_amiga_122_lh1.lzh", Constants.LZHUFF1_METHOD)]
        [InlineData("lha_amiga_122_lh4.lzh", Constants.LZHUFF4_METHOD)]
        [InlineData("lha_unix114i_h0_subdir.lzh", Constants.LZHDIRS_METHOD)]
        [InlineData("lha_unix114i_h1_subdir.lzh", Constants.LZHDIRS_METHOD)]
        [InlineData("lha_unix114i_h2_subdir.lzh", Constants.LZHDIRS_METHOD)]
        [InlineData("unlha32_h2_lhx.lzh", Constants.LZHUFFX_METHOD)]
        [InlineData("lhark04d_lh7.lzh", Constants.LHARK7_METHOD)]
        [InlineData("pmarc124_pm1.pma", Constants.PMARC1_METHOD)]
        [InlineData("pmarc124_mtcd.pma", Constants.PMARC1_METHOD)]
        public async Task WhenExtractEntriesThenCrcIsValidAndEntryStreamBytesAreEqual(string fileName,
            string expectedMethod)
        {
            // arrange - open lha file
            var path = Path.Combine("TestData", "Lha", "lhasa", fileName);
            await using var stream = File.OpenRead(path);
            var lhaArchive = new LhaArchive(stream);

            // act - read entries
            var entries = (await lhaArchive.Entries()).ToList();

            // assert - archive contains entry with expected method
            Assert.NotEmpty(entries);
            Assert.Contains(entries, x => x.Method == expectedMethod);

            foreach (var entry in entries)
            {
                // act - extract entry, crc is verified
                using var expectedStream = new MemoryStream();
                lhaArchive.Extract(entry, expectedStream);
                Assert.Equal(entry.OriginalSize, expectedStream.Length);

                // act - read entry stream with small buffer to read across decode boundaries
                await using var entryStream = lhaArchive.OpenEntry(entry);
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
        public async Task WhenReadSymbolicLinkEntryThenNameAndRealNameAreSplit(string fileName)
        {
            // arrange - open lha file
            var path = Path.Combine("TestData", "Lha", "lhasa", fileName);
            await using var stream = File.OpenRead(path);
            var lhaArchive = new LhaArchive(stream);

            // act - read entries
            var entries = (await lhaArchive.Entries()).ToList();

            // assert - symbolic link entry has name and real name
            var entry = Assert.Single(entries);
            Assert.Equal(Constants.LZHDIRS_METHOD, entry.Method);
            Assert.Equal(Constants.UNIX_FILE_SYMLINK, entry.UnixMode & Constants.UNIX_FILE_SYMLINK);
            Assert.False(string.IsNullOrEmpty(entry.Name));
            Assert.False(string.IsNullOrEmpty(entry.RealName));

            // act - extract symbolic link entry, which has no data
            using var output = new MemoryStream();
            lhaArchive.Extract(entry, output);
            Assert.Equal(0, output.Length);
        }
    }
}
