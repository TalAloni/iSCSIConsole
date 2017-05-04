
namespace SCSI
{
    public enum ServiceAction : byte
    {
        None = 0x00,
        ReadCapacity16 = 0x10,
        ReadLong16 = 0x11,
    }
}
