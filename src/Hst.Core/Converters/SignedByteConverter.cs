namespace Hst.Core.Converters
{
    public static class SignedByteConverter
    {
        public static int ConvertByteToSignedByte(byte[] bytes, int offset = 0)
        {
            var value = bytes[offset];
            return value >= 128 ? value - 256 : value;
        }

        public static void ConvertSignedByteToByte(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)(value < 0 ? value + 256 : value);
        }
    }
}