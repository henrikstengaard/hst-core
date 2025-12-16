namespace Hst.Core.IO
{
    public class LayeredStreamOptions
    {
        /// <summary>
        /// Size of blocks the layered stream stores.
        /// </summary>
        public int BlockSize { get; set; } = 1024 * 1024;

        /// <summary>
        /// Leave the base stream open after disposing the layered stream.
        /// </summary>
        public bool LeaveBaseStreamOpen { get; set; } = false;

        /// <summary>
        /// Leave the layer stream open after disposing the layered stream.
        /// </summary>
        public bool LeaveLayerStreamOpen { get; set; } = false;
        
        /// <summary>
        /// Flush changed blocks to base stream on disposing the layered stream.
        /// </summary>
        public bool FlushLayerOnDispose { get; set; } = true;
    }
}