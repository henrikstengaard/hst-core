using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Core.IO;
using Xunit;

namespace Hst.Core.Tests.IOTests;

public class GivenHybridStreamWithDataLessThanSizeThresholdInFirstStream
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
        
        // arrange - data 1 of 800 bytes to write
        var data1 = new byte[800];
        Array.Fill<byte>(data1, 1);

        // arrange - data 2 of 200 bytes to write
        var data2 = new byte[200];
        Array.Fill<byte>(data2, 2);

        // arrange - write data 1 to first stream
        await firstStream.WriteAsync(data1);
        
        // act - write data 2 to hybrid stream below size threshold not triggering flush to second stream
        await hybridStream.WriteAsync(data2);
        
        // assert - data flushed event args is null as size threshold not reached by hybrid stream
        Assert.Null(dataFlushedEventArgs);

        // assert - first stream has data 1 and data 2
        Assert.Equal(1000, firstStream.Length);
        var firstStreamData = new byte[1000];
        firstStream.Position = 0;
        await firstStream.ReadExactlyAsync(firstStreamData);
        Assert.Equal(data1.Concat(data2), firstStreamData);
        
        // assert - second stream has no data
        Assert.Equal(0, secondStream.Length);

        // assert - hybrid stream has data 1 and data 2
        Assert.Equal(1000, hybridStream.Length);
        var hybridStreamData = new byte[1000];
        hybridStream.Position = 0;
        await hybridStream.ReadExactlyAsync(hybridStreamData);
        Assert.Equal(data1.Concat(data2), hybridStreamData);
    }

    [Fact]
    public async Task When_WritingDataMoreThenSizeThreshold_Then_FirstStreamIsFlushToSecondStream()
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
        
        // arrange - data 1 of 800 bytes to write
        var data1 = new byte[800];
        Array.Fill<byte>(data1, 1);

        // arrange - data 2 of 800 bytes to write
        var data2 = new byte[800];
        Array.Fill<byte>(data2, 2);
        
        // arrange - write data 1 to first stream
        await firstStream.WriteAsync(data1);
        
        // act - write data 2 to hybrid stream reaching size threshold and triggers flush to second stream
        await hybridStream.WriteAsync(data2);
        
        // assert - data flushed event args bytes total is 800 from flush of first stream to second stream
        Assert.NotNull(dataFlushedEventArgs);
        Assert.Equal(800, dataFlushedEventArgs.BytesTotal);
        
        // assert - first stream has data 1
        Assert.Equal(800, firstStream.Length);
        var firstStreamData = new byte[800];
        firstStream.Position = 0;
        await firstStream.ReadExactlyAsync(firstStreamData);
        Assert.Equal(data1, firstStreamData);
        
        // assert - second stream has data 1 written to first stream flushed to second stream with data 2
        Assert.Equal(1600, secondStream.Length);
        var secondStreamData = new byte[1600];
        secondStream.Position = 0;
        await secondStream.ReadExactlyAsync(secondStreamData);
        Assert.Equal(data1.Concat(data2), secondStreamData);
        
        // assert - hybrid stream has data 1 and data 2
        Assert.Equal(1600, hybridStream.Length);
        var hybridStreamData = new byte[1600];
        hybridStream.Position = 0;
        await hybridStream.ReadExactlyAsync(hybridStreamData);
        Assert.Equal(data1.Concat(data2), hybridStreamData);
    }
}