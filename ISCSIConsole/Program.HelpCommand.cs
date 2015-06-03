using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace ISCSIConsole
{
    public partial class Program
    {
        public static void HelpCommand(string[] args)
        {
            if (args.Length == 1)
            {
                Console.WriteLine();
                Console.WriteLine("Available commands:");
                Console.WriteLine("-------------------");
                Console.WriteLine("ATTACH  - Attach selected disk or volume to an iSCSI target.");
                Console.WriteLine("CREATE  - Create a new VHD.");
                Console.WriteLine("DETAIL  - Provide details about a selected object.");
                Console.WriteLine("LIST    - List disks, volumes, partitions or volume extents.");
                Console.WriteLine("ONLINE  - Takes the selected disk online.");
                Console.WriteLine("OFFLINE - Takes the selected disk offline.");
                Console.WriteLine("SELECT  - Select disk, volume, partition or extent.");
                Console.WriteLine("SET     - Set program variables.");
                Console.WriteLine("START   - Start the iSCSI Server");
                Console.WriteLine("STOP    - Stop the iSCSI Server");
                Console.WriteLine();
                Console.WriteLine("- Use the 'HELP XXX' command for help regarding command XXX.");
            }
            else
            {
                switch (args[1].ToLower())
                {
                    case "attach":
                        HelpAttach();
                        break;
                    case "create":
                        HelpCreate();
                        break;
                    case "detail":
                        HelpDetail();
                        break;
                    case "list":
                        HelpList();
                        break;
                    case "offline":
                        HelpOffline();
                        break;
                    case "online":
                        HelpOnline();
                        break;
                    case "select":
                        HelpSelect();
                        break;
                    case "set":
                        HelpSet();
                        break;
                    case "start":
                        HelpStart();
                        break;
                    case "stop":
                        HelpStop();
                        break;
                    default:
                        Console.WriteLine("No such command: {0}", args[1]);
                        break;
                }
            }
        }
    }
}
