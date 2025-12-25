using System;
using System.IO;
using System.Threading.Tasks;
using Hst.Core.IO;
using Xunit;

namespace Hst.Core.Tests.IOTests;

public class GivenLayeredStreamWithEmptyLayer
{
    private const int LONG_SIZE = 8;
    private const int INT_SIZE = 4;
    private const int MAGIC_SIZE = 4;
    private const int LAYER_HEADER_SIZE = MAGIC_SIZE + INT_SIZE + LONG_SIZE + INT_SIZE;
    private const int BLOCK_HEADER_SIZE = LONG_SIZE;

    private const int BLOCK_SIZE = 512;
    private const int NUMBER_OF_BLOCKS = 10;
    private const int SIZE = BLOCK_SIZE * NUMBER_OF_BLOCKS;
    private const int BLOCK_ALLOCATION_TABLE_SIZE = NUMBER_OF_BLOCKS * (LONG_SIZE + 1);
    
    private readonly byte[] _blockTestDataBytes = new byte[BLOCK_SIZE];

    public GivenLayeredStreamWithEmptyLayer()
    {
        for (var i = 0; i < BLOCK_SIZE; i++)
        {
            _blockTestDataBytes[i] = (byte)(i % 256);
        }
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(45)]
    [InlineData(234)]
    [InlineData(389)]
    [InlineData(456)]
    public void When_ReadingDataFromSingleBlock_Then_DataIsReadFromBaseToSingleBlockLayer(int offset)
    {
        const int bytesToRead = 50;
        
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(SIZE);

        // arrange - empty layer stream
        using var layerStream = new MemoryStream();

        // arrange - layered stream on top of base and layer streams
        using var layeredStream = new LayeredStream(baseStream, layerStream, new LayeredStreamOptions{ BlockSize =  BLOCK_SIZE });

        // arrange - write data to base stream at position 0
        baseStream.Position = 0;
        baseStream.Write(this._blockTestDataBytes, 0, BLOCK_SIZE);

        // act - read data at position offset from layered stream
        var data = new byte[bytesToRead];
        layeredStream.Position = offset;
        layeredStream.ReadExactly(data, 0, bytesToRead);

        // assert - read data is equal to base stream data
        var expectedData = new byte[bytesToRead];
        Array.Copy(_blockTestDataBytes, offset, expectedData, 0, bytesToRead);
        Assert.Equal(expectedData, data);
        
        // assert - layered stream structure is equal to expected size
        const int numberOfBlocks = 1;
        var expectedLayeredStreamSize = LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE + (LONG_SIZE + BLOCK_SIZE) * numberOfBlocks;
        Assert.Equal(expectedLayeredStreamSize, layerStream.Length);
        
        // read block allocation table
        var blockAllocationTableBytes = new byte[BLOCK_ALLOCATION_TABLE_SIZE];
        layerStream.Position = LAYER_HEADER_SIZE;
        layerStream.ReadExactly(blockAllocationTableBytes, 0, BLOCK_ALLOCATION_TABLE_SIZE);
        
        // create expected block allocation table
        var expectedBlockAllocationTableBytes = new byte[BLOCK_ALLOCATION_TABLE_SIZE];

        // write block number 0 offset in expected block allocation table position 0 (offset 0)
        var blockOffsetBytes = BitConverter.GetBytes(Convert.ToInt64(LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE));
        const int blockAllocationTableOffset = 0;
        Array.Copy(blockOffsetBytes, 0, expectedBlockAllocationTableBytes, blockAllocationTableOffset, LONG_SIZE);
        
        // assert block allocation table
        Assert.Equal(expectedBlockAllocationTableBytes.Length, blockAllocationTableBytes.Length);
        Assert.Equal(expectedBlockAllocationTableBytes, blockAllocationTableBytes);

        // read block
        var block0Bytes = new byte[BLOCK_HEADER_SIZE + BLOCK_SIZE];
        layerStream.Position = LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE;
        layerStream.ReadExactly(block0Bytes, 0, block0Bytes.Length);
        
        // assert block
        var expectedBlockBytes = new byte[BLOCK_HEADER_SIZE + BLOCK_SIZE];
        Array.Copy(BitConverter.GetBytes(0L), 0, expectedBlockBytes, 0, LONG_SIZE); // block number
        Array.Copy(BitConverter.GetBytes(BLOCK_SIZE), 0, expectedBlockBytes, 8, INT_SIZE); // block size
        Array.Copy(_blockTestDataBytes, 0, expectedBlockBytes, BLOCK_HEADER_SIZE, BLOCK_SIZE); // block data
        Assert.Equal(expectedBlockBytes.Length, block0Bytes.Length);
        Assert.Equal(expectedBlockBytes, block0Bytes);

        // assert layer status
        var layerStatus = layeredStream.GetLayerStatus();
        Assert.Equal(SIZE, layerStatus.Size);
        Assert.Equal(BLOCK_SIZE, layerStatus.LayerSize);
        Assert.Equal(1, layerStatus.AllocatedBlocks);
        Assert.Equal(0, layerStatus.ChangedBlocks);
        Assert.Equal(BLOCK_SIZE, layerStatus.ChangedLayerSize);
        Assert.Equal(0, layerStatus.UnchangedBlocks);
        Assert.Equal(0, layerStatus.UnchangedLayerSize);
    }

