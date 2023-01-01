/* ------------------------------------------------------------------------ */
/* LHa for UNIX    Archiver Driver                                          */
/*                                                                          */
/*      Modified                Nobutaka Watazaki                           */
/*                                                                          */
/*  Ver. 1.14   Soruce All chagned              1995.01.14  N.Watazaki      */
/*  Ver. 1.14i  Modified and bug fixed          2000.10.06  t.okamoto       */
/* ------------------------------------------------------------------------ */
/*
    Included...
        lharc.h     interface.h     slidehuf.h
*/
namespace Hst.Compression.Lha
{
    using System;

    public class LzHeader
    {
        public int HeaderSize { get; set; }
        public int SizeFieldLength { get; set; }
        public string Method { get; set; }
        public long PackedSize { get; set; }
        public long OriginalSize { get; set; }
        public int Attribute { get; set; }
        public int HeaderLevel { get; set; }
        public string Name { get; set; }
        public string RealName { get; set; }
        public short Crc { get; set; }
        public bool HasCrc { get; set; }
        public short HeaderCrc { get; set; }
        public int ExtendType { get; set; }
        public byte MinorVersion { get; set; }

        public DateTime LastModifiedStamp { get; set; }
        public DateTime UnixLastModifiedStamp { get; set; }
        public ushort UnixMode { get; set; }
        public ushort UnixGid { get; set; }
        public ushort UnixUid { get; set; }
        public string UnixGroupName { get; set; }
        public string UnixUserName { get; set; }
        
        public long HeaderOffset { get; set; }
    }
}