
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
        ERROR_INVALID_DATA = 0x0D,
        ERROR_NOT_READY = 0x15,
        ERROR_SECTOR_NOT_FOUND = 0x1B,
        ERROR_CRC = 0x17, // This is the same error as STATUS_DEVICE_DATA_ERROR, and it means the disk has a bad block
        ERROR_SHARING_VIOLATION = 0x20,
        ERROR_INSUFFICIENT_BUFFER = 0x7A,
        ERROR_MORE_DATA = 0xEA, // buffer was not long enough
        ERROR_NO_MORE_ITEMS = 0x103,
        ERROR_MEDIA_CHANGED = 0x456,
        ERROR_NO_MEDIA_IN_DRIVE = 0x458,
        ERROR_IO_DEVICE = 0x45D, // Reading from disk region that has sectors with mismatching CRC may return this
        ERROR_DEVICE_NOT_CONNECTED = 0x48F,
        ERROR_NO_SYSTEM_RESOURCES = 0x5AA, // Occurs when we try to read too many bytes at once 
    }
}
