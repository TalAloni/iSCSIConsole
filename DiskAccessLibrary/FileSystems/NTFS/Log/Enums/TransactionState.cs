
namespace DiskAccessLibrary.FileSystems.NTFS
{
    public enum TransactionState : byte
    {
        Uninitialized = 0,
        Active = 1,
        Prepared = 2,
        Committed = 3,
    }
}
