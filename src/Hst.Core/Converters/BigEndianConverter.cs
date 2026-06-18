namespace Hst.Core.Converters
{
    /// <summary>
    /// <para>
    /// Big-endian converter that convert integer values to bytes and bytes to integer values.
    /// A big-endian system stores the most significant byte (MSB) at the lowest memory address.
    /// The "big end" (the most significant part of the data) comes first.
    /// </para>
    /// <para>
    /// For the same 32-bit integer 0x12345678, a big-endian system would store it as:
    /// <code>
    /// Address:  00  01  02  03<br/>
    /// Data:     12  34  56  78
    /// </code>
    /// </para>
    /// <para>
    /// The most significant byte is 0x12, placed at the lowest address (00), followed by 0x34, 0x56, and 0x78 at the highest address (03).
    /// </para>
    /// </summary>
    public static class BigEndianConverter
    {
        public static short ConvertBytesToInt16(byte[] bytes, int offset = 0)
        {
            return (short)(bytes[offset + 1] & 0x00ff | (bytes[offset] << 8) & 0xff00);
        }

        public static int ConvertBytesToInt32(byte[] bytes, int offset = 0)
        {
            return (int)(bytes[offset + 3] & 0x000000ff) |
                   (int)((bytes[offset + 2] << 8) & 0x0000ff00) |
                   (int)((bytes[offset + 1] << 16) & 0x00ff0000) |
                   (int)((bytes[offset] << 24) & 0xff000000);
        }

        public static ushort ConvertBytesToUInt16(byte[] bytes, int offset = 0)
        {
            return (ushort)(bytes[offset + 1] +
                            (bytes[offset] << 8));
        }

        public static uint ConvertBytesToUInt32(byte[] bytes, int offset = 0)
        {
            return bytes[offset + 3] +
                   (uint)(bytes[offset + 2] << 8) +
                   (uint)(bytes[offset + 1] << 16) +
                   (uint)(bytes[offset] << 24);
        }

        public static void ConvertInt16ToBytes(short value, byte[] data, int offset = 0)
        {
            data[offset] = (byte)((value >> 8) & 0xFF);
            data[offset + 1] = (byte)(value & 0xFF);
        }

        public static void ConvertInt32ToBytes(int value, byte[] data, int offset = 0)
        {
            data[offset] = (byte)((value >> 24) & 0xFF);
            data[offset + 1] = (byte)((value >> 16) & 0xFF);
            data[offset + 2] = (byte)((value >> 8) & 0xFF);
            data[offset + 3] = (byte)(value & 0xFF);
        }
        
        public static void ConvertUInt16ToBytes(ushort value, byte[] data, int offset = 0)
        {
            data[offset] = (byte)((value >> 8) & 0xFF);
            data[offset + 1] = (byte)(value & 0xFF);
        }

        public static void ConvertUInt32ToBytes(uint value, byte[] data, int offset = 0)
        {
            data[offset] = (byte)((value >> 24) & 0xFF);
            data[offset + 1] = (byte)((value >> 16) & 0xFF);
            data[offset + 2] = (byte)((value >> 8) & 0xFF);
            data[offset + 3] = (byte)(value & 0xFF);
        }
    }
}