using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DiskAccessLibrary;
using Utilities;

namespace ISCSIConsole
{
    public partial class Program
    {
        private static Disk m_selectedDisk;
        private static Volume m_selectedVolume;

        public static void SelectCommand(string[] args)
        {
            if (args.Length == 1)
            {
                HelpSelect();
                return;
            }

            switch (args[1].ToLower())
            {
                case "disk":
                    {
                        if (args.Length == 3)
                        {
                            int diskIndex = Conversion.ToInt32(args[2], -1);
                            if (diskIndex >= 0)
                            {
                                PhysicalDisk disk = null;
                                try
                                {
                                    disk = new PhysicalDisk(diskIndex);
                                }
                                catch
                                {
                                    Console.WriteLine();
                                    Console.WriteLine("Error: Invalid disk number");
                                }

                                if (disk != null)
                                {
                                    m_selectedDisk = disk;
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine();
                            Console.WriteLine("Error: Invalid number of arguments");
                        }
                        break;
                    }
                case "vdisk":
                    {
                        if (args.Length == 3)
                        {
                            KeyValuePairList<string, string> parameters = ParseParameters(args, 2);
                            if (parameters.ContainsKey("file"))
                            {
                                string path = parameters.ValueOf("file");
                                if (new FileInfo(path).Exists)
                                {
                                    try
                                    {
                                        m_selectedDisk = DiskImage.GetDiskImage(path);
                                    }
                                    catch (InvalidDataException)
                                    {
                                        Console.WriteLine("Invalid virtual disk format");
                                    }
                                    catch (NotImplementedException)
                                    {
                                        Console.WriteLine("Unsupported virtual disk format");
                                    }
                                    catch (IOException ex)
                                    {
                                        Console.WriteLine("Cannot read file: " + ex.Message);
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("File not found: \"{0}\"", path);
                                }
                            }
                            else
                            {
                                Console.WriteLine();
                                Console.WriteLine("Error: Invalid argument");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Error: Invalid number of arguments");
                            
                        }
                        break;
                    }
                case "partition":
                    {
                        if (m_selectedDisk != null) 
                        {
                            if (args.Length == 3)
                            {
                                int partitionIndex = Conversion.ToInt32(args[2], -1);
                                List<Partition> partitions = BasicDiskHelper.GetPartitions(m_selectedDisk);
                                if (partitionIndex >= 0 && partitionIndex < partitions.Count)
                                {
                                    m_selectedVolume = partitions[partitionIndex];
                                }
                                else
                                {
                                    Console.WriteLine("Error: Invalid partition number");
                                }
                            }
                            else
                            {
                                Console.WriteLine();
                                Console.WriteLine("Error: Partition number was not specified");
                            }
                        }
                        else
                        {
                            Console.WriteLine("No disk has been selected");
                        }
                        break;
                    }
                case "volume":
                    {
                        if (args.Length == 3)
                        {
                            List<Volume> volumes;
                            try
                            {
                                volumes = WindowsVolumeHelper.GetVolumes();
                            }
                            catch
                            {
                                volumes = new List<Volume>();
                            }

                            int volumeIndex = Conversion.ToInt32(args[2], -1);
                            if (volumeIndex >= 0 && volumeIndex < volumes.Count)
                            {
                                m_selectedVolume = volumes[volumeIndex];
                            }
                            else
                            {
                                Console.WriteLine();
                                Console.WriteLine("Error: Invalid volume number");
                            }
                        }
                        else
                        {
                            Console.WriteLine();
                            Console.WriteLine("Error: Volume number was not specified");
                        }
                        break;
                    }
                default:
                    HelpSelect();
                    break;
            }
        }

        public static void HelpSelect()
        {
            Console.WriteLine();
            Console.WriteLine("SELECT DISK <N>          - Select physical disk.");
            Console.WriteLine("SELECT VDISK FILE=<path> - Select virtual disk file.");
            Console.WriteLine("SELECT VOLUME <N>        - Select volume.");
            Console.WriteLine("SELECT PARTITION <N>     - Select partition.");
        }
    }
}
