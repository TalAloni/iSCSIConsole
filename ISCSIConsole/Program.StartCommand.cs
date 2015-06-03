using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using ISCSI;
using Utilities;

namespace ISCSIConsole
{
    partial class Program
    {
        public const int DefaultISCSIPort = 3260;

        private static List<ISCSITarget> m_targets = new List<ISCSITarget>();
        private static ISCSIServer m_server;

        public static void StartCommand(string[] args)
        {
            if (m_server == null)
            {
                if (m_targets.Count > 0)
                {
                    KeyValuePairList<string, string> parameters = ParseParameters(args, 1);
                    if (!VerifyParameters(parameters, "port", "log"))
                    {
                        Console.WriteLine();
                        Console.WriteLine("Invalid parameter");
                        HelpStart();
                        return;
                    }

                    int port = DefaultISCSIPort;
                    if (parameters.ContainsKey("port"))
                    {
                        port = Conversion.ToInt32(parameters.ValueOf("port"), DefaultISCSIPort);
                    }
                    string logFile = String.Empty;
                    if (parameters.ContainsKey("log"))
                    {
                        logFile = parameters.ValueOf("log");
                    }
                    m_server = new ISCSIServer(m_targets, port, logFile);
                    try
                    {
                        ISCSIServer.Log("Starting Server");
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Could not append to log file");
                        return;
                    }

                    try
                    {
                        m_server.Start();
                        Console.WriteLine("Server started, listening on port {0}", port);
                    }
                    catch (SocketException)
                    {
                        Console.WriteLine("Could not start iSCSI server");
                        m_server.Stop();
                        m_server = null;
                    }
                }
                else
                {
                    Console.WriteLine("No disks have been attached");
                }
            }
        }

        public static void StopCommand(string[] args)
        {
            if (m_server != null)
            {
                m_server.Stop();
                m_server = null;
                Console.WriteLine("iSCSI target is stopping");
            }
            else
            {
                Console.WriteLine("iSCSI target has not been started");
            }
        }

        public static void HelpStart()
        {
            Console.WriteLine();
            Console.WriteLine("ISCSI START [PORT=<port>] [LOG=<path>] - starts the iSCSI server");
        }

        public static void HelpStop()
        {
            Console.WriteLine();
            Console.WriteLine("ISCSI STOP - stops the iSCSI server");
        }
    }
}