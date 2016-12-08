using System;

namespace DiskAccessLibrary.LogicalDiskManager
{
    // As reported by DMDiag:
    [Flags]
    public enum VolumeFlags : uint
    {
        Writeback = 0x000001, // Set by default
        Writecopy = 0x000002, // Set by default
        Crashed = 0x000004,   // 
        DefaultUnknown = 0x000010,  // Set by default
        BadLog = 0x000100,
        KDetach = 0x000400,
        Storage = 0x000800,
        AppRecover = 0x001000,
        Pending = 0x002000,
        RaidNtft = 0x100000,
        BootVolume = 0x200000,
        SystemVolume = 0x400000,
        RetainPartition = 0x800000,
    }
}
