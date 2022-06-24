namespace Hst.Core.Extensions
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Converters;

    public static class StreamExtensions
    {
        public static async Task<byte[]> ReadBytes(this Stream stream, int count)
        {
            var data = new byte[count];
            var bytesRead = await stream.ReadAsync(data, 0, count);
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
        public static async Task<short> ReadBigEndianInt16(this Stream stream)
        {
            return BigEndianConverter.ConvertBytesToInt16(await stream.ReadBytes(2));
        }

        /// <summary>
        /// Read big endian int32 (4 bytes) value from stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static async Task<int> ReadBigEndianInt32(this Stream stream)
        {
            return BigEndianConverter.ConvertBytesToInt32(await stream.ReadBytes(4));
        }

        /// <summary>
        /// Read big endian uint16/ushort (2 bytes) value from stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static async Task<ushort> ReadBigEndianUInt16(this Stream stream)
        {
            return BigEndianConverter.ConvertBytesToUInt16(await stream.ReadBytes(2));
        }
        
        /// <summary>
        /// Read big endian uint32 (4 bytes) value from stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static async Task<uint> ReadBigEndianUInt32(this Stream stream)
        {
            return BigEndianConverter.ConvertBytesToUInt32(await stream.ReadBytes(4));
        }

        /// <summary>
        /// Write big endian int16/short (2 bytes) value to stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static async Task WriteBigEndianInt16(this Stream stream, short value)
        {
            await stream.WriteBytes(BigEndianConverter.ConvertInt16ToBytes(value));
        }
        
        /// <summary>
        /// Write big endian int32 (4 bytes) value to stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static async Task WriteBigEndianInt32(this Stream stream, int value)
        {
            await stream.WriteBytes(BigEndianConverter.ConvertInt32ToBytes(value));
        }

        /// <summary>
        /// Write big endian uint16/ushort (2 bytes) value to stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        public static async Task WriteBigEndianUInt16(this Stream stream, ushort value)
        {
            await stream.WriteBytes(BigEndianConverter.ConvertUInt16ToBytes(value));
        }
        
        /// Write big endian uint32 (4 bytes) value to stream
        public static async Task WriteBigEndianUInt32(this Stream stream, uint value)
        {
            await stream.WriteBytes(BigEndianConverter.ConvertUInt32ToBytes(value));
        }
        
        /// <summary>
        /// Find first occurence of byte pattern in stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public static async Task<long> Find(this Stream stream, byte[] pattern)
        {
            var chunkSize = 32768;
            byte[] chunk;
            do
            {
                var position = stream.Position;
                chunk = await stream.ReadBytes(chunkSize);

                if (chunk.Length == 0)
                {
                    break;
                }
                
                for (var i = 0; i < chunk.Length; i++)
                {
                    // skip, if first byte is not equal
                    if (chunk[i] != pattern[0])
                    {
                        continue;
                    }
        
                    // found a match on first byte, now try to match rest of the pattern
                    var patternIndex = 1;
                    for (var j = 1; j < pattern.Length; j++, patternIndex++) 
                    {
                        if (j + i >= chunk.Length)
                        {
                            chunk = await stream.ReadBytes(chunkSize);
                            i = 0;
                            patternIndex = 0;
                        }

                        if (chunk[i + patternIndex] != pattern[j])
                        {
                            break;
                        }

                        if (j == pattern.Length - 1)
                        {
                            return position + i;
                        }
                    }
                }
                
            } while (chunk.Length == chunkSize);
            
            return -1;
        }
    }
}