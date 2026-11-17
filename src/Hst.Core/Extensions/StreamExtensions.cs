namespace Hst.Core.Extensions
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Converters;

    public static class StreamExtensions
    {
        /// <summary>
        /// Read bytes from stream. Reads until count bytes are read or end of stream is reached.
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        /// <param name="count">Number of bytes to read.</param>
        /// <returns>Bytes read. Fewer than count bytes, if end of stream is reached.</returns>
        public static async Task<byte[]> ReadBytes(this Stream stream, int count)
        {
            var data = new byte[count];
            var bytesRead = await ReadFully(stream, data);
            if (bytesRead == 0)
            {
                return Array.Empty<byte>();
            }

            if (bytesRead >= count)
            {
                return data;
            }
            
            var partialData = new byte[bytesRead];
            Array.Copy(data, 0, partialData, 0, bytesRead);
            return partialData;
        }

        /// <summary>
        /// Read until buffer is filled or end of stream is reached.
        /// </summary>
        /// <returns>Number of bytes read.</returns>
        private static async Task<int> ReadFully(Stream stream, byte[] buffer)
        {
            var total = 0;
            while (total < buffer.Length)
            {
                var bytesRead = await stream.ReadAsync(buffer, total, buffer.Length - total);
                if (bytesRead == 0)
                {
                    break;
                }

                total += bytesRead;
            }

            return total;
        }

        /// <summary>
        /// Read exactly count bytes from stream.
        /// </summary>
        /// <exception cref="EndOfStreamException">End of stream is reached before count bytes are read.</exception>
        private static async Task<byte[]> ReadExactly(Stream stream, int count)
        {
            var data = new byte[count];
            var bytesRead = await ReadFully(stream, data);
            if (bytesRead != count)
            {
                throw new EndOfStreamException(
                    $"Unable to read {count} bytes, end of stream reached after {bytesRead} bytes");
            }

            return data;
        }

        public static async Task WriteByte(this Stream stream, byte value)
        {
            var data = new[] { value };
            await stream.WriteAsync(data, 0, data.Length);
        }
        
        public static async Task WriteBytes(this Stream stream, byte[] data)
        {
            await stream.WriteAsync(data, 0, data.Length);
        }
        
        /// <summary>
        /// Read big endian int16/short (2 bytes) value from stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        /// <exception cref="EndOfStreamException">End of stream is reached before value is read.</exception>
        public static async Task<short> ReadBigEndianInt16(this Stream stream)
        {
            return BigEndianConverter.ConvertBytesToInt16(await ReadExactly(stream, 2));
        }

        /// <summary>
        /// Read big endian int32 (4 bytes) value from stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        /// <exception cref="EndOfStreamException">End of stream is reached before value is read.</exception>
        public static async Task<int> ReadBigEndianInt32(this Stream stream)
        {
            return BigEndianConverter.ConvertBytesToInt32(await ReadExactly(stream, 4));
        }

        /// <summary>
        /// Read big endian uint16/ushort (2 bytes) value from stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        /// <exception cref="EndOfStreamException">End of stream is reached before value is read.</exception>
        public static async Task<ushort> ReadBigEndianUInt16(this Stream stream)
        {
            return BigEndianConverter.ConvertBytesToUInt16(await ReadExactly(stream, 2));
        }
        
        /// <summary>
        /// Read big endian uint32 (4 bytes) value from stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        /// <exception cref="EndOfStreamException">End of stream is reached before value is read.</exception>
        public static async Task<uint> ReadBigEndianUInt32(this Stream stream)
        {
            return BigEndianConverter.ConvertBytesToUInt32(await ReadExactly(stream, 4));
        }

        /// <summary>
        /// Write big endian int16/short (2 bytes) value to stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static async Task WriteBigEndianInt16(this Stream stream, short value)
        {
            var bytes = new byte[2];
            BigEndianConverter.ConvertInt16ToBytes(value, bytes);
            await stream.WriteBytes(bytes);
        }
        
        /// <summary>
        /// Write big endian int32 (4 bytes) value to stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static async Task WriteBigEndianInt32(this Stream stream, int value)
        {
            var bytes = new byte[4];
            BigEndianConverter.ConvertInt32ToBytes(value, bytes);
            await stream.WriteBytes(bytes);
        }

        /// <summary>
        /// Write big endian uint16/ushort (2 bytes) value to stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static async Task WriteBigEndianUInt16(this Stream stream, ushort value)
        {
            var bytes = new byte[2];
            BigEndianConverter.ConvertUInt16ToBytes(value, bytes);
            await stream.WriteBytes(bytes);
        }
        
        /// Write big endian uint32 (4 bytes) value to stream
        public static async Task WriteBigEndianUInt32(this Stream stream, uint value)
        {
            var bytes = new byte[4];
            BigEndianConverter.ConvertUInt32ToBytes(value, bytes);
            await stream.WriteBytes(bytes);
        }
        
        /// <summary>
        /// Find first occurrence of byte pattern in stream, searching from current position.
        /// </summary>
        /// <param name="stream">Stream to search. Position of stream after search is undefined.</param>
        /// <param name="pattern">Byte pattern to find.</param>
        /// <returns>Stream position of first byte of pattern or -1, if pattern is not found.</returns>
        /// <exception cref="ArgumentNullException">Stream or pattern is null.</exception>
        /// <exception cref="ArgumentException">Pattern is empty.</exception>
        public static async Task<long> Find(this Stream stream, byte[] pattern)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (pattern == null)
            {
                throw new ArgumentNullException(nameof(pattern));
            }

            if (pattern.Length == 0)
            {
                throw new ArgumentException("Pattern must contain at least one byte", nameof(pattern));
            }

            const int chunkSize = 32768;

            // bytes kept from end of buffer between reads, so a pattern spanning two reads is found
            var overlap = pattern.Length - 1;
            var buffer = new byte[overlap + chunkSize];

            // stream position of first byte in buffer
            var bufferPosition = stream.Position;
            var length = 0;

            while (true)
            {
                var bytesRead = await stream.ReadAsync(buffer, length, buffer.Length - length);
                if (bytesRead == 0)
                {
                    return -1;
                }

                length += bytesRead;

                var index = buffer.AsSpan(0, length).IndexOf(pattern);
                if (index >= 0)
                {
                    return bufferPosition + index;
                }

                // keep last bytes, which can be start of pattern continuing in next read
                var keep = Math.Min(overlap, length);
                Array.Copy(buffer, length - keep, buffer, 0, keep);
                bufferPosition += length - keep;
                length = keep;
            }
        }
    }
}