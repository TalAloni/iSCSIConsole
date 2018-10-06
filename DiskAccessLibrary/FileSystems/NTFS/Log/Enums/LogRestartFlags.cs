using System;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    [Flags]
    public enum LogRestartFlags : ushort
    {
        RestartSinglePageIO = 0x0001, // RESTART_SINGLE_PAGE_IO
        LogFileIsClean = 0x0002,      // NTFS v3.1
    }
}
