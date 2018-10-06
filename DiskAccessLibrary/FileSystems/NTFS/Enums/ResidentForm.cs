using System;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    [Flags]
    public enum ResidentForm : byte
    {
        Indexed = 0x01, // RESIDENT_FORM_INDEXED
    }
}
