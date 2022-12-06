
namespace SCSI
{
    public enum ModePageCodeName : byte
    {
        VendorSpecificPage = 0x00, // Windows 2000 will request this page
        CachingParametersPage = 0x08,
        ControlModePage = 0x0A,
        PowerConditionModePage = 0x1A,
        InformationalExceptionsControlModePage = 0x1C,
        MMCapabilitiesAndMechanicalStatus = 0x2A,
        ReturnAllPages = 0x3F,
    }
}
