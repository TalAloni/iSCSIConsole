
namespace DiskAccessLibrary.LogicalDiskManager
{
    public enum ExtentLayoutName : byte
    {
        Stripe = 1,
        Concatenated = 2,
        RAID5 = 3,
    }
}
