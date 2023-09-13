using System;
using System.IO;
using System.Linq;
using Hst.Core.IO;
using NuGet.Frameworks;
using Xunit;

namespace Hst.Core.Tests.IOTests;

public class GivenBlockMemoryStreamWithBlockSizeOf512Bytes
{
    [Fact]
    public void WhenRead100BytesThenDataIsRead()
    {
        // arrange - block memory stream with 1024 bytes of data
        var stream = new BlockMemoryStream();
        var blockBytes = new byte[1024];
        stream.Write(blockBytes, 0, blockBytes.Length);
        stream.Seek(0, SeekOrigin.Begin);
        
        // act - read block bytes
        blockBytes = new byte[100];
        var bytesRead = stream.Read(blockBytes, 0, blockBytes.Length);
        
        // assert - bytes read and stream position is equal to 100 bytes
        Assert.Equal(100, bytesRead);
        Assert.Equal(100, stream.Position);
    }

    [Fact]
    public void WhenRead512BytesFrom100BytesStreamThenDataIsRead()
    {
        // arrange - block memory stream with 100 bytes of data
        var stream = new BlockMemoryStream();
        var blockBytes = new byte[100];
        Array.Fill<byte>(blockBytes, 1);
        stream.Write(blockBytes, 0, blockBytes.Length);
        stream.Seek(0, SeekOrigin.Begin);
        
        // act - read block bytes
        blockBytes = new byte[512];
        var bytesRead = stream.Read(blockBytes, 0, blockBytes.Length);
        
        // assert - bytes read and stream position is equal to 100 bytes
        Assert.Equal(100, bytesRead);
        Assert.Equal(100, stream.Position);
    }
    
    [Fact]
    public void WhenRead100BytesFromEmptyStreamThenNoDataIsRead()
    {
        // arrange - block memory stream
        var stream = new BlockMemoryStream();
        
        // act - read block bytes
        var blockBytes = new byte[100];
        var bytesRead = stream.Read(blockBytes, 0, blockBytes.Length);
        
        // assert - bytes read and stream position is equal to 0 bytes
        Assert.Equal(0, bytesRead);
        Assert.Equal(0, stream.Position);
    }
    
    [Fact]
    public void WhenWrite1000BytesThenDataIsWrittenAndPositionAndLengthMatches()
    {
        // arrange - 100 bytes of data to write
        var blockBytes = new byte[1000];
        Array.Fill<byte>(blockBytes, 1, 0, 512);
        Array.Fill<byte>(blockBytes, 2, 512, 1000 - 512);

        // arrange - block memory stream
        var stream = new BlockMemoryStream();
        
        // act - write data
        stream.Write(blockBytes, 0, blockBytes.Length);

        // assert - stream position is 1000
        Assert.Equal(1000, stream.Position);
        
        // assert - block 1 is written at offset 0
        Assert.True(stream.Blocks.ContainsKey(0));
        
        // assert - block 2 contains 1 all 512 bytes
        var expectedBlock1 = new byte[512];
        Array.Fill<byte>(expectedBlock1, 1);
        Assert.Equal(expectedBlock1, stream.Blocks[0]);
        
        // assert - block 2 is written at offset 512
        Assert.True(stream.Blocks.ContainsKey(512));

        // assert - block 2 contains 2 for first 488 bytes and 0 for remaining bytes
        var expectedBlock2 = new byte[512];
        Array.Fill<byte>(expectedBlock2, 2, 0, 1000 - 512);
        Assert.Equal(expectedBlock2, stream.Blocks[512]);
    }
    
    [Fact]
    public void WhenSeekBeginOffset100AndWrite1024BytesThenDataIsWrittenAndLengthMatches()
    {
        // arrange - create data to write
        var blockBytes = new byte[1024];
        Array.Fill<byte>(blockBytes, 1, 0, 512);
        Array.Fill<byte>(blockBytes, 2, 512, 512);
        
        // arrange - create block memory stream and write block bytes at offset 100
        var stream = new BlockMemoryStream();
        
        // act - seek to offset 100 from beginning
        stream.Seek(100, SeekOrigin.Begin);
        
        // assert - stream position is 100
        Assert.Equal(100, stream.Position);

        // act - write 1024 bytes
        stream.Write(blockBytes, 0, blockBytes.Length);

        // assert - stream position is 100 + 1024
        Assert.Equal(100 + 1024, stream.Position);
        
        // assert - 3 blocks was written
        Assert.Equal(3, stream.Blocks.Count);
        
        // assert - block 1 is written at offset 0
        Assert.True(stream.Blocks.ContainsKey(0));
        
        // assert - block 1 contains 1 from offset 100 to 512
        var expectedBlock1 = new byte[512];
        Array.Fill<byte>(expectedBlock1, 1, 100, 512 - 100);
        Assert.Equal(expectedBlock1, stream.Blocks[0]);
        
        // assert - block 2 is written at offset 512
        Assert.True(stream.Blocks.ContainsKey(512));

        // assert - block 2 contains 1 from offset 0 to 99 and 2 from offset 100 to 512
        var expectedBlock2 = new byte[512];
        Array.Fill<byte>(expectedBlock2, 1, 0, 100);
        Array.Fill<byte>(expectedBlock2, 2, 100, 512 - 100);
        Assert.Equal(expectedBlock2, stream.Blocks[512]);
        
        // assert - block 3 is written at offset 1024
        Assert.True(stream.Blocks.ContainsKey(1024));
        
        // assert - block 3 contains 2 from offset 0 to 99
        var expectedBlock3 = new byte[512];
        Array.Fill<byte>(expectedBlock3, 2, 0, 100);
        Assert.Equal(expectedBlock3, stream.Blocks[1024]);
        
        // assert - stream length is equal to offset 100 + 1025 bytes of data written
        Assert.Equal(100 + 1024, stream.Length);
    }

