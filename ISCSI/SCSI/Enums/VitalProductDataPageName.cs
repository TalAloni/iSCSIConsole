
namespace SCSI
{
    public enum VitalProductDataPageName : byte
    {
        SupportedVPDPages = 0x00,
        UnitSerialNumber = 0x80,
        DeviceIdentification = 0x83,
        BlockLimits = 0xB0,
        BlockDeviceCharacteristics = 0xB1,
    }
}
