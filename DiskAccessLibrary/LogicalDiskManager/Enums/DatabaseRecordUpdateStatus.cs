
namespace DiskAccessLibrary.LogicalDiskManager
{
    public enum DatabaseRecordUpdateStatus : ushort
    {
        Active = 0x00,                // consistant state
        ActivePendingDeletion = 0x01, // about to be deleted, but is still active
        PendingActivation = 0x02,     // just been created, but it is not yet active
    }
}
