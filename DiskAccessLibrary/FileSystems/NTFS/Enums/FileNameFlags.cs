using System;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    [Flags]
    public enum FileNameFlags : byte
    {
        POSIX = 0x00, // 255 Unicode characters
        Win32 = 0x01, // FILE_NAME_NTFS, 255 Unicode characters
        DOS = 0x02,   // FILE_NAME_DOS, 8.3 filename
    }
}
