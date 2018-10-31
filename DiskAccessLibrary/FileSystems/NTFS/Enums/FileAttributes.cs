using System;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    [Flags]
    public enum FileAttributes : uint
    {
        Readonly = 0x00000001,     // FILE_ATTRIBUTE_READONLY
        Hidden = 0x00000002,       // FILE_ATTRIBUTE_HIDDEN
        System = 0x00000004,       // FILE_ATTRIBUTE_SYSTEM

        /// <remarks>
        /// This flag is not in use.
        /// FileRecordFlags.IsDirectory is used instead for FileRecordSegment,
        /// DUP_FILE_NAME_INDEX_PRESENT is used instead for FileNameRecord.
        /// </remarks>
        Directory = 0x00000010,    // FILE_ATTRIBUTE_DIRECTORY
        Archive = 0x00000020,      // FILE_ATTRIBUTE_ARCHIVE

        /// <remarks>This flag is not in use.</remarks>
        Normal = 0x00000080,       // FILE_ATTRIBUTE_NORMAL
        Temporary = 0x00000100,    // FILE_ATTRIBUTE_TEMPORARY
        Sparse = 0x00000200,       // FILE_ATTRIBUTE_SPARSE_FILE
        ReparsePoint = 0x00000400, // FILE_ATTRIBUTE_REPARSE_POINT
        Compressed = 0x00000800,   // FILE_ATTRIBUTE_COMPRESSED
        Offline = 0x00001000,      // FILE_ATTRIBUTE_OFFLINE
        PropertySet = 0x00002000,  // FILE_ATTRIBUTE_PROPERTY_SET

        /// <remarks>This flag should only be used in FileNameRecord, and should not be used in StandardInformationRecord</remarks>
        FileNameIndexPresent = 0x10000000, // DUP_FILE_NAME_INDEX_PRESENT

        /// <summary>Indicates the presence of object ID index, quota index, security index or EFS related index</summary>
        /// <remarks>NTFS 3.0+</remarks>
        ViewIndexPresent = 0x20000000,
    }
}
