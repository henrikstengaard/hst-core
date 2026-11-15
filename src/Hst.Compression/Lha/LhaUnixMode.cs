namespace Hst.Compression.Lha
{
    /// <summary>
    /// Unix file type and permission bits of <see cref="LhaEntry.UnixMode"/>.
    /// </summary>
    public static class LhaUnixMode
    {
        public const int TypeMask = 0xf000;
        public const int Regular = 0x8000;
        public const int Directory = 0x4000;
        public const int Symlink = 0xa000;
        public const int SetUid = 0x800;
        public const int SetGid = 0x400;
        public const int Sticky = 0x200;
        public const int OwnerRead = 0x100;
        public const int OwnerWrite = 0x80;
        public const int OwnerExecute = 0x40;
        public const int GroupRead = 0x20;
        public const int GroupWrite = 0x10;
        public const int GroupExecute = 0x8;
        public const int OtherRead = 0x4;
        public const int OtherWrite = 0x2;
        public const int OtherExecute = 0x1;
    }
}
