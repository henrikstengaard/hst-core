namespace Hst.Compression.Lha
{
    /// <summary>
    /// Constants for lha archives: compression methods, operating system types and unix file modes.
    /// </summary>
    public static class Constants
    {
        // operating system types (extend type) stored in headers
        public const byte EXTEND_GENERIC = 0;
        public const byte EXTEND_UNIX = (byte)'U';
        public const byte EXTEND_MSDOS = (byte)'M';
        public const byte EXTEND_MACOS = (byte)'m';
        public const byte EXTEND_OS9 = (byte)'9';
        public const byte EXTEND_OS2 = (byte)'2';
        public const byte EXTEND_OS68K = (byte)'K';
        public const byte EXTEND_OS386 = (byte)'3';
        public const byte EXTEND_HUMAN = (byte)'H';
        public const byte EXTEND_CPM = (byte)'C';
        public const byte EXTEND_FLEX = (byte)'F';
        public const byte EXTEND_RUNSER = (byte)'R';
        public const byte EXTEND_AMIGA = (byte)'A';
        public const byte EXTEND_ATARI = (byte)'a';
        public const byte EXTEND_WIN95 = (byte)'w';
        public const byte EXTEND_WINNT = (byte)'W';
        public const byte EXTEND_TOWNSOS = (byte)'T';
        public const byte EXTEND_XOSK = (byte)'X';
        public const byte EXTEND_JAVA = (byte)'J';
        public const byte EXTEND_LHARK = (byte)' ';

        // unix file types and permissions
        public const int UNIX_FILE_TYPEMASK = 0xf000;
        public const int UNIX_FILE_REGULAR = 0x8000;
        public const int UNIX_FILE_DIRECTORY = 0x4000;
        public const int UNIX_FILE_SYMLINK = 0xa000;
        public const int UNIX_SETUID = 0x800;
        public const int UNIX_SETGID = 0x400;
        public const int UNIX_STICKYBIT = 0x200;
        public const int UNIX_OWNER_READ_PERM = 0x100;
        public const int UNIX_OWNER_WRITE_PERM = 0x80;
        public const int UNIX_OWNER_EXEC_PERM = 0x40;
        public const int UNIX_GROUP_READ_PERM = 0x20;
        public const int UNIX_GROUP_WRITE_PERM = 0x10;
        public const int UNIX_GROUP_EXEC_PERM = 0x8;
        public const int UNIX_OTHER_READ_PERM = 0x4;
        public const int UNIX_OTHER_WRITE_PERM = 0x2;
        public const int UNIX_OTHER_EXEC_PERM = 0x1;
        public const int UNIX_RW_RW_RW = 0x1b6;

        // compression methods
        public const string LZHUFF0_METHOD = "-lh0-";
        public const string LZHUFF1_METHOD = "-lh1-";
        public const string LZHUFF2_METHOD = "-lh2-";
        public const string LZHUFF3_METHOD = "-lh3-";
        public const string LZHUFF4_METHOD = "-lh4-";
        public const string LZHUFF5_METHOD = "-lh5-";
        public const string LZHUFF6_METHOD = "-lh6-";
        public const string LZHUFF7_METHOD = "-lh7-";
        public const string LZHUFFX_METHOD = "-lhx-";
        public const string LHARK7_METHOD = "-lk7-";
        public const string LARC_METHOD = "-lzs-";
        public const string LARC5_METHOD = "-lz5-";
        public const string LARC4_METHOD = "-lz4-";
        public const string LZHDIRS_METHOD = "-lhd-";
        public const string PMARC0_METHOD = "-pm0-";
        public const string PMARC1_METHOD = "-pm1-";
        public const string PMARC2_METHOD = "-pm2-";
    }
}
