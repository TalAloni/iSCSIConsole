/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */

namespace DiskAccessLibrary
{
    public class MBRPartition : Partition
    {
        private byte m_partitionType;
        
        public MBRPartition(byte partitionType, DiskExtent extent) : base(extent)
        {
            m_partitionType = partitionType;
        }

        public MBRPartition(byte partitionType, Disk disk, long firstSector, long size) : base(disk, firstSector, size)
        {
            m_partitionType = partitionType;
        }

        public PartitionTypeName PartitionTypeName
        {
            get
            {
                return (PartitionTypeName)m_partitionType;
            }
        }
    }
}
