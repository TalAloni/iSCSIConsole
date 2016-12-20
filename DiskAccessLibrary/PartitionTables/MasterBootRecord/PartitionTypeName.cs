
namespace DiskAccessLibrary
{
    public enum PartitionTypeName : byte
    {
        Primary = 0x07,
        Extended = 0x05,
        DynamicData = 0x42,
        EFIGPT = 0xEE,
    }
}
