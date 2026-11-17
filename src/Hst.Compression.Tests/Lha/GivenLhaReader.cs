namespace Hst.Compression.Tests.Lha
{
    using System;
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
            var entries = new List<LhaEntry>();
            LhaEntry header;
            do
            {
                header = await lhaReader.ReadAsync();
            
                if (header == null)
                {
                    continue;
                }
            
                entries.Add(header);
            } while (header != null);

            // assert - entries have been read from lha file
            Assert.NotEmpty(entries);
            Assert.Equal(6, entries.Count);
            
            // assert - entry "dir" is present and equal
            var dir1Entry = entries.FirstOrDefault(x => x.Name == "dir\\");
            Assert.NotNull(dir1Entry);
            Assert.Equal(new DateTime(1970, 1, 2, 0, 0, 1, DateTimeKind.Utc), dir1Entry.Date);

            // assert - entry "dir2" is present and equal
            var dir2Entry = entries.FirstOrDefault(x => x.Name == "dir2\\");
            Assert.NotNull(dir2Entry);
            Assert.Equal(new DateTime(1970, 1, 2, 0, 0, 1, DateTimeKind.Utc), dir2Entry.Date);

            // assert - entry "symlink1" is present and equal
            var symlink1Entry = entries.FirstOrDefault(x => x.Name == @"dir2\symlink1");
            Assert.NotNull(symlink1Entry);
            Assert.Equal(@"..\file1", symlink1Entry.LinkTarget);
            Assert.Equal(new DateTime(1970, 1, 3, 0, 0, 2, DateTimeKind.Utc), symlink1Entry.Date);

            // assert - entry "symlink2" is present and equal
            var symlink2Entry = entries.FirstOrDefault(x => x.Name == @"dir2\symlink2");
            Assert.NotNull(symlink2Entry);
            Assert.Equal(@"..\file2", symlink2Entry.LinkTarget);
            Assert.Equal(new DateTime(1970, 1, 3, 0, 0, 2, DateTimeKind.Utc), symlink2Entry.Date);

            // assert - entry "file1" is present and equal
            var file1Entry = entries.FirstOrDefault(x => x.Name == "file1");
            Assert.NotNull(file1Entry);
            Assert.Equal(new DateTime(1970, 1, 2, 0, 0, 1, DateTimeKind.Utc), file1Entry.Date);
            
            // assert - entry "file2" is present and equal
            var file2Entry = entries.FirstOrDefault(x => x.Name == "file2");
            Assert.NotNull(file2Entry);
            Assert.Equal(new DateTime(1970, 1, 2, 0, 0, 1, DateTimeKind.Utc), file2Entry.Date);
        }

        [Fact]
        public async Task WhenReadLevel1HeadersFromLhaFileThenHeadersAreReturned()
        {
            // arrange - create lha reader
            var path = Path.Combine("TestData", "Lha", "test_read_format_lha_header1.lzh");
            await using var stream = File.OpenRead(path);
            var lhaReader = new LhaReader(stream, LhaOptions.Default);

            // act - read entries from lha file
            var entries = new List<LhaEntry>();
            LhaEntry header;
            do
            {
                header = await lhaReader.ReadAsync();
            
                if (header == null)
                {
                    continue;
                }
            
                entries.Add(header);
            } while (header != null);

            // assert - entries have been read from lha file
            Assert.NotEmpty(entries);
            Assert.Equal(6, entries.Count);
            
            // assert - entry "dir" is present and equal
            var dir1Entry = entries.FirstOrDefault(x => x.Name == "dir\\");
            Assert.NotNull(dir1Entry);
            Assert.Equal(new DateTime(1970, 1, 2, 0, 0, 1, DateTimeKind.Utc), dir1Entry.Date);

            // assert - entry "dir2" is present and equal
            var dir2Entry = entries.FirstOrDefault(x => x.Name == "dir2\\");
            Assert.NotNull(dir2Entry);
            Assert.Equal(new DateTime(1970, 1, 2, 0, 0, 1, DateTimeKind.Utc), dir2Entry.Date);

            // assert - entry "symlink1" is present and equal
            var symlink1Entry = entries.FirstOrDefault(x => x.Name == @"dir2\symlink1");
            Assert.NotNull(symlink1Entry);
            Assert.Equal(@"..\file1", symlink1Entry.LinkTarget);
            Assert.Equal(new DateTime(1970, 1, 3, 0, 0, 2, DateTimeKind.Utc), symlink1Entry.Date);

            // assert - entry "symlink2" is present and equal
            var symlink2Entry = entries.FirstOrDefault(x => x.Name == @"dir2\symlink2");
            Assert.NotNull(symlink2Entry);
            Assert.Equal(@"..\file2", symlink2Entry.LinkTarget);
            Assert.Equal(new DateTime(1970, 1, 3, 0, 0, 2, DateTimeKind.Utc), symlink2Entry.Date);

            // assert - entry "file1" is present and equal
            var file1Entry = entries.FirstOrDefault(x => x.Name == "file1");
            Assert.NotNull(file1Entry);
            Assert.Equal(new DateTime(1970, 1, 2, 0, 0, 1, DateTimeKind.Utc), file1Entry.Date);
            
            // assert - entry "file2" is present and equal
            var file2Entry = entries.FirstOrDefault(x => x.Name == "file2");
            Assert.NotNull(file2Entry);
            Assert.Equal(new DateTime(1970, 1, 2, 0, 0, 1, DateTimeKind.Utc), file2Entry.Date);
        }

        [Fact]
        public async Task WhenReadLevel2HeadersFromLhaFileThenHeadersAreReturned()
        {
            // arrange - create lha reader
            var path = Path.Combine("TestData", "Lha", "test_read_format_lha_header2.lzh");
            await using var stream = File.OpenRead(path);
            var lhaReader = new LhaReader(stream, LhaOptions.Default);

            // act - read entries from lha file
            var entries = new List<LhaEntry>();
            LhaEntry header;
            do
            {
                header = await lhaReader.ReadAsync();
            
                if (header == null)
                {
                    continue;
                }
            
                entries.Add(header);
            } while (header != null);

            // assert - entries have been read from lha file
            Assert.NotEmpty(entries);
            Assert.Equal(6, entries.Count);
            
            // assert - entry "dir" is present and equal
            var dir1Entry = entries.FirstOrDefault(x => x.Name == "dir\\");
            Assert.NotNull(dir1Entry);
            Assert.Equal(new DateTime(1970, 1, 2, 0, 0, 1, DateTimeKind.Utc), dir1Entry.Date);

            // assert - entry "dir2" is present and equal
            var dir2Entry = entries.FirstOrDefault(x => x.Name == "dir2\\");
            Assert.NotNull(dir2Entry);
            Assert.Equal(new DateTime(1970, 1, 2, 0, 0, 1, DateTimeKind.Utc), dir2Entry.Date);

            // assert - entry "symlink1" is present and equal
            var symlink1Entry = entries.FirstOrDefault(x => x.Name == @"dir2\symlink1");
            Assert.NotNull(symlink1Entry);
            Assert.Equal(@"..\file1", symlink1Entry.LinkTarget);
            Assert.Equal(new DateTime(1970, 1, 3, 0, 0, 2, DateTimeKind.Utc), symlink1Entry.Date);

            // assert - entry "symlink2" is present and equal
            var symlink2Entry = entries.FirstOrDefault(x => x.Name == @"dir2\symlink2");
            Assert.NotNull(symlink2Entry);
            Assert.Equal(@"..\file2", symlink2Entry.LinkTarget);
            Assert.Equal(new DateTime(1970, 1, 3, 0, 0, 2, DateTimeKind.Utc), symlink2Entry.Date);

            // assert - entry "file1" is present and equal
            var file1Entry = entries.FirstOrDefault(x => x.Name == "file1");
            Assert.NotNull(file1Entry);
            Assert.Equal(new DateTime(1970, 1, 2, 0, 0, 1, DateTimeKind.Utc), file1Entry.Date);
            
            // assert - entry "file2" is present and equal
            var file2Entry = entries.FirstOrDefault(x => x.Name == "file2");
            Assert.NotNull(file2Entry);
            Assert.Equal(new DateTime(1970, 1, 2, 0, 0, 1, DateTimeKind.Utc), file2Entry.Date);
        }

        [Fact]
        public async Task WhenReadLevel3HeadersFromLhaFileThenHeadersAreReturned()
        {
            // arrange - create lha reader
            var path = Path.Combine("TestData", "Lha", "test_read_format_lha_header3.lzh");
            await using var stream = File.OpenRead(path);
            var lhaReader = new LhaReader(stream, LhaOptions.Default);

            // act - read entries from lha file
            var entries = new List<LhaEntry>();
            LhaEntry header;
            do
            {
                header = await lhaReader.ReadAsync();
            
                if (header == null)
                {
                    continue;
                }
            
                entries.Add(header);
            } while (header != null);

            // assert - entries have been read from lha file
            Assert.NotEmpty(entries);
            Assert.Equal(4, entries.Count);
            
            // assert - entry "dir" is present and equal
            var dir1Entry = entries.FirstOrDefault(x => x.Name == "dir\\");
            Assert.NotNull(dir1Entry);
            Assert.Equal(new DateTime(1970, 1, 2, 0, 0, 1, DateTimeKind.Utc), dir1Entry.Date);

            // assert - entry "dir2" is present and equal
            var dir2Entry = entries.FirstOrDefault(x => x.Name == "dir2\\");
            Assert.NotNull(dir2Entry);
            Assert.Equal(new DateTime(1970, 1, 2, 0, 0, 1, DateTimeKind.Utc), dir2Entry.Date);

            // assert - entry "file1" is present and equal
            var file1Entry = entries.FirstOrDefault(x => x.Name == "file1");
            Assert.NotNull(file1Entry);
            Assert.Equal(new DateTime(1970, 1, 2, 0, 0, 1, DateTimeKind.Utc), file1Entry.Date);
            
            // assert - entry "file2" is present and equal
            var file2Entry = entries.FirstOrDefault(x => x.Name == "file2");
            Assert.NotNull(file2Entry);
            Assert.Equal(new DateTime(1970, 1, 2, 0, 0, 1, DateTimeKind.Utc), file2Entry.Date);
        }
        
        [Fact]
        public async Task WhenReadHeadersFromAmigaLhaFileThenHeadersAreReturned()
        {
            // arrange - open lha file
            var path = Path.Combine("TestData", "Lha", "amiga.lha");
            await using var stream = File.OpenRead(path);
            var lhaReader = new LhaReader(stream, LhaOptions.AmigaLhaOptions);

            // act - read entries from lha file
            var entries = new List<LhaEntry>();
            LhaEntry header;
            do
            {
                header = await lhaReader.ReadAsync();

                if (header == null)
                {
                    continue;
                }

                entries.Add(header);
            } while (header != null);

            // assert - entries have been read from lha file
            Assert.NotEmpty(entries);
            Assert.Equal(5, entries.Count);
            
            // assert - entry "test.txt" is present and equal
            var testTxtEntry = entries.FirstOrDefault(x => x.Name == "test.txt");
            Assert.NotNull(testTxtEntry);
            Assert.Equal(0, testTxtEntry.Attributes); // ----RWED
            Assert.Equal(new DateTime(1980, 3, 28, 9, 28, 56, DateTimeKind.Utc), testTxtEntry.Date);

            // assert - entry "test1.info" is present and equal
            var test1InfoEntry = entries.FirstOrDefault(x => x.Name == "test1.info");
            Assert.NotNull(test1InfoEntry);
            Assert.Equal(0, test1InfoEntry.Attributes); // ----RWED
            Assert.Equal(new DateTime(1980, 3, 28, 9, 27, 44, DateTimeKind.Utc), test1InfoEntry.Date);

            // assert - entry "test1\test1.txt" is present and equal
            var test1Test1TxtEntry = entries.FirstOrDefault(x => x.Name == @"test1\test1.txt");
            Assert.NotNull(test1Test1TxtEntry);
            Assert.Equal(0, test1Test1TxtEntry.Attributes); // ----RWED
            Assert.Equal(new DateTime(1980, 3, 28, 9, 28, 56, DateTimeKind.Utc), test1Test1TxtEntry.Date);

            // assert - entry "test1\test1.txt" is present and equal
            var test1Test2InfoEntry = entries.FirstOrDefault(x => x.Name == @"test1\test2.info");
            Assert.NotNull(test1Test2InfoEntry);
            Assert.Equal(0, test1Test2InfoEntry.Attributes); // ----RWED
            Assert.Equal(new DateTime(1980, 3, 28, 9, 27, 54, DateTimeKind.Utc), test1Test2InfoEntry.Date);

            // assert - entry "test1\test2\test2.txt" is present and equal
            var test1Test2Test2TxtEntry = entries.FirstOrDefault(x => x.Name == @"test1\test2\test2.txt");
            Assert.NotNull(test1Test2Test2TxtEntry);
            Assert.Equal(0, test1Test2Test2TxtEntry.Attributes); // ----RWED
            Assert.Equal(new DateTime(1980, 3, 28, 9, 28, 56, DateTimeKind.Utc), test1Test2Test2TxtEntry.Date);
        }
        
        [Fact]
        public async Task WhenReadHeadersFromAmigaLhaFileWithChangedAttributesThenHeadersAreReturned()
        {
            // arrange - open lha file
            var path = Path.Combine("TestData", "Lha", "amiga-attributes.lha");
            await using var stream = File.OpenRead(path);
            var lhaReader = new LhaReader(stream, LhaOptions.AmigaLhaOptions);

            // act - read entries from lha file
            var entries = new List<LhaEntry>();
            LhaEntry header;
            do
            {
                header = await lhaReader.ReadAsync();

                if (header == null)
                {
                    continue;
                }

                entries.Add(header);
            } while (header != null);

            // assert - entries have been read from lha file
            Assert.NotEmpty(entries);
            Assert.Equal(2, entries.Count);
            
            // assert - entry "test.txt" is present and equal
            var test1Entry = entries.FirstOrDefault(x => x.Name == "test1");
            Assert.NotNull(test1Entry);
            Assert.Equal(4, test1Entry.Attributes); // ----R-ED
            Assert.Equal(new DateTime(2022, 12, 25, 15, 59, 48, DateTimeKind.Utc), test1Entry.Date);
            
            var test2Entry = entries.FirstOrDefault(x => x.Name == "test2");
            Assert.NotNull(test2Entry);
            Assert.Equal(2, test2Entry.Attributes); // ----RW-D
            Assert.Equal(new DateTime(2022, 12, 25, 15, 59, 56, DateTimeKind.Utc), test2Entry.Date);
        }
    }
}