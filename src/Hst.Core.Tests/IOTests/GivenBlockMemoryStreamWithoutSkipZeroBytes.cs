using Hst.Core.IO;
using Xunit;

namespace Hst.Core.Tests.IOTests;

public class GivenBlockMemoryStreamWithoutSkipZeroBytes
{
    [Fact]
    public void WhenWriteBlockWithZerosThenBlockIsWritten()
    {
        // arrange - create block bytes to write with zeroes
        var blockBytes = new byte[512];

        // arrange - create block memory stream and write block bytes
        var stream = new BlockMemoryStream(skipZeroBytes: false);
        stream.Write(blockBytes, 0, blockBytes.Length);

        // assert - stream has 1 block written at offset 0
        Assert.Single(stream.Blocks);
        Assert.True(stream.Blocks.ContainsKey(0));

        // assert - stream length is increased by block bytes written
        Assert.Equal(512, stream.Length);
    }
    
    [Fact]
    public void WhenWriteBlockWithZerosAtOffset1024ThenBlockIsWritten()
    {
        // arrange - create block bytes to write with zeroes
        var blockBytes = new byte[512];

        // arrange - create block memory stream and write block bytes
        var stream = new BlockMemoryStream(skipZeroBytes: false);
        stream.Write(blockBytes, 0, blockBytes.Length);

        // assert - stream has 1 block written at offset 0
        Assert.Single(stream.Blocks);
        Assert.True(stream.Blocks.ContainsKey(0));

        // assert - stream length is increased by block bytes written
        Assert.Equal(512, stream.Length);
    }
}