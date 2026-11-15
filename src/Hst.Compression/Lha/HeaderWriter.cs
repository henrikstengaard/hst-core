/* ------------------------------------------------------------------------ */
/* LHa for UNIX                                                             */
/*              header.c -- header manipulate functions                     */
/*                                                                          */
/*      Modified                Nobutaka Watazaki                           */
/*                                                                          */
/*  Original                                                Y.Tagawa        */
/*  modified                                    1991.12.16  M.Oki           */
/*  Ver. 1.10  Symbolic Link added              1993.10.01  N.Watazaki      */
/*  Ver. 1.13b Symbolic Link Bug Fix            1994.08.22  N.Watazaki      */
/*  Ver. 1.14  Source All chagned               1995.01.14  N.Watazaki      */
/*  Ver. 1.14i bug fixed                        2000.10.06  t.okamoto       */
/*  Ver. 1.14i Contributed UTF-8 convertion for Mac OS X                    */
/*                                              2002.06.29  Hiroto Sakai    */
/*  Ver. 1.14i autoconfiscated & rewritten      2003.02.23  Koji Arai       */
/* ------------------------------------------------------------------------ */
namespace Hst.Compression.Lha
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Writes lha headers level 0, 1 and 2.
    /// </summary>
    internal class HeaderWriter
    {
        private const int I_HEADER_SIZE = 0; /* level 0,1,2   */
        private const int I_HEADER_CHECKSUM = 1; /* level 0,1     */
        private const int I_METHOD = 2; /* level 0,1,2,3 */
        private const int I_PACKED_SIZE = 7; /* level 0,1,2,3 */

        private const int I_GENERIC_HEADER_SIZE = 24; /* + name_length */
        private const int I_LEVEL0_HEADER_SIZE = 36; /* + name_length (unix extended) */
        private const int I_LEVEL1_HEADER_SIZE = 27; /* + name_length */

        private const byte CURRENT_UNIX_MINOR_VERSION = 0x00;
        private const byte LHA_PATHSEP = 0xff;

        private readonly Encoding encoding;
        private readonly bool genericFormat;

        public HeaderWriter(Encoding encoding, bool genericFormat)
        {
            this.encoding = encoding;
            this.genericFormat = genericFormat;
        }

        /// <summary>
        /// Create header bytes for lha header.
        /// </summary>
        /// <param name="hdr">Header to write. Packed size is size of entry data excluding headers.</param>
        /// <returns>Header bytes.</returns>
        public byte[] Write(LzHeader hdr)
        {
            string pathname;
            if ((hdr.UnixMode & Constants.UNIX_FILE_SYMLINK) == Constants.UNIX_FILE_SYMLINK)
            {
                pathname = string.Concat(hdr.Name.Replace('|', '_'), "|", hdr.RealName ?? string.Empty);
            }
            else
            {
                pathname = hdr.Name ?? string.Empty;
            }

            switch (hdr.HeaderLevel)
            {
                case 0:
                    return WriteHeaderLevel0(hdr, ConvertFilename(pathname, (byte)'\\'));
                case 1:
                    return WriteHeaderLevel1(hdr, ConvertFilename(pathname, LHA_PATHSEP));
                case 2:
                    return WriteHeaderLevel2(hdr, ConvertFilename(pathname, LHA_PATHSEP));
                default:
                    throw new ArgumentException($"Unknown header (level {hdr.HeaderLevel})", nameof(hdr));
            }
        }

        /// <summary>
        /// Convert filename to bytes using encoding and replace path separators '/' and '\' with delimiter.
        /// </summary>
        private byte[] ConvertFilename(string pathname, byte delimiter)
        {
            var parts = pathname.Split('/', '\\');
            var bytes = new List<byte>();
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    bytes.Add(delimiter);
                }

                bytes.AddRange(encoding.GetBytes(parts[i]));
            }

            return bytes.ToArray();
        }

        private static long UnixToGenericStamp(DateTime t)
        {
            var year = Math.Min(Math.Max(t.Year, 1980), 1980 + 127);
            return ((long)(year - 1980) << 25) +
                   (t.Month << 21) +
                   (t.Day << 16) +
                   (t.Hour << 11) +
                   (t.Minute << 5) +
                   (t.Second / 2);
        }

        private static long ToUnixStamp(DateTime t)
        {
            if (t.Kind == DateTimeKind.Local)
            {
                t = t.ToUniversalTime();
            }

            var seconds = (long)(t - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            return Math.Min(Math.Max(seconds, 0), uint.MaxValue);
        }

        private static int CalcSum(byte[] p, int offset, int len) => ChecksumHelper.CalcSum(p, offset, len);

        private void WriteUnixInfo(BinaryWriter writer, LzHeader hdr)
        {
            /* UNIX specific informations */

            writer.Write((ushort)5); /* size */
            writer.Write((byte)0x50); /* permission */
            writer.Write(hdr.UnixMode);

            writer.Write((ushort)7); /* size */
            writer.Write((byte)0x51); /* gid and uid */
            writer.Write(hdr.UnixGid);
            writer.Write(hdr.UnixUid);

            if (!string.IsNullOrEmpty(hdr.UnixGroupName))
            {
                var group = encoding.GetBytes(hdr.UnixGroupName);
                writer.Write((ushort)(group.Length + 3)); /* size */
                writer.Write((byte)0x52); /* group name */
                writer.Write(group);
            }

            if (!string.IsNullOrEmpty(hdr.UnixUserName))
            {
                var user = encoding.GetBytes(hdr.UnixUserName);
                writer.Write((ushort)(user.Length + 3)); /* size */
                writer.Write((byte)0x53); /* user name */
                writer.Write(user);
            }

            if (hdr.HeaderLevel == 1)
            {
                writer.Write((ushort)7); /* size */
                writer.Write((byte)0x54); /* time stamp */
                writer.Write((uint)ToUnixStamp(hdr.UnixLastModifiedStamp));
            }
        }

        private static byte[] GetMethodBytes(LzHeader hdr)
        {
            var method = Encoding.ASCII.GetBytes(hdr.Method ?? string.Empty);
            if (method.Length != 5)
            {
                throw new ArgumentException($"Invalid method \"{hdr.Method}\"", nameof(hdr));
            }

            return method;
        }

        private byte[] WriteHeaderLevel0(LzHeader hdr, byte[] pathname)
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);

            writer.Write((byte)0x00); /* header size */
            writer.Write((byte)0x00); /* check sum */
            writer.Write(GetMethodBytes(hdr));
            writer.Write((uint)hdr.PackedSize);
            writer.Write((uint)hdr.OriginalSize);
            writer.Write((uint)UnixToGenericStamp(hdr.UnixLastModifiedStamp));
            writer.Write((byte)hdr.Attribute);
            writer.Write((byte)hdr.HeaderLevel); /* level 0 */

            /* write pathname (level 0 header contains the directory part) */
            var nameLength = pathname.Length;
            var limit = genericFormat
                ? 255 - I_GENERIC_HEADER_SIZE + 2
                : 255 - I_LEVEL0_HEADER_SIZE + 2;

            if (nameLength > limit)
            {
                throw new ArgumentException($"The length of pathname \"{hdr.Name}\" is too long", nameof(hdr));
            }

            writer.Write((byte)nameLength);
            writer.Write(pathname, 0, nameLength);
            writer.Write((ushort)hdr.Crc);

            int headerSize;
            if (genericFormat)
            {
                headerSize = I_GENERIC_HEADER_SIZE + nameLength - 2;
            }
            else
            {
                /* write old-style extend header */
                writer.Write(Constants.EXTEND_UNIX);
                writer.Write(CURRENT_UNIX_MINOR_VERSION);
                writer.Write((uint)ToUnixStamp(hdr.UnixLastModifiedStamp));
                writer.Write(hdr.UnixMode);
                writer.Write(hdr.UnixUid);
                writer.Write(hdr.UnixGid);

                /* size of extended header is 12 */
                headerSize = I_LEVEL0_HEADER_SIZE + nameLength - 2;
            }

            writer.Flush();
            var data = stream.ToArray();
            data[I_HEADER_SIZE] = (byte)headerSize;
            data[I_HEADER_CHECKSUM] = (byte)CalcSum(data, I_METHOD, headerSize);

            return data;
        }

        private static void SplitPathname(byte[] pathname, out byte[] basename, out byte[] dirname)
        {
            var index = Array.LastIndexOf(pathname, LHA_PATHSEP);
            if (index >= 0)
            {
                basename = pathname.Skip(index + 1).ToArray();
                dirname = pathname.Take(index + 1).ToArray();
            }
            else
            {
                basename = pathname;
                dirname = Array.Empty<byte>();
            }
        }

        private byte[] WriteHeaderLevel1(LzHeader hdr, byte[] pathname)
        {
            SplitPathname(pathname, out var basename, out var dirname);
            var nameLength = basename.Length;
            var dirLength = dirname.Length;

            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);

            writer.Write((byte)0x00); /* header size */
            writer.Write((byte)0x00); /* check sum */
            writer.Write(GetMethodBytes(hdr));
            writer.Write((uint)hdr.PackedSize);
            writer.Write((uint)hdr.OriginalSize);
            writer.Write((uint)UnixToGenericStamp(hdr.UnixLastModifiedStamp));
            writer.Write((byte)0x20);
            writer.Write((byte)hdr.HeaderLevel); /* level 1 */

            /* level 1 header: write filename (basename only) */
            const int limit = 255 - I_LEVEL1_HEADER_SIZE + 2;
            if (nameLength > limit)
            {
                writer.Write((byte)0); /* name length */
            }
            else
            {
                writer.Write((byte)nameLength);
                writer.Write(basename);
            }

            writer.Write((ushort)hdr.Crc);
            writer.Write(genericFormat ? Constants.EXTEND_GENERIC : Constants.EXTEND_UNIX);

            /* write extend header from here. */

            writer.Flush();
            var extendHeaderTop = stream.Position + 2; /* +2 for the field `next header size' */
            var headerSize = (int)(extendHeaderTop - 2);

            /* write filename and dirname */

            if (nameLength > limit)
            {
                writer.Write((ushort)(nameLength + 3)); /* size */
                writer.Write((byte)0x01); /* filename */
                writer.Write(basename);
            }

            if (dirLength > 0)
            {
                writer.Write((ushort)(dirLength + 3)); /* size */
                writer.Write((byte)0x02); /* dirname */
                writer.Write(dirname);
            }

            if (!genericFormat)
            {
                WriteUnixInfo(writer, hdr);
            }

            writer.Write((ushort)0x0000); /* next header size */
            writer.Flush();

            var extendHeaderSize = stream.Position - extendHeaderTop;
            var data = stream.ToArray();

            /* On level 1 header, the packed size field is contains the ext-header */
            var packedSize = hdr.PackedSize + extendHeaderSize;

            /* put `skip size' */
            var packedSizeBytes = BitConverter.GetBytes((uint)packedSize);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(packedSizeBytes);
            }

            Array.Copy(packedSizeBytes, 0, data, I_PACKED_SIZE, 4);

            data[I_HEADER_SIZE] = (byte)headerSize;
            data[I_HEADER_CHECKSUM] = (byte)CalcSum(data, I_METHOD, headerSize);

            return data;
        }

        private byte[] WriteHeaderLevel2(LzHeader hdr, byte[] pathname)
        {
            SplitPathname(pathname, out var basename, out var dirname);
            var nameLength = basename.Length;
            var dirLength = dirname.Length;

            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);

            writer.Write((ushort)0x0000); /* header size */
            writer.Write(GetMethodBytes(hdr));
            writer.Write((uint)hdr.PackedSize);
            writer.Write((uint)hdr.OriginalSize);
            writer.Write((uint)ToUnixStamp(hdr.UnixLastModifiedStamp));
            writer.Write((byte)0x20);
            writer.Write((byte)hdr.HeaderLevel); /* level 2 */

            writer.Write((ushort)hdr.Crc);
            writer.Write(genericFormat ? Constants.EXTEND_GENERIC : Constants.EXTEND_UNIX);

            /* write extend header from here. */

            /* write common header */
            writer.Write((ushort)5);
            writer.Write((byte)0x00);
            writer.Flush();
            var headerCrcPosition = (int)stream.Position;
            writer.Write((ushort)0x0000); /* header CRC */

            /* write filename and dirname */
            /* must have this header, even if the name_length is 0. */
            writer.Write((ushort)(nameLength + 3)); /* size */
            writer.Write((byte)0x01); /* filename */
            writer.Write(basename);

            if (dirLength > 0)
            {
                writer.Write((ushort)(dirLength + 3)); /* size */
                writer.Write((byte)0x02); /* dirname */
                writer.Write(dirname);
            }

            if (!genericFormat)
            {
                WriteUnixInfo(writer, hdr);
            }

            writer.Write((ushort)0x0000); /* next header size */
            writer.Flush();

            var headerSize = (int)stream.Position;
            if ((headerSize & 0xff) == 0)
            {
                /* cannot put zero at the first byte on level 2 header. */
                /* adjust header size. */
                writer.Write((byte)0); /* padding */
                writer.Flush();
                headerSize++;
            }

            if (headerSize > ushort.MaxValue)
            {
                throw new ArgumentException($"Header for \"{hdr.Name}\" is too large", nameof(hdr));
            }

            var data = stream.ToArray();

            /* put header size */
            data[I_HEADER_SIZE] = (byte)(headerSize & 0xff);
            data[I_HEADER_SIZE + 1] = (byte)((headerSize >> 8) & 0xff);

            /* put header CRC in extended header */
            var crcIo = new CrcIo();
            crcIo.InitializeCrc();
            var hcrc = crcIo.calccrc(data, 0, headerSize);
            data[headerCrcPosition] = (byte)(hcrc & 0xff);
            data[headerCrcPosition + 1] = (byte)((hcrc >> 8) & 0xff);

            return data;
        }
    }
}
