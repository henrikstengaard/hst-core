namespace Hst.Core.IO
{
    public class HybridStreamOptions
    {
        /// <summary>
        /// Leave the first stream open after disposing the hybrid stream.
        /// </summary>
        public bool LeaveLayerStreamOpen { get; set; } = false;

        /// <summary>
        /// Leave the second stream open after disposing the hybrid stream.
        /// </summary>
        public bool LeaveBaseStreamOpen { get; set; } = false;
        
        /// <summary>
        /// Size threshold to switch from first stream to second stream.
        /// Default is 512 MB.
        /// </summary>
        public long SizeThreshold { get; set; } = 512 * 1024 * 1024;
    }
}