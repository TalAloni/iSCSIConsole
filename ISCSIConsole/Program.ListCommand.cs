using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DiskAccessLibrary.LogicalDiskManager;
using DiskAccessLibrary;
using Utilities;

namespace ISCSIConsole
{
    partial class Program
    {
        public static void ListCommand(string[] args)
        {
            if (args.Length >= 2)
            {
                switch (args[1].ToLower())
                {
                    case "disk":
                        Console.WriteLine();
                        ListPhysicalDisks();
                        break;
                    case "partition":
                        Console.WriteLine();
                        ListPartitions();
                        break;
                    case "volume":
                        Console.WriteLine();
                        ListVolumes();
                        break;
                    case "extent":
                        Console.WriteLine();
                        ListExtents();
                        break;
                    default:
                        Console.WriteLine();
                        Console.WriteLine("Invalid parameter.");
                        HelpList();
                        break;
                }
            }
            else
            {
                HelpList();
            }
        }

        public static void HelpList()
        {
            Console.WriteLine();
            Console.WriteLine("LIST DISK      - Print a list of physical disks.");
            Console.WriteLine("LIST PARTITION - Print a list of partitions on the selected disk.");
            Console.WriteLine("LIST VOLUME    - Print a list of supported volumes.");
            Console.WriteLine("LIST EXTENT    - Print a list of extents of the selected volume.");
        }

        public static void ListPhysicalDisks()
        {
            List<PhysicalDisk> disks = PhysicalDiskHelper.GetPhysicalDisks();

            Console.WriteLine("Disk ##  Size     GPT  Dyn  DiskID  Disk Group Name   ");
            Console.WriteLine("-------  -------  ---  ---  ------  ------------------");
            foreach (PhysicalDisk disk in disks)
            {
                int index = disk.PhysicalDiskIndex;

                string diskNumber = index.ToString().PadLeft(2);
                MasterBootRecord mbr = MasterBootRecord.ReadFromDisk(disk);
                string isGPTStr = (mbr != null && mbr.IsGPTBasedDisk) ? " * " : "   ";
                string isDynStr = DynamicDisk.IsDynamicDisk(disk) ? " * " : "   ";
                string diskID = String.Empty;
                string diskGroupName = String.Empty;
                VolumeManagerDatabase database = VolumeManagerDatabase.ReadFromDisk(disk);
                if (database != null)
                {
                    PrivateHeader privateHeader = PrivateHeader.ReadFromDisk(disk);
                    DiskRecord diskRecord = database.FindDiskByDiskGuid(privateHeader.DiskGuid);
                    diskID = diskRecord.DiskId.ToString();
                    diskGroupName = database.DiskGroupName; 
                }

                diskID = diskID.PadLeft(6);
                Console.WriteLine("Disk {0}  {1}  {2}  {3}  {4}  {5}", diskNumber, FormattingHelper.GetStandardSizeString(disk.Size), isGPTStr, isDynStr, diskID, diskGroupName);
            }
        }

        public static void ListPartitions()
        {
            if (m_selectedDisk != null)
            {
                List<Partition> partitions = BasicDiskHelper.GetPartitions(m_selectedDisk);
                Console.WriteLine("Partition #  Type              Size     Offset   Start Sector");
                Console.WriteLine("-----------  ----------------  -------  -------  ------------");
                for (int index = 0; index < partitions.Count; index++)
                {
                    Partition partition = partitions[index];
                    long offset = partition.FirstSector * m_selectedDisk.BytesPerSector;
                    long size = partition.Size;
                    string partitionType;
                    if (partition is GPTPartition)
                    {
                        partitionType = ((GPTPartition)partition).PartitionTypeName;
                    }
                    else // partition is MBRPartition
                    {
                        partitionType = ((MBRPartition)partition).PartitionTypeName.ToString();
                    }
                    partitionType = partitionType.PadRight(16);
                    string startSector = partition.FirstSector.ToString().PadLeft(12);
                    Console.WriteLine("Partition {0}  {1}  {2}  {3}  {4}", index.ToString(), partitionType, FormattingHelper.GetStandardSizeString(size), FormattingHelper.GetStandardSizeString(offset), startSector);
                }
            }
            else
            {
                Console.WriteLine("No disk has been selected");
            }
        }

