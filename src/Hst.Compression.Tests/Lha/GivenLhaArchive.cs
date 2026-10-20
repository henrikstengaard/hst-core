namespace Hst.Compression.Tests.Lha
{
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Compression.Lha;
    using Xunit;

    public class GivenLhaArchive
    {
        [Fact]
        public async Task WhenReadEntriesFromLhaArchiveThenEntriesAreReturned()
        {
            // arrange - open lha file
            var path = Path.Combine("TestData", "Lha", "amiga.lha");
            await using var stream = File.OpenRead(path);
            var lhaArchive = new LhaArchive(stream, LhaOptions.AmigaLhaOptions);

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

        [Fact]
        public async Task WhenExtractCompressedLh0DataFromArchiveThenBytesAreEqual()
        {
            // arrange - open lha file
            var path = Path.Combine("TestData", "Lha", "amiga.lha");
            await using var stream = File.OpenRead(path);
            var lhaArchive = new LhaArchive(stream);

            // act - read entries
            var entries = (await lhaArchive.Entries()).ToList();

            // assert - entry "test.txt" exist and uses lh0 method
            var entry = entries.FirstOrDefault(x => x.Name == "test.txt");
            Assert.NotNull(entry);
            Assert.Equal(Constants.LZHUFF0_METHOD,entry.Method);

            // act - extract entry
            await using var output = new MemoryStream();
            lhaArchive.Extract(entry, output);
            
            // assert - output size is equal to entry uncompressed length
            Assert.Equal(entry.OriginalSize, output.Length);
        }
        
        [Fact]
        public async Task WhenExtractCompressedLh6DataFromArchiveThenBytesAreEqual()
        {
            // arrange - open lha file
            var path = Path.Combine("TestData", "Lha", "test_read_format_lha_lh6.lzh");
            await using var stream = File.OpenRead(path);
            var lhaArchive = new LhaArchive(stream);

            // act - read entries
            var entries = (await lhaArchive.Entries()).ToList();

            // assert - entry "file1" exist and uses lh6 method
            var entry = entries.FirstOrDefault(x => x.Name == "file1");
            Assert.NotNull(entry);
            Assert.Equal(Constants.LZHUFF6_METHOD,entry.Method);

            // act - extract entry
            await using var output = new MemoryStream();
            lhaArchive.Extract(entry, output);
            
            // assert - output size is equal to entry uncompressed length
            Assert.Equal(entry.OriginalSize, output.Length);
        }
        
        [Fact]
        public async Task WhenExtractCompressedLh7DataFromArchiveThenBytesAreEqual()
        {
            // arrange - open lha file
            var path = Path.Combine("TestData", "Lha", "test_read_format_lha_lh7.lzh");
            await using var stream = File.OpenRead(path);
            var lhaArchive = new LhaArchive(stream);

            // act - read entries
            var entries = (await lhaArchive.Entries()).ToList();

            // assert - entry "file1" exist and uses lh7 method
            var entry = entries.FirstOrDefault(x => x.Name == "file1");
            Assert.NotNull(entry);
            Assert.Equal(Constants.LZHUFF7_METHOD,entry.Method);

            // act - extract entry
            await using var output = new MemoryStream();
            lhaArchive.Extract(entry, output);
            
            // assert - output size is equal to entry uncompressed length
            Assert.Equal(entry.OriginalSize, output.Length);
        }
        
        [Fact]
        public async Task WhenExtractLargeCompressedLh5DataFromArchiveThenBytesAreEqual()
        {
            // // method used to create random data file 
            // var random = new Random();
            // var data = new byte[1024 * 1024];
            // for (var i = 0; i < data.Length; i++)
            // {
            //     data[i] = (byte)(i / 255 % 2 == 0 ? i % 256 : random.Next(0, 255));
            // }
            //
            // await File.WriteAllBytesAsync("random-data-file", data);
            
            // arrange - open lha file
            var path = Path.Combine("TestData", "Lha", "large-file.lha");
            await using var stream = File.OpenRead(path);
            var lhaArchive = new LhaArchive(stream);

            // act - read entries
            var entries = (await lhaArchive.Entries()).ToList();

            // assert - entry exist
            Assert.Single(entries);
            var entry = entries.FirstOrDefault();
            Assert.NotNull(entry);
            Assert.Equal(Constants.LZHUFF5_METHOD,entry.Method);

            // act - extract entry
            await using var output = new MemoryStream();
            lhaArchive.Extract(entry, output);
            
            // assert - output size is equal to entry uncompressed length
            Assert.Equal(entry.OriginalSize, output.Length);
        }
        
        [Fact]
        public async Task WhenListEntriesLhaArchiveWithJunkDataThenEntriesAreReturned()
        {
            // arrange - open lha file
            var path = Path.Combine("TestData", "Lha", "test_read_format_lha_lh0.lzh");
            await using var stream = File.OpenRead(path);
            var lhaArchive = new LhaArchive(stream);

            // act - read entries
            var entries = (await lhaArchive.Entries()).ToList();

            // assert - entries have been read from lha file
            Assert.NotEmpty(entries);
            Assert.Equal(6, entries.Count);
        }

        [Theory]
        [InlineData("amiga.lha")]
        [InlineData("large-file.lha")]
        [InlineData("test_read_format_lha_lh0.lzh")]
        [InlineData("test_read_format_lha_lh6.lzh")]
        [InlineData("test_read_format_lha_lh7.lzh")]
        public async Task WhenOpenEntryStreamAndReadThenBytesAreEqualToExtractedBytes(string fileName)
        {
            // arrange - open lha file
            var path = Path.Combine("TestData", "Lha", fileName);
            await using var stream = File.OpenRead(path);
            var lhaArchive = new LhaArchive(stream);

            // act - read entries
            var entries = (await lhaArchive.Entries()).ToList();
            Assert.NotEmpty(entries);

            foreach (var entry in entries)
            {
                // act - extract entry
                using var expectedStream = new MemoryStream();
                lhaArchive.Extract(entry, expectedStream);

                // act - open entry stream and read with small buffer to read across decode boundaries
                await using var entryStream = lhaArchive.OpenEntry(entry);
                using var actualStream = new MemoryStream();
                var buffer = new byte[7];
                int bytesRead;
                while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    actualStream.Write(buffer, 0, bytesRead);
                }

                // assert - entry stream bytes are equal to extracted bytes
                Assert.Equal(entry.OriginalSize, entryStream.Length);
                Assert.Equal(entry.OriginalSize, entryStream.Position);
                Assert.Equal(expectedStream.ToArray(), actualStream.ToArray());
            }
        }

        [Fact]
        public async Task WhenReadMultipleEntryStreamsInterleavedThenBytesAreEqualToExtractedBytes()
        {
            // arrange - open lha file
            var path = Path.Combine("TestData", "Lha", "amiga.lha");
            await using var stream = File.OpenRead(path);
            var lhaArchive = new LhaArchive(stream, LhaOptions.AmigaLhaOptions);
            var entries = (await lhaArchive.Entries()).ToList();
            var entry1 = entries.First(x => x.Name == "test1.info");
            var entry2 = entries.First(x => x.Name == @"test1\test2.info");

            // arrange - extract expected bytes
            using var expectedStream1 = new MemoryStream();
            lhaArchive.Extract(entry1, expectedStream1);
            using var expectedStream2 = new MemoryStream();
            lhaArchive.Extract(entry2, expectedStream2);

            // act - open entry streams and read them interleaved
            await using var entryStream1 = lhaArchive.OpenEntry(entry1);
            await using var entryStream2 = lhaArchive.OpenEntry(entry2);
            using var actualStream1 = new MemoryStream();
            using var actualStream2 = new MemoryStream();
            var buffer = new byte[16];
            int bytesRead1, bytesRead2;
            do
            {
                bytesRead1 = entryStream1.Read(buffer, 0, buffer.Length);
                actualStream1.Write(buffer, 0, bytesRead1);
                bytesRead2 = entryStream2.Read(buffer, 0, buffer.Length);
                actualStream2.Write(buffer, 0, bytesRead2);
            } while (bytesRead1 > 0 || bytesRead2 > 0);

            // assert - entry streams bytes are equal to extracted bytes
            Assert.Equal(expectedStream1.ToArray(), actualStream1.ToArray());
            Assert.Equal(expectedStream2.ToArray(), actualStream2.ToArray());
        }
    }
}