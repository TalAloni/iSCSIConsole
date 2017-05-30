
namespace SCSI
{
    public enum PeripheralDeviceType : byte
    {
        DirectAccessBlockDevice = 0x00,
        SequentialAccessDevice = 0x01,
        CDRomDevice = 0x05,
    }
}
