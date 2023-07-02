namespace Hst.Compression.Tests.Lzx;

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
        // arrange - expected filenames
        var expectedFileNames = new[]
        {
            "xpkBLZW.library",
            "xpkCBR0.library",
            "xpkCRM2.library",
            "xpkCRMS.library",
            "xpkDHUF.library",
            "xpkDLTA.library",
            "xpkENCO.library",
            "xpkFAST.library",
            "xpkFEAL.library",
            "xpkHFMN.library",
            "xpkHUFF.library",
            "xpkIDEA.library",
            "xpkIMPL.library",
            "xpkLHLB.library",
            "xpkMASH.library",
            "xpkNONE.library",
            "xpkNUKE.library",
            "xpkPWPK.library",
            "xpkRAKE.library",
            "xpkRDCN.library",
            "xpkRLEN.library",
            "xpkSHRI.library",
            "xpkSMPL.library",
            "xpkSQSH.library"
        };
        
        // arrange - open lzx file
        var path = Path.Combine("TestData", "Lzx", "xpk_compress.lzx");
        await using var stream = File.OpenRead(path);
        var lzxArchive = new LzxArchive(stream);

        // act - read entries
        var entries = (await lzxArchive.Entries()).ToList();
        
        // assert - lzx archive contains expected filename entries
        Assert.Equal(expectedFileNames.Length, entries.Count);
        Assert.Equal(expectedFileNames, entries.Select(x => x.Name));
    }
    
    [Fact]
    public async Task WhenExtractEntriesFromLzxArchiveThenEntriesBytesMatch()
    {
        // arrange - open lzx file
        var path = Path.Combine("TestData", "Lzx", "xpk_compress.lzx");
        await using var stream = File.OpenRead(path);
        var lzxArchive = new LzxArchive(stream);

        var iterator = lzxArchive.GetIterator();
        LzxEntry entry;
        while ((entry = await iterator.Next()) != null)
        {
            // arrange - read expected bytes
            var expectedBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "Lzx", entry.Name));
            
            // act - extract entry
            byte[] actualBytes;
            using (var memoryStream = new MemoryStream())
            {
                iterator.Extract(memoryStream);
                actualBytes = memoryStream.ToArray();
            }

            // assert - expected files are equal to extracted actual bytes 
            Assert.Equal(expectedBytes.Length, actualBytes.Length);
            Assert.Equal(expectedBytes, actualBytes);
        }
    }    
}