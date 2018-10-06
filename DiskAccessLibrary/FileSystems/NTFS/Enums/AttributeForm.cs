
namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <remarks>
    /// https://docs.microsoft.com/en-us/windows/desktop/DevNotes/attribute-record-header
    /// </remarks>
    public enum AttributeForm : byte
    {
        Resident = 0x00,    // RESIDENT_FORM
        NonResident = 0x01, // NONRESIDENT_FORM
    }
}
