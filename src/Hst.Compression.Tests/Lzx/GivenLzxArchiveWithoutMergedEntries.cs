using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Compression.Lzx;
using Xunit;

namespace Hst.Compression.Tests.Lzx;

public class GivenLzxArchiveWithoutMergedEntries
{
    private readonly string _lzxPath = Path.Combine("TestData", "Lzx", "dirs-files.lzx");
    private readonly Dictionary<string, byte[]> _expectedEntryData = new()
    {
        {"dir1/file1.txt", [0xa] }
    };
    
    [Fact]
    public async Task When_ListingArchiveEntries_Then_EntriesAreReturned()
    {
        // arrange - paths
        var lzxTempPath = $"{Guid.NewGuid()}.lzx";

        try
        {
            // arrange - copy lzx test data to lzx temp path
            File.Copy(_lzxPath, lzxTempPath, true);

            // arrange - lzx archive from file
            await using var stream = File.OpenRead(lzxTempPath);
            var lzxArchive = new LzxArchive(stream);
            
            // act - read entries
            var entries = (await lzxArchive.Entries()).ToList();
            
            // assert - lzx archive contains expected entry names
            var expectedNames = new[]
            {
                "dir1/dir3/",
                "dir1/file1.txt",
                "dir2/"
            };
            Assert.Equal(expectedNames.Length, entries.Count);
            Assert.Equal(expectedNames, entries.OrderBy(x => x.Name).Select(x => x.Name).ToArray());

            // assert - no merged entries
            Assert.True(entries.All(x => !x.IsMergedEntry));
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
    public async Task When_IteratingArchiveEntries_Then_EntriesAreReturned()
    {
        // arrange - paths
        var lzxTempPath = $"{Guid.NewGuid()}.lzx";

        try
        {
            // arrange - copy lzx test data to lzx temp path
            File.Copy(_lzxPath, lzxTempPath, true);

            // arrange - lzx archive from file
            await using var stream = File.OpenRead(lzxTempPath);
            var lzxArchive = new LzxArchive(stream);
            
            // act - iterate entries
            var entries = new List<LzxEntry>();
            while (await lzxArchive.Next() is { } lzxEntry)
            {
                entries.Add(lzxEntry);
            }
            
            // assert - lzx archive contains expected entry names
            var expectedNames = new[]
            {
                "dir1/dir3/",
                "dir1/file1.txt",
                "dir2/"
            };
            Assert.Equal(expectedNames.Length, entries.Count);
            Assert.Equal(expectedNames, entries.OrderBy(x => x.Name).Select(x => x.Name).ToArray());

            // assert - no merged entries
            Assert.True(entries.All(x => !x.IsMergedEntry));
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
    public async Task When_ExtractingArchiveEntries_Then_EntriesBytesMatch()
    {
        // arrange - paths
        var lzxTempPath = $"{Guid.NewGuid()}.lzx";

        try
        {
            // arrange - copy lzx test data to lzx temp path
            File.Copy(_lzxPath, lzxTempPath, true);

            // arrange - lzx archive from file
            await using var stream = File.OpenRead(lzxTempPath);
            var lzxArchive = new LzxArchive(stream);
            
            // act - extract entries
            var entries = new List<LzxEntry>();
            while (await lzxArchive.Next() is { } lzxEntry)
            {
                entries.Add(lzxEntry);

                if (lzxEntry.PackedSize == 0)
                {
                    continue;
                }
                
                // act - extract entry bytes
                byte[] actualBytes;
                using (var memoryStream = new MemoryStream())
                {
                    await lzxArchive.Extract(memoryStream);
                    actualBytes = memoryStream.ToArray();
                }
                
                // assert - entry data is as expected
                Assert.True(_expectedEntryData.ContainsKey(lzxEntry.Name));
                var expectedBytes = _expectedEntryData[lzxEntry.Name];
                Assert.Equal(expectedBytes, actualBytes);
            }
            
            // assert - lzx archive contains expected entry names
            var expectedNames = new[]
            {
                "dir1/dir3/",
                "dir1/file1.txt",
                "dir2/"
            };
            Assert.Equal(expectedNames.Length, entries.Count);
            Assert.Equal(expectedNames, entries.OrderBy(x => x.Name).Select(x => x.Name).ToArray());

            // assert - no merged entries
            Assert.True(entries.All(x => !x.IsMergedEntry));
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