    [Theory]
    [InlineData(550)]
    [InlineData(600)]
    [InlineData(700)]
    [InlineData(800)]
    [InlineData(900)]
    [InlineData(1000)]
    public void When_ReadingDataFromTwoBlocks_Then_DataIsReadFromBaseToBlocksInLayer(int offset)
    {
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(SIZE);

        // arrange - empty layer stream
        using var layerStream = new MemoryStream();

        // arrange - layered stream on top of base and layer streams
        using var layeredStream = new LayeredStream(baseStream, layerStream, new LayeredStreamOptions{ BlockSize =  BLOCK_SIZE });

        // arrange - write data to base stream
        for(var blockNumber = 0; blockNumber < NUMBER_OF_BLOCKS; blockNumber++)
        {
            baseStream.Position = blockNumber * BLOCK_SIZE;
            baseStream.Write(this._blockTestDataBytes, 0, BLOCK_SIZE);
        }

        // act - read data at position offset from layered stream
        var blockBytes = new byte[BLOCK_SIZE];
        layeredStream.Position = offset;
        layeredStream.ReadExactly(blockBytes, 0, BLOCK_SIZE);
        
        // assert - read data is equal to base stream data
        var expectedBlockBytes = new byte[BLOCK_SIZE];
        var part1BytesToCopy = BLOCK_SIZE - (offset % BLOCK_SIZE);
        Array.Copy(_blockTestDataBytes, offset % BLOCK_SIZE, expectedBlockBytes, 0, part1BytesToCopy);
        var part2BytesToCopy = BLOCK_SIZE - part1BytesToCopy;
        Array.Copy(_blockTestDataBytes, 0, expectedBlockBytes, part1BytesToCopy, part2BytesToCopy);
        Assert.Equal(expectedBlockBytes.Length, blockBytes.Length);
        Assert.Equal(expectedBlockBytes, blockBytes);

        // assert - layered stream structure is equal to expected size
        const int numberOfBlocks = 2;
        var expectedLayeredStreamSize = LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE + (LONG_SIZE + BLOCK_SIZE) * numberOfBlocks;
        Assert.Equal(expectedLayeredStreamSize, layerStream.Length);
        
        // read block allocation table
        var blockAllocationTableBytes = new byte[BLOCK_ALLOCATION_TABLE_SIZE];
        layerStream.Position = LAYER_HEADER_SIZE;
        layerStream.ReadExactly(blockAllocationTableBytes, 0, BLOCK_ALLOCATION_TABLE_SIZE);
        
        // create expected block allocation table
        var expectedBlockAllocationTableBytes = new byte[BLOCK_ALLOCATION_TABLE_SIZE];

        // write block number 0 offset in expected block allocation table position 0
        var blockOffsetBytes = BitConverter.GetBytes(Convert.ToInt64(LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE));
        var blockAllocationTableOffset = LONG_SIZE + 1; // offset 9
        Array.Copy(blockOffsetBytes, 0, expectedBlockAllocationTableBytes, blockAllocationTableOffset, LONG_SIZE);
        
        // write block number 1 offset in expected block allocation table position 1
        blockOffsetBytes = BitConverter.GetBytes(Convert.ToInt64(LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE + BLOCK_HEADER_SIZE + BLOCK_SIZE));
        blockAllocationTableOffset = (LONG_SIZE + 1) * 2; // offset 18
        Array.Copy(blockOffsetBytes, 0, expectedBlockAllocationTableBytes, blockAllocationTableOffset, LONG_SIZE);
        
        // assert block allocation table
        Assert.Equal(expectedBlockAllocationTableBytes.Length, blockAllocationTableBytes.Length);
        Assert.Equal(expectedBlockAllocationTableBytes, blockAllocationTableBytes);

        // read blocks
        var block1Bytes = new byte[BLOCK_HEADER_SIZE + BLOCK_SIZE];
        layerStream.Position = LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE;
        layerStream.ReadExactly(block1Bytes, 0, block1Bytes.Length);
        var block2Bytes = new byte[BLOCK_HEADER_SIZE + BLOCK_SIZE];
        layerStream.Position = LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE + block1Bytes.Length;
        layerStream.ReadExactly(block2Bytes, 0, block2Bytes.Length);
        
        // assert block 1
        var expectedBlock1Bytes = new byte[BLOCK_HEADER_SIZE + BLOCK_SIZE];
        Array.Copy(BitConverter.GetBytes(1L), 0, expectedBlock1Bytes, 0, LONG_SIZE); // block number
        Array.Copy(_blockTestDataBytes, 0, expectedBlock1Bytes, BLOCK_HEADER_SIZE, _blockTestDataBytes.Length); // block data
        Assert.Equal(expectedBlock1Bytes.Length, block1Bytes.Length);
        Assert.Equal(expectedBlock1Bytes, block1Bytes);
        
        // assert block 2
        var expectedBlock2Bytes = new byte[BLOCK_HEADER_SIZE + BLOCK_SIZE];
        Array.Copy(BitConverter.GetBytes(2L), 0, expectedBlock2Bytes, 0, LONG_SIZE); // block number
        Array.Copy(_blockTestDataBytes, 0, expectedBlock2Bytes, BLOCK_HEADER_SIZE, _blockTestDataBytes.Length); // block data
        Assert.Equal(expectedBlock2Bytes.Length, block2Bytes.Length);
        Assert.Equal(expectedBlock2Bytes, block2Bytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(45)]
    [InlineData(234)]
    [InlineData(389)]
    [InlineData(456)]
    public void When_WritingDataToSingleBlock_Then_DataIsWrittenToBlockInLayer(int offset)
    {
        const int bytesToWrite = 50;
        
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(SIZE);
        
        // arrange - empty layer stream
        using var layerStream = new MemoryStream();
        
        // arrange - layered stream on top of base and layer streams
        using var layeredStream = new LayeredStream(baseStream, layerStream, new LayeredStreamOptions{ BlockSize =  BLOCK_SIZE });
        
        // act - write data at position offset to layered stream
        layeredStream.Position = offset;
        layeredStream.Write(_blockTestDataBytes, offset, bytesToWrite);
        
        // assert - layered stream structure is equal to expected size
        const int numberOfBlocks = 1;
        var expectedLayeredStreamSize = LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE + (LONG_SIZE + BLOCK_SIZE) * numberOfBlocks;
        Assert.Equal(expectedLayeredStreamSize, layerStream.Length);
        
        // read block allocation table
        var blockAllocationTableBytes = new byte[BLOCK_ALLOCATION_TABLE_SIZE];
        layerStream.Position = LAYER_HEADER_SIZE;
        layerStream.ReadExactly(blockAllocationTableBytes, 0, BLOCK_ALLOCATION_TABLE_SIZE);
        
        // create expected block allocation table
        var expectedBlockAllocationTableBytes = new byte[BLOCK_ALLOCATION_TABLE_SIZE];

        // write block number 0 offset in expected block allocation table position 0
        var blockOffsetBytes = BitConverter.GetBytes(Convert.ToInt64(LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE));
        const int blockAllocationTableOffset = 0; // offset 0
        Array.Copy(blockOffsetBytes, 0, expectedBlockAllocationTableBytes, blockAllocationTableOffset, LONG_SIZE);
        expectedBlockAllocationTableBytes[blockAllocationTableOffset + 8] = 1; // mark block as changed
        
        // assert block allocation table
        Assert.Equal(expectedBlockAllocationTableBytes.Length, blockAllocationTableBytes.Length);
        Assert.Equal(expectedBlockAllocationTableBytes, blockAllocationTableBytes);
        
        // read block
        var block0Bytes = new byte[BLOCK_HEADER_SIZE + BLOCK_SIZE];
        layerStream.Position = LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE;
        layerStream.ReadExactly(block0Bytes, 0, block0Bytes.Length);
        
        // assert block
        var expectedBlockBytes = new byte[BLOCK_HEADER_SIZE + BLOCK_SIZE];
        Array.Copy(BitConverter.GetBytes(0L), 0, expectedBlockBytes, 0, LONG_SIZE); // block number
        Array.Copy(_blockTestDataBytes, offset, expectedBlockBytes, 
            BLOCK_HEADER_SIZE + offset, bytesToWrite); // block data
        Assert.Equal(expectedBlockBytes, block0Bytes);
        
        // assert base stream is unchanged
        var baseStreamBytes = new byte[SIZE];
        baseStream.Position = 0;
        baseStream.ReadExactly(baseStreamBytes, 0, SIZE);
        var expectedBaseStreamBytes = new byte[SIZE];
        Assert.Equal(expectedBaseStreamBytes.Length, baseStreamBytes.Length);
        Assert.Equal(expectedBaseStreamBytes, baseStreamBytes);
    }
    
    [Theory]
    [InlineData(550)]
    [InlineData(600)]
    [InlineData(700)]
    [InlineData(800)]
    [InlineData(900)]
    [InlineData(1000)]
    public void When_WritingDataToTwoBlocks_Then_DataIsWrittenToBlocksInLayer(int offset)
    {
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(SIZE);
        
        // arrange - empty layer stream
        using var layerStream = new MemoryStream();
        
        // arrange - layered stream on top of base and layer streams
        using var layeredStream = new LayeredStream(baseStream, layerStream, new LayeredStreamOptions{ BlockSize =  BLOCK_SIZE });
        
        // act - write data at position offset to layered stream, which is larger than block size and causes data to span multiple blocks
        layeredStream.Position = offset;
        layeredStream.Write(_blockTestDataBytes, 0, _blockTestDataBytes.Length);
        
        // assert - layered stream structure is equal to expected size
        const int numberOfBlocks = 2;
        var expectedLayeredStreamSize = LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE + (BLOCK_HEADER_SIZE + BLOCK_SIZE) * numberOfBlocks;
        Assert.Equal(expectedLayeredStreamSize, layerStream.Length);
        
        // read block allocation table
        var blockAllocationTableBytes = new byte[BLOCK_ALLOCATION_TABLE_SIZE];
        layerStream.Position = LAYER_HEADER_SIZE;
        layerStream.ReadExactly(blockAllocationTableBytes, 0, BLOCK_ALLOCATION_TABLE_SIZE);
        
        // create expected block allocation table
        var expectedBlockAllocationTableBytes = new byte[BLOCK_ALLOCATION_TABLE_SIZE];

        // write block number 1 offset in expected block allocation table position 1
        var blockOffsetBytes = BitConverter.GetBytes(Convert.ToInt64(LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE));
        var blockAllocationTableOffset = LONG_SIZE + 1; // offset 9
        Array.Copy(blockOffsetBytes, 0, expectedBlockAllocationTableBytes, blockAllocationTableOffset, LONG_SIZE);
        expectedBlockAllocationTableBytes[blockAllocationTableOffset + 8] = 1; // mark block as changed

        // write block number 2 offset in expected block allocation table position 2
        blockOffsetBytes = BitConverter.GetBytes(Convert.ToInt64(LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE + BLOCK_HEADER_SIZE + BLOCK_SIZE));
        blockAllocationTableOffset = 2 * (LONG_SIZE + 1); // offset 18
        Array.Copy(blockOffsetBytes, 0, expectedBlockAllocationTableBytes, blockAllocationTableOffset, LONG_SIZE);
        expectedBlockAllocationTableBytes[blockAllocationTableOffset + 8] = 1; // mark block as changed

        // assert block allocation table
        Assert.Equal(expectedBlockAllocationTableBytes.Length, blockAllocationTableBytes.Length);
        Assert.Equal(expectedBlockAllocationTableBytes, blockAllocationTableBytes);
        
        // read blocks
        var block1Bytes = new byte[BLOCK_HEADER_SIZE + BLOCK_SIZE];
        layerStream.Position = LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE;
        layerStream.ReadExactly(block1Bytes, 0, block1Bytes.Length);
        var block2Bytes = new byte[BLOCK_HEADER_SIZE + BLOCK_SIZE];
        layerStream.Position = LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE + block1Bytes.Length;
        layerStream.ReadExactly(block2Bytes, 0, block2Bytes.Length);
        
        // assert block 1
        var blockHeaderSize = BLOCK_HEADER_SIZE;
        var expectedBlock1Bytes = new byte[blockHeaderSize + BLOCK_SIZE];
        Array.Copy(BitConverter.GetBytes(1L), 0, expectedBlock1Bytes, 0, LONG_SIZE); // block number
        var blockBytesOffset = offset % BLOCK_SIZE;
        var block1BytesToWrite = BLOCK_SIZE - blockBytesOffset;
        Array.Copy(_blockTestDataBytes, 0, expectedBlock1Bytes, blockHeaderSize + blockBytesOffset, block1BytesToWrite); // block data
        Assert.Equal(expectedBlock1Bytes.Length, block1Bytes.Length);
        Assert.Equal(expectedBlock1Bytes, block1Bytes);
        
        // assert block 2
        var expectedBlock2Bytes = new byte[BLOCK_HEADER_SIZE + BLOCK_SIZE];
        Array.Copy(BitConverter.GetBytes(2L), 0, expectedBlock2Bytes, 0, LONG_SIZE); // block number
        blockBytesOffset = BLOCK_SIZE - (offset % BLOCK_SIZE);
        var block2BytesToWrite = BLOCK_SIZE - blockBytesOffset;
        Array.Copy(_blockTestDataBytes, blockBytesOffset, expectedBlock2Bytes, blockHeaderSize, block2BytesToWrite); // block data
        Assert.Equal(expectedBlock2Bytes.Length, block2Bytes.Length);
        Assert.Equal(expectedBlock2Bytes, block2Bytes);
        
        // assert base stream is unchanged
        var baseStreamBytes = new byte[SIZE];
        baseStream.Position = 0;
        baseStream.ReadExactly(baseStreamBytes, 0, SIZE);
        var expectedBaseStreamBytes = new byte[SIZE];
        Assert.Equal(expectedBaseStreamBytes.Length, baseStreamBytes.Length);
        Assert.Equal(expectedBaseStreamBytes, baseStreamBytes);
    }
    
    [Fact]
    public async Task When_WritingDataToSingleBlockAndFlushLayer_Then_DataIsWrittenFromLayerToBaseStream()
    {
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(SIZE);
        
        // arrange - empty layer stream
        using var layerStream = new MemoryStream();
        
        // arrange - layered stream on top of base and layer streams
        await using var layeredStream = new LayeredStream(baseStream, layerStream, new LayeredStreamOptions{ BlockSize =  BLOCK_SIZE });
        
        // act - write data at position 50 to layered stream
        layeredStream.Position = 50;
        layeredStream.Write(_blockTestDataBytes, 0, 4);
        await layeredStream.FlushLayer();
        
        // assert - layered stream structure is equal to expected size
        const int numberOfBlocks = 1;
        var expectedLayeredStreamSize = LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE + (LONG_SIZE + BLOCK_SIZE) * numberOfBlocks;
        Assert.Equal(expectedLayeredStreamSize, layerStream.Length);
        
        // read block allocation table
        var blockAllocationTableBytes = new byte[BLOCK_ALLOCATION_TABLE_SIZE];
        layerStream.Position = LAYER_HEADER_SIZE;
        await layerStream.ReadExactlyAsync(blockAllocationTableBytes, 0, BLOCK_ALLOCATION_TABLE_SIZE);
        
        // create expected block allocation table
        var expectedBlockAllocationTableBytes = new byte[BLOCK_ALLOCATION_TABLE_SIZE];

        // write block number 0 offset in expected block allocation table position 0
        var blockOffsetBytes = BitConverter.GetBytes(Convert.ToInt64(LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE));
        const int blockAllocationTableOffset = 0; // offset 0
        Array.Copy(blockOffsetBytes, 0, expectedBlockAllocationTableBytes, blockAllocationTableOffset, LONG_SIZE);
        
        // assert block allocation table
        Assert.Equal(expectedBlockAllocationTableBytes.Length, blockAllocationTableBytes.Length);
        Assert.Equal(expectedBlockAllocationTableBytes, blockAllocationTableBytes);
        
        // read block
        var block0Bytes = new byte[BLOCK_HEADER_SIZE + BLOCK_SIZE];
        layerStream.Position = LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE;
        await layerStream.ReadExactlyAsync(block0Bytes, 0, block0Bytes.Length);
        
        // assert block
        var expectedBlockBytes = new byte[BLOCK_HEADER_SIZE + BLOCK_SIZE];
        Array.Copy(BitConverter.GetBytes(0L), 0, expectedBlockBytes, 0, LONG_SIZE); // block number
        const int blockBytesOffset = 50;
        const int blockBytesToWrite = 4;
        Array.Copy(_blockTestDataBytes, 0, expectedBlockBytes, BLOCK_HEADER_SIZE + blockBytesOffset, blockBytesToWrite); // block data
        Assert.Equal(expectedBlockBytes.Length, block0Bytes.Length);
        Assert.Equal(expectedBlockBytes, block0Bytes);
        
        // assert base stream is unchanged
        var baseStreamBytes = new byte[SIZE];
        baseStream.Position = 0;
        await baseStream.ReadExactlyAsync(baseStreamBytes, 0, SIZE);
        var expectedBaseStreamBytes = new byte[SIZE];
        Array.Copy(_blockTestDataBytes, 0, expectedBaseStreamBytes, 50, 4);
        Assert.Equal(expectedBaseStreamBytes.Length, baseStreamBytes.Length);
        Assert.Equal(expectedBaseStreamBytes, baseStreamBytes);
    }

    [Fact]
    public async Task When_WritingDataToSingleBlockBeyondSize_Then_ExceptionIsThrown()
    {
        // arrange - base stream with size
        using var baseStream = new MemoryStream(1024);
        baseStream.SetLength(SIZE);
        
        // arrange - empty layer stream
        using var layerStream = new MemoryStream();
        
        // arrange - layered stream on top of base and layer streams
        await using var layeredStream = new LayeredStream(baseStream, layerStream, new LayeredStreamOptions{ BlockSize =  BLOCK_SIZE });
        
        // act and assert - write data at end of layered stream larger than size throws exception
        layeredStream.Position = SIZE - 50;
        Assert.Throws<IOException>(() => layeredStream.Write(_blockTestDataBytes, 0, 100));
    }
    
    [Fact]
    public async Task When_ReadingDataFromSingleBlockBeyondSize_Then_DataUntilSizeIsRead()
    {
        // arrange - base stream with size
        using var baseStream = new MemoryStream(1024);
        baseStream.SetLength(SIZE);
        
        // arrange - empty layer stream
        using var layerStream = new MemoryStream();
        
        // arrange - layered stream on top of base and layer streams
        await using var layeredStream = new LayeredStream(baseStream, layerStream, new LayeredStreamOptions{ BlockSize =  BLOCK_SIZE });
        
        // act - read data at end of layered stream larger than size
        layeredStream.Position = SIZE - 50;
        var buffer = new byte[100];
        var bytesRead = layeredStream.Read(buffer, 0, 100);
        
        // assert - bytes read is 50
        Assert.Equal(50, bytesRead);
    }

    [Theory]
    [InlineData(2048)]
    [InlineData(10000)]
    [InlineData(15360)]
    [InlineData(28672)]
    [InlineData(40000)]
    public async Task When_ReadingDataFromManyBlocks_Then_DataIsRead(int bufferSize)
    {
        const int size = 512 * 128;
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(size);

        // arrange - empty layer stream
        using var layerStream = new MemoryStream();

        // arrange - layered stream on top of base and layer streams
        await using var layeredStream = new LayeredStream(baseStream, layerStream, new LayeredStreamOptions{ BlockSize =  BLOCK_SIZE });

        // act - read data at position 0 from layered stream
        layeredStream.Position = 0;
        var buffer = new byte[bufferSize];
        var bytesRead = await layeredStream.ReadAsync(buffer);
        
        // assert - bytes read is equal to buffer size
        Assert.Equal(bufferSize, bytesRead);
    }
    
    [Theory]
    [InlineData(2048)]
    [InlineData(10000)]
    [InlineData(15360)]
    [InlineData(28672)]
    [InlineData(40000)]
    public async Task When_ReadingDataFromAllBlocks_Then_DataIsRead(int size)
    {
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(size);

        // arrange - empty layer stream
        using var layerStream = new MemoryStream();

        // arrange - layered stream on top of base and layer streams
        await using var layeredStream = new LayeredStream(baseStream, layerStream, new LayeredStreamOptions{ BlockSize =  BLOCK_SIZE });

        // act - read data at position 0 from layered stream
        layeredStream.Position = 0;
        var buffer = new byte[size];
        var bytesRead = await layeredStream.ReadAsync(buffer);
        
        // assert - bytes read is equal to size
        Assert.Equal(size, bytesRead);
    }
    
    [Fact]
    public void When_FlushLayerOnDisposeIsFalse_Then_LayerIsNotFlushedToBaseStream()
    {
        var options = new LayeredStreamOptions
        {
            BlockSize = BLOCK_SIZE,
            LeaveBaseStreamOpen = true,
            FlushLayerOnDispose = false
        };
        
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(SIZE);
        
        // arrange - empty layer stream
        using var layerStream = new MemoryStream();
        
        // arrange - layered stream on top of base and layer streams
        using (var layeredStream = new LayeredStream(baseStream, layerStream, options))
        {
            // act - write data at position 50 to layered stream
            layeredStream.Position = 50;
            layeredStream.Write(_blockTestDataBytes, 0, 4);
        }
        
        // assert base stream is unchanged
        var baseStreamBytes = new byte[SIZE];
        baseStream.Position = 0;
        baseStream.ReadExactly(baseStreamBytes, 0, SIZE);
        var expectedBaseStreamBytes = new byte[SIZE];
        Assert.Equal(expectedBaseStreamBytes.Length, baseStreamBytes.Length);
        Assert.Equal(expectedBaseStreamBytes, baseStreamBytes);
    }
    
    [Fact]
    public void When_LeavingBaseStreamOpenIsTrue_Then_BaseStreamIsNotClosedOnDispose()
    {
        var options = new LayeredStreamOptions
        {
            BlockSize = BLOCK_SIZE,
            LeaveBaseStreamOpen = true,
            FlushLayerOnDispose = true
        };
        
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(SIZE);
        
        // arrange - empty layer stream
        using var layerStream = new MemoryStream();
        
        // arrange - layered stream on top of base and layer streams
        using (var layeredStream = new LayeredStream(baseStream, layerStream, options))
        {
            // act - write data at position 50 to layered stream
            layeredStream.Position = 50;
            layeredStream.Write(_blockTestDataBytes, 0, 4);
        }
        
        // assert base stream is not closed
        Assert.True(baseStream.CanRead);
    }
    
    [Fact]
    public void When_LeavingLayerStreamOpenIsTrue_Then_LayerStreamIsNotClosedOnDispose()
    {
        var options = new LayeredStreamOptions
        {
            BlockSize = BLOCK_SIZE,
            LeaveLayerStreamOpen = true,
            FlushLayerOnDispose = true
        };
        
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(SIZE);
        
        // arrange - empty layer stream
        using var layerStream = new MemoryStream();
        
        // arrange - layered stream on top of base and layer streams
        using (var layeredStream = new LayeredStream(baseStream, layerStream, options))
        {
            // act - write data at position 50 to layered stream
            layeredStream.Position = 50;
            layeredStream.Write(_blockTestDataBytes, 0, 4);
        }
        
        // assert layer stream is not closed
        Assert.True(layerStream.CanRead);
    }
    
    [Fact]
    public void When_BlockSizeIsLessThan512_Then_ExceptionIsThrown()
    {
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(SIZE);
        
        // arrange - empty layer stream
        using var layerStream = new MemoryStream();
        
        // act and assert - creating layered stream with block size less than 512 throws exception
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new LayeredStream(baseStream, layerStream, new LayeredStreamOptions{ BlockSize = 100 });
        });
    }

    [Theory]
    [InlineData(512)]
    [InlineData(600)]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(3000)]
    public void When_BlockSizeIsSetWritingAndReading_Then_DataIsEqual(int size)
    {
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(SIZE);

        // arrange - empty layer stream
        using var layerStream = new MemoryStream();
        
        // arrange - layered stream on top of base and layer streams
        using var layeredStream = new LayeredStream(baseStream, layerStream, new LayeredStreamOptions{ BlockSize = size });
        
        // act - write data at position 0 to layered stream
        var numberOfBlocks = Convert.ToInt32(baseStream.Length / BLOCK_SIZE);
        for (var blockNumber = 0; blockNumber < numberOfBlocks; blockNumber++)
        {
            layeredStream.Position = blockNumber * BLOCK_SIZE;
            var bytesToWrite = blockNumber == numberOfBlocks - 1 && size % BLOCK_SIZE != 0 ? size % BLOCK_SIZE : BLOCK_SIZE;
            layeredStream.Write(_blockTestDataBytes, 0, bytesToWrite);
        }

        // assert - read data at position 0 from layered stream is equal to written data
        for (var blockNumber = 0; blockNumber < numberOfBlocks; blockNumber++)
        {
            layeredStream.Position = blockNumber * BLOCK_SIZE;
            var bytesToRead = blockNumber == numberOfBlocks - 1 && size % BLOCK_SIZE != 0
                ? size % BLOCK_SIZE
                : BLOCK_SIZE;
            var buffer = new byte[bytesToRead];
            layeredStream.ReadExactly(buffer, 0, bytesToRead);
            var expectedBuffer = new byte[bytesToRead];
            Array.Copy(_blockTestDataBytes, 0, expectedBuffer, 0, bytesToRead);
            Assert.Equal(expectedBuffer, buffer);
        }
    }
}