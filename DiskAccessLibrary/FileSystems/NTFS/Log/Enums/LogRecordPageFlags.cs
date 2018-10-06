using System;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    [Flags]
    public enum LogRecordPageFlags : uint
    {
        RecordEnd = 0x00000001, // LOG_PAGE_LOG_RECORD_END
    }
}
