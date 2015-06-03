using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DiskAccessLibrary;
using DiskAccessLibrary.LogicalDiskManager;
using DiskAccessLibrary.VHD;
using Utilities;

namespace ISCSIConsole
{
    public partial class Program
    {
        public static void CreateCommand(string[] args)
        {
            if (args.Length >= 2)
            {
                if (args[1].ToLower() == "vdisk")
                {
                    KeyValuePairList<string, string> parameters = ParseParameters(args, 2);
                    CreateVDisk(parameters);
                }
                else
                {
                    Console.WriteLine("Invalid argument.");
                    HelpCreate();
                }
            }
            else
            {
                HelpCreate();
            }
        }

        public static void CreateVDisk(KeyValuePairList<string, string> parameters)
        {
            if (!VerifyParameters(parameters, "file", "size"))
            {
                Console.WriteLine();
                Console.WriteLine("Invalid parameter.");
                HelpCreate();
                return;
            }

            long sizeInBytes;

            if (parameters.ContainsKey("size"))
            {
                long requestedSizeInMB = Conversion.ToInt64(parameters.ValueOf("size"), 0);
                sizeInBytes = requestedSizeInMB * 1024 * 1024;
                if (requestedSizeInMB <= 0)
                {
                    Console.WriteLine("Invalid size (must be specified in MB).");
                    return;
                }
            }
            else
            {
                Console.WriteLine("The SIZE parameter must be specified.");
                return;
            }

            if (parameters.ContainsKey("file"))
            {
                string path = parameters.ValueOf("file");

                if (new FileInfo(path).Exists)
                {
                    Console.WriteLine("Error: file already exists.");
                    return;
                }

                try
                {
                    m_selectedDisk = VirtualHardDisk.Create(path, sizeInBytes);
                    Console.WriteLine("The virtual disk file was created successfully.");
                }
                catch (IOException)
                {
                    Console.WriteLine("Error: Could not write the virtual disk file.");
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("Error: Access Denied, Could not write the virtual disk file.");
                    return;
                }
            }
            else
            {
                Console.WriteLine("The FILE parameter was not specified.");
            }
        }

        public static void HelpCreate()
        {
            Console.WriteLine();
            Console.WriteLine("CREATE VDISK FILE=<path> SIZE=<N>");
            Console.WriteLine();
            Console.WriteLine("Note:");
            Console.WriteLine("-----");
            Console.WriteLine("1. SIZE must be specified in MB, and without any suffixes.");
        }
    }
}
