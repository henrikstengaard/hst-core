namespace Hst.Compression.Lha
{
    using System;

    /// <summary>
    /// Options for a file or directory added to a lha archive with <see cref="LhaWriter" />.
    /// </summary>
    public class LhaEntryOptions
    {
        /// <summary>
        /// Last modified date of entry. Default is null: now.
        /// </summary>
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// Compression method used for a file, overrides method from writer options. Default is null: method from
        /// writer options. Ignored for directories.
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Attributes of entry, e.g. Amiga protection bits as stored on disk when writing with
        /// <see cref="LhaOsType.Amiga" />. Default is null: ms-dos archive (0x20) for files and directory (0x10) for
        /// directories, or 0 (no protection bits set) for Amiga.
        /// </summary>
        public int? Attributes { get; set; }

        /// <summary>
        /// Comment of entry stored after a zero character in the name like Amiga LhA. Default is null: no comment.
        /// </summary>
        public string Comment { get; set; }
    }
}
