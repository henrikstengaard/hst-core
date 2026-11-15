namespace Hst.Compression.Lha
{
    using System;

    /// <summary>
    /// Entry in a lha archive read from or written to a header.
    /// </summary>
    public class LhaEntry
    {
        /// <summary>
        /// Offset of header in archive stream.
        /// </summary>
        public long HeaderOffset { get; set; }

        /// <summary>
        /// Total size of header in bytes including extended headers. Entry data starts at header offset + header size.
        /// </summary>
        public int HeaderSize { get; set; }

        /// <summary>
        /// Header level 0, 1, 2 or 3.
        /// </summary>
        public int HeaderLevel { get; set; }

        /// <summary>
        /// CRC-16 of header from common extended header.
        /// </summary>
        public short HeaderCrc { get; set; }

        /// <summary>
        /// Compression method, e.g. "-lh5-". See <see cref="LhaMethod"/> for methods.
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Name of entry with directory path using '\' as path separator. Directories end with '\'.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Target of symbolic link using '\' as path separator.
        /// </summary>
        public string LinkTarget { get; set; }

        /// <summary>
        /// Last modified date. Ms-dos timestamps from level 0 and 1 headers are returned as is with kind utc.
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Size of packed (compressed) data in bytes.
        /// </summary>
        public long PackedSize { get; set; }

        /// <summary>
        /// Size of unpacked (original) data in bytes.
        /// </summary>
        public long UnpackedSize { get; set; }

        /// <summary>
        /// CRC-16 of unpacked data.
        /// </summary>
        public short Crc { get; set; }

        /// <summary>
        /// True, if header has CRC-16 of unpacked data. Old level 0 headers doesn't have a crc.
        /// </summary>
        public bool HasCrc { get; set; }

        /// <summary>
        /// Attributes, e.g. ms-dos attributes or amiga protection bits.
        /// </summary>
        public int Attributes { get; set; }

        /// <summary>
        /// Operating system type, which created the entry. See <see cref="LhaOsType"/>.
        /// </summary>
        public byte OsType { get; set; }

        /// <summary>
        /// Unix file type and permissions. See <see cref="LhaUnixMode"/>.
        /// </summary>
        public ushort UnixMode { get; set; }

        /// <summary>
        /// Unix user id.
        /// </summary>
        public ushort UnixUserId { get; set; }

        /// <summary>
        /// Unix group id.
        /// </summary>
        public ushort UnixGroupId { get; set; }

        /// <summary>
        /// Unix user name.
        /// </summary>
        public string UnixUserName { get; set; }

        /// <summary>
        /// Unix group name.
        /// </summary>
        public string UnixGroupName { get; set; }
    }
}
