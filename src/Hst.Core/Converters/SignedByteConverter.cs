namespace Hst.Core.Converters
{
    public static class SignedByteConverter
    {
        public static sbyte ConvertByteToSignedByte(byte value)
        {
            return (sbyte)(value >= 128 ? value - 256 : value);
        }
        
        public static sbyte ConvertByteToSignedByte(byte[] bytes, int offset = 0)
        {
            return ConvertByteToSignedByte(bytes[offset]);
        }

        public static byte ConvertSignedByteToByte(sbyte value)
        {
            return (byte)(value < 0 ? value + 256 : value);
        }
        
        public static void ConvertSignedByteToByte(byte[] bytes, int offset, sbyte value)
        {
            bytes[offset] = ConvertSignedByteToByte(value);
        }
    }
}