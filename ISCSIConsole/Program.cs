using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Reflection;
using System.Text;
using DiskAccessLibrary.LogicalDiskManager;
using DiskAccessLibrary;
using Utilities;

namespace ISCSIConsole
{
    partial class Program
    {
        public static bool m_debug = false;

        static void Main(string[] args)
        {
            Console.WriteLine("iSCSI Console v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine("Author: Tal Aloni (tal.aloni.il@gmail.com)");

            MainLoop();
        }

        public static void MainLoop()
        {
            bool exit = false;
            while (true)
            {
                if (m_debug)
                {
                    exit = ProcessCommand();
                }
                else
                {
                    try
                    {
                        exit = ProcessCommand();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Unhandled exception: " + ex.ToString());
                    }
                }

                if (exit)
                {
                    break;
                }
            }
        }

        /// <returns>true to exit</returns>
        public static bool ProcessCommand()
        {
            Console.WriteLine();
            Console.Write("iSCSI> ");
            string command = Console.ReadLine();
            string[] args = GetCommandArgsIgnoreEscape(command);
            bool exit = false;
            if (args.Length > 0)
            {
                string commandName = args[0];
                switch (commandName.ToLower())
                {
                    case "attach":
                        AttachCommand(args);
                        break;
                    case "create":
                        CreateCommand(args);
                        break;
                    case "detail":
                        DetailCommand(args);
                        break;
                    case "exit":
                        exit = true;
                        if (m_server != null)
                        {
                            m_server.Stop();
                            m_server = null;
                        }
                        break;
                    case "help":
                        {
                            HelpCommand(args);
                            break;
                        }
                    case "list":
                        ListCommand(args);
                        break;
                    case "offline":
                        OfflineCommand(args);
                        break;
                    case "online":
                        OnlineCommand(args);
                        break;
                    case "select":
                        SelectCommand(args);
                        break;
                    case "set":
                        SetCommand(args);
                        break;
                    case "start":
                        StartCommand(args);
                        break;
                    case "stop":
                        StopCommand(args);
                        break;
                    default:
                        Console.WriteLine("Invalid command. use the 'HELP' command to see the list of commands.");
                        break;
                }
            }
            return exit;
        }

        public static KeyValuePairList<string, string> ParseParameters(string[] args, int start)
        {
            KeyValuePairList<string, string> result = new KeyValuePairList<string, string>();
            for (int index = start; index < args.Length; index++)
            {
                string[] pair = args[index].Split('=');
                if (pair.Length >= 2)
                {
                    string key = pair[0].ToLower(); // we search by the key, so it should be set to lowercase
                    string value = pair[1];
                    value = Unquote(value);
                    result.Add(key, value);
                }
                else
                {
                    result.Add(pair[0].ToLower(), String.Empty);
                }
            }
            return result;
        }

        /// <summary>
        /// Make sure all given parameters are allowed
        /// </summary>
        public static bool VerifyParameters(KeyValuePairList<string, string> parameters, params string[] allowedKeys)
        {
            List<string> allowedList = new List<string>(allowedKeys);
            List<string> keys = parameters.Keys;
            foreach(string key in keys)
            {
                if (!allowedList.Contains(key))
                {
                    return false;
                }
            }
            return true;
        }

        private static int IndexOfUnquotedSpace(string str)
        {
            return IndexOfUnquotedSpace(str, 0);
        }

        private static int IndexOfUnquotedSpace(string str, int startIndex)
        {
            return QuotedStringUtils.IndexOfUnquotedChar(str, ' ', startIndex);
        }

        public static string Unquote(string str)
        {
            string quote = '"'.ToString();
            if (str.StartsWith(quote) && str.EndsWith(quote))
            {
                return str.Substring(1, str.Length - 2);
            }
            else
            {
                return str;
            }
        }

        private static string[] GetCommandArgsIgnoreEscape(string commandLine)
        {
            List<string> argsList = new List<string>();
            int endIndex = IndexOfUnquotedSpace(commandLine);
            int startIndex = 0;
            while (endIndex != -1)
            {
                int length = endIndex - startIndex;
                string nextArg = commandLine.Substring(startIndex, length);
                nextArg = Unquote(nextArg);
                argsList.Add(nextArg);
                startIndex = endIndex + 1;
                endIndex = IndexOfUnquotedSpace(commandLine, startIndex);
            }

            string lastArg = commandLine.Substring(startIndex);
            lastArg = Unquote(lastArg);
            if (lastArg != String.Empty)
            {
                argsList.Add(lastArg);
            }

            return argsList.ToArray();
        }
    }
}
