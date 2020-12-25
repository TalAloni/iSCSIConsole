/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;

namespace DiskAccessLibrary.LogicalDiskManager
{
    // While DynamicDisk is just a Disk, this class was created to avoid the need to repeatedly read the PrivateHeader and TOCBlock
    public class DynamicDisk
    {
        private Disk m_disk;
        private PrivateHeader m_privateHeader;
        private TOCBlock m_tocBlock;

        public DynamicDisk(Disk disk, PrivateHeader privateHeader, TOCBlock tocBlock)
        {
            m_disk = disk;
            m_privateHeader = privateHeader;
            m_tocBlock = tocBlock;
        }

        public Disk Disk
        {
            get
            {
                return m_disk;
            }
        }

        public PrivateHeader PrivateHeader
        {
            get
            {
                return m_privateHeader;
            }
        }

        public TOCBlock TOCBlock
        {
            get
            {
                return m_tocBlock;
            }
        }

        public Guid DiskGuid
        {
            get
            {
                return PrivateHeader.DiskGuid;
            }
        }

        public Guid DiskGroupGuid
        {
            get
            {
                return PrivateHeader.DiskGroupGuid;
            }
        }

        public int BytesPerSector
        {
            get
            {
                return Disk.BytesPerSector;
            }
        }

        public static DynamicDisk ReadFromDisk(Disk disk)
        {
            if (IsDynamicDisk(disk))
            {
                PrivateHeader privateHeader = PrivateHeader.ReadFromDisk(disk);
                if (privateHeader != null)
                {
                    TOCBlock tocBlock = TOCBlock.ReadFromDisk(disk, privateHeader);
                    if (tocBlock != null)
                    {
                        return new DynamicDisk(disk, privateHeader, tocBlock);
                    }
                }
            }
            return null;
        }

        public static bool IsDynamicDisk(Disk disk)
        { 
            MasterBootRecord mbr = MasterBootRecord.ReadFromDisk(disk);
            if (mbr != null)
            {
                if (mbr.PartitionTable[0].PartitionType == (byte)PartitionTypeName.DynamicData)
                {
                    return true;
                }
                else if (mbr.IsGPTBasedDisk)
                {
                    List<GuidPartitionEntry> entries = GuidPartitionTable.ReadEntriesFromDisk(disk);
                    if (entries != null)
                    {
                        if (GuidPartitionEntryCollection.ContainsPartitionTypeGuid(entries, GPTPartition.PrivateRegionPartitionTypeGuid) &&
                            GuidPartitionEntryCollection.ContainsPartitionTypeGuid(entries, GPTPartition.PublicRegionPartitionTypeGuid))
                        {
                            return true;
                        }
                    }   
                }

                return false;
            }
            else
            { 
                // if the disk has no master boot record, it can be a dynamic disk if it has a valid PrivateHeader at sector 6
                PrivateHeader privateHeader = PrivateHeader.ReadFromDiskStart(disk);
                return (privateHeader != null);
            }
        }
    }
}
