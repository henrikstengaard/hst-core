namespace Hst.Compression.Lha
{
    /// <summary>
    /// CRC-16 (reflected polynomial 0xA001, initial value 0) used by lha archives for data and headers.
    /// </summary>
    internal static class Crc16
    {
        private const ushort POLYNOMIAL = 0xa001;

        private static readonly ushort[] Table = CreateTable();

        private static ushort[] CreateTable()
        {
            var table = new ushort[256];
            for (var value = 0; value < table.Length; value++)
            {
                var crc = (ushort)value;
                for (var bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ POLYNOMIAL) : (ushort)(crc >> 1);
                }

                table[value] = crc;
            }

            return table;
        }

        /// <summary>
        /// Update crc with bytes from buffer.
        /// </summary>
        public static ushort Update(ushort crc, byte[] buffer, int offset, int count)
        {
            var end = offset + count;
            for (var i = offset; i < end; i++)
            {
                crc = (ushort)((crc >> 8) ^ Table[(byte)(crc ^ buffer[i])]);
            }

            return crc;
        }
    }
}
