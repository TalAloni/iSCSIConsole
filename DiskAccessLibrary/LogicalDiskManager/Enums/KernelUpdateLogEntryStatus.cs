
namespace DiskAccessLibrary.LogicalDiskManager
{
    public enum KernelUpdateLogEntryStatus : byte
    {
        NotExist = 0x00,
        Detach = 0x01,    // as reported by DMDIAG
        Dirty = 0x02,     // as reported by DMDIAG
        Commit = 0x03,    // as reported by DMDIAG
        LogDetach = 0x04, // as reported by DMDIAG
        // APP_DIRTY = 0x05 (as reported by DMDIAG, which also reports 'recover_seqno 0' for this entry)
        // DMDIAG reports > 0x05 as INVALID
    }
}
