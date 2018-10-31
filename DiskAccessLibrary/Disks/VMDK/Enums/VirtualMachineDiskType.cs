
namespace DiskAccessLibrary.VMDK
{
    public enum VirtualMachineDiskType
    {
        Custom,
        MonolithicSparse, // Single sparse extent with embedded descriptor file
        MonolithicFlat,   // Single flat extent with separate descriptor file
        TwoGbMaxExtentSparse,
        TwoGbMaxExtentFlat,
        FullDevice,
        PartitionedDevice,
        VmfsPreallocated,
        VmfsEagerZeroedThick,
        VmfsThin,
        VmfsSparse,
        VmfsRDM,
        VmfsRDMP,
        VmfsRaw,
        StreamOptimized,
    }
}
