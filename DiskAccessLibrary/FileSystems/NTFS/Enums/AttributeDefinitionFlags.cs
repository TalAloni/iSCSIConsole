using System;

namespace DiskAccessLibrary.FileSystems
{
    [Flags]
    public enum AttributeDefinitionFlags : uint
    {
        Indexable = 0x00000002,         // ATTRIBUTE_DEF_INDEXABLE
        DuplicatesAllowed = 0x00000004, // ATTRIBUTE_DEF_DUPLICATES_ALLOWED
        MayNotBeNull = 0x00000008,      // ATTRIBUTE_DEF_MAY_NOT_BE_NULL
        MustBeIndexed = 0x00000010,     // ATTRIBUTE_DEF_MUST_BE_INDEXED
        MustBeNamed = 0x00000020,       // ATTRIBUTE_DEF_MUST_BE_NAMED
        MustBeResident = 0x00000040,    // ATTRIBUTE_DEF_MUST_BE_RESIDENT
        LogNonResident = 0x00000080,    // ATTRIBUTE_DEF_LOG_NONRESIDENT
    }
}