    [Fact]
    public void WhenSeekOffsetEndAndWrite512BytesThenDataIsWritten()
    {
        // arrange - block memory stream with 400 bytes of data
        var stream = new BlockMemoryStream();
        var blockBytes = new byte[400];
        stream.Write(blockBytes, 0, blockBytes.Length);

        // act - seek offset 0
        stream.Seek(0, SeekOrigin.Begin);
        
        // assert - stream position is 0
        Assert.Equal(0, stream.Position);

        // act - seek offset 0
        stream.Seek(0, SeekOrigin.End);
        
        // assert - stream position is 400
        Assert.Equal(400, stream.Position);

        // arrange - create block bytes to write
        blockBytes = new byte[512];
        Array.Fill<byte>(blockBytes, 1);
        stream.Write(blockBytes, 0, blockBytes.Length);

        // assert - stream position and length is 912
        Assert.Equal(912, stream.Position);
        Assert.Equal(912, stream.Length);
        
        // assert - block 1 is written at offset 0
        Assert.True(stream.Blocks.ContainsKey(0));
        
        // assert - block 1 contains 1 from offset 400 to offset 512
        var expectedBlock1 = new byte[512];
        Array.Fill<byte>(expectedBlock1, 1, 400, 112);
        Assert.Equal(expectedBlock1, stream.Blocks[0]);
        
        // assert - block 2 is written at offset 512
        Assert.True(stream.Blocks.ContainsKey(512));

        // assert - block 2 contains 1 from offset 0 to offset 399
        var expectedBlock2 = new byte[512];
        Array.Fill<byte>(expectedBlock2, 1, 0, 400);
        Assert.Equal(expectedBlock2, stream.Blocks[512]);
    }

    [Fact]
    public void WhenSeekCurrentOffset100AndRead100BytesThenDataIsRead()
    {
        // arrange - create data to write with 1 from offset 100 to 200
        var blockBytes = new byte[512];
        Array.Fill<byte>(blockBytes, 1, 200, 100);

        // arrange - create block memory stream and write block bytes at offset 100
        var stream = new BlockMemoryStream();
        stream.Write(blockBytes, 0, blockBytes.Length);

        // act - seek to offset 100 from beginning
        stream.Seek(100, SeekOrigin.Begin);

        // act - seek to offset 100 from current (advances 100 from current position)
        stream.Seek(100, SeekOrigin.Current);
        
        // assert - stream position is 200
        Assert.Equal(200, stream.Position);
        
        // act - read 100 bytes
        var buffer = new byte[100];
        var bytesRead = stream.Read(buffer, 0, buffer.Length);

        // assert - stream position is 300
        Assert.Equal(300, stream.Position);
        
        // assert - 100 bytes was read and all bytes are 1
        Assert.Equal(100, bytesRead);
        Assert.True(buffer.All(x => x == 1));
    }
    
    [Fact]
    public void WhenWrite100BytesOfDataToOffset100ThenWrittenDataIsWritten()
    {
        // arrange - create data to write with 1 from offset 0 to 99
        var blockBytes = new byte[100];
        Array.Fill<byte>(blockBytes, 1, 0, blockBytes.Length);

        // arrange - create block memory stream and write block bytes at offset 100
        var stream = new BlockMemoryStream();
        stream.Seek(100, SeekOrigin.Begin);
        stream.Write(blockBytes, 0, blockBytes.Length);
        
        // assert - 1 block was written
        Assert.Single(stream.Blocks);
        
        // assert - block 1 is written at offset 0
        Assert.True(stream.Blocks.ContainsKey(0));
        
        // assert - block 1 contains 1 from offset 100 to 199
        var expectedBlock1 = new byte[512];
        Array.Fill<byte>(expectedBlock1, 1, 100, 100);
        Assert.Equal(expectedBlock1, stream.Blocks[0]);
        
        // assert - stream length is equal to offset 100 + 100 bytes of data written
        Assert.Equal(200, stream.Length);
    }
    
    [Fact]
    public void WhenRead100BytesFromOffset100ThenDataIsRead()
    {
        // arrange - create data to write with 1 from offset 100 to 200
        var blockBytes = new byte[512];
        Array.Fill<byte>(blockBytes, 1, 100, 100);

        // arrange - create block memory stream and write block bytes at offset 100
        var stream = new BlockMemoryStream();
        stream.Write(blockBytes, 0, blockBytes.Length);

        // act - seek to offset 100 and read 100 bytes
        stream.Seek(100, SeekOrigin.Begin);
        var buffer = new byte[100];
        var bytesRead = stream.Read(buffer, 0, buffer.Length);
        
        // assert - 100 bytes was read and all bytes are 1
        Assert.Equal(100, bytesRead);
        Assert.True(buffer.All(x => x == 1));
    }
}