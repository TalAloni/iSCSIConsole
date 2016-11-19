/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace ISCSI.Server
{
    public class SessionParameters
    {
        public const int DefaultMaxConnections = 1;
        public const bool DefaultInitialR2T = true;
        public const bool DefaultImmediateData = true;
        public const int DefaultMaxBurstLength = 262144;
        public const int DefaultFirstBurstLength = 65536;
        public const int DefaultDefaultTime2Wait = 2;
        public const int DefaultDefaultTime2Retain = 20;
        public const int DefaultMaxOutstandingR2T = 1;
        public const bool DefaultDataPDUInOrder = true;
        public const bool DefaultDataSequenceInOrder = true;
        public const int DefaultErrorRecoveryLevel = 0;

        public static uint DefaultCommandQueueSize = 64;

        /// <summary>
        /// The maximum number of connections per session.
        /// </summary>
        public int MaxConnections = DefaultMaxConnections;

        /// <summary>
        /// Allow the initiator to start sending data to a target as if it has received an initial R2T
        /// </summary>
        public bool InitialR2T = DefaultInitialR2T;

        public bool ImmediateData = DefaultImmediateData;

        /// <summary>
        /// The total of all the DataSegmentLength of all PDUs in a sequence MUST not exceed MaxBurstLength.
        /// Maximum SCSI data payload in bytes in a Data-In or a solicited Data-Out iSCSI sequence (i.e. that belongs to a single command).
        /// Irrelevant to the target in general, the initiator instructs us using ExpectedDataTransferLength.
        /// </summary>
        public int MaxBurstLength = DefaultMaxBurstLength;

        /// <summary>
        /// The total of all the DataSegmentLength of all PDUs in a sequence MUST not exceed FirstBurstLength for unsolicited data.
        /// Maximum amount in bytes of unsolicited [SCSI] data an iSCSI initiator may send to the target during the execution of a single SCSI command.
        /// Irrelevant to the target in general, irrelevant when (InitialR2T = Yes and) ImmediateData = No.
        /// </summary>
        public int FirstBurstLength = DefaultFirstBurstLength;
        
        /// <summary>
        /// minimum time, in seconds, to wait before attempting an explicit/implicit logout after connection termination / reset.
        /// </summary>
        public int DefaultTime2Wait = DefaultDefaultTime2Wait;

        /// <summary>
        /// maximum time, in seconds after an initial wait (Time2Wait), before which an active task reassignment
        /// is still possible after an unexpected connection termination or a connection reset.
        /// </summary>
        public int DefaultTime2Retain = DefaultDefaultTime2Retain;

        public int MaxOutstandingR2T = DefaultMaxOutstandingR2T;
        public bool DataPDUInOrder = DefaultDataPDUInOrder;
        public bool DataSequenceInOrder = DefaultDataSequenceInOrder;
        public int ErrorRecoveryLevel = DefaultErrorRecoveryLevel;

        /// <summary>
        /// - CommandQueueSize = 0 means the initiator can send one command at a time (because MaxCmdSN = ExpCmdSN + CommandQueueSize),
        ///   (in this case there won't be any queue following the currently processed command).
        /// - Over a low-latency connection, most of the gain comes from increasing the queue size from 0 to 1
        /// - CmdSN is session wide, so CommandQueueSize is a session parameter.
        /// </summary>
        public uint CommandQueueSize = DefaultCommandQueueSize;

        public ulong ISID; // Initiator Session ID
        public ushort TSIH; // Target Session Identifying Handle
        public bool IsDiscovery; // Indicate whether this is a discovery session
        public bool IsFullFeaturePhase; // Indicate whether login has been completed
        public bool CommandNumberingStarted;
        public uint ExpCmdSN;

        /// <summary>
        /// Target Transfer Tag:
        /// There are no protocol specific requirements with regard to the value of these tags,
        /// but it is assumed that together with the LUN, they will enable the target to associate data with an R2T.
        /// </summary>
        private static uint m_nextTransferTag;

        public uint GetNextTransferTag()
        {
            uint transferTag = m_nextTransferTag;
            m_nextTransferTag++;
            return transferTag;
        }
    }
}
