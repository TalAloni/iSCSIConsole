using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary;
using DiskAccessLibrary.LogicalDiskManager;

namespace ISCSIConsole
{
    public partial class Program
    {
        public static void DetailCommand(string[] args)
        {
            if (args.Length == 2)
            {
                switch (args[1].ToLower())
                {
                    case "disk":
                        {
                            Console.WriteLine();
                            if (m_selectedDisk != null)
                            {
                                Console.WriteLine("Size: {0} bytes", m_selectedDisk.Size.ToString("###,###,###,###,##0"));
                                if (m_selectedDisk is PhysicalDisk)
                                {
                                    PhysicalDisk disk = (PhysicalDisk)m_selectedDisk;
                                    Console.WriteLine("Geometry: Heads: {0}, Cylinders: {1}, Sectors Per Track: {2}", disk.TracksPerCylinder, disk.Cylinders, disk.SectorsPerTrack);
                                    Console.WriteLine();
                                }
                                else if (m_selectedDisk is DiskImage)
                                {
                                    DiskImage disk = (DiskImage)m_selectedDisk;
                                    Console.WriteLine("Disk image path: {0}", disk.Path);
                                    Console.WriteLine();
                                }
                                
                                MasterBootRecord mbr = MasterBootRecord.ReadFromDisk(m_selectedDisk);
                                if (mbr != null)
                                {
                                    Console.WriteLine("Partitioning scheme: " + (mbr.IsGPTBasedDisk ? "GPT" : "MBR"));
                                }
                                DynamicDisk dynamicDisk = DynamicDisk.ReadFromDisk(m_selectedDisk);
                                Console.WriteLine("Disk type: " + ((dynamicDisk != null) ? "Dynamic Disk" : "Basic Disk"));
                            }
                            else
                            {
                                Console.WriteLine("No disk has been selected.");
                            }
                            break;
                        }
                    case "volume":
                    case "partition":
                        {
                            Console.WriteLine();
                            if (m_selectedVolume != null)
                            {
                                Console.WriteLine("Volume size: {0} bytes", m_selectedVolume.Size.ToString("###,###,###,###,##0"));
                                if (m_selectedVolume is GPTPartition)
                                {
                                    Console.WriteLine("Partition name: {0}", ((GPTPartition)m_selectedVolume).PartitionName);
                                }

                                Guid? windowsVolumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(m_selectedVolume);
                                if (windowsVolumeGuid.HasValue)
                                {
                                    List<string> mountPoints = WindowsVolumeManager.GetMountPoints(windowsVolumeGuid.Value);
                                    foreach (string volumePath in mountPoints)
                                    {
                                        Console.WriteLine("Volume path: {0}", volumePath);
                                    }
                                    bool isMounted = WindowsVolumeManager.IsMounted(windowsVolumeGuid.Value);
                                    Console.WriteLine("Mounted: {0}", isMounted);
                                }
                            }
                            else
                            {
                                Console.WriteLine("No volume has been selected.");
                            }
                            break;
                        }
                    default:
                        Console.WriteLine("Invalid argument.");
                        HelpDetail();
                        break;
                }
            }
            else if (args.Length > 2)
            {
                Console.WriteLine("Too many arguments.");
                HelpDetail();
            }
            else
            {
                HelpDetail();
            }
        }

        public static void HelpDetail()
        {
            Console.WriteLine();
            Console.WriteLine("DETAIL DISK       - Display selected disk details");
            Console.WriteLine("DETAIL VOLUME     - Display selected volume details");
        }
    }
}
