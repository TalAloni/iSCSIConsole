
namespace DiskAccessLibrary
{
    /// <summary>
    /// Win32 error codes:
    /// </summary>
    public enum Win32Error : uint
    {
        ERROR_SUCCESS = 0x00,
        ERROR_INVALID_FUNCTION = 0x01,
        ERROR_FILE_NOT_FOUND = 0x02,
        ERROR_ACCESS_DENIED = 0x05,
        ERROR_SECTOR_NOT_FOUND = 0x1B,
        ERROR_CRC = 0x17, // This is the same error as STATUS_DEVICE_DATA_ERROR, and it means the disk has a bad block
        ERROR_SHARING_VIOLATION = 0x20,
        ERROR_INSUFFICIENT_BUFFER = 0x7A,
        ERROR_MORE_DATA = 0xEA, // buffer was not long enough
        ERROR_NO_MORE_ITEMS = 0x103,
        ERROR_NO_SYSTEM_RESOURCES = 0x5AA, // Occurs when we try to read too many bytes at once 
    }
}
