namespace Hst.Compression.Tests.Lha
{
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Compression.Lha;
    using Xunit;

    public class GivenLhaArchive
    {
        [Fact]
        public async Task WhenReadEntriesFromLhaArchiveThenEntriesAreReturned()
        {
            // arrange - open lha file
            var path = Path.Combine("TestData", "lha", "amiga.lha");
            await using var stream = File.OpenRead(path);
            var lhaArchive = new LhaArchive(stream, Encoding.GetEncoding("ISO-8859-1"));

            // act - read entries
            var entries = (await lhaArchive.Entries()).ToList();

            // assert - entry "test.txt" is equal
            var entry1 = entries.FirstOrDefault(x => x.Name == "test.txt");
            Assert.NotNull(entry1);
            Assert.Equal(15, entry1.OriginalSize);
            Assert.Equal(15, entry1.PackedSize);
            
            // act - extract entry "test.txt" data
            using var dataStream1 = new MemoryStream();
            lhaArchive.Extract(entry1, dataStream1);
            
            // assert - entry "test.txt" data length is equal
            Assert.Equal(entry1.OriginalSize, dataStream1.Length);
            
            // assert - entry "test1.info" is equal
            var entry2 = entries.FirstOrDefault(x => x.Name == "test1.info");
            Assert.NotNull(entry2);
            Assert.Equal(900, entry2.OriginalSize);
            Assert.Equal(435, entry2.PackedSize);

            // act - extract entry "test1.info" data
            using var dataStream2 = new MemoryStream();
            lhaArchive.Extract(entry2, dataStream2);

            // assert - entry "test1.info" data length is equal
            Assert.Equal(entry2.OriginalSize, dataStream2.Length);
            
            // assert - entry "test1\test1.txt" is equal
            var entry3 = entries.FirstOrDefault(x => x.Name == @"test1\test1.txt");
            Assert.NotNull(entry3);
            Assert.Equal(15, entry3.OriginalSize);
            Assert.Equal(15, entry3.PackedSize);

            // act - extract entry "test1\test1.txt" data
            using var dataStream3 = new MemoryStream();
            lhaArchive.Extract(entry3, dataStream3);

            // assert - entry "test1\test1.txt" data length is equal
            Assert.Equal(entry3.OriginalSize, dataStream3.Length);
            
            // assert - entry "test1\test2.info" is equal
            var entry4 = entries.FirstOrDefault(x => x.Name == @"test1\test2.info");
            Assert.NotNull(entry4);
            Assert.Equal(900, entry4.OriginalSize);
            Assert.Equal(432, entry4.PackedSize);

            // act - extract entry "test1\test2.info" data
            using var dataStream4 = new MemoryStream();
            lhaArchive.Extract(entry4, dataStream4);

            // assert - entry "test1\test2.info" data length is equal
            Assert.Equal(entry4.OriginalSize, dataStream4.Length);

            // assert - entry "test1\test2\test2.txt" is equal
            var entry5 = entries.FirstOrDefault(x => x.Name == @"test1\test2\test2.txt");
            Assert.NotNull(entry5);
            Assert.Equal(15, entry5.OriginalSize);
            Assert.Equal(15, entry5.PackedSize);

            // act - extract entry "test1\test2\test2.txt" data
            using var dataStream5 = new MemoryStream();
            lhaArchive.Extract(entry5, dataStream5);

            // assert - entry "test1\test2\test2.txt" data length is equal
            Assert.Equal(entry5.OriginalSize, dataStream5.Length);
        }
    }
}