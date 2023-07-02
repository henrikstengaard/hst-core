namespace Hst.Compression.Lzx
{
    using System.Collections.Generic;

    public class LzxMergedChunk
    {
        public readonly IList<LzxEntry> Entries;

        /// <summary>
        /// Pack mode for merged chunk
        /// </summary>
        public readonly PackModeEnum PackMode;

        /// <summary>
        /// Compressed data size of merged entry
        /// </summary>
        public readonly int PackSize;

        public readonly long CompressedDataOffset;
        
        public LzxMergedChunk(IList<LzxEntry> entries, PackModeEnum packMode, int packSize, long compressedDataOffset)
        {
            Entries = entries;
            PackMode = packMode;
            PackSize = packSize;
            CompressedDataOffset = compressedDataOffset;
        }
    }
}