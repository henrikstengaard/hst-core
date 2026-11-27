namespace Hst.Compression.Lha
{
    using System.Text;

    /// <summary>
    /// Options for writing lha archives.
    /// </summary>
    public class LhaWriterOptions
    {
        /// <summary>
        /// Encoding used for entry names. Default is UTF-8.
        /// </summary>
        public Encoding Encoding { get; set; }

        /// <summary>
        /// Header level used for entries, supports 0, 1 and 2. Default is 2.
        /// </summary>
        public int HeaderLevel { get; set; }

        /// <summary>
        /// Compression method used for files, supports "-lh0-", "-lh4-", "-lh5-", "-lh6-" and "-lh7-".
        /// Default is "-lh5-".
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Write generic headers without unix extended headers (unix timestamp, permissions, uid and gid).
        /// Default is false.
        /// </summary>
        public bool GenericFormat { get; set; }

        /// <summary>
        /// Operating system type written in headers, see <see cref="LhaOsType" />. Unix extended headers (unix
        /// timestamp, permissions, uid and gid) are only written for <see cref="LhaOsType.Unix" />. With
        /// <see cref="LhaOsType.Amiga" /> attributes are Amiga protection bits as stored on disk and comments are
        /// stored after a zero character in the name like Amiga LhA. Default is null: <see cref="LhaOsType.Generic" />
        /// with <see cref="GenericFormat" />, otherwise <see cref="LhaOsType.Unix" />.
        /// </summary>
        public byte? OsType { get; set; }

        public LhaWriterOptions()
        {
            Encoding = Encoding.UTF8;
            HeaderLevel = 2;
            Method = LhaMethod.Lh5;
            GenericFormat = false;
            OsType = null;
        }

        /// <summary>
        /// Operating system type written in headers.
        /// </summary>
        internal byte EffectiveOsType => OsType ?? (GenericFormat ? LhaOsType.Generic : LhaOsType.Unix);

        public static LhaWriterOptions Default => new LhaWriterOptions();

        /// <summary>
        /// Options for writing Amiga lha archives like Amiga LhA: Amiga operating system type with protection bits and
        /// comments, ISO-8859-1 encoded names, header level 2 and method "-lh5-".
        /// </summary>
        public static LhaWriterOptions Amiga => new LhaWriterOptions
        {
            Encoding = Encoding.GetEncoding("ISO-8859-1"),
            OsType = LhaOsType.Amiga
        };
    }
}
