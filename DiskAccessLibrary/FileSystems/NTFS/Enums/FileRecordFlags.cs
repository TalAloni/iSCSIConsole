using System;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <summary>
    /// FILE_RECORD_SEGMENT_HEADER: https://msdn.microsoft.com/de-de/windows/desktop/bb470124
    /// </summary>
    [Flags]
    public enum FileRecordFlags : ushort
    {
        InUse = 0x0001,       // FILE_RECORD_SEGMENT_IN_USE
        IsDirectory = 0x0002, // FILE_FILE_NAME_INDEX_PRESENT
    }
}
