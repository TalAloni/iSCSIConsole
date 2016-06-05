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
        public const int DefaultMaxBurstLength = 262144;
        public const int DefaultFirstBurstLength = 65536;
        public static uint DefaultCommandQueueSize = 64;

        /// <summary>
        /// - CommandQueueSize = 0 means the initiator can send one command at a time (because MaxCmdSN = ExpCmdSN + CommandQueueSize),
        ///   (in this case there won't be any queue following the currently processed command).
        /// - Over a low-latency connection, most of the gain comes from increasing the queue size from 0 to 1
        /// - CmdSN is session wide, so CommandQueueSize is a session parameter.
        /// </summary>
        public uint CommandQueueSize = DefaultCommandQueueSize;

        /// <summary>
        /// Allow the initiator to start sending data to a target as if it has received an initial R2T
        /// </summary>
        public bool InitialR2T;
        public bool ImmediateData;

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
        public int DefaultTime2Wait;
        /// <summary>
        /// maximum time, in seconds after an initial wait (Time2Wait), before which an active task reassignment
        /// is still possible after an unexpected connection termination or a connection reset.
        /// </summary>
        public int DefaultTime2Retain;

        public int MaxOutstandingR2T;

        public bool DataPDUInOrder;
        public bool DataSequenceInOrder;

        public int ErrorRecoveryLevel;

        /// <summary>
        /// The maximum number of connections per session.
        /// </summary>
        public int MaxConnections;

        public ulong ISID; // Initiator Session ID
        public ushort TSIH; // Target Session Identifying Handle
        public bool IsDiscovery; // Indicate whether this is a discovery session
        public bool CommandNumberingStarted;
        public uint ExpCmdSN;

        public object WriteLock = new object();

        // Target Transfer Tag:
        // There are no protocol specific requirements with regard to the value of these tags,
        // but it is assumed that together with the LUN, they will enable the target to associate data with an R2T
        public static uint m_nextTransferTag; // TargetTransferTag

        // R2Ts are sequenced per command and must start with 0 for each new command.
        // We use a dictionary to store which R2TSN should be used next.
        // We use the transfer-tag that belongs to the command (SCSI Data-Out requests
        // will specify the TargetTransferTag, not the CmdSN)
        // (p.s. assuming the initator always sends the maximum amount of immediate data,
        //  it's possible to calculate the next R2TSN from the BufferOffset)

        // Dictionary of next R2TSN to use: <transfer-tag, NextR2TSN>
        public Dictionary<uint, uint> NextR2TSN = new Dictionary<uint, uint>();

        public uint GetNextTransferTag()
        {
            uint transferTag = m_nextTransferTag;
            m_nextTransferTag++;
            return transferTag;
        }

        public uint GetNextR2TSN(uint transferTag)
        {
            uint nextR2TSN = NextR2TSN[transferTag];
            NextR2TSN[transferTag]++;
            return nextR2TSN;
        }
    }
}
