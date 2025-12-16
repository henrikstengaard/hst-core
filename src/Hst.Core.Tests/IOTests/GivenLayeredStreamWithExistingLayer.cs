using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Hst.Core.IO;
using Xunit;

namespace Hst.Core.Tests.IOTests;

public class GivenLayeredStreamWithExistingLayer
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

    public GivenLayeredStreamWithExistingLayer()
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
    public void When_ReadingDataFromSingleBlock_Then_DataIsReadFromLayer(int offset)
    {
        const int bytesToRead = 50;
        
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(SIZE);

        // arrange - empty layer stream
        using var layerStream = new MemoryStream();
        
        // arrange - layered stream on top of base and layer streams
        using var layeredStream = new LayeredStream(baseStream, layerStream, new LayeredStreamOptions{ BlockSize =  BLOCK_SIZE });
        
        // arrange - write layer header
        layerStream.Write(Encoding.UTF8.GetBytes("HSTL"), 0, 4);
        layerStream.Write(BitConverter.GetBytes(1), 0, 4);
        layerStream.Write(BitConverter.GetBytes((long)SIZE), 0, 8);
        layerStream.Write(BitConverter.GetBytes(BLOCK_SIZE), 0, 4);
        
        // arrange - write block allocation table with first block allocated
        for (var blockNumber = 0; blockNumber < NUMBER_OF_BLOCKS; blockNumber++)
        {
            if (blockNumber == 0)
            {
                layerStream.Write(BitConverter.GetBytes((long)(LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE + blockNumber * (BLOCK_HEADER_SIZE + BLOCK_SIZE))));
                layerStream.WriteByte(1);
                continue;
            }

            // unallocated blocks
            layerStream.Write(new byte[LONG_SIZE]);
            layerStream.WriteByte(0);
        }
        
        // arrange - write first block header and data
        var layerData = new byte[BLOCK_SIZE];
        Array.Copy(_blockTestDataBytes, 0, layerData, 0, BLOCK_SIZE);
        Array.Reverse(layerData);
        layerStream.Write(BitConverter.GetBytes((long)0), 0, 8);
        layerStream.Write(layerData, 0, BLOCK_SIZE);
        
        // act - read data at position offset from layered stream
        var data = new byte[bytesToRead];
        layeredStream.Position = offset;
        layeredStream.ReadExactly(data, 0, bytesToRead);

        // assert - read data is equal to base stream data
        var expectedData = new byte[bytesToRead];
        Array.Copy(layerData, offset, expectedData, 0, bytesToRead);
        Assert.Equal(expectedData, data);
        
        // assert - base stream is still empty
        baseStream.Position = 0;
        var baseStreamData = new byte[SIZE];
        baseStream.ReadExactly(baseStreamData, 0, SIZE);
        var emptyData = new byte[SIZE];
        Assert.Equal(emptyData, baseStreamData);
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
        
        // arrange - write layer header
        layerStream.Write(Encoding.UTF8.GetBytes("HSTL"), 0, 4);
        layerStream.Write(BitConverter.GetBytes(1), 0, 4);
        layerStream.Write(BitConverter.GetBytes((long)SIZE), 0, 8);
        layerStream.Write(BitConverter.GetBytes(BLOCK_SIZE), 0, 4);
        
        // arrange - write block allocation table with first block allocated
        for (var blockNumber = 0; blockNumber < NUMBER_OF_BLOCKS; blockNumber++)
        {
            if (blockNumber < 3)
            {
                layerStream.Write(BitConverter.GetBytes((long)(LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE + blockNumber * (BLOCK_HEADER_SIZE + BLOCK_SIZE))));
                layerStream.WriteByte(1);
                continue;
            }

            // unallocated blocks
            layerStream.Write(new byte[LONG_SIZE]);
            layerStream.WriteByte(0);
        }
        
        // arrange - write first block header and data
        layerStream.Write(BitConverter.GetBytes(0L), 0, 8);
        layerStream.Write(_blockTestDataBytes, 0, BLOCK_SIZE);
        
        // arrange - write second block header and data
        layerStream.Write(BitConverter.GetBytes(1L), 0, 8);
        layerStream.Write(_blockTestDataBytes, 0, BLOCK_SIZE);

        // arrange - write third block header and data
        layerStream.Write(BitConverter.GetBytes(2L), 0, 8);
        layerStream.Write(_blockTestDataBytes, 0, BLOCK_SIZE);
        
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
        Assert.Equal(expectedBlockBytes, blockBytes);
        
        // assert - base stream is still empty
        baseStream.Position = 0;
        var baseStreamData = new byte[SIZE];
        baseStream.ReadExactly(baseStreamData, 0, SIZE);
        var emptyData = new byte[SIZE];
        Assert.Equal(emptyData, baseStreamData);
    }

    [Theory]
    [InlineData(2048)]
    [InlineData(10000)]
    [InlineData(15360)]
    [InlineData(28672)]
    [InlineData(40000)]
    public void When_ReadingDataFromAllBlocks_Then_DataIsRead(int size)
    {
        var numberOfBlocks = Convert.ToInt32(Math.Ceiling((double)size / BLOCK_SIZE));
        var blockAllocationTableSize = numberOfBlocks * (LONG_SIZE + 1);
        
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(size);

        // arrange - empty layer stream
        using var layerStream = new MemoryStream();

        // arrange - layered stream on top of base and layer streams
        using var layeredStream = new LayeredStream(baseStream, layerStream, new LayeredStreamOptions{ BlockSize =  BLOCK_SIZE });
        
        // arrange - write layer header
        layerStream.Write(Encoding.UTF8.GetBytes("HSTL"), 0, 4);
        layerStream.Write(BitConverter.GetBytes(1), 0, 4);
        layerStream.Write(BitConverter.GetBytes((long)size), 0, 8);
        layerStream.Write(BitConverter.GetBytes(BLOCK_SIZE), 0, 4);
        
        // arrange - write block allocation table
        for (var blockNumber = 0; blockNumber < numberOfBlocks; blockNumber++)
        {
            layerStream.Write(BitConverter.GetBytes((long)(LAYER_HEADER_SIZE + blockAllocationTableSize + blockNumber * (BLOCK_HEADER_SIZE + BLOCK_SIZE))));
            layerStream.WriteByte(1);
        }
        
        // arrange - write all block headers and data
        for (var blockNumber = 0; blockNumber < numberOfBlocks; blockNumber++)
        {
            layerStream.Write(BitConverter.GetBytes((long)blockNumber), 0, 8);
            var bytesToWrite = blockNumber == numberOfBlocks - 1 && size % BLOCK_SIZE != 0 ? size % BLOCK_SIZE : BLOCK_SIZE;
            layerStream.Write(_blockTestDataBytes, 0, bytesToWrite);
        }
        
        // act - read data at position 0 from layered stream
        layeredStream.Position = 0;
        var actualData = new byte[size];
        layeredStream.ReadExactly(actualData);
        
        // assert - buffer data is equal to base stream data
        var expectedData = new byte[size];
        var blocksToRead = Convert.ToInt32(Math.Ceiling((double)size / BLOCK_SIZE));
        for (var blockNumber = 0; blockNumber < blocksToRead; blockNumber++)
        {
            var bytesToCopy = blockNumber == blocksToRead - 1 && size % BLOCK_SIZE != 0 ? size % BLOCK_SIZE : BLOCK_SIZE;
            Array.Copy(_blockTestDataBytes, 0, expectedData, blockNumber * BLOCK_SIZE, bytesToCopy);
        }
        Assert.Equal(expectedData, actualData);
    }
    
    [Fact]
    public async Task When_FlushLayerWithChanges_Then_DataIsFlushedFromLayerToBaseStream()
    {
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(SIZE);

        // arrange - empty layer stream
        using var layerStream = new MemoryStream();

        // arrange - layered stream on top of base and layer streams
        await using var layeredStream = new LayeredStream(baseStream, layerStream, new LayeredStreamOptions{ BlockSize =  BLOCK_SIZE });
        
        // arrange - write layer header
        layerStream.Write(Encoding.UTF8.GetBytes("HSTL"), 0, 4);
        layerStream.Write(BitConverter.GetBytes(1), 0, 4);
        layerStream.Write(BitConverter.GetBytes((long)SIZE), 0, 8);
        layerStream.Write(BitConverter.GetBytes(BLOCK_SIZE), 0, 4);
        
        // arrange - write block allocation table
        for (var blockNumber = 0; blockNumber < NUMBER_OF_BLOCKS; blockNumber++)
        {
            layerStream.Write(BitConverter.GetBytes((long)(LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE + blockNumber * (BLOCK_HEADER_SIZE + BLOCK_SIZE))));
            layerStream.WriteByte(1);
        }

        // arrange - write all block headers and data
        for (var blockNumber = 0; blockNumber < NUMBER_OF_BLOCKS; blockNumber++)
        {
            layerStream.Write(BitConverter.GetBytes((long)blockNumber), 0, 8);
            layerStream.Write(_blockTestDataBytes, 0, BLOCK_SIZE);
        }
        
        // act - flush layered stream
        await layeredStream.FlushLayer();
        
        // assert - block read from base stream is equal to block test data bytes
        for (var blockNumber = 0; blockNumber < NUMBER_OF_BLOCKS; blockNumber++)
        {
            var baseStreamBlockBytes = new byte[BLOCK_SIZE];
            baseStream.Position = blockNumber * BLOCK_SIZE;
            await baseStream.ReadExactlyAsync(baseStreamBlockBytes, 0, BLOCK_SIZE);
            Assert.Equal(_blockTestDataBytes, baseStreamBlockBytes);
        }
    }

    [Fact]
    public async Task When_FlushLayerWithoutChanges_Then_DataIsFlushedFromLayerToBaseStream()
    {
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(SIZE);

        // arrange - empty layer stream
        using var layerStream = new MemoryStream();

        // arrange - layered stream on top of base and layer streams
        await using var layeredStream = new LayeredStream(baseStream, layerStream, new LayeredStreamOptions{ BlockSize =  BLOCK_SIZE });
        
        // arrange - write layer header
        layerStream.Write(Encoding.UTF8.GetBytes("HSTL"), 0, 4);
        layerStream.Write(BitConverter.GetBytes(1), 0, 4);
        layerStream.Write(BitConverter.GetBytes((long)SIZE), 0, 8);
        layerStream.Write(BitConverter.GetBytes(BLOCK_SIZE), 0, 4);
        
        // arrange - write block allocation table with all blocks allocated and unchanged
        for (var blockNumber = 0; blockNumber < NUMBER_OF_BLOCKS; blockNumber++)
        {
            layerStream.Write(BitConverter.GetBytes((long)(LAYER_HEADER_SIZE + BLOCK_ALLOCATION_TABLE_SIZE + blockNumber * (BLOCK_HEADER_SIZE + BLOCK_SIZE))));
            layerStream.WriteByte(0);
        }

        // arrange - write all block headers and data
        for (var blockNumber = 0; blockNumber < NUMBER_OF_BLOCKS; blockNumber++)
        {
            layerStream.Write(BitConverter.GetBytes((long)blockNumber), 0, 8);
            layerStream.Write(_blockTestDataBytes, 0, BLOCK_SIZE);
        }
        
        // act - flush layered stream
        await layeredStream.FlushLayer();
        
        // assert - block read from base stream is equal to zeroes
        var actualData = new byte[SIZE];
        baseStream.Position = 0;
        await baseStream.ReadExactlyAsync(actualData, 0, SIZE);
        var expectedData = new byte[SIZE];
        Assert.Equal(expectedData, actualData);
    }
    
    [Fact]
    public void When_ReadingDataFromLayerSizeNotEqualToBase_Then_ExceptionIsThrown()
    {
        const int bytesToRead = 1000;
        
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(bytesToRead);

        // arrange - empty layer stream
        using var layerStream = new MemoryStream();

        // arrange - layered stream on top of base and layer streams
        var layeredStream = new LayeredStream(baseStream, layerStream, new LayeredStreamOptions{ BlockSize =  BLOCK_SIZE });
        
        // arrange - write layer header
        layerStream.Write(Encoding.UTF8.GetBytes("HSTL"), 0, 4);
        layerStream.Write(BitConverter.GetBytes(1), 0, 4);
        layerStream.Write(BitConverter.GetBytes((long)SIZE), 0, 8);
        layerStream.Write(BitConverter.GetBytes(BLOCK_SIZE), 0, 4);
        
        // arrange - write block allocation table
        for (var blockNumber = 0; blockNumber < NUMBER_OF_BLOCKS; blockNumber++)
        {
            layerStream.Write(new byte[LONG_SIZE]);
            layerStream.WriteByte(0);
        }
        
        // act - read data at position 0 from layered stream
        layeredStream.Position = 0;
        var buffer = new byte[bytesToRead];
        Assert.Throws<IOException>(() => layeredStream.ReadExactly(buffer));
    }
}