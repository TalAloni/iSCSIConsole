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
        public static int ReceiveBufferSize = ISCSIPDU.BasicHeaderSegmentLength + ISCSIServer.DeclaredParameters.MaxRecvDataSegmentLength;
        public ISCSIConnectionReceiveBuffer ReceiveBuffer = new ISCSIConnectionReceiveBuffer(ReceiveBufferSize);

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
