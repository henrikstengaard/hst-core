using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace Hst.Core.IO
{
    /// <summary>
    /// Layered stream is a stream that applies changes on top of a base stream using a layer stream.
    /// The layer stream stores only the changed blocks of data for optimized and efficient storage of modifications.
    /// Only fixed size base streams are supported.
    /// </summary>
    public class LayeredStream : Stream
    {
        public class LayerStatus
        {
            /// <summary>
            /// Size of base stream
            /// </summary>
            public long Size { get; set; }

            /// <summary>
            /// Number of blocks in base stream
            /// </summary>
            public int Blocks { get; set; }

            /// <summary>
            /// Size of layer stream
            /// </summary>
            public long LayerSize { get; set; }
            
            /// <summary>
            /// Number of allocated blocks in layer stream
            /// </summary>
            public int AllocatedBlocks { get; set; }

            /// <summary>
            /// Number of changed blocks in layer stream
            /// </summary>
            public int ChangedBlocks { get; set; }

            /// <summary>
            /// Size of changed blocks in layer stream
            /// </summary>
            public long ChangedLayerSize { get; set; }
            
            /// <summary>
            /// Number of unchanged blocks in layer stream
            /// </summary>
            public int UnchangedBlocks { get; set; }

            /// <summary>
            /// Size of unchanged blocks in layer stream
            /// </summary>
            public long UnchangedLayerSize { get; set; }
        }
        
        public class DataFlushedEventArgs : EventArgs
        {
            public DataFlushedEventArgs(double percentComplete, long bytesProcessed, long bytesRemaining, long bytesTotal,
                TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal, long bytesPerSecond)
            {
                PercentComplete = percentComplete;
                BytesProcessed = bytesProcessed;
                BytesRemaining = bytesRemaining;
                BytesTotal = bytesTotal;
                TimeElapsed = timeElapsed;
                TimeRemaining = timeRemaining;
                TimeTotal = timeTotal;
                BytesPerSecond = bytesPerSecond;
            }

            public readonly double PercentComplete;
            public readonly long BytesProcessed;
            public readonly long BytesRemaining;
            public readonly long BytesTotal;
            public readonly TimeSpan TimeElapsed;
            public readonly TimeSpan TimeRemaining;
            public readonly TimeSpan TimeTotal;
            public readonly long BytesPerSecond;
        }

        private class AllocatedBlock
        {
            /// <summary>
            /// Number of block
            /// </summary>
#if NETSTANDARD2_0
            public long Number { get; set; }
#else
            public long Number { get; init; }
#endif

            /// <summary>
            /// Base offset of block in base stream
            /// </summary>
#if NETSTANDARD2_0
            public long BaseOffset { get; set; }
#else
            public long BaseOffset { get; init; }
#endif
            
            /// <summary>
            /// Layer offset of block in layer stream
            /// </summary>
#if NETSTANDARD2_0
            public long LayerOffset { get; set; }
#else
            public long LayerOffset { get; init; }
#endif
            
            /// <summary>
            /// Size of block data
            /// </summary>
            public int Size { get; set; }
        
            /// <summary>
            /// Is block changed
            /// </summary>
            public bool IsChanged { get; set; }
        
            /// <summary>
            /// Number of reads from block
            /// </summary>
            public int ReadCount { get; set; }
        
            /// <summary>
            /// Number of writes to block
            /// </summary>
            public int WriteCount { get; set; }
        }

        private const string MAGIC = "HSTL";
        private const int VERSION = 1;
        private const int LAYER_HEADER_SIZE = 4 + 4 + 8 + 4; // magic (4 bytes) + version (4 bytes) + size (8 bytes) + block size (8 bytes)
        private const int BLOCK_HEADER_SIZE = 8; // block number (8 bytes)

        private readonly Timer _timer = new Timer();
        private DataFlushedEventArgs _dataFlushedEventArgs;
        private readonly Stream _baseStream;
        private readonly Stream _layerStream;
        private readonly LayeredStreamOptions _options;
        private readonly long _size;
        private readonly int _numberOfBlocks;
        private readonly IDictionary<long, AllocatedBlock> _blockAllocationTable;
        private readonly byte[] _blockBuffer;
    
        private bool _isInitialized;
        private long _position;
        private long _nextAvailableBlockLayerOffset;

        public bool IsDisposed { get; private set; }

        public event EventHandler<DataFlushedEventArgs> DataFlushed;

        /// <summary>
        /// Creates layered stream applying layer stream on top of base stream with default options.
        /// </summary>
        /// <param name="baseStream">Base stream to apply layer.</param>
        /// <param name="layerStream">Layer stream to use as layer.</param>
        public LayeredStream(Stream baseStream, Stream layerStream)
            : this(baseStream, layerStream, new LayeredStreamOptions())
        {
        }

        /// <summary>
        /// Creates layered stream applying layer stream on top of base stream.
        /// </summary>
        /// <param name="baseStream">Base stream to apply layer.</param>
        /// <param name="layerStream">Layer stream to use as layer.</param>
        /// <param name="options">Layered stream options.</param>
        public LayeredStream(Stream baseStream, Stream layerStream, LayeredStreamOptions options)
        {
            if (baseStream == null)
            {
                throw new ArgumentNullException(nameof(baseStream));
            }
            
            if (layerStream == null)
            {
                throw new ArgumentNullException(nameof(layerStream));
            }
            
            if (!baseStream.CanRead || !baseStream.CanSeek)
            {
                throw new ArgumentException("Base stream must support read and seek", nameof(baseStream));
            }

            if (!layerStream.CanRead || !layerStream.CanSeek || !layerStream.CanWrite)
            {
                throw new ArgumentException("Layer stream must support read, seek and write", nameof(layerStream));
            }
            
            if (options.BlockSize < 512)
            {
                throw new ArgumentOutOfRangeException(nameof(options.BlockSize), "Block size must be at least 512 bytes");
            }
            
            _baseStream = baseStream;
            _layerStream = layerStream;
            _options = options;
            _size = baseStream.Length;
            _blockBuffer = new byte[options.BlockSize];
            _numberOfBlocks = Convert.ToInt32(Math.Ceiling((double)_size / options.BlockSize));
            var blockAllocationTableSize = _numberOfBlocks * 9;
            _blockAllocationTable = new Dictionary<long, AllocatedBlock>();
            
            // set next available block layer offset after header and block allocation table
            _nextAvailableBlockLayerOffset = LAYER_HEADER_SIZE + blockAllocationTableSize;
            
            _timer.Enabled = true;
            _timer.Interval = 1000;
            _timer.Elapsed += SendDataFlushed;
            _dataFlushedEventArgs = null;
        }

        private void SendDataFlushed(object sender, EventArgs args)
        {
            if (_dataFlushedEventArgs == null)
            {
                return;
            }

            DataFlushed?.Invoke(this, _dataFlushedEventArgs);
            _dataFlushedEventArgs = null;
        }
        
        public LayerStatus GetLayerStatus()
        {
            var layerSize = 0L;
            var changedBlocks = 0;
            var changedLayerSize = 0L;
            var unchangedBlocks = 0;
            var unchangedLayerSize = 0L;
            
            foreach (var item in this._blockAllocationTable)
            {
                layerSize += item.Value.Size;
                changedBlocks += item.Value.IsChanged ? 1 : 0;
                changedLayerSize += item.Value.IsChanged ? item.Value.Size : 0;
                unchangedBlocks += item.Value.IsChanged ? 0 : 1;
                unchangedLayerSize += item.Value.IsChanged ? 0 : item.Value.Size;
            }
            
            return new LayerStatus
            {
                Size = _size,
                Blocks = _numberOfBlocks,
                LayerSize = layerSize,
                AllocatedBlocks = _blockAllocationTable.Count,
                ChangedBlocks = changedBlocks,
                ChangedLayerSize = changedLayerSize,
                UnchangedBlocks = unchangedBlocks,
                UnchangedLayerSize = unchangedLayerSize
            };
        }
        
        private void OnDataFlushed(double percentComplete, long bytesProcessed, long bytesRemaining, long bytesTotal,
            TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal, long bytesPerSecond)
        {
            _dataFlushedEventArgs = new DataFlushedEventArgs(percentComplete, bytesProcessed, bytesRemaining,
                bytesTotal, timeElapsed, timeRemaining, timeTotal, bytesPerSecond);
            
            if (percentComplete >= 100)
            {
                SendDataFlushed(this, EventArgs.Empty);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
            {
                return;
            }
        
            if (disposing)
            {
                _timer.Elapsed -= SendDataFlushed;
                _timer.Stop();
                SendDataFlushed(this, EventArgs.Empty);

                if (_options.FlushLayerOnDispose)
                {
                    FlushLayer().GetAwaiter().GetResult();
                }

                if (!_options.LeaveLayerStreamOpen)
                {
                    _layerStream.Close();
                    _layerStream.Dispose();
                }
            
                if (!_options.LeaveBaseStreamOpen)
                {
                    _baseStream.Close();
                    _baseStream.Dispose();
                }
            }
            base.Dispose(disposing);
        
            IsDisposed = true;
        }

        private void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }
            
            if (_layerStream.Length == 0)
            {
                CreateNewLayer();
                _isInitialized = true;
                return;
            }
            
            ReadExistingLayer();
            _isInitialized = true;
        }

        private void CreateNewLayer()
        {
            // write header
#if NETSTANDARD2_0
            _layerStream.Write(Encoding.ASCII.GetBytes(MAGIC), 0, 4);
            _layerStream.Write(BitConverter.GetBytes(VERSION), 0, 4);
            _layerStream.Write(BitConverter.GetBytes(_size), 0, 8);
            _layerStream.Write(BitConverter.GetBytes(_options.BlockSize), 0, 4);
#else
            _layerStream.Write(Encoding.ASCII.GetBytes(MAGIC));
            _layerStream.Write(BitConverter.GetBytes(VERSION));
            _layerStream.Write(BitConverter.GetBytes(_size));
            _layerStream.Write(BitConverter.GetBytes(_options.BlockSize));
#endif

            // initialize block allocation table with zeros for block layer offsets
            for (var i = 0; i < _numberOfBlocks; i++)
            {
#if NETSTANDARD2_0
                _layerStream.Write(BitConverter.GetBytes(0L), 0, 8); // block layer offset
#else
                _layerStream.Write(BitConverter.GetBytes(0L)); // block layer offset
#endif
                _layerStream.WriteByte(0); // is changed
            }
        }
        
        private void ReadExistingLayer()
        {
            // read layer header
            var layerHeaderBytes = new byte[LAYER_HEADER_SIZE];
            _layerStream.Seek(0, SeekOrigin.Begin);
#if NETSTANDARD2_0
            var headerBytesRead = _layerStream.Read(layerHeaderBytes, 0, LAYER_HEADER_SIZE);
            if (headerBytesRead != LAYER_HEADER_SIZE)
            {
                throw new IOException("Failed to read layered stream header");
            }
#else
            _layerStream.ReadExactly(layerHeaderBytes, 0, LAYER_HEADER_SIZE);
#endif

            // validate magic
            var magic = Encoding.ASCII.GetString(layerHeaderBytes, 0, 4);
            if (magic != MAGIC)
            {
                throw new IOException("Invalid layered stream magic");
            }

            // validate version
            if (BitConverter.ToInt32(layerHeaderBytes, 4) != VERSION)
            {
                throw new IOException("Invalid layered stream version");
            }
            
            // validate size
            if (BitConverter.ToInt64(layerHeaderBytes, 8) != _size)
            {
                throw new IOException("Invalid layered stream size");
            }
            
            // validate block size
            if (BitConverter.ToInt32(layerHeaderBytes, 16) != _options.BlockSize)
            {
                throw new IOException("Invalid layered stream block size");
            }
            
            // read block allocation table
            for (var blockNumber = 0; blockNumber < _numberOfBlocks; blockNumber++)
            {
                // read block layer offset
                var blockLayerOffsetBytes = new byte[8];
#if NETSTANDARD2_0
                var bytesRead = _layerStream.Read(blockLayerOffsetBytes, 0, 8);
                if (bytesRead != 8)
                {
                    throw new IOException("Failed to read block layer offset from layer");
                }
#else
                _layerStream.ReadExactly(blockLayerOffsetBytes, 0, 8);
#endif
                var blockLayerOffset = BitConverter.ToInt64(blockLayerOffsetBytes, 0);

                // read is changed flag
                var isChanged = _layerStream.ReadByte() > 0;
                
                // skip unallocated blocks, which is indicated by block layer offset 0
                if (blockLayerOffset == 0)
                {
                    continue;
                }
                
                // add allocated block to block allocation table
                var allocatedBlock = new AllocatedBlock
                {
                    Number = blockNumber,
                    BaseOffset = CalculateBlockOffset(blockNumber),
                    LayerOffset = blockLayerOffset,
                    Size = CalculateBlockSize(blockNumber),
                    IsChanged = isChanged
                };
                _blockAllocationTable.Add(blockNumber, allocatedBlock);
                
                // update next available block layer offset
                _nextAvailableBlockLayerOffset = blockLayerOffset + BLOCK_HEADER_SIZE + _options.BlockSize;
            }
        }

        /// <summary>
        /// Flushes changed blocks from layer stream to base stream.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <exception cref="IOException"></exception>
        public async Task FlushLayer(CancellationToken cancellationToken = default)
        {
            Initialize();
            
            await _layerStream.FlushAsync(cancellationToken);
            
            if (!_baseStream.CanWrite)
            {
                return;
            }

            var allocatedBlocks = _blockAllocationTable.Values.Where(block => block.IsChanged).ToList();

            if (allocatedBlocks.Count == 0)
            {
                return;
            }
            
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            
            await _baseStream.FlushAsync(cancellationToken);
            
            var bytesProcessed = 0L;
            var bytesTotal = allocatedBlocks.Sum(b => (long)b.Size);
            OnDataFlushed(0, 0, bytesTotal, bytesTotal, 
                TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, 0);
        
            foreach (var allocatedBlock in allocatedBlocks)
            {
                // seek to block offset in layer stream
                _layerStream.Position = allocatedBlock.LayerOffset;
            
                // read block number from layer stream 
                var blockNumberBytes = new byte[8];
#if NETSTANDARD2_0
                var blockNumberBytesRead = await _layerStream.ReadAsync(blockNumberBytes, 0, 8, cancellationToken);
                if (blockNumberBytesRead != 8)
                {
                    throw new IOException("Failed to block number from layer during flush");
                }
#else
                await _layerStream.ReadExactlyAsync(blockNumberBytes, 0, 8, cancellationToken);
#endif
                var blockNumber = BitConverter.ToInt64(blockNumberBytes, 0);
            
                // validate block number
                if (blockNumber != allocatedBlock.Number)
                {
                    throw new IOException($"Unexpected block number {blockNumber} while flush changed block number {allocatedBlock.Number}");
                }

                // read block data from layer stream
#if NETSTANDARD2_0
                var layerBlockBytesRead = await _layerStream.ReadAsync(_blockBuffer, 0, allocatedBlock.Size, cancellationToken);
                if (layerBlockBytesRead != allocatedBlock.Size)
                {
                    throw new IOException("Failed to read block data from layer during flush");
                }
#else
                await _layerStream.ReadExactlyAsync(_blockBuffer, 0, allocatedBlock.Size, cancellationToken);
#endif
            
                // write block data to base stream
                _baseStream.Position = allocatedBlock.Number * _options.BlockSize;
#if NETSTANDARD2_0
                await _baseStream.WriteAsync(_blockBuffer, 0, allocatedBlock.Size, cancellationToken);
#else
                await _baseStream.WriteAsync(_blockBuffer.AsMemory(0, allocatedBlock.Size), cancellationToken);
#endif

                // reset is changed flag in layer stream block allocation table to indicate block has been flushed
                _layerStream.Position = LAYER_HEADER_SIZE + allocatedBlock.Number * 9 + 8;
                _layerStream.WriteByte(0);
                
                // reset allocated block change state
                allocatedBlock.IsChanged = false;
                allocatedBlock.ReadCount = 0;
                allocatedBlock.WriteCount = 0;

                bytesProcessed += allocatedBlock.Size;
                var bytesRemaining = bytesTotal == 0 ? 0 : bytesTotal - bytesProcessed;
                var percentComplete = bytesTotal == 0 || bytesProcessed == 0 ? 0 : Math.Round((double)100 / bytesTotal * bytesProcessed, 1);
                var timeElapsed = stopwatch.Elapsed;
                var timeRemaining = bytesTotal == 0 ? TimeSpan.Zero : CalculateTimeRemaining(percentComplete, timeElapsed);
                var timeTotal = bytesTotal == 0 ? TimeSpan.Zero : timeElapsed + timeRemaining;
                var bytesPerSecond = Convert.ToInt64(bytesProcessed / timeElapsed.TotalSeconds);

                OnDataFlushed(percentComplete, bytesProcessed, bytesRemaining, bytesTotal, timeElapsed, timeRemaining,
                    timeTotal, bytesPerSecond);
            }
            
            stopwatch.Stop();
            
            OnDataFlushed(100, bytesProcessed, 0, bytesTotal, stopwatch.Elapsed,
                TimeSpan.Zero, stopwatch.Elapsed, 0);
        }
        
        private static TimeSpan CalculateTimeRemaining(double percentComplete, TimeSpan timeElapsed)
        {
            return percentComplete > 0
                ? TimeSpan.FromMilliseconds(timeElapsed.TotalMilliseconds / percentComplete *
                                            (100 - percentComplete))
                : TimeSpan.Zero;
        }

        private AllocatedBlock ReadBlockFromBaseToLayer(long blockNumber)
        {
            var blockSize = CalculateBlockSize(blockNumber);
            var baseBlockOffset = CalculateBlockOffset(blockNumber);
            
            // seek to block offset in base stream
            _baseStream.Position = baseBlockOffset;

            // read block from base stream
            var bytesRead = _baseStream.Read(_blockBuffer, 0, blockSize);
        
            // write block to layer stream at next available block offset
            _layerStream.Position = _nextAvailableBlockLayerOffset;
#if NETSTANDARD2_0
            _layerStream.Write(BitConverter.GetBytes(blockNumber), 0, 8); // block number
#else
            _layerStream.Write(BitConverter.GetBytes(blockNumber)); // block number
#endif
            _layerStream.Write(_blockBuffer, 0, bytesRead);

            // update block allocation table with new allocated block
            var allocatedBlock = new AllocatedBlock
            {
                Number = blockNumber,
                BaseOffset = baseBlockOffset,
                LayerOffset = _nextAvailableBlockLayerOffset,
                Size = bytesRead,
                IsChanged = false
            };
            _blockAllocationTable[blockNumber] = allocatedBlock;
        
            // update next available block layer offset
            _nextAvailableBlockLayerOffset += BLOCK_HEADER_SIZE + _options.BlockSize;
        
            // update block allocation table in layer stream
            _layerStream.Seek(LAYER_HEADER_SIZE + blockNumber * 9, SeekOrigin.Begin);
#if NETSTANDARD2_0
            _layerStream.Write(BitConverter.GetBytes(allocatedBlock.LayerOffset), 0, 8);
#else
            _layerStream.Write(BitConverter.GetBytes(allocatedBlock.LayerOffset));
#endif
            return allocatedBlock;
        }

        /// <summary>
        /// Get allocated block for given block number, reading from base stream to layer stream if not allocated yet.
        /// </summary>
        /// <param name="blockNumber">Block number to get allocated block for.</param>
        /// <returns>Allocated block.</returns>
        private AllocatedBlock GetAllocatedBlock(long blockNumber)
        {
            return !_blockAllocationTable.TryGetValue(blockNumber, out var value)
                ? ReadBlockFromBaseToLayer(blockNumber) : value;
        }

        private int CalculateBlockSize(long blockNumber)
        {
            if (blockNumber >= _numberOfBlocks)
            {
                throw new ArgumentOutOfRangeException(nameof(blockNumber), "Block number out of range");
            }
            
            // calculate remaining bytes in last block
            var remainingBlockBytes = _size % _options.BlockSize;
            return blockNumber == _numberOfBlocks - 1 && remainingBlockBytes != 0
                ? Convert.ToInt32(remainingBlockBytes) : _options.BlockSize;
        }
        
        private long CalculateBlockNumber => Convert.ToInt64(Math.Floor((double)_position / _options.BlockSize));
        
        /// <summary>
        /// Calculates block offset in base stream for given block number.
        /// </summary>
        /// <param name="blockNumber">Block number to calculate offset for.</param>
        /// <returns>Block offset in base stream.</returns>
        private long CalculateBlockOffset(long blockNumber) => blockNumber * _options.BlockSize;

        public override void Flush()
        {
            _layerStream.Flush();
        }
        
        public override int Read(byte[] buffer, int offset, int count)
        {
            Initialize();
            
            var blockNumber = CalculateBlockNumber;
        
            if (blockNumber >= _numberOfBlocks)
            {
                return 0;
            }
        
            var bufferPosition = 0;
            var bytesRead = 0;

            int blockBytesRead;
            do
            {
                // get allocated block
                var allocatedBlock = GetAllocatedBlock(blockNumber);
            
                // calculate position in block
                var positionInBlock = Convert.ToInt32(_position % _options.BlockSize);
            
                // determine bytes to read
                var bytesToRead = Math.Min(count - bytesRead, _options.BlockSize - positionInBlock);
                _layerStream.Position = allocatedBlock.LayerOffset + BLOCK_HEADER_SIZE + positionInBlock;
                blockBytesRead = _layerStream.Read(buffer, offset + bufferPosition, bytesToRead);
                allocatedBlock.ReadCount++;
                _position += blockBytesRead;
                bufferPosition += blockBytesRead;
                bytesRead += blockBytesRead;
                blockNumber++;
            } while (bytesRead < count && blockBytesRead > 0 && blockNumber < _numberOfBlocks);

            return bufferPosition;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _position = offset;
                    break;
                case SeekOrigin.Current:
                    _position += offset;
                    break;
                case SeekOrigin.End:
                    _position = _size + offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
            }

            return _position;
        }
        
        /// <summary>
        /// Sets the length of the stream. Only fixed size streams are supported and calls will be ignored without changing the size.
        /// </summary>
        /// <param name="value"></param>
        public override void SetLength(long value)
        {
        }
        
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!_baseStream.CanWrite)
            {
                throw new IOException("Base stream doesn't support writing");
            }

            Initialize();
            
            var blockNumber = CalculateBlockNumber;
        
            if (blockNumber >= _numberOfBlocks)
            {
                throw new IOException($"Out of bounds at position {_position} writing {count} bytes with size {_size}");
            }

            // position in buffer
            var bufferPosition = offset;
            var bytesWritten = 0;
        
            do
            {
                if (blockNumber >= _numberOfBlocks)
                {
                    throw new IOException($"Out of bounds at position {_position} writing {count} bytes with size {_size}");
                }
                
                // get allocated block
                var allocatedBlock = GetAllocatedBlock(blockNumber);

                var positionInBlock = Convert.ToInt32(_position % _options.BlockSize);
                var bytesToWrite = Math.Min(count - bytesWritten, _options.BlockSize - positionInBlock);
                _layerStream.Position = allocatedBlock.LayerOffset + BLOCK_HEADER_SIZE + positionInBlock;
                _layerStream.Write(buffer, bufferPosition, bytesToWrite);
                allocatedBlock.WriteCount++;

                if (!allocatedBlock.IsChanged)
                {
                    // update is changed flag in layer stream block allocation table to indicate block has been modified
                    _layerStream.Position = LAYER_HEADER_SIZE + blockNumber * 9 + 8;
                    _layerStream.WriteByte(1);
                }
                
                allocatedBlock.IsChanged = true;
                _position += bytesToWrite;
                bufferPosition += bytesToWrite;
                bytesWritten += bytesToWrite;
                blockNumber++;
            } while (bytesWritten < count);
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => _baseStream.CanWrite;
        public override long Length => _baseStream.Length;

        public override long Position
        {
            get => _position;
            set => _position = value;
        }
    }
}