
namespace DiskAccessLibrary.FileSystems.NTFS
{
    public enum FilenameNamespace : byte
    {
        POSIX = 0x00, // 255 bytes Unicode
        Win32 = 0x01, // 255 bytes Unicode
        DOS = 0x02,   // 8.3 Notation
        Win32AndDOS = 0x03,   // 8.3 Notation
    }
}
