using System;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    [Flags]
    public enum IndexHeaderFlags : byte
    {
        /// <summary>
        /// This node is a parent node (not a leaf)
        /// </summary>
        ParentNode = 0x01, // INDEX_NODE
    }
}
