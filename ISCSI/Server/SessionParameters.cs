/* Copyright (C) 2012-2015 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace ISCSI
{
    public class SessionParameters
    {
        public ulong ISID; // Initiator Session ID
        public ushort TSIH; // Target Session Identifying Handle
        
        /// <summary>
        /// Maximum amount in bytes of unsolicited [SCSI] data an iSCSI initiator may send to the target during the execution of a single SCSI command.
        /// Irrelevant to the target in general, irrelevant when (InitialR2T = Yes and) ImmediateData = No.
        /// </summary>
        public uint FirstBurstLength = ISCSIServer.DefaultFirstBurstLength;
        
        /// <summary>
        /// Maximum SCSI data payload in bytes in a Data-In or a solicited Data-Out iSCSI sequence (i.e. that belongs to a single command).
        /// Irrelevant to the target in general, the initiator instructs us using ExpectedDataTransferLength.
        /// </summary>
        public uint MaxBurstLength = ISCSIServer.DefaultMaxBurstLength;
        public bool IsDiscovery; // Indicate whether this is a discovery session
        public uint ExpCmdSN; // CmdSN is session wide

        // R2Ts are sequenced per command and must start with 0 for each new command.
        // We use a dictionary to store which R2TSN should be used next.
        // We use the transfer-tag that belongs to the command (SCSI Data-Out requests
        // will specify the TargetTransferTag, not the CmdSN)
        // (p.s. assuming the initator always sends the maximum amount of immediate data,
        //  it's possible to calculate the next R2TSN from the BufferOffset)

        // Dictionary of next R2TSN to use: <transfer-tag, NextR2TSN>
        public Dictionary<uint, uint> NextR2TSN = new Dictionary<uint, uint>();
    }
}
