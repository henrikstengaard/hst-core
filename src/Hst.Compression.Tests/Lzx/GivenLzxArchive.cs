namespace Hst.Compression.Tests.Lzx;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Compression.Lzx;
using Xunit;

public class GivenLzxArchive
{
    private static readonly LzxEntry[] ExpectedEntries = {
        CreateEntry("xpkBLZW.library", 5164, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkCBR0.library", 4336, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkCRM2.library", 4148, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkCRMS.library", 4160, 0, new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkDHUF.library", 9592, 0, new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkDLTA.library", 3996, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkENCO.library", 4252, 0, new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkFAST.library", 5164, 0, new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkFEAL.library", 5796, 12386, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkHFMN.library", 4964, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkHUFF.library", 5732, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkIDEA.library", 3940, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkIMPL.library", 7084, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkLHLB.library", 5468, 0, new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkMASH.library", 2796, 0, new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkNONE.library", 4076, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkNUKE.library", 6332, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkPWPK.library", 4848, 16752, new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkRAKE.library", 10068, 0, new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkRDCN.library", 4400, 0, new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkRLEN.library", 3860, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkSHRI.library", 12268, 0, new DateTime(1996, 10, 15, 0, 26, 49, DateTimeKind.Local)),
        CreateEntry("xpkSMPL.library", 4776, 0, new DateTime(1996, 10, 15, 0, 26, 49, DateTimeKind.Local)),
        CreateEntry("xpkSQSH.library", 5268, 16056, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local))
    };

    private static LzxEntry CreateEntry(string name, int unpackedSize, int packedSize, DateTime date)
    {
        return new LzxEntry
        {
            Name = name,
            UnpackedSize = unpackedSize,
            PackedSize = packedSize,
            Date = date
        };
    }

    private static void AssertEntries(IList<LzxEntry> expectedEntries, IList<LzxEntry> actualEntries)
    {
        Assert.Equal(expectedEntries.Count, actualEntries.Count);
        for (var i = 0; i < expectedEntries.Count; i++)
        {
            Assert.Equal(expectedEntries[i].Name, actualEntries[i].Name);
            Assert.Equal(expectedEntries[i].UnpackedSize, actualEntries[i].UnpackedSize);
            Assert.Equal(expectedEntries[i].PackedSize, actualEntries[i].PackedSize);
            Assert.Equal(expectedEntries[i].Date, actualEntries[i].Date);
        }
    }
    
    [Fact]
    public async Task WhenReadEntriesFromLzxArchiveThenEntriesAreReturned()
    {
        // arrange - open lzx file
        var path = Path.Combine("TestData", "Lzx", "xpk_compress.lzx");
        await using var stream = File.OpenRead(path);
        var lzxArchive = new LzxArchive(stream);

        // act - read entries
        var entries = (await lzxArchive.Entries()).ToList();
        
        // assert - lzx archive contains expected entries
        AssertEntries(ExpectedEntries, entries);
    }
    
    [Fact]
    public async Task WhenExtractEntriesFromLzxArchiveThenEntriesBytesMatch()
    {
        // arrange - open lzx file
        var path = Path.Combine("TestData", "Lzx", "xpk_compress.lzx");
        await using var stream = File.OpenRead(path);
        var lzxArchive = new LzxArchive(stream);

        // act - iterate archive entries
        var entries = new List<LzxEntry>();
        while (await lzxArchive.Next() is { } entry)
        {
            entries.Add(entry);
            
            // act - extract entry bytes
            byte[] actualBytes;
            using (var memoryStream = new MemoryStream())
            {
                await lzxArchive.Extract(memoryStream);
                actualBytes = memoryStream.ToArray();
            }

            // assert - expected files are equal to extracted actual bytes 
            var expectedBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "Lzx", entry.Name));
            Assert.Equal(expectedBytes.Length, actualBytes.Length);
            Assert.Equal(expectedBytes, actualBytes);
        }
        
        // assert - lzx archive contains expected entries
        AssertEntries(ExpectedEntries, entries);
    }
    
    [Fact]
    public async Task WhenIterateWithoutExtractingEntriesFromLzxArchiveThenEntriesMatch()
    {
        // arrange - open lzx file
        var path = Path.Combine("TestData", "Lzx", "xpk_compress.lzx");
        await using var stream = File.OpenRead(path);
        var lzxArchive = new LzxArchive(stream);

        // act - iterate archive entries
        var entries = new List<LzxEntry>();
        while (await lzxArchive.Next() is { } entry)
        {
            entries.Add(entry);
        }
        
        // assert - lzx archive contains expected entries
        AssertEntries(ExpectedEntries, entries);
    }
}