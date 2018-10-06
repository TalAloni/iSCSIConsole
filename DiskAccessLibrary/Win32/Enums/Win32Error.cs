
namespace DiskAccessLibrary
{
    /// <summary>
    /// Win32 error codes:
    /// </summary>
    /// <remarks>All Win32 error codes MUST be in the range 0x0000 to 0xFFFF</remarks>
    public enum Win32Error : ushort
    {
        ERROR_SUCCESS = 0x0000,
        ERROR_INVALID_FUNCTION = 0x0001,
        ERROR_FILE_NOT_FOUND = 0x0002,
        ERROR_ACCESS_DENIED = 0x0005,
        ERROR_INVALID_DATA = 0x000D,
        ERROR_NOT_READY = 0x0015,
        ERROR_SECTOR_NOT_FOUND = 0x001B,
        ERROR_CRC = 0x0017, // This is the same error as STATUS_DEVICE_DATA_ERROR, and it means the disk has a bad block
        ERROR_SHARING_VIOLATION = 0x0020,
        ERROR_DISK_FULL = 0x0070,
        ERROR_INSUFFICIENT_BUFFER = 0x007A,
        ERROR_DIR_NOT_EMPTY = 0x0091,
        ERROR_ALREADY_EXISTS = 0x00B7,
        ERROR_MORE_DATA = 0x00EA, // buffer was not long enough
        ERROR_NO_MORE_ITEMS = 0x0103,
        ERROR_IO_PENDING = 0x3E5,
        ERROR_MEDIA_CHANGED = 0x0456,
        ERROR_NO_MEDIA_IN_DRIVE = 0x0458,
        ERROR_IO_DEVICE = 0x045D, // Reading from disk region that has sectors with mismatching CRC may return this
        ERROR_DEVICE_NOT_CONNECTED = 0x048F,
        ERROR_NO_SYSTEM_RESOURCES = 0x05AA, // Occurs when we try to read too many bytes at once 
    }
}
