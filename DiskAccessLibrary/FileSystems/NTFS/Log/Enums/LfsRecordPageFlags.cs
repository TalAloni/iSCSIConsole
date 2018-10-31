using System;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    [Flags]
    public enum LfsRecordPageFlags : uint
    {
        /// <summary>Indicates that a log record ends on this page</summary>
        RecordEnd = 0x00000001, // LOG_PAGE_LOG_RECORD_END
    }
}
