/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using SCSI;
using Utilities;

namespace ISCSI.Server
{
    /// <summary>
    /// iSCSI Connection state object
    /// </summary>
    internal class ConnectionState
    {
        public Socket ClientSocket = null;
        /// <summary>
        /// DataSegmentLength MUST not exceed MaxRecvDataSegmentLength for the direction it is sent and the total of all the DataSegmentLength of all PDUs in a sequence MUST not exceed MaxBurstLength (or FirstBurstLength for unsolicited data).
        /// </summary>
        public const int ReceiveBufferSize = 131072; // Note: FirstBurstLength, MaxBurstLength and MaxRecvDataSegmentLength put a cap on DataSegmentLength, NOT on the PDU length.
        public byte[] ReceiveBuffer = new byte[ReceiveBufferSize]; // immediate receive buffer
        public byte[] ConnectionBuffer = new byte[0]; // we append the receive buffer here until we have a complete PDU

        public ConnectionParameters ConnectionParameters = new ConnectionParameters();

        public CountdownLatch RunningSCSICommands = new CountdownLatch();
        public BlockingQueue<ISCSIPDU> SendQueue = new BlockingQueue<ISCSIPDU>();

        public void OnCommandCompleted(SCSIStatusCodeName status, byte[] responseBytes, object task)
        {
            RunningSCSICommands.Decrement();
            SCSICommandPDU command = (SCSICommandPDU)task;
            List<ISCSIPDU> responseList = TargetResponseHelper.PrepareSCSICommandResponse(command, status, responseBytes, ConnectionParameters);
            SendQueue.Enqueue(responseList);
        }

        public string ConnectionIdentifier
        {
            get
            {
                return ConnectionParameters.ConnectionIdentifier;
            }
        }

        public ISCSISession Session
        {
            get
            {
                return ConnectionParameters.Session;
            }
        }

        public ISCSITarget Target
        {
            get
            {
                return Session.Target;
            }
        }
    }
}
