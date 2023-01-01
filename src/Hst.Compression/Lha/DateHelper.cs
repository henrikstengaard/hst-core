namespace Hst.Compression.Lha
{
    using System;

    public static class DateHelper
    {
        private static int SubBits(long n, int off, int len) => (int)(((n) >> (off)) & ((1 << (len)) - 1));
        
        public static DateTime GenericToUnixStamp(long timeStamp)
        {
            var second  = SubBits(timeStamp,  0, 5) * 2;
            var minute  = SubBits(timeStamp,  5, 6);
            var hour = SubBits(timeStamp, 11, 5);
            var day = SubBits(timeStamp, 16, 5);
            var month  = SubBits(timeStamp, 21, 4);
            var year = SubBits(timeStamp, 25, 7);

            return new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Unspecified).AddYears(year).AddMonths(month).AddDays(day).AddHours(hour).AddMinutes(minute).AddSeconds(second);
        }

        public static DateTime UnixStamp(long timeStamp)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timeStamp);
        }
    }
}