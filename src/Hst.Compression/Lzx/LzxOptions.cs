namespace Hst.Compression.Lzx
{
    using System.Text;

    public class LzxOptions
    {
        public Encoding Encoding { get; set; }

        public LzxOptions()
        {
            Encoding = Encoding.UTF8;
        }

        public static LzxOptions Default => new LzxOptions();

        /// <summary>
        /// Options for Amiga lzx archives
        /// </summary>
        public static LzxOptions AmigaLzxOptions =>
            new LzxOptions
            {
                Encoding = Encoding.GetEncoding("ISO-8859-1"),
            };    
    }
}