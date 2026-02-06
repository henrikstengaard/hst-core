using System;
using System.Diagnostics;
using System.IO;
using System.Timers;

namespace Hst.Core.IO
{
    /// <summary>
    /// Hybrid stream that changes from one stream to another based on size.
    /// </summary>
    public class HybridStream : Stream
    {
        private readonly Timer _timer = new Timer();
        private DataFlushedEventArgs _dataFlushedEventArgs;
        
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
        
        private readonly byte[] _buffer = new byte[1024 * 1024];
        private Stream _currentStream;
        private readonly Stream _firstStream;
        private readonly Stream _secondStream;
        private readonly HybridStreamOptions _options;

        public bool IsDisposed { get; private set; }

        public event EventHandler<DataFlushedEventArgs> DataFlushed;

        /// <summary>
        /// Hybrid stream that changes from one stream to another based on size.
        /// </summary>
        /// <param name="firstStream">First stream to use.</param>
        /// <param name="secondStream">Second stream to use.</param>
        /// <param name="options">Hybrid stream options.</param>
        public HybridStream(Stream firstStream, Stream secondStream, HybridStreamOptions options)
        {
            _firstStream = firstStream;
            _secondStream = secondStream;
            _options = options;
            _currentStream = _firstStream;
            
            _timer.Enabled = true;
            _timer.Interval = 1000;
            _timer.Elapsed += SendDataFlushed;
            _dataFlushedEventArgs = null;
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
                
                if (!_options.LeaveLayerStreamOpen)
                {
                    _firstStream.Close();
                    _firstStream.Dispose();
                }
            
                if (!_options.LeaveBaseStreamOpen)
                {
                    _secondStream.Close();
                    _secondStream.Dispose();
                }
            }
            
            base.Dispose(disposing);
        
            IsDisposed = true;
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

        private void SwitchToSecondStream()
        {
            // check if we need to switch to second stream
            if (_currentStream == _secondStream)
            {
                return;
            }

            // if first stream is empty, switch directly to second stream
            if (_currentStream.Length == 0)
            {
                _currentStream = _secondStream;
                
                return;
            }
            
            // get current position from stream
            var position = _currentStream.Position;
            
            var bytesProcessed = 0L;
            var bytesTotal = _firstStream.Length;
            OnDataFlushed(0, 0, bytesTotal, bytesTotal, 
                TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, 0);

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // flush first stream
            _firstStream.Flush();
            
            // copy data from first to second stream
            _firstStream.Position = 0;
            _secondStream.Position = 0;
            int bytesRead;
            do
            {
                bytesRead = _firstStream.Read(_buffer, 0, _buffer.Length);
                if (bytesRead > 0)
                {
                    _secondStream.Write(_buffer, 0, bytesRead);
                }
                
                bytesProcessed += bytesRead;
                var bytesRemaining = bytesTotal == 0 ? 0 : bytesTotal - bytesProcessed;
                var percentComplete = bytesTotal == 0 || bytesProcessed == 0 ? 0 : Math.Round((double)100 / bytesTotal * bytesProcessed, 1);
                var timeElapsed = stopwatch.Elapsed;
                var timeRemaining = bytesTotal == 0 ? TimeSpan.Zero : CalculateTimeRemaining(percentComplete, timeElapsed);
                var timeTotal = bytesTotal == 0 ? TimeSpan.Zero : timeElapsed + timeRemaining;
                var bytesPerSecond = Convert.ToInt64(bytesProcessed / timeElapsed.TotalSeconds);

                OnDataFlushed(percentComplete, bytesProcessed, bytesRemaining, bytesTotal, timeElapsed, timeRemaining,
                    timeTotal, bytesPerSecond);
            } while (bytesRead == _buffer.Length);
            
            stopwatch.Stop();
            
            OnDataFlushed(100, bytesProcessed, 0, bytesTotal, stopwatch.Elapsed,
                TimeSpan.Zero, stopwatch.Elapsed, 0);
            
            // switch to second stream
            _currentStream = _secondStream;
            
            // set stream position
            _currentStream.Position = position;
        }

        private static TimeSpan CalculateTimeRemaining(double percentComplete, TimeSpan timeElapsed)
        {
            return percentComplete > 0
                ? TimeSpan.FromMilliseconds(timeElapsed.TotalMilliseconds / percentComplete *
                                            (100 - percentComplete))
                : TimeSpan.Zero;
        }
        
        public override void Flush()
        {
            _currentStream.Flush();
            
            if (_currentStream.Length > _options.SizeThreshold)
            {
                SwitchToSecondStream();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_currentStream.Position + count > _options.SizeThreshold)
            {
                SwitchToSecondStream();
            }
                
            return _currentStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (_currentStream == _secondStream)
            {
                return _currentStream.Seek(offset, origin);
            }
            
            bool switchStream;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    switchStream = offset > _options.SizeThreshold;
                    break;
                case SeekOrigin.Current:
                    switchStream = _currentStream.Position + offset > _options.SizeThreshold;
                    break;
                case SeekOrigin.End:
                    switchStream = _currentStream.Length + offset > _options.SizeThreshold;
                    break;
                default:
                    switchStream = false;
                    break;
            }

            if (switchStream)
            {
                SwitchToSecondStream();
            }

            return _currentStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            if (value > _options.SizeThreshold)
            {
                SwitchToSecondStream();
            }

            _currentStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_currentStream.Position + count > _options.SizeThreshold)
            {
                SwitchToSecondStream();
            }
                
            _currentStream.Write(buffer, offset, count);
        }

        public override bool CanRead => _currentStream.CanRead;
        public override bool CanSeek => _currentStream.CanSeek;
        public override bool CanWrite => _currentStream.CanWrite;
        public override long Length => _currentStream.Length;
        public override long Position { get => _currentStream.Position; set => _currentStream.Position = value; }
    }
}