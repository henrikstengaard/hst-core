namespace Hst.Core.Extensions
{
    using System;
    using System.Text;

    public static class ByteArrayExtensions
    {
        public static byte[] CopyBytes(this byte[] bytes, int offset, int length)
        {
            var data = new byte[length];
            Array.Copy(bytes, offset, data, 0, length);
            return data;
        }
    }
}