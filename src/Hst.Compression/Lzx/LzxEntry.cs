namespace Hst.Compression.Lzx
{
    using System;

    public class LzxEntry
    {
        public long Offset { get; set; }
        public string Name { get; set; }
        public string Comment { get; set; }
        public DateTime Date { get; set; }
        public int PackMode { get; set; }
        public int PackedSize { get; set; }
        public int UnpackedSize { get; set; }
        public int Attributes { get; set; }
        public int DataCrc { get; set; }
    }
}