using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Compression.Lzx;
using Xunit;

namespace Hst.Compression.Tests.Lzx;

public class GivenLzxArchiveWithSpecialCharsInEntries
{
    [Fact]
    public async Task When_ListingEntries_Then_EntriesAreReturned()
    {
        var lzxPath = Path.Combine("TestData", "Lzx", "special_chars.lzx");
        var lzxTempPath = $"{Guid.NewGuid()}.lzx";

        try
        {
            // arrange - copy lzx test data to lzx temp path
            File.Copy(lzxPath, lzxTempPath, true);

            // arrange - lzx archive from file
            await using var stream = File.OpenRead(lzxTempPath);
            var lzxArchive = new LzxArchive(stream);

            // act - read entries
            var entries = (await lzxArchive.Entries()).ToList();
            
            // assert - lzx archive contains expected entries
            string[] expectedEntries =
            [
                "dir1*/",
                "dir2/",
                "file1*",
                "file2<",
                "file3",
                "file4.",
                "file5..",
                "file6.t",
                "file7..t"
            ];
            var actualEntries = entries.Select(x => x.Name).OrderBy(x => x).ToArray();
            Assert.Equal(expectedEntries.Length, actualEntries.Length);
            Assert.Equal(expectedEntries, actualEntries);
        }
        finally
        {
            if (File.Exists(lzxTempPath))
            {
                File.Delete(lzxTempPath);
            }
        }
    }
    
    [Fact]
    public async Task When_IteratingAndExtractingEntries_Then_EntriesAreReturned()
    {
        var lzxPath = Path.Combine("TestData", "Lzx", "special_chars.lzx");
        var lzxTempPath = $"{Guid.NewGuid()}.lzx";

        try
        {
            // arrange - copy lzx test data to lzx temp path
            File.Copy(lzxPath, lzxTempPath, true);

            // arrange - lzx archive from file
            await using var stream = File.OpenRead(lzxTempPath);
            var lzxArchive = new LzxArchive(stream);

            // act - iterate archive entries
            var entries = new List<LzxEntry>();
            while (await lzxArchive.Next() is { } entry)
            {
                entries.Add(entry);

                // act - extract entry bytes
                using var memoryStream = new MemoryStream();
                await lzxArchive.Extract(memoryStream);
            }

            // assert - lzx archive contains expected entries
            string[] expectedEntries =
            [
                "dir1*/",
                "dir2/",
                "file1*",
                "file2<",
                "file3",
                "file4.",
                "file5..",
                "file6.t",
                "file7..t"
            ];
            var actualEntries = entries.Select(x => x.Name).OrderBy(x => x).ToArray();
            Assert.Equal(expectedEntries.Length, actualEntries.Length);
            Assert.Equal(expectedEntries, actualEntries);
        }
        finally
        {
            if (File.Exists(lzxTempPath))
            {
                File.Delete(lzxTempPath);
            }
        }
    }

    [Fact]
    public async Task When_IteratingWithoutExtractingEntries_Then_EntriesAreReturned()
    {
        var lzxPath = Path.Combine("TestData", "Lzx", "special_chars.lzx");
        var lzxTempPath = $"{Guid.NewGuid()}.lzx";

        try
        {
            // arrange - copy lzx test data to lzx temp path
            File.Copy(lzxPath, lzxTempPath, true);

            // arrange - lzx archive from file
            await using var stream = File.OpenRead(lzxTempPath);
            var lzxArchive = new LzxArchive(stream);

            // act - iterate archive entries
            var entries = new List<LzxEntry>();
            while (await lzxArchive.Next() is { } entry)
            {
                entries.Add(entry);
            }
            
            // assert - lzx archive contains expected entries
            string[] expectedEntries =
            [
                "dir1*/",
                "dir2/",
                "file1*",
                "file2<",
                "file3",
                "file4.",
                "file5..",
                "file6.t",
                "file7..t"
            ];
            var actualEntries = entries.Select(x => x.Name).OrderBy(x => x).ToArray();
            Assert.Equal(expectedEntries.Length, actualEntries.Length);
            Assert.Equal(expectedEntries, actualEntries);
        }
        finally
        {
            if (File.Exists(lzxTempPath))
            {
                File.Delete(lzxTempPath);
            }
        }
    }
}