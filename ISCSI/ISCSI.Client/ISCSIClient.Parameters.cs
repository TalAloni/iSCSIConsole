/* Copyright (C) 2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;

namespace ISCSI.Client
{
    public partial class ISCSIClient
    {
        public class DesiredParameters
        {
            // Session parameters that will be offered to the target:
            public static bool InitialR2T = true;
            public static bool ImmediateData = true;
            public static int MaxBurstLength = DefaultParameters.Session.MaxBurstLength;
            public static int FirstBurstLength = DefaultParameters.Session.FirstBurstLength;
            public static int DefaultTime2Wait = 0;
            public static int DefaultTime2Retain = 20;
            public static int MaxOutstandingR2T = 1;
            public static bool DataPDUInOrder = true;
            public static bool DataSequenceInOrder = true;
            public static int ErrorRecoveryLevel = 0;
            public static int MaxConnections = 1;
        }

        public class DeclaredParameters
        {
            // Connection parameters:
            public static int MaxRecvDataSegmentLength = 262144;
        }
    }
}
