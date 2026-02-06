using System;
using System.IO;
using System.Threading.Tasks;
using Hst.Core.IO;
using Xunit;

namespace Hst.Core.Tests.IOTests;

public class GivenHybridStreamWithEmptyFirstStream
{
    [Fact]
    public async Task When_WritingDataLessThenSizeThreshold_Then_OnlyFirstStreamIsUsed()
    {
        // arrange - hybrid stream with size threshold
        const int sizeThreshold = 1024;
        using var firstStream = new MemoryStream();
        using var secondStream = new MemoryStream();
        await using var hybridStream = new HybridStream(firstStream, secondStream, new HybridStreamOptions
        {
            SizeThreshold = sizeThreshold
        });

        // arrange - data flushed event args
        HybridStream.DataFlushedEventArgs dataFlushedEventArgs = null;
        hybridStream.DataFlushed += (_, e) => dataFlushedEventArgs = e;

        // arrange - data of 512 bytes to write
        var data = new byte[512];
        Array.Fill<byte>(data, 1);
        
        // act - write data 1 less than size threshold
        await hybridStream.WriteAsync(data);
        
        // assert - data flushed event args is null as size threshold not reached by hybrid stream
        Assert.Null(dataFlushedEventArgs);

        // assert - first stream has data
        Assert.Equal(512, firstStream.Length);
        
        // assert - second stream is empty
        Assert.Equal(0, secondStream.Length);
        
        // assert - hybrid stream length
        Assert.Equal(512, hybridStream.Length);
    }

    [Fact]
    public async Task When_WritingDataMoreThenSizeThreshold_Then_FirstStreamIsNotFlushedAndDataIsWrittenToSecondStream()
    {
        // arrange - hybrid stream with size threshold
        const int sizeThreshold = 1024;
        using var firstStream = new MemoryStream();
        using var secondStream = new MemoryStream();
        await using var hybridStream = new HybridStream(firstStream, secondStream, new HybridStreamOptions
        {
            SizeThreshold = sizeThreshold
        });

        // arrange - data flushed event args
        HybridStream.DataFlushedEventArgs dataFlushedEventArgs = null;
        hybridStream.DataFlushed += (_, e) => dataFlushedEventArgs = e;

        // arrange - data of 2048 bytes to write
        var data = new byte[2048];
        Array.Fill<byte>(data, 1);

        // act - write data more than size threshold triggering flush to second stream
        await hybridStream.WriteAsync(data);

        // assert - data flushed event args is null as first stream was empty
        Assert.Null(dataFlushedEventArgs);
        
        // assert - first stream has no data
        Assert.Equal(0, firstStream.Length);

        // assert - data flushed event args is null (first stream was empty)
        Assert.Null(dataFlushedEventArgs);
        
        // assert - second stream all data
        Assert.Equal(2048, secondStream.Length);

        // assert - hybrid stream length is same as data written
        Assert.Equal(2048, hybridStream.Length);
    }
}