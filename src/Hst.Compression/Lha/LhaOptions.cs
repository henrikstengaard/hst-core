namespace Hst.Compression.Lha
{
    using System.Text;

    public class LhaOptions
    {
        public Encoding Encoding { get; set; }

        public LhaOptions()
        {
            Encoding = Encoding.UTF8;
        }

        public static LhaOptions Default => new LhaOptions();

        /// <summary>
        /// Options for Amiga lha archives
        /// </summary>
        public static LhaOptions AmigaLhaOptions =>
            new LhaOptions
            {
                Encoding = Encoding.GetEncoding("ISO-8859-1"),
            };
    }
}