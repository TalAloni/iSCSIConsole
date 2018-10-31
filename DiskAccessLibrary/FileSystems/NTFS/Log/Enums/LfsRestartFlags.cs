using System;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    [Flags]
    public enum LfsRestartFlags : ushort
    {
        /// <summary>Each log page is written in a separate IO transfer (PageCount of 1)</summary>
        SinglePageIO = 0x0001, // RESTART_SINGLE_PAGE_IO

        /// <summary>Indicated that the volume is dismounted cleanly</summary>
        CleanDismount = 0x0002, // NTFS v3.1
    }
}
