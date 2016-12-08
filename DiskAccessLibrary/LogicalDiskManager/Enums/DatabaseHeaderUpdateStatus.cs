
namespace DiskAccessLibrary.LogicalDiskManager
{
    // as reported by DMDiag
    public enum DatabaseHeaderUpdateStatus : ushort
    {
        Clean = 0x01,     // consistant state
        Change = 0x02,    // in a creation phase / during update
        Commit = 0x03,    // replaces 'Change' header and comes immediately before 'Clean' header
        Abort = 0x04,
        New = 0x05,
        Stale = 0x06,
        Offline = 0x08,
    }
}
