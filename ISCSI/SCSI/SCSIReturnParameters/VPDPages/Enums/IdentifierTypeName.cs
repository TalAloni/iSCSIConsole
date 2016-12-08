
namespace SCSI
{
    public enum IdentifierTypeName : byte
    {
        VendorSpecific = 0x00,
        T10 = 0x01, // T10 vendor identification
        EUI64 = 0x02, // EUI-64
        NAA = 0x03,
        RelativeTargetPort = 0x04,
        TargetPortGroup = 0x05,
        LogicalUnitGroup = 0x06,
        MD5LogicalUnitIdentifier = 0x07,
        ScsiNameString = 0x08,
    }
}
