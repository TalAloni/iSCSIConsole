
namespace DiskAccessLibrary.FileSystems.NTFS
{
    public enum CollationRule : uint
    {
        Binary = 0x00000000,                // COLLATION_BINARY
        Filename = 0x00000001,              // COLLATION_FILE_NAME
        UnicodeString = 0x00000002,         // COLLATION_UNICODE_STRING
        UnsignedLong = 0x00000010,
        Sid = 0x00000011,
        SecurityHash = 0x00000012,
        MultipleUnsignedLongs = 0x00000013,
    }
}
