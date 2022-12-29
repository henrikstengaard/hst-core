namespace Hst.Compression.Lha
{
    using System;

    public static class DateHelper
    {
        private static int SubBits(long n, int off, int len) => (int)(((n) >> (off)) & ((1 << (len)) - 1));
        
        public static DateTime GenericToUnixStamp(long t)
        {
            var second  = SubBits(t,  0, 5) * 2;
            var minute  = SubBits(t,  5, 6);
            var hour = SubBits(t, 11, 5);
            var day = SubBits(t, 16, 5);
            var month  = SubBits(t, 21, 4);
            var year = SubBits(t, 25, 7) + 1980;

            return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
        }        
    }
}