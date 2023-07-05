namespace Hst.Compression.Lzx
{
    using System;

    public class LzxEntry
    {
        public long EntryOffset { get; set; }
        public long DataOffset { get; set; }
        public string Name { get; set; }
        public string Comment { get; set; }
        public DateTime Date { get; set; }
        public PackModeEnum PackMode { get; set; }
        public int PackedSize { get; set; }
        public int UnpackedSize { get; set; }
        public AttributesEnum Attributes { get; set; }
        public int DataCrc { get; set; }
        public bool IsMergedEntry { get; set; }
    }
}