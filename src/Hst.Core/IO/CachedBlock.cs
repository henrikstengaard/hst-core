namespace Hst.Core.IO
{
    public class CachedBlock
    {
        public long Offset { get; set; }
        public byte[] Data { get; set; }
        
        /// <summary>
        /// Length of data
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Number of times data is read from cached block
        /// </summary>
        public int ReadCount { get; set; }
        /// <summary>
        /// Number of times data is written to cached block
        /// </summary>
        public int WriteCount { get; set; }
    }
}