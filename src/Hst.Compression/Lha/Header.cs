/* ------------------------------------------------------------------------ */
/* LHa for UNIX                                                             */
/*              extract.c -- extrcat from archive                           */
/*                                                                          */
/*      Modified                Nobutaka Watazaki                           */
/*                                                                          */
/*  Ver. 1.14   Source All chagned              1995.01.14  N.Watazaki      */
/* ------------------------------------------------------------------------ */

namespace Hst.Compression.Lha
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Core.Extensions;

    public class Header
    {
        private const int I_HEADER_SIZE = 0; /* level 0,1,2   */
        private const int I_HEADER_CHECKSUM = 1; /* level 0,1     */
        private const int I_METHOD = 2; /* level 0,1,2,3 */
        private const int I_PACKED_SIZE = 7; /* level 0,1,2,3 */
        private const int I_ATTRIBUTE = 19; /* level 0,1,2,3 */
        private const int I_HEADER_LEVEL = 20; /* level 0,1,2,3 */

        private const int COMMON_HEADER_SIZE = 21; /* size of common part */

        private const int I_GENERIC_HEADER_SIZE = 24; /* + name_length */
        private const int I_LEVEL0_HEADER_SIZE = 36; /* + name_length (unix extended) */
        private const int I_LEVEL1_HEADER_SIZE = 27; /* + name_length */
        private const int I_LEVEL2_HEADER_SIZE = 26; /* + padding */
        private const int I_LEVEL3_HEADER_SIZE = 32;

        private readonly LhaOptions options;

        public Header(LhaOptions options)
        {
            this.options = options;
        }

        public async Task<LzHeader> GetHeader(Stream stream)
        {
            var headerOffset = stream.Position;
            var data = await stream.ReadBytes(I_HEADER_LEVEL + 1);

            if (data == null || data.Length == 0 || (data.Length == 1 && data[0] == 0) ||
                data.Length < I_HEADER_LEVEL + 1)
            {
                return null;
            }

            LzHeader header;
            switch (data[I_HEADER_LEVEL])
            {
                case 0:
                    header = await GetHeaderLevel0(stream, headerOffset, data);
                    break;
                case 1:
                    header = await GetHeaderLevel1(stream, headerOffset, data);
                    break;
                case 2:
                    header = await GetHeaderLevel2(stream, headerOffset, data);
                    break;
                case 3:
                    header = await GetHeaderLevel3(stream, headerOffset, data);
                    break;
                default:
                    throw new IOException("read header (level %x) is unknown", data[I_HEADER_LEVEL]);
            }

            if ((header.UnixMode & Constants.UNIX_FILE_SYMLINK) == Constants.UNIX_FILE_SYMLINK)
            {
                if (header.Name.IndexOf("|", StringComparison.Ordinal) < 0)
                {
                    throw new ArgumentException($"unknown symlink name \"{header.Name}\"", nameof(header.Name));
                }

                /* split symbolic link */
                var components = header.Name.Split('|');
                header.Name = components[0]; // name is symbolic link name
                header.RealName = components[1]; // realname is real name
            }

            return header;
        }

        /*
https://unix.stackexchange.com/questions/450480/file-permission-with-six-bytes-in-git-what-does-it-mean
         
The values shown are the 16-bit file modes as stored by Git, following the layout of POSIX types and modes:

--------------------------------

  32-bit mode, split into (high to low bits)

    4-bit object type
      valid values in binary are 1000 (regular file), 1010 (symbolic link)
      and 1110 (gitlink)

    3-bit unused

    9-bit unix permission. Only 0755 and 0644 are valid for regular files.
    Symbolic links and gitlinks have value 0 in this field.         

--------------------------------
         
That file doesn’t mention directories; they are represented using object type 0100.

Each digit in the six-digit value is in octal, representing three bits; 16 bits thus need six digits, the first of which only represents one bit:

--------------------------------

Type|---|Perm bits

1000 000 111101101
1 0   0   7  5  5

1000 000 110100100
1 0   0   6  4  4

--------------------------------

Git doesn’t store arbitrary modes, only a subset of the values are allowed, from the usual POSIX types and modes (in octal, 12 for a symbolic link, 10 for a regular file, 04 for a directory) to which git adds 16 for Git links. The mode is appended, using four octal digits. For files, you’ll only ever see 100755 or 100644 (although 100664 is also technically possible); directories are 040000 (permissions are ignored), symbolic links 120000. The set-user-ID, set-group-ID and sticky bits aren’t supported at all (they would be stored in the unused bits).
         */

        private int GetOctalMode(ushort unixMode)
        {
            const int type1Mask = 0x8000;
            const int type2Mask = 0x7000;
            const int userMask = 0x1c0;
            const int groupMask = 0x38;
            const int othersMask = 0x7;

            return
                ((unixMode & type1Mask) >> 15) * 100000 +
                ((unixMode & type2Mask) >> 12) * 10000 +
                ((unixMode & userMask) >> 6) * 100 +
                ((unixMode & groupMask) >> 3) * 10 +
                (unixMode & othersMask);
        }

        public async Task<LzHeader> GetHeaderLevel0(Stream stream, long headerOffset, byte[] data)
        {
            var headerSize = data[0];
            var checksum = data[1];

            var remainSize = headerSize + 2 - COMMON_HEADER_SIZE;
            if (remainSize <= 0)
            {
                throw new IOException("Invalid header size");
            }

            var remainingBytes = await stream.ReadBytes(remainSize);
            var headerBytes = data.Concat(remainingBytes).ToArray();
            var headerReader = new BinaryReader(new MemoryStream(headerBytes));

            if (ChecksumHelper.CalcSum(headerBytes, I_METHOD, headerSize) != checksum)
            {
                throw new IOException("Checksum error");
            }

            var header = new LzHeader
            {
                SizeFieldLength = 2,
                HeaderSize = headerSize,
                HeaderOffset = headerOffset
            };

            // skip already read header size and checksum (first 2 data bytes)
            headerReader.BaseStream.Seek(2, SeekOrigin.Begin);

            header.Method = Encoding.ASCII.GetString(headerReader.ReadBytes(5));
            header.PackedSize = headerReader.ReadInt32();
            header.OriginalSize = headerReader.ReadInt32();
            header.UnixLastModifiedStamp = DateHelper.ToGenericTimeStamp(headerReader.ReadInt32());
            header.Attribute = headerReader.ReadByte();
            header.HeaderLevel = headerReader.ReadByte();

            var nameLength = headerReader.ReadByte();
            header.Name = options.Encoding.GetString(headerReader.ReadBytes(nameLength));

            /* defaults for other type */
            header.UnixMode = Constants.UNIX_FILE_REGULAR | Constants.UNIX_RW_RW_RW;
            header.UnixGid = 0;
            header.UnixGid = 0;

            var extendSize = headerSize + 2 - nameLength - 24;

            if (extendSize < 0)
            {
                if (extendSize == -2)
                {
                    /* CRC field is not given */
                    header.ExtendType = Constants.EXTEND_GENERIC;
                    header.HasCrc = false;

                    return header;
                }

                throw new IOException("Unknown header");
            }

            header.HasCrc = true;
            header.Crc = headerReader.ReadInt16();

            if (extendSize != 0)
            {
                header.ExtendType = headerReader.ReadByte();
                extendSize--;

                if (header.ExtendType == Constants.EXTEND_UNIX)
                {
                    if (extendSize >= 11)
                    {
                        header.MinorVersion = headerReader.ReadByte();
                        header.UnixLastModifiedStamp = DateHelper.ToUnixTimeStamp(headerReader.ReadInt32());
                        header.UnixMode = headerReader.ReadUInt16();
                        header.UnixUid = headerReader.ReadUInt16();
                        header.UnixGid = headerReader.ReadUInt16();
                        extendSize -= 11;
                    }
                    else
                    {
                        header.ExtendType = Constants.EXTEND_GENERIC;
                    }
                }

                if (extendSize > 0)
                {
                    headerReader.ReadBytes(extendSize);
                }
            }

            header.HeaderSize += 2;

            return header;
        }

        public async Task<LzHeader> GetHeaderLevel1(Stream stream, long headerOffset, byte[] data)
        {
            var headerSize = data[0];
            var checksum = data[1];

            var remainSize = headerSize + 2 - COMMON_HEADER_SIZE;
            if (remainSize <= 0)
            {
                throw new IOException("Invalid header size");
            }

            var remainingBytes = await stream.ReadBytes(remainSize);
            var headerBytes = data.Concat(remainingBytes).ToArray();
            var headerReader = new BinaryReader(new MemoryStream(headerBytes));

            if (ChecksumHelper.CalcSum(headerBytes, I_METHOD, headerSize) != checksum)
            {
                throw new IOException("Checksum error");
            }

            var header = new LzHeader
            {
                SizeFieldLength = 2,
                HeaderSize = headerSize,
                HeaderOffset = headerOffset
            };

            // skip already read header size and checksum (first 2 data bytes)
            headerReader.BaseStream.Seek(2, SeekOrigin.Begin);

            header.Method = Encoding.ASCII.GetString(headerReader.ReadBytes(5));
            header.PackedSize = headerReader.ReadInt32();
            header.OriginalSize = headerReader.ReadInt32();
            header.UnixLastModifiedStamp = DateHelper.ToGenericTimeStamp(headerReader.ReadInt32());
            header.Attribute = headerReader.ReadByte();
            header.HeaderLevel = headerReader.ReadByte();

            var nameLength = headerReader.ReadByte();
            header.Name = options.Encoding.GetString(headerReader.ReadBytes(nameLength));

            /* defaults for other type */
            header.UnixMode = Constants.UNIX_FILE_REGULAR | Constants.UNIX_RW_RW_RW;
            header.UnixGid = 0;
            header.UnixGid = 0;

            header.HasCrc = true;
            header.Crc = headerReader.ReadInt16();
            header.ExtendType = headerReader.ReadByte();

            var skipBytes = header.HeaderSize + 2 - nameLength - I_LEVEL1_HEADER_SIZE;
            if (skipBytes > 0)
            {
                await stream.ReadBytes(skipBytes); /* skip old style extend header */
            }

            var extendSize = (int)headerReader.ReadInt16();
            extendSize = await GetExtendedHeader(stream, header, extendSize, null);
            if (extendSize == -1)
            {
                throw new IOException();
            }

            /* On level 1 header, size fields should be adjusted. */
            /* the `packed_size' field contains the extended header size. */
            /* the `header_size' field does not. */
            header.PackedSize -= extendSize;
            header.HeaderSize += extendSize;
            header.HeaderSize += 2;

            return header;
        }

        public async Task<LzHeader> GetHeaderLevel2(Stream stream, long headerOffset, byte[] data)
        {
            var headerSize = (data[1] << 8) + data[0];
            var remainSize = headerSize - I_LEVEL2_HEADER_SIZE;
            if (remainSize <= 0)
            {
                throw new IOException("Invalid header size");
            }

            var remainingBytes = await stream.ReadBytes(I_LEVEL2_HEADER_SIZE - COMMON_HEADER_SIZE);
            var headerBytes = data.Concat(remainingBytes).ToArray();
            var headerReader = new BinaryReader(new MemoryStream(headerBytes));

            var header = new LzHeader
            {
                SizeFieldLength = 2,
                HeaderSize = headerSize,
                HeaderOffset = headerOffset
            };

            // skip already read header size (first 2 data bytes)
            headerReader.BaseStream.Seek(2, SeekOrigin.Begin);

            header.Method = Encoding.ASCII.GetString(headerReader.ReadBytes(5));
            header.PackedSize = headerReader.ReadInt32();
            header.OriginalSize = headerReader.ReadInt32();
            header.UnixLastModifiedStamp = DateHelper.ToUnixTimeStamp(headerReader.ReadInt32());
            header.Attribute = headerReader.ReadByte();
            header.HeaderLevel = headerReader.ReadByte();

            /* defaults for other type */
            header.UnixMode = Constants.UNIX_FILE_REGULAR | Constants.UNIX_RW_RW_RW;
            header.UnixGid = 0;
            header.UnixUid = 0;

            header.HasCrc = true;
            header.Crc = headerReader.ReadInt16();
            header.ExtendType = headerReader.ReadByte();
            var extendSize = (int)headerReader.ReadInt16();

            var crcIo = new CrcIo();
            crcIo.InitializeCrc();
            crcIo.calccrc(headerBytes, (uint)headerReader.BaseStream.Position);

            extendSize = await GetExtendedHeader(stream, header, extendSize, crcIo);
            if (extendSize == -1)
            {
                throw new IOException();
            }

            var padding = headerSize - I_LEVEL2_HEADER_SIZE - extendSize;
            /* padding should be 0 or 1 */
            if (padding != 0 && padding != 1)
            {
                throw new IOException($"Invalid header size (padding: {padding})");
            }

            while (padding-- > 0)
            {
                crcIo.UPDATE_CRC(headerReader.ReadByte());
            }

            var headerCrc = (short)crcIo.crc;
            if (header.HeaderCrc != headerCrc)
            {
                throw new IOException("header CRC error");
            }

            return header;
        }

        public async Task<LzHeader> GetHeaderLevel3(Stream stream, long headerOffset, byte[] data)
        {
            var remainingBytes = await stream.ReadBytes(I_LEVEL3_HEADER_SIZE - COMMON_HEADER_SIZE);
            var headerBytes = data.Concat(remainingBytes).ToArray();
            var headerReader = new BinaryReader(new MemoryStream(headerBytes));

            var header = new LzHeader
            {
                HeaderOffset = headerOffset
            };

            header.SizeFieldLength = headerReader.ReadInt16();
            header.Method = Encoding.ASCII.GetString(headerReader.ReadBytes(5));
            header.PackedSize = headerReader.ReadInt32();
            header.OriginalSize = headerReader.ReadInt32();
            header.UnixLastModifiedStamp = DateHelper.ToUnixTimeStamp(headerReader.ReadInt32());
            header.Attribute = headerReader.ReadByte();
            header.HeaderLevel = headerReader.ReadByte();

            /* defaults for other type */
            header.UnixMode = Constants.UNIX_FILE_REGULAR | Constants.UNIX_RW_RW_RW;
            header.UnixGid = 0;
            header.UnixUid = 0;

            header.HasCrc = true;
            header.Crc = headerReader.ReadInt16();
            header.ExtendType = headerReader.ReadByte();
            header.HeaderSize = headerReader.ReadInt32();
            var remainSize = header.HeaderSize - I_LEVEL3_HEADER_SIZE;
            if (remainSize < 0)
            {
                throw new IOException("Invalid header size");
            }

            var extendSize = headerReader.ReadInt32();

            var crcIo = new CrcIo();
            crcIo.InitializeCrc();
            crcIo.calccrc(headerBytes, (uint)headerReader.BaseStream.Position);

            extendSize = await GetExtendedHeader(stream, header, extendSize, crcIo);
            if (extendSize == -1)
            {
                throw new IOException();
            }

            var padding = remainSize - extendSize;
            /* padding should be 0 */
            if (padding != 0)
            {
                throw new IOException($"Invalid header size (padding: {padding})");
            }

            var headerCrc = (short)crcIo.crc;
            if (header.HeaderCrc != headerCrc)
            {
                throw new IOException("header CRC error");
            }

            return header;
        }

        public async Task<int> GetExtendedHeader(Stream stream, LzHeader header, int headerSize, CrcIo crcIo)
        {
            if (header.HeaderLevel == 0)
            {
                return 0;
            }

            var extendedHeaderSize = 0;
            var n = 1 + header.SizeFieldLength; /* `ext-type' + `next-header size' */
            string directory = null;

            while (headerSize > 0)
            {
                extendedHeaderSize += headerSize;

                var data = await stream.ReadBytes(headerSize);
                var dataReader = new BinaryReader(new MemoryStream(data));
                var extType = dataReader.ReadByte();

                switch (extType)
                {
                    case 0:
                        /* header crc (CRC-16) */
                        header.HeaderCrc = dataReader.ReadInt16();
                        /* clear buffer for CRC calculation. */
                        data[1] = data[2] = 0;
                        dataReader.ReadBytes(headerSize - n - 2);
                        break;
                    case 1:
                        // filename
                        header.Name = options.Encoding.GetString(dataReader.ReadBytes(headerSize - n));
                        break;
                    case 2:
                        // directory
                        var directoryBytes = dataReader.ReadBytes(headerSize - n);
                        for (var i = 0; i < directoryBytes.Length; i++)
                        {
                            if (directoryBytes[i] == 255)
                            {
                                directoryBytes[i] = (byte)'\\';
                            }
                        }

                        directory = options.Encoding.GetString(directoryBytes);
                        break;
                    case 0x40:
                        // MS-DOS attribute
                        header.Attribute = dataReader.ReadInt16();
                        break;
                    case 0x41:
                        /* Windows time stamp (FILETIME structure) */
                        /* it is time in 100 nano seconds since 1601-01-01 00:00:00 */

                        dataReader.ReadBytes(8); /* create time is ignored */

                        /* set last modified time */
                        if (header.HeaderLevel >= 2)
                        {
                            dataReader.ReadBytes(8); /* time_t has been already set */
                        }
                        else
                        {
                            header.UnixLastModifiedStamp =
                                DateHelper.ToUnixTimeStamp(wintime_to_unix_stamp(dataReader));
                        }

                        dataReader.ReadBytes(8); /* last access time is ignored */
                        break;
                    case 0x42:
                        /* 64bits file size header (UNLHA32 extension) */
                        header.PackedSize = dataReader.ReadInt64();
                        header.OriginalSize = dataReader.ReadInt64();
                        break;
                    case 0x50:
                        /* UNIX permission */
                        header.UnixMode = dataReader.ReadUInt16();
                        break;
                    case 0x51:
                        /* UNIX gid and uid */
                        header.UnixGid = dataReader.ReadUInt16();
                        header.UnixUid = dataReader.ReadUInt16();
                        break;
                    case 0x52:
                        /* UNIX group name */
                        header.UnixGroupName = options.Encoding.GetString(dataReader.ReadBytes(headerSize - n - 1));
                        dataReader.ReadByte();
                        break;
                    case 0x53:
                        /* UNIX user name */
                        header.UnixUserName = options.Encoding.GetString(dataReader.ReadBytes(headerSize - n - 1));
                        dataReader.ReadByte();
                        break;
                    case 0x54:
                        header.UnixLastModifiedStamp = DateHelper.ToUnixTimeStamp(dataReader.ReadInt32());
                        break;
                    default:
                        /* other headers */
                        /* 0x39: multi-disk header
                           0x3f: uncompressed comment
                           0x42: 64bit large file size
                           0x48-0x4f(?): reserved for authenticity verification
                           0x7d: encapsulation
                           0x7e: extended attribute - platform information
                           0x7f: extended attribute - permission, owner-id and timestamp
                                 (level 3 on OS/2)
                           0xc4: compressed comment (dict size: 4096)
                           0xc5: compressed comment (dict size: 8192)
                           0xc6: compressed comment (dict size: 16384)
                           0xc7: compressed comment (dict size: 32768)
                           0xc8: compressed comment (dict size: 65536)
                           0xd0-0xdf(?): operating systemm specific information
                           0xfc: encapsulation (another opinion)
                           0xfe: extended attribute - platform information(another opinion)
                           0xff: extended attribute - permission, owner-id and timestamp
                                 (level 3 on UNLHA32) */
                        dataReader.ReadBytes(headerSize - n);
                        break;
                }

                if (crcIo != null)
                {
                    crcIo.calccrc(data, (uint)headerSize);
                }

                headerSize = header.SizeFieldLength == 2 ? dataReader.ReadInt16() : dataReader.ReadInt32();
            }

            /* concatenate dirname and filename */
            if (!string.IsNullOrWhiteSpace(directory))
            {
                header.Name = string.Concat(directory, header.Name);
            }

            return extendedHeaderSize;
        }

        private static long wintime_to_unix_stamp(BinaryReader dataReader)
        {
#if HAVE_UINT64_T
    uint64_t t;
    uint64_t epoch = ((uint64_t)0x019db1de << 32) + 0xd53e8000;
                     /* 0x019db1ded53e8000ULL: 1970-01-01 00:00:00 (UTC) */

    t = (unsigned long)get_longword();
    t |= (uint64_t)(unsigned long)get_longword() << 32;
    t = (t - epoch) / 10000000;
    return t;
#else
            int i, borrow;
            int t, q, x;
            var wintime = new int[8];
            byte[] epoch = { 0x01, 0x9d, 0xb1, 0xde, 0xd5, 0x3e, 0x80, 0x00 };
            /* 1970-01-01 00:00:00 (UTC) */
            /* wintime -= epoch */
            borrow = 0;
            for (i = 7; i >= 0; i--)
            {
                wintime[i] = dataReader.ReadByte() - epoch[i] - borrow;
                borrow = (wintime[i] > 0xff) ? 1 : 0;
                wintime[i] &= 0xff;
            }

            /* q = wintime / 10000000 */
            t = q = 0;
            x = 10000000; /* x: 24bit */
            for (i = 0; i < 8; i++)
            {
                t = (t << 8) + wintime[i]; /* 24bit + 8bit. t must be 32bit variable */
                q <<= 8; /* q must be 32bit (time_t) */
                q += t / x;
                t %= x; /* 24bit */
            }

            return q;
#endif
        }
    }
}