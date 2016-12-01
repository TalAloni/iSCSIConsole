/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary;
using ISCSI.Server;
using Utilities;

namespace ISCSIConsole
{
    public partial class Program
    {
        public static void SetCommand(string[] args)
        {
            if (args.Length == 2)
            {
                KeyValuePairList<string, string> parameters = ParseParameters(args, 1);
                if (!VerifyParameters(parameters, "debug", "commandqueue", "MaxRecvDataSegmentLength".ToLower(), "MaxBurstLength".ToLower(), "FirstBurstLength".ToLower()))
                {
                    Console.WriteLine("Invalid parameter.");
                    return;
                }

                if (parameters.ContainsKey("debug"))
                {
                    m_debug = true;
                }

                if (parameters.ContainsKey("CommandQueue".ToLower()))
                {
                    int requestedCommandQueueSize = Conversion.ToInt32(parameters.ValueOf("commandqueue"), 0);
                    if (requestedCommandQueueSize < 0)
                    {
                        Console.WriteLine("Invalid queue size (must be non-negative).");
                        return;
                    }

                    SessionParameters.DefaultCommandQueueSize = (uint)requestedCommandQueueSize;
                }

                if (parameters.ContainsKey("MaxRecvDataSegmentLength".ToLower()))
                {
                    int requestedMaxRecvDataSegmentLength = Conversion.ToInt32(parameters.ValueOf("MaxRecvDataSegmentLength".ToLower()), 0);
                    if (requestedMaxRecvDataSegmentLength <= 0)
                    {
                        Console.WriteLine("Invalid length (must be positive).");
                        return;
                    }

                    ISCSIServer.DeclaredParameters.MaxRecvDataSegmentLength = requestedMaxRecvDataSegmentLength;
                    Console.WriteLine("MaxRecvDataSegmentLength has been set to " + ISCSIServer.DesiredParameters.MaxBurstLength);
                }

                if (parameters.ContainsKey("MaxBurstLength".ToLower()))
                {
                    int requestedMaxBurstLength = Conversion.ToInt32(parameters.ValueOf("MaxBurstLength".ToLower()), 0);
                    if (requestedMaxBurstLength <= 0)
                    {
                        Console.WriteLine("Invalid length (must be positive).");
                        return;
                    }

                    ISCSIServer.DesiredParameters.MaxBurstLength = requestedMaxBurstLength;
                    Console.WriteLine("Offered MaxBurstLength has been set to " + ISCSIServer.DesiredParameters.MaxBurstLength);
                    if (ISCSIServer.DesiredParameters.MaxBurstLength < ISCSIServer.DesiredParameters.FirstBurstLength)
                    {
                        // FirstBurstLength MUST NOT exceed MaxBurstLength
                        ISCSIServer.DesiredParameters.FirstBurstLength = ISCSIServer.DesiredParameters.MaxBurstLength;
                        Console.WriteLine("Offered FirstBurstLength has been set to " + ISCSIServer.DesiredParameters.FirstBurstLength);
                    }
                }

                if (parameters.ContainsKey("FirstBurstLength".ToLower()))
                {
                    int requestedFirstBurstLength = Conversion.ToInt32(parameters.ValueOf("FirstBurstLength".ToLower()), 0);
                    if (requestedFirstBurstLength <= 0)
                    {
                        Console.WriteLine("Invalid length (must be positive).");
                        return;
                    }

                    ISCSIServer.DesiredParameters.FirstBurstLength = requestedFirstBurstLength;
                    Console.WriteLine("Offered FirstBurstLength has been set to " + ISCSIServer.DesiredParameters.FirstBurstLength);
                    if (ISCSIServer.DesiredParameters.MaxBurstLength < ISCSIServer.DesiredParameters.FirstBurstLength)
                    {
                        // FirstBurstLength MUST NOT exceed MaxBurstLength
                        ISCSIServer.DesiredParameters.MaxBurstLength = ISCSIServer.DesiredParameters.FirstBurstLength;
                        Console.WriteLine("Offered MaxBurstLength has been set to " + ISCSIServer.DesiredParameters.MaxBurstLength);
                    }
                }
            }
            else if (args.Length > 2)
            {
                Console.WriteLine("Too many arguments.");
                HelpSet();
            }
            else
            {
                HelpSet();
            }
        }

        public static void HelpSet()
        {
            Console.WriteLine();
            Console.WriteLine("SET CommandQueue=<N>             - Sets the iSCSI server command queue size.");
            Console.WriteLine("SET MaxRecvDataSegmentLength=<N> - Declare this value to the initator.");
            Console.WriteLine("SET MaxBurstLength=<N>           - Offer this value to the initator.");
            Console.WriteLine("SET FirstBurstLength=<N>         - Offer this value to the initator.");
            
            Console.WriteLine();
            Console.WriteLine("Command queue size can be set to 0 (no queue, single command at a time).");
        }
    }
}
