
namespace DiskAccessLibrary.LogicalDiskManager
{
    // DMDiag reports this as read policy
    // Names as reported by DMDiag
    public enum ReadPolicyName : byte
    {
        Round = 0x01,
        Prefer = 0x02,
        Select = 0x03, // Use this for simple volumes
        RAID = 0x04,
    }
}
