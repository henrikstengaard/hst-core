namespace Hst.Compression.Tests.Lzx;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Compression.Lzx;
using Xunit;

public class GivenLzxArchiveWithMergedEntries
{
    private readonly string _lzxPath = Path.Combine("TestData", "Lzx", "xpk_compress.lzx");
    
    private static readonly AttributesEnum Attributes =
        AttributesEnum.Read | AttributesEnum.Write | AttributesEnum.Executable | AttributesEnum.Delete;

    private static readonly LzxEntry[] ExpectedEntries =
    {
        CreateEntry("xpkBLZW.library", Attributes, 5164, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkCBR0.library", Attributes, 4336, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkCRM2.library", Attributes, 4148, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkCRMS.library", Attributes, 4160, 0, new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkDHUF.library", Attributes, 9592, 0, new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkDLTA.library", Attributes, 3996, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkENCO.library", Attributes, 4252, 0, new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkFAST.library", Attributes, 5164, 0, new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkFEAL.library", Attributes, 5796, 12386,
            new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkHFMN.library", Attributes, 4964, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkHUFF.library", Attributes, 5732, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkIDEA.library", Attributes, 3940, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkIMPL.library", Attributes, 7084, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkLHLB.library", Attributes, 5468, 0, new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkMASH.library", Attributes, 2796, 0, new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkNONE.library", Attributes, 4076, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkNUKE.library", Attributes, 6332, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkPWPK.library", Attributes, 4848, 16752,
            new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkRAKE.library", Attributes, 10068, 0, new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkRDCN.library", Attributes, 4400, 0, new DateTime(1996, 10, 15, 0, 26, 51, DateTimeKind.Local)),
        CreateEntry("xpkRLEN.library", Attributes, 3860, 0, new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local)),
        CreateEntry("xpkSHRI.library", Attributes, 12268, 0, new DateTime(1996, 10, 15, 0, 26, 49, DateTimeKind.Local)),
        CreateEntry("xpkSMPL.library", Attributes, 4776, 0, new DateTime(1996, 10, 15, 0, 26, 49, DateTimeKind.Local)),
        CreateEntry("xpkSQSH.library", Attributes, 5268, 16056,
            new DateTime(1996, 10, 15, 0, 26, 50, DateTimeKind.Local))
    };

    private static LzxEntry CreateEntry(string name, AttributesEnum attributes, int unpackedSize, int packedSize,
        DateTime date)
    {
        return new LzxEntry
        {
            Name = name,
            Attributes = attributes,
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
            Assert.Equal(expectedEntries[i].Attributes, actualEntries[i].Attributes);
            Assert.Equal(expectedEntries[i].UnpackedSize, actualEntries[i].UnpackedSize);
            Assert.Equal(expectedEntries[i].PackedSize, actualEntries[i].PackedSize);
            Assert.Equal(expectedEntries[i].Date, actualEntries[i].Date);
        }
    }

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

            // assert - lzx archive contains expected entries
            AssertEntries(ExpectedEntries, entries);

            // assert - contains merged entries
            Assert.Contains(entries, x => x.IsMergedEntry);
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

            // act - iterate archive entries
            var entries = new List<LzxEntry>();
            while (await lzxArchive.Next() is { } entry)
            {
                entries.Add(entry);
            }

            // assert - lzx archive contains expected entries
            AssertEntries(ExpectedEntries, entries);

            // assert - contains merged entries
            Assert.Contains(entries, x => x.IsMergedEntry);
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

            // assert - contains merged entries
            Assert.Contains(entries, x => x.IsMergedEntry);
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