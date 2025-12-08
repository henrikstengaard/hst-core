namespace Hst.Core.Tests.IOTests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO;
using Xunit;

public class GivenCachedStream
{
    [Fact]
    public void WhenReadingSameOffset10TimesThenStreamOffsetsAreOnlyReadOnce()
    {
        // arrange - data to read
        var data = new byte[50];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i + 1);
        }

        // arrange - cached stream with block size 10
        var stream = new MemoryStream(data);
        var monitorStream = new MonitorStream(stream);
        var cachedStream = new CachedStream(monitorStream, 10, 10);

        var expectedBuffer = data.Skip(10).Take(10).ToArray();
        var buffer = new byte[10];
        
        for (var i = 0; i < 10; i++)
        {
            // arrange - seek position 10, in the beginning of second block
            cachedStream.Seek(10, SeekOrigin.Begin);

            // act - read 10 bytes, 10 from second block
            var bytesRead = cachedStream.Read(buffer, 0, buffer.Length);
            Assert.Equal(10, bytesRead);
            Assert.Equal(expectedBuffer, buffer);
        }
        
        // assert - 1 offset have been read, offset 10
        Assert.Equal(1, monitorStream.Reads.Count);
        Assert.Equal(new []{ 10L }, monitorStream.Reads.ToArray());
        
        // assert - no offsets have been written to
        Assert.Equal(0, monitorStream.Writes.Count);
    }
    
    [Fact]
    public void WhenReadingOver2BlocksThenDataMatchesAndStreamOffsetsAreOnlyReadOnce()
    {
        // arrange - data to read
        var data = new byte[50];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i + 1);
        }

        // arrange - cached stream with block size 10
        var stream = new MemoryStream(data);
        var monitorStream = new MonitorStream(stream);
        var cachedStream = new CachedStream(monitorStream, 10, 10);

        // arrange - seek position 5, in the middle of first block
        cachedStream.Seek(5, SeekOrigin.Begin);
        
        // act - read 10 bytes, 5 from first block and 5 from second block
        var buffer = new byte[10];
        var bytesRead = cachedStream.Read(buffer, 0, buffer.Length);
        Assert.Equal(10, bytesRead);
        Assert.Equal(data.Skip(5).Take(10), buffer);
        
        // assert - 2 offsets have been read, offset 0 and 10
        Assert.Equal(2, monitorStream.Reads.Count);
        Assert.Equal(new []{ 0L, 10L }, monitorStream.Reads.ToArray());
        
        // assert - no offsets have been written to
        Assert.Equal(0, monitorStream.Writes.Count);
    }

    [Fact]
    public void WhenReadingOver3BlocksThenDataMatchesAndStreamOffsetsAreOnlyReadOnce()
    {
        // arrange - data to read
        var data = new byte[50];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i + 1);
        }

        // arrange - cached stream with block size 10
        var stream = new MemoryStream(data);
        var monitorStream = new MonitorStream(stream);
        var cachedStream = new CachedStream(monitorStream, 10, 10);

        // arrange - seek position 5, in the middle of first block
        cachedStream.Seek(5, SeekOrigin.Begin);
        
        // act - read 20 bytes, 5 from first block, 10 from second block, 5 from third block
        var buffer = new byte[20];
        var bytesRead = cachedStream.Read(buffer, 0, buffer.Length);
        Assert.Equal(20, bytesRead);
        Assert.Equal(data.Skip(5).Take(20), buffer);
        
        // assert - 3 offsets have been read, offset 0, 10 and 20
        Assert.Equal(3, monitorStream.Reads.Count);
        Assert.Equal(new []{ 0L, 10L, 20L }, monitorStream.Reads.ToArray());
        
        // assert - no offsets have been written to
        Assert.Equal(0, monitorStream.Writes.Count);
    }

    [Fact]
    public void WhenReadingOver2BlocksWith1MbBlockSizeThenDataMatchesAndStreamOffsetsAreOnlyReadOnce()
    {
        // arrange - data to read
        var data = new byte[2 * 1024 * 1024];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i + 1);
        }
        
        // arrange - cached stream with block size 10
        var stream = new MemoryStream(data);
        var monitorStream = new MonitorStream(stream);
        var cachedStream = new CachedStream(monitorStream, 1024 * 1024, 10);

        // arrange - seek position 1048064, near end of first block
        cachedStream.Seek(1048064, SeekOrigin.Begin);
        
        // act - read 4096 bytes, 515 from first block and 3583 from second block
        var buffer = new byte[4096];
        var bytesRead = cachedStream.Read(buffer, 0, buffer.Length);
        Assert.Equal(4096, bytesRead);
        Assert.Equal(data.Skip(1048064).Take(4096), buffer);
        
        // assert - 2 offsets have been read, offset 0 and 1048576
        Assert.Equal(2, monitorStream.Reads.Count);
        Assert.Equal(new []{ 0L, 1048576L }, monitorStream.Reads.ToArray());
        
        // assert - no offsets have been written to
        Assert.Equal(0, monitorStream.Writes.Count);
    }

    [Fact]
    public void WhenReadingFrom1BlockThenDataMatchesAndStreamOffsetIsOnlyReadOnce()
    {
        // arrange - data to read
        var data = new byte[10 * 1024];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i + 1);
        }
        
        // arrange - cached stream with block size 10240
        var stream = new MemoryStream(data);
        var monitorStream = new MonitorStream(stream);
        var cachedStream = new CachedStream(monitorStream, 10 * 1024, 10);

        // arrange - seek position 1536
        cachedStream.Seek(1536, SeekOrigin.Begin);
        
        // act - read 4096 bytes, 515 from first block and 3583 from second block
        var buffer = new byte[512];
        var bytesRead = cachedStream.Read(buffer, 0, buffer.Length);
        Assert.Equal(512, bytesRead);
        Assert.Equal(data.Skip(1536).Take(512), buffer);
        
        // assert - 1 offset have been read, offset 0
        Assert.Equal(1, monitorStream.Reads.Count);
        Assert.Equal(new []{ 0L }, monitorStream.Reads.ToArray());
        
        // assert - no offsets have been written to
        Assert.Equal(0, monitorStream.Writes.Count);
    }
    
    [Fact]
    public void WhenWritingTo1BlockThenDataMatchesAndStreamOffsetIsOnlyWrittenOnce()
    {
        // arrange - data to read
        var data = new byte[10 * 1024];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i + 1);
        }
        
        // arrange - cached stream with block size 10
        var stream = new MemoryStream(data);
        var monitorStream = new MonitorStream(stream);
        var cachedStream = new CachedStream(monitorStream, 10 * 1024, 10);

        // arrange - seek position 1536
        cachedStream.Seek(1536, SeekOrigin.Begin);
        
        // act - write 512 bytes, 515 in first block
        var buffer = new byte[512];
        cachedStream.Write(buffer, 0, buffer.Length);
        
        // act - write updated cached blocks
        cachedStream.Flush();

        // arrange - update data with expected change
        Array.Copy(buffer, 0, data, 1536, 512);
        
        // assert - expected data matches updated data in stream
        var updatedData = stream.ToArray();
        Assert.Equal(data, updatedData);
        
        // assert - 1 offsets have been written, offset 0
        Assert.Equal(1, monitorStream.Writes.Count);
        Assert.Equal(new []{ 0L }, monitorStream.Writes.ToArray());
    }


    [Fact]
    public void WhenWritingOver2BlocksWith1MbBlockSizeThenDataMatchesAndStreamOffsetsAreOnlyWrittenOnce()
    {
        // arrange - data to read
        var data = new byte[2 * 1024 * 1024];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i + 1);
        }
        
        // arrange - cached stream with block size 10
        var stream = new MemoryStream(data);
        var monitorStream = new MonitorStream(stream);
        var cachedStream = new CachedStream(monitorStream, 1024 * 1024, 10);

        // arrange - seek position 1048064, near end of first block
        cachedStream.Seek(1048064, SeekOrigin.Begin);
        
        // act - write 4096 bytes, 515 from first block and 3583 from second block
        var buffer = new byte[4096];
        cachedStream.Write(buffer, 0, buffer.Length);
        
        // act - write updated cached blocks
        cachedStream.Flush();

        // arrange - update data with expected change
        Array.Copy(buffer, 0, data, 1048064, 4096);
        
        // assert - expected data matches updated data in stream
        var updatedData = stream.ToArray();
        Assert.Equal(data, updatedData);
        
        // assert - 2 offsets have been written, offset 0 and 1048576
        Assert.Equal(2, monitorStream.Writes.Count);
        Assert.Equal(new []{ 0L, 1048576L }, monitorStream.Writes.ToArray());
    }

    [Fact]
    public void WhenWritingDataWithoutFlushingThenCachedStreamDoesNotWriteDataToStream()
    {
        // arrange - data to read
        var data = new byte[50];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i + 1);
        }
        
        // arrange - cached stream with block size 10
        var stream = new MemoryStream();
        var monitorStream = new MonitorStream(stream);
        var cachedStream = new CachedStream(monitorStream, 10, 10);
        
        // act - write 5 bytes of data, first block is read and updated
        cachedStream.Write(data, 0, 5);
        
        // assert - stream contains 0 bytes of data, cached blocks have not been written to stream
        Assert.Equal(0, stream.Length);
    }
    
    [Fact]
    public void WhenWritingDataOver1BlockToEmptyStreamAndFlushedThenBlockIsCreatedAndDataMatches()
    {
        // arrange - data to read
        var data = new byte[50];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i + 1);
        }
        
        // arrange - cached stream with block size 10
        var stream = new MemoryStream();
        var monitorStream = new MonitorStream(stream);
        var cachedStream = new CachedStream(monitorStream, 10, 10);
        
        // act - write 5 bytes of data, first block
        cachedStream.Write(data, 0, 5);
        
        // act - write updated cached blocks to it's base stream
        cachedStream.Flush();
        
        // assert - stream contains 5 bytes of data
        Assert.Equal(5, stream.Length);
        Assert.Equal(data.Take(5), stream.ToArray());
    }
    
    [Fact]
    public void WhenWritingDataOver2BlocksToEmptyStreamAndFlushedThenBlockIsCreatedAndDataMatches()
    {
        // arrange - data to read
        var data = new byte[50];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i + 1);
        }
        
        // arrange - cached stream with block size 10
        var stream = new MemoryStream();
        var monitorStream = new MonitorStream(stream);
        var cachedStream = new CachedStream(monitorStream, 10, 10);
        
        // act - write 15 bytes of data, first and second block
        cachedStream.Write(data, 0, 5);
        cachedStream.Write(data, 5, 10);
        
        // act - write updated cached blocks to it's base stream
        cachedStream.Flush();
        
        // assert - stream contains 15 bytes of data
        Assert.Equal(15, stream.Length);
        Assert.Equal(data.Take(15), stream.ToArray());
    }
    
    [Fact]
    public void WhenReadingDataOver3BlocksAndBlocksLimitOf2ThenCachedBlocksWithLowestReadsArePurged()
    {
        // arrange - data to read
        var data = new byte[50];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i + 1);
        }
        
        // arrange - cached stream with block size 10 bytes and blocks limit of 2, will cause cached stream to purge cached block with lowest reads
        var stream = new MemoryStream(data);
        var monitorStream = new MonitorStream(stream);
        var cachedStream = new CachedStream(monitorStream, 10, 2);
        
        var buffer = new byte[10];
        int bytesRead;
        
        // act - read 10 times from offset 10 (second block)
        for (var i = 0; i < 10; i++)
        {
            // act - seek position 10
            cachedStream.Seek(10, SeekOrigin.Begin);

            // act - read 10 bytes
            bytesRead = cachedStream.Read(buffer, 0, buffer.Length);
            Assert.Equal(10, bytesRead);
        }

        // assert - cached stream has cached offset 10
        var cachedBlockOffsets = cachedStream.CachedBlockOffsets.ToList();
        Assert.Single(cachedBlockOffsets);
        Assert.Equal(1, cachedBlockOffsets.Count(x => x == 10));

        // act - read 3 times from offset 20
        for (var i = 0; i < 3; i++)
        {
            // act - seek position 20
            cachedStream.Seek(20, SeekOrigin.Begin);

            // act - read 10 bytes
            bytesRead = cachedStream.Read(buffer, 0, buffer.Length);
            Assert.Equal(10, bytesRead);
        }

        // assert - cached stream has cached offset 10 and 20
        cachedBlockOffsets = cachedStream.CachedBlockOffsets.ToList();
        Assert.Equal(2, cachedBlockOffsets.Count);
        Assert.Equal(1, cachedBlockOffsets.Count(x => x == 10));
        Assert.Equal(1, cachedBlockOffsets.Count(x => x == 20));
        
        // act - seek position 30
        cachedStream.Seek(30, SeekOrigin.Begin);

        // act - read 10 bytes
        bytesRead = cachedStream.Read(buffer, 0, buffer.Length);
        Assert.Equal(10, bytesRead);
        
        // assert - cached stream has cached offset 10 and 30
        cachedBlockOffsets = cachedStream.CachedBlockOffsets.ToList();
        Assert.Equal(2, cachedBlockOffsets.Count);
        Assert.Equal(1, cachedBlockOffsets.Count(x => x == 10));
        Assert.Equal(1, cachedBlockOffsets.Count(x => x == 30));
        
        // assert - offset 10 is read 1 time
        Assert.Equal(1, monitorStream.Reads.Count(x => x == 10));

        // assert - offset 20 is read 1 time
        Assert.Equal(1, monitorStream.Reads.Count(x => x == 20));

        // assert - offset 30 is read 1 time
        Assert.Equal(1, monitorStream.Reads.Count(x => x == 30));
    }

    [Fact]
    public async Task WhenWritingDataAndWaitUntilFlushTimerTriggersThenCachedBlocksAreWrittenToStream()
    {
        // arrange - data to read
        var data = new byte[50];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i + 1);
        }
        
        // arrange - cached stream with block size 10 bytes, blocks limit of 10 and flush interval of 200 milliseconds
        var stream = new MemoryStream();
        var monitorStream = new MonitorStream(stream);
        var cachedStream = new CachedStream(monitorStream, 10, 10, TimeSpan.FromMilliseconds(200));
        
        // act - write 5 bytes of data, first block is read and updated
        cachedStream.Write(data, 0, 5);

        // assert - cached stream has not flushed updated cached blocks
        Assert.Empty(monitorStream.Writes);

        // act - wait 300 milliseconds
        await Task.Delay(300);
        
        // assert - stream contains 5 bytes of data, cached blocks should have been written to stream
        Assert.Equal(5, stream.Length);

        // assert - cached stream has not flushed updated cached blocks
        Assert.Single(monitorStream.Writes);
        Assert.Equal(1, monitorStream.Writes.Count(x => x == 0));
    }
    
    [Fact]
    public async Task WhenWritingDataWhileFlushTimerTriggersThenCachedBlocksAreWrittenToStream2()
    {
        // arrange - data to read
        var data = new byte[50];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i + 1);
        }
        
        // arrange - cached stream with block size 10 bytes, blocks limit of 10 and flush interval of 1000 milliseconds
        var stream = new MemoryStream();
        var monitorStream = new MonitorStream(stream);
        var cachedStream = new CachedStream(monitorStream, 10, 10, TimeSpan.FromMilliseconds(1000));
        
        // act - write 5 bytes of data, first block is read and updated
        cachedStream.Write(data, 0, 5);

        // act - seek position 0
        cachedStream.Seek(0, SeekOrigin.Begin);
        
        // act - read 5 bytes
        var readBuffer = new byte[5];
        var bytesRead = cachedStream.Read(readBuffer, 0, 5);

        // assert - 5 bytes was read and matches data written 
        Assert.Equal(5, bytesRead);
        Assert.Equal(data.Take(5), readBuffer);
        
        // assert - cached stream has not flushed updated cached blocks
        Assert.Empty(monitorStream.Writes);

        // act - wait 950 milliseconds
        await Task.Delay(950);
        
        // act - read and write 5 bytes at offset 0 10 times while flush timer will trigger
        for (var i = 0; i < 10; i++)
        {
            // act - seek position 0
            cachedStream.Seek(0, SeekOrigin.Begin);

            // assert - 5 bytes was read and matches data written 
            bytesRead = cachedStream.Read(readBuffer, 0, 5);
            Assert.Equal(5, bytesRead);
            Assert.Equal(data.Take(5), readBuffer);
            
            // act - change first 5 data bytes
            for (var d = 0; d < 5; d++)
            {
                data[d] = (byte)(100 + i);
            }

            // act - seek position 0
            cachedStream.Seek(0, SeekOrigin.Begin);
            
            // act - write 5 bytes of data
            cachedStream.Write(data, 0, 5);
            
            // act - wait 10 milliseconds
            await Task.Delay(10);
        }

        // act - wait 1000 milliseconds, flush timer should have triggered then
        await Task.Delay(1000);

        // act - dispose cached stream to flush any remaining cached blocks and stop timer
        await cachedStream.DisposeAsync();
        
        // assert - stream contains 5 bytes of data, cached blocks should have been written to stream
        Assert.Equal(5, stream.Length);

        // assert - cached stream has flushed updated cached blocks and written offset
        Assert.Equal(new[]{ 0L, 0L }, monitorStream.Writes);
    }
}