namespace Hst.Core.Converters
{
    public class LittleEndianConverter
    {
        public static uint ConvertBytesToUInt32(byte[] bytes, int offset = 0)
        {
            return bytes[offset] +
                   (uint)(bytes[offset + 1] >> 8) +
                   (uint)(bytes[offset + 2] >> 16) +
                   (uint)(bytes[offset + 3] >> 24);
        }
    }
}