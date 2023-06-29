﻿namespace Hst.Compression.Tests.Lzx;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Compression.Lzx;
using Xunit;

public class GivenLzxArchive
{
    [Fact]
    public async Task WhenReadEntriesFromLzxArchiveThenEntriesAreReturned()
    {
        // arrange - open lzx file
        var path = Path.Combine("TestData", "Lzx", "xpk_compress.lzx");
        await using var stream = File.OpenRead(path);
        var lzxArchive = new LzxArchive(stream);

        // act - read entries
        var entries = (await lzxArchive.Entries()).ToList();
        
        // assert - lzx archive contains 24 entries
        Assert.Equal(24, entries.Count);

        var entry = entries.FirstOrDefault(x => x.Name == "xpkFEAL.library");
        byte[] actualBytes;
        using (var memoryStream = new MemoryStream())
        {
            LzxExtract.Extract(lzxArchive, entry, memoryStream);
            actualBytes = memoryStream.ToArray();
        }

        var expectedBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "Lzx", "xpkFEAL.library"));
        
        Assert.Equal(expectedBytes.Length, actualBytes.Length);
        for (var i = 0; i < expectedBytes.Length; i++)
        {
            if (expectedBytes[i] != actualBytes[i])
            {
                
            }
        }
    }
}