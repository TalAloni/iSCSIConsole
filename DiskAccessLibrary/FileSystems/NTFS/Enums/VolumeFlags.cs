
namespace DiskAccessLibrary.FileSystems.NTFS
{
    public enum VolumeFlags : ushort
    {
        Dirty = 0x0001,         // VOLUME_DIRTY
        ResizeLogFile = 0x0002, // VOLUME_RESIZE_LOG_FILE
        Repaired = 0x8000,      // CHKDSK sets this flag
    }
}
