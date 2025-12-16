# Hst.Core.IO

## Layered stream

This directory contains classes for layered stream operations that enables reading and writing data to a layer stream on top of another stream. The layer stream can be used as a buffer for reading and writing data to the underlying stream to improve performance and reduce the number of I/O operations to the underlying stream. It can also be used to make revertible changes as changes can be reverted by discarding the layer stream without affecting the underlying stream when flush layer on dispose is set to false.

### Structure of layer stream

The layer stream is composed of a block allocation table and blocks of data that are read from or written to the underlying stream.
The size of the block allocation table is determined by the size of the underlying stream and the size of the blocks.
The block allocation table contains entries for each block that exists in the layer stream and indicates whether the block has been changed or not compared to the underlying stream.

When reading from or writing to the layered stream, the block is first read from the underlying base stream if it doesn't exist in the layer stream and added to the layer stream. Then the data is read from or written to the layer stream.
When writing to the layered stream, the block is marked as changed in the layer stream and the data is written to the layer stream.

The layered stream can be flushed to the underlying stream by writing all changed blocks from the layer stream to the underlying stream. This sets all the changed flags in the block allocation table to not changed.

The header of layer stream is structured as follows:

| Offset | Size | Description                                                 |
|-------| --- |-------------------------------------------------------------|
| 0x0   | 4 | Magic number "HSTL"                                         |
| 0x4   | 4 | Version (currently 1)                                       |
| 0x8   | 8 | Long - Size of the underlying base stream                   |
| 0x10  | 4 | Int - Size of blocks                                        |

The block allocation table is structured as follows for each block:

| Offset | Size | Description                                                 |
|-------| --- |-------------------------------------------------------------|
| 0x14  | 8 | Long - Block offset in the layer stream                     |
| 0x1C  | 1 | Byte - Block is changed flag (0 = not changed, 1 = changed) |
| ...   | ... | Repeated for each block             |

The blocks of data are structured as follows for each block:
| Offset | Size | Description                                                 |
|-------| --- |-------------------------------------------------------------|
| ... | 8 | Long - Block number |
| ... | N | Byte - Block data |
| ... | ... | Repeated for each block when allocated             |
### Usage

Example of using layered stream to apply changes to a file when disposing:

```csharp
// open base stream and layer stream to create layered stream
await using var baseStream = File.Open("data.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);
await using var layerStream = File.Open("data.hstl", FileMode.OpenOrCreate, FileAccess.ReadWrite);
await using var layeredStream = new LayeredStream(baseStream, layerStream);

// read data from layered stream
var buffer = new byte[512];
await layeredStream.ReadExactlyAsync(buffer, 0, buffer.Length);

// make changes to data
for (int i = 0; i < buffer.Length; i++)
{
    buffer[i] = 1;
}

// write changes to layered stream
await layeredStream.Seek(0, SeekOrigin.Begin);
await layeredStream.WriteAsync(buffer, 0, buffer.Length);

// changes in layered stream are flushed to base stream on dispose
``` 

Example of using layered stream to make revertible changes to a file:

```csharp
// open base stream and layer stream to create layered stream
await using var baseStream = File.Open("data.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite);
await using var layerStream = File.Open("data.hstl", FileMode.OpenOrCreate, FileAccess.ReadWrite);
await using var layeredStream = new LayeredStream(baseStream, layerStream, new LayeredStreamOptions
{
    FlushLayerOnDispose = false // do not flush changes to base stream on dispose
});

// read data from layered stream
var buffer = new byte[512];
await layeredStream.ReadExactlyAsync(buffer, 0, buffer.Length);

// make changes to data
for (int i = 0; i < buffer.Length; i++)
{
    buffer[i] = 1;
}

// write changes to layered stream
await layeredStream.Seek(0, SeekOrigin.Begin);
await layeredStream.WriteAsync(buffer, 0, buffer.Length);

// changes in layered stream can be discarded and the base stream remains unchanged when disposed
``` 