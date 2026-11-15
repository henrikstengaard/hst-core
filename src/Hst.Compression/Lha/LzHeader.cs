namespace Hst.Compression.Lha
{
    using System;

    /// <summary>
    /// Header of an entry in a lha archive.
    /// </summary>
    public class LzHeader
    {
        /// <summary>
        /// Total size of header in bytes including extended headers. Entry data starts at header offset + header size.
        /// </summary>
        public int HeaderSize { get; set; }

        /// <summary>
        /// Size of extended header size fields: 2 for header level 0, 1 and 2, 4 for header level 3.
        /// </summary>
        public int SizeFieldLength { get; set; }

        /// <summary>
        /// Compression method, e.g. "-lh5-". See <see cref="Constants"/> for methods.
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Size of compressed data in bytes.
        /// </summary>
        public long PackedSize { get; set; }

        /// <summary>
        /// Size of original (uncompressed) data in bytes.
        /// </summary>
        public long OriginalSize { get; set; }

        /// <summary>
        /// Attribute, e.g. ms-dos attributes or amiga protection bits.
        /// </summary>
        public int Attribute { get; set; }

        /// <summary>
        /// Header level 0, 1, 2 or 3.
        /// </summary>
        public int HeaderLevel { get; set; }

        /// <summary>
        /// Name of entry with directory path using '\' as path separator. Directories end with '\'.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Target of symbolic link using '\' as path separator.
        /// </summary>
        public string RealName { get; set; }

        /// <summary>
        /// CRC-16 of original data.
        /// </summary>
        public short Crc { get; set; }

        /// <summary>
        /// True, if header has CRC-16 of original data. Old level 0 headers doesn't have a crc.
        /// </summary>
        public bool HasCrc { get; set; }

        /// <summary>
        /// CRC-16 of header from common extended header.
        /// </summary>
        public short HeaderCrc { get; set; }

        /// <summary>
        /// Operating system type, which created the entry. See EXTEND_ in <see cref="Constants"/>.
        /// </summary>
        public int ExtendType { get; set; }

        /// <summary>
        /// Minor version of level 0 unix extended area.
        /// </summary>
        public byte MinorVersion { get; set; }

        /// <summary>
        /// Last modified date. Ms-dos timestamps from level 0 and 1 headers are returned as is with kind utc.
        /// </summary>
        public DateTime UnixLastModifiedStamp { get; set; }

        /// <summary>
        /// Unix file type and permissions. See UNIX_ in <see cref="Constants"/>.
        /// </summary>
        public ushort UnixMode { get; set; }

        /// <summary>
        /// Unix group id.
        /// </summary>
        public ushort UnixGid { get; set; }

        /// <summary>
        /// Unix user id.
        /// </summary>
        public ushort UnixUid { get; set; }

        /// <summary>
        /// Unix group name.
        /// </summary>
        public string UnixGroupName { get; set; }

        /// <summary>
        /// Unix user name.
        /// </summary>
        public string UnixUserName { get; set; }

        /// <summary>
        /// Offset of header in archive stream.
        /// </summary>
        public long HeaderOffset { get; set; }
    }
}
