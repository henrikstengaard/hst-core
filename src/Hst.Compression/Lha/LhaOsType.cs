namespace Hst.Compression.Lha
{
    /// <summary>
    /// Operating system types stored in lha headers, which identifies the operating system or program that created
    /// an entry.
    /// </summary>
    public static class LhaOsType
    {
        public const byte Generic = 0;
        public const byte MsDos = (byte)'M';
        public const byte Unix = (byte)'U';
        public const byte MacOs = (byte)'m';
        public const byte Os9 = (byte)'9';
        public const byte Os2 = (byte)'2';
        public const byte Os68K = (byte)'K';
        public const byte Os386 = (byte)'3';
        public const byte Human68K = (byte)'H';
        public const byte CpM = (byte)'C';
        public const byte Flex = (byte)'F';
        public const byte Runser = (byte)'R';
        public const byte Amiga = (byte)'A';
        public const byte Atari = (byte)'a';
        public const byte Windows95 = (byte)'w';
        public const byte WindowsNt = (byte)'W';
        public const byte TownsOs = (byte)'T';
        public const byte Xosk = (byte)'X';
        public const byte Java = (byte)'J';
        public const byte Lhark = (byte)' ';
    }
}
