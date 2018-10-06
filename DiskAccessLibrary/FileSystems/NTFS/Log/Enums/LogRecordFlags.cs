using System;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    [Flags]
    public enum LogRecordFlags : ushort
    {
        MultiPage = 0x0001, // LOG_RECORD_MULTI_PAGE
    }
}
