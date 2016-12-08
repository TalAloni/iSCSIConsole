
namespace DiskAccessLibrary.LogicalDiskManager
{
    public enum RecordType : byte
    {
        None = 0,
        Volume = 1,
        Component = 2,
        Extent = 3, // partition
        Disk = 4,
        DiskGroup = 5
    }
}
