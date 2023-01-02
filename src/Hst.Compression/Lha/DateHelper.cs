namespace Hst.Compression.Lha
{
    using System;

    public static class DateHelper
    {
        private static int SubBits(long n, int off, int len) => (int)(((n) >> (off)) & ((1 << (len)) - 1));

        public static DateTime ToGenericTimeStamp(long timeStamp)
        {
            var second = SubBits(timeStamp, 0, 5) * 2;
            var minute = SubBits(timeStamp, 5, 6);
            var hour = SubBits(timeStamp, 11, 5);
            var day = SubBits(timeStamp, 16, 5);
            var month = SubBits(timeStamp, 21, 4);
            var year = SubBits(timeStamp, 25, 7);

            return new DateTime(1980 + year, month == 0 ? 1 : month, day == 0 ? 1 : day, hour, minute, second,
                DateTimeKind.Utc);
        }

        public static DateTime ToUnixTimeStamp(long timeStamp)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timeStamp);
        }
    }
}