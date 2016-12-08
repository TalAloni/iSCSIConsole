/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DiskAccessLibrary
{
    public class GPTPartition : Partition
    {
        public static readonly Guid BasicDataPartititionTypeGuid = new Guid("EBD0A0A2-B9E5-4433-87C0-68B6B72699C7");
        public static readonly Guid MicrosoftReservedPartititionTypeGuid = new Guid("E3C9E316-0B5C-4DB8-817D-F92DF00215AE");
        public static readonly Guid PrivateRegionPartitionTypeGuid = new Guid("5808C8AA-7E8F-42E0-85D2-E1E90434CFB3");
        public static readonly Guid PublicRegionPartitionTypeGuid = new Guid("AF9B60A0-1431-4F62-BC68-3311714A69AD");
        public static readonly Guid EFISystemPartitionTypeGuid = new Guid("C12A7328-F81F-11D2-BA4B-00A0C93EC93B");
        public static readonly Guid HFSPlusPartitionTypeGuid = new Guid("48465300-0000-11AA-AA11-00306543ECAC");
        public static readonly Guid AppleBootPartitionTypeGuid = new Guid("426F6F74-0000-11AA-AA11-00306543ECAC");

        private Guid m_volumeGuid;
        private Guid m_typeGuid;
        private string m_name;

        public GPTPartition(Guid volumeGuid, Guid typeGuid, string name, DiskExtent extent) : base(extent)
        {
            m_volumeGuid = volumeGuid;
            m_typeGuid = typeGuid;
        }

        public GPTPartition(Guid volumeGuid, Guid typeGuid, string name, Disk disk, long firstSector, long size) : base(disk, firstSector, size)
        {
            m_volumeGuid = volumeGuid;
            m_typeGuid = typeGuid;
            m_name = name;
        }

        public Guid VolumeGuid
        {
            get 
            {
                return m_volumeGuid;
            }
        }

        public Guid TypeGuid
        {
            get
            {
                return m_typeGuid;
            }
        }

        public string PartitionTypeName
        {
            get
            {
                if (m_typeGuid == BasicDataPartititionTypeGuid)
                {
                    return "Primary";
                }
                else if (m_typeGuid == MicrosoftReservedPartititionTypeGuid)
                {
                    return "MSFT Reserved";
                }
                else if (m_typeGuid == PrivateRegionPartitionTypeGuid)
                {
                    // This is either the private region itself (on dynamic disks), or a reserved partition for it (on basic disks)
                    return "Dynamic Reserved";
                }
                else if (m_typeGuid == PublicRegionPartitionTypeGuid)
                {
                    return "Dynamic Data";
                }
                else if (m_typeGuid == EFISystemPartitionTypeGuid)
                {
                    return "EFI System";
                }
                else if (m_typeGuid == HFSPlusPartitionTypeGuid)
                {
                    return "HFS+";
                }
                else if (m_typeGuid == AppleBootPartitionTypeGuid)
                {
                    return "Apple Boot";
                }
                else
                {
                    return "Unknown";
                }
            }
        }

        public string PartitionName
        {
            get
            {
                return m_name;
            }
        }
    }
}
