
namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <remarks>
    /// The comment specifies the record type which follows the record.
    /// </remarks>
    public enum NTFSLogOperation : ushort
    {
        Noop = 0x0000,
        CompensationLogRecord = 0x0001,
        InitializeFileRecordSegment = 0x0002,          // FILE_RECORD_SEGMENT
        DeallocateFileRecordSegment = 0x0003,
        WriteEndOfFileRecordSegment = 0x0004,          // ATTRIBUTE_RECORD_HEADER
        CreateAttribute = 0x0005,                      // ATTRIBUTE_RECORD_HEADER
        DeleteAttribute = 0x0006,
        UpdateResidentAttributeValue = 0x0007,         // (value)
        UpdateNonResidentAttributeValue = 0x0008,      // (value)
        UpdateMappingPairs = 0x0009,                   // (value = mapping pairs bytes)
        DeleteDirtyClusters = 0x000A,                  // array of LCN_RANGE
        SetNewAttributeSizes = 0x000B,                 // NEW_ATTRIBUTE_SIZES
        AddIndexEntryToRoot = 0x000C,                  // INDEX_ENTRY
        DeleteIndexEntryFromRoot = 0x000D,             // INDEX_ENTRY
        AddIndexEntryToAllocationBuffer = 0x000E,      // INDEX_ENTRY
        DeleteIndexEntryFromAllocationBuffer = 0x000F, // INDEX_ENTRY
        WriteEndOfIndexBuffer = 0x0010,                // INDEX_ENTRY
        SetIndexEntryVcnInRoot = 0x0011,               // VCN
        SetIndexEntryVcnInAllocationBuffer = 0x0012,   // VCN
        UpdateFileNameInRoot = 0x0013,                 // DUPLICATED_INFORMATION
        UpdateFileNameInAllocationBuffer = 0x0014,     // DUPLICATED_INFORMATION
        SetBitsInNonResidentBitMap = 0x0015,           // BITMAP_RANGE
        ClearBitsInNonResidentBitMap = 0x0016,         // BITMAP_RANGE
        HotFix = 0x0017,
        EndTopLevelAction = 0x0018,
        PrepareTransaction = 0x0019,
        CommitTransaction = 0x001A,
        ForgetTransaction = 0x001B,
        OpenNonResidentAttribute = 0x001C,             // OPEN_ATTRIBUTE_ENTRY (The attribute name is stored in the UndoData field)
        OpenAttributeTableDump = 0x001D,               // OPEN_ATTRIBUTE_ENTRY restart table
        AttributeNamesDump = 0x001E,                   // ATTRIBUTE_NAME_ENTRY array
        DirtyPageTableDump = 0x001F,                   // DIRTY_PAGE_ENTRY restart table
        TransactionTableDump = 0x0020,                 // TRANSACTION_ENTRY restart table
        UpdateRecordDataInRoot = 0x0021,               // (value)
        UpdateRecordDataInAllocationBuffer = 0x0022,   // (value)
    }
}
