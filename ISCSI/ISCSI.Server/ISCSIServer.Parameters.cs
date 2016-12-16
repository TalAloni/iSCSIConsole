/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;

namespace ISCSI.Server
{
    public partial class ISCSIServer
    {
        /// <summary>
        /// - CommandQueueSize = 0 means the initiator can send one command at a time (because MaxCmdSN = ExpCmdSN + CommandQueueSize),
        ///   (in this case there won't be any queue following the currently processed command).
        /// - Over a low-latency connection, most of the gain comes from increasing the queue size from 0 to 1
        /// - CmdSN is session wide, so CommandQueueSize is a session parameter.
        /// </summary>
        public static uint DefaultCommandQueueSize = 64;

        public class DesiredParameters
        {
            // Session parameters that will be offered to the initiator:
            public static int MaxConnections = 1; // implementation limit
            public static bool InitialR2T = false;
            public static bool ImmediateData = true;
            public static int MaxBurstLength = DefaultParameters.Session.MaxBurstLength;
            public static int FirstBurstLength = DefaultParameters.Session.FirstBurstLength;
            public static int DefaultTime2Wait = 0;
            public static int DefaultTime2Retain = 20;
            public static int MaxOutstandingR2T = 16;
            public static bool DataPDUInOrder = true; // implementation limit
            public static bool DataSequenceInOrder = true; // implementation limit
            public static int ErrorRecoveryLevel = 0; // implementation limit
        }

        public class DeclaredParameters
        {
            // Connection parameters:
            public static int MaxRecvDataSegmentLength = 262144;
        }
    }
}
