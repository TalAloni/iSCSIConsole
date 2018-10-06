
namespace DiskAccessLibrary.FileSystems.NTFS
{
    public enum LogRecordType : uint
    {
        ClientRecord = 1,  // LfsClientRecord
        ClientRestart = 2, // LfsClientRestart
    }
}