        public static void ListVolumes()
        {
            List<Volume> volumes = WindowsVolumeHelper.GetVolumes();
            Console.WriteLine("Volume ##  ID    Name          Type       Size     Status   ");
            Console.WriteLine("---------  ----  ------------  ---------  -------  ---------");

            for (int index = 0; index < volumes.Count; index++)
            {
                Volume volume = volumes[index];
                string type = VolumeHelper.GetVolumeTypeString(volume);
                string status = VolumeHelper.GetVolumeStatusString(volume);

                ulong volumeID = 0;
                string name = String.Empty;

                if (volume is DynamicVolume)
                {
                    volumeID = ((DynamicVolume)volume).VolumeID;
                    name = ((DynamicVolume)volume).Name;
                }

                string volumeNumber = index.ToString().PadLeft(2);
                type = type.ToString().PadRight(9);
                name = name.ToString().PadRight(12);
                status = status.ToString().PadRight(9);

                string volumeIDString = String.Empty;
                if (volumeID != 0)
                {
                    volumeIDString = volumeID.ToString();
                }
                volumeIDString = volumeIDString.PadRight(4);

                Console.WriteLine("Volume {0}  {1}  {2}  {3}  {4}  {5}", volumeNumber, volumeIDString, name, type, FormattingHelper.GetStandardSizeString(volume.Size), status);
            }
        }

        public static void ListExtents()
        {
            if (m_selectedVolume != null)
            {
                Console.WriteLine("Extent ##  ID    Name       Size     DiskID  Offset   Start Sector");
                Console.WriteLine("---------  ----  ---------  -------  ------  -------  ------------");

                for (int index = 0; index < m_selectedVolume.Extents.Count; index++)
                {
                    DiskExtent extent = m_selectedVolume.Extents[index];

                    string extentNumber = index.ToString().PadLeft(2);
                    ulong extentID = 0;
                    ulong diskID = 0;
                    string name = String.Empty;
                    
                    if (extent is DynamicDiskExtent)
                    {
                        extentID = ((DynamicDiskExtent)extent).ExtentID;
                        name = ((DynamicDiskExtent)extent).Name;

                        if (extent.Disk != null)
                        {
                            VolumeManagerDatabase database = VolumeManagerDatabase.ReadFromDisk(extent.Disk);
                            if (database != null)
                            {
                                ExtentRecord extentRecord = database.FindExtentByExtentID(extentID);
                                diskID = extentRecord.DiskId;
                            }
                        }
                    }

                    string offsetString;
                    if (extent.Disk != null)
                    {
                        long offset = extent.FirstSector * extent.Disk.BytesPerSector;
                        offsetString = FormattingHelper.GetStandardSizeString(offset);
                    }
                    else
                    {
                        offsetString = "    N/A";
                    }
                    long size = extent.Size;

                    name = name.ToString().PadRight(9);

                    string extentIDString = String.Empty;
                    if (extentID != 0)
                    {
                        extentIDString = extentID.ToString();
                    }
                    extentIDString = extentIDString.PadLeft(4);

                    string diskIDString = String.Empty;
                    if (diskID != 0)
                    {
                        diskIDString = diskID.ToString();
                    }
                    diskIDString = diskIDString.PadLeft(6);

                    string startSector = extent.FirstSector.ToString().PadLeft(12);

                    Console.WriteLine("Extent {0}  {1}  {2}  {3}  {4}  {5}  {6}", extentNumber, extentIDString, name, FormattingHelper.GetStandardSizeString(size), diskIDString, offsetString, startSector);
                }
            }
            else
            {
                Console.WriteLine("No volume has been selected");
            }
        }
    }
}
