namespace Hst.Core.Converters
{
    /// <summary>
    /// <para>
    /// Little-endian converter that convert integer values to bytes and bytes to integer values.
    /// A little-endian system stores the least significant byte (LSB) at the lowest memory address.
    /// The "little end" (the least significant part of the data) comes first.
    /// </para>
    /// <para>
    /// For the same 32-bit integer 0x12345678, a little-endian system would store it as:
    /// <code>
    /// Address:  00  01  02  03<br/>
    /// Data:     78  56  34  12
    /// </code>
    /// </para>
    /// <para>
    /// The least significant byte is 0x78, placed at the lowest address (00), followed by 0x56, 0x34, and 0x12 at the highest address (03).
    /// </para>
    /// </summary>
    public static class LittleEndianConverter
    {
        public static short ConvertBytesToInt16(byte[] bytes, int offset = 0)
        {
            return (short)(bytes[offset] +
                            (bytes[offset + 1] << 8));
        }

        public static int ConvertBytesToInt32(byte[] bytes, int offset = 0)
        {
            return bytes[offset] +
                   (bytes[offset + 1] << 8) +
                   (bytes[offset + 2] << 16) +
                   (bytes[offset + 3] << 24);
        }

        public static ushort ConvertBytesToUInt16(byte[] bytes, int offset = 0)
        {
            return (ushort)(bytes[offset] +
                            (bytes[offset + 1] << 8));
        }

        public static uint ConvertBytesToUInt32(byte[] bytes, int offset = 0)
        {
            return bytes[offset] +
                   ((uint)bytes[offset + 1] << 8) +
                   ((uint)bytes[offset + 2] << 16) +
                   ((uint)bytes[offset + 3] << 24);
        }

        public static void ConvertInt16ToBytes(short value, byte[] data, int offset = 0)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        public static void ConvertInt32ToBytes(int value, byte[] data, int offset = 0)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
            data[offset + 2] = (byte)((value >> 16) & 0xFF);
            data[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
        
        public static void ConvertUInt16ToBytes(ushort value, byte[] data, int offset = 0)
        {
            data[offset] = (byte)(value & 0xFF);            
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        public static void ConvertUInt32ToBytes(uint value, byte[] data, int offset = 0)
        {
            data[offset] = (byte)(value & 0xFF);            
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
            data[offset + 2] = (byte)((value >> 16) & 0xFF);
            data[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}