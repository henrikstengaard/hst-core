namespace Hst.Compression.Tests.Lha
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Compression.Lha;
    using Xunit;

    public class GivenLhaReader
    {
        [Fact]
        public async Task WhenReadLevel0HeadersFromLhaFileThenHeadersAreReturned()
        {
            // arrange - create lha reader
            var path = Path.Combine("TestData", "Lha", "test_read_format_lha_header0.lzh");
            await using var stream = File.OpenRead(path);
            var lhaReader = new LhaReader(stream, LhaOptions.Default);

            // act - read entries from lha file
            var entries = new List<LzHeader>();
            LzHeader header;
            do
            {
                header = await lhaReader.Read();
            
                if (header == null)
                {
                    continue;
                }
            
                entries.Add(header);
            } while (header != null);

            // assert - entries have been read from lha file
            Assert.NotEmpty(entries);
            Assert.Equal(6, entries.Count);
            
            // assert - entries exist
            var entry1 = entries.FirstOrDefault(x => x.Name == "dir\\");
            Assert.NotNull(entry1);
            var entry2 = entries.FirstOrDefault(x => x.Name == "dir2\\");
            Assert.NotNull(entry2);
            var entry3 = entries.FirstOrDefault(x => x.Name == @"dir2\symlink1");
            Assert.NotNull(entry3);
            Assert.Equal(@"..\file1", entry3.RealName);
            var entry4 = entries.FirstOrDefault(x => x.Name == @"dir2\symlink2");
            Assert.NotNull(entry4);
            Assert.Equal(@"..\file1", entry3.RealName);
            var entry5 = entries.FirstOrDefault(x => x.Name == "file1");
            Assert.NotNull(entry5);
            var entry6 = entries.FirstOrDefault(x => x.Name == "file2");
            Assert.NotNull(entry6);
        }

        [Fact]
        public async Task WhenReadLevel1HeadersFromLhaFileThenHeadersAreReturned()
        {
            // arrange - create lha reader
            var path = Path.Combine("TestData", "Lha", "test_read_format_lha_header1.lzh");
            await using var stream = File.OpenRead(path);
            var lhaReader = new LhaReader(stream, LhaOptions.Default);

            // act - read entries from lha file
            var entries = new List<LzHeader>();
            LzHeader header;
            do
            {
                header = await lhaReader.Read();
            
                if (header == null)
                {
                    continue;
                }
            
                entries.Add(header);
            } while (header != null);

            // assert - entries have been read from lha file
            Assert.NotEmpty(entries);
            Assert.Equal(6, entries.Count);
            
            // assert - entries exist
            var entry1 = entries.FirstOrDefault(x => x.Name == "dir\\");
            Assert.NotNull(entry1);
            var entry2 = entries.FirstOrDefault(x => x.Name == "dir2\\");
            Assert.NotNull(entry2);
            var entry3 = entries.FirstOrDefault(x => x.Name == @"dir2\symlink1");
            Assert.NotNull(entry3);
            Assert.Equal(@"..\file1", entry3.RealName);
            var entry4 = entries.FirstOrDefault(x => x.Name == @"dir2\symlink2");
            Assert.NotNull(entry4);
            Assert.Equal(@"..\file1", entry3.RealName);
            var entry5 = entries.FirstOrDefault(x => x.Name == "file1");
            Assert.NotNull(entry5);
            var entry6 = entries.FirstOrDefault(x => x.Name == "file2");
            Assert.NotNull(entry6);
        }

        [Fact]
        public async Task WhenReadLevel2HeadersFromLhaFileThenHeadersAreReturned()
        {
            // arrange - create lha reader
            var path = Path.Combine("TestData", "Lha", "test_read_format_lha_header2.lzh");
            await using var stream = File.OpenRead(path);
            var lhaReader = new LhaReader(stream, LhaOptions.Default);

            // act - read entries from lha file
            var entries = new List<LzHeader>();
            LzHeader header;
            do
            {
                header = await lhaReader.Read();
            
                if (header == null)
                {
                    continue;
                }
            
                entries.Add(header);
            } while (header != null);

            // assert - entries have been read from lha file
            Assert.NotEmpty(entries);
            Assert.Equal(6, entries.Count);
            
            // assert - entries exist
            var entry1 = entries.FirstOrDefault(x => x.Name == "dir\\");
            Assert.NotNull(entry1);
            var entry2 = entries.FirstOrDefault(x => x.Name == "dir2\\");
            Assert.NotNull(entry2);
            var entry3 = entries.FirstOrDefault(x => x.Name == @"dir2\symlink1");
            Assert.NotNull(entry3);
            Assert.Equal(@"..\file1", entry3.RealName);
            var entry4 = entries.FirstOrDefault(x => x.Name == @"dir2\symlink2");
            Assert.NotNull(entry4);
            Assert.Equal(@"..\file1", entry3.RealName);
            var entry5 = entries.FirstOrDefault(x => x.Name == "file1");
            Assert.NotNull(entry5);
            var entry6 = entries.FirstOrDefault(x => x.Name == "file2");
            Assert.NotNull(entry6);
        }

        [Fact]
        public async Task WhenReadLevel3HeadersFromLhaFileThenHeadersAreReturned()
        {
            // arrange - create lha reader
            var path = Path.Combine("TestData", "Lha", "test_read_format_lha_header3.lzh");
            await using var stream = File.OpenRead(path);
            var lhaReader = new LhaReader(stream, LhaOptions.Default);

            // act - read entries from lha file
            var entries = new List<LzHeader>();
            LzHeader header;
            do
            {
                header = await lhaReader.Read();
            
                if (header == null)
                {
                    continue;
                }
            
                entries.Add(header);
            } while (header != null);

            // assert - entries have been read from lha file
            Assert.NotEmpty(entries);
            Assert.Equal(4, entries.Count);
            
            // assert - entries exist
            var entry1 = entries.FirstOrDefault(x => x.Name == "dir\\");
            Assert.NotNull(entry1);
            var entry2 = entries.FirstOrDefault(x => x.Name == "dir2\\");
            Assert.NotNull(entry2);
            var entry3 = entries.FirstOrDefault(x => x.Name == "file1");
            Assert.NotNull(entry3);
            var entry4 = entries.FirstOrDefault(x => x.Name == "file2");
            Assert.NotNull(entry4);
        }
        
        [Fact]
        public async Task WhenReadHeadersFromAmigaLhaFileThenHeadersAreReturned()
        {
            // arrange - open lha file
            var path = Path.Combine("TestData", "lha", "amiga.lha");
            await using var stream = File.OpenRead(path);
            var lhaReader = new LhaReader(stream, LhaOptions.AmigaLhaOptions);

            // act - read entries from lha file
            var entries = new List<LzHeader>();
            LzHeader header;
            do
            {
                header = await lhaReader.Read();

                if (header == null)
                {
                    continue;
                }

                entries.Add(header);
            } while (header != null);

            // assert - entries have been read from lha file
            Assert.NotEmpty(entries);
            var expectedEntryNames = new[]
                { "test.txt", "test1.info", @"test1\test1.txt", @"test1\test2.info", @"test1\test2\test2.txt" };
            Assert.Equal(expectedEntryNames, entries.Select(x => x.Name));
        }
    }
}