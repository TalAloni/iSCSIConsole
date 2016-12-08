/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace ISCSI.Client
{
    internal class ISCSISession
    {
        public ulong ISID; // Initiator Session ID
        public ushort TSIH; // Target Session Identifying Handle

        public int MaxConnections = DefaultParameters.Session.MaxConnections;
        public bool InitialR2T = DefaultParameters.Session.InitialR2T;
        public bool ImmediateData = DefaultParameters.Session.ImmediateData;
        public int MaxBurstLength = DefaultParameters.Session.MaxBurstLength;
        public int FirstBurstLength = DefaultParameters.Session.FirstBurstLength;
        public int DefaultTime2Wait = DefaultParameters.Session.DefaultTime2Wait;
        public int DefaultTime2Retain = DefaultParameters.Session.DefaultTime2Retain;
        public int MaxOutstandingR2T = DefaultParameters.Session.MaxOutstandingR2T;
        public bool DataPDUInOrder = DefaultParameters.Session.DataPDUInOrder;
        public bool DataSequenceInOrder = DefaultParameters.Session.DataSequenceInOrder;
        public int ErrorRecoveryLevel = DefaultParameters.Session.ErrorRecoveryLevel;

        private ushort m_nextCID = 1;
        private uint m_nextTaskTag = 1;
        private uint m_nextCmdSN = 1;

        public ushort GetNextCID()
        {
            ushort cid = m_nextCID;
            m_nextCID++;
            return cid;
        }

        /// <summary>
        /// Allocate Initiator Task Tag
        /// </summary>
        public uint GetNextTaskTag()
        {
            uint taskTag = m_nextTaskTag;
            m_nextTaskTag++;
            return taskTag;
        }

        // CmdSN does not advance after a command marked for immediate delivery is sent
        public uint GetNextCmdSN(bool increment)
        {
            uint cmdSN = m_nextCmdSN;
            if (increment)
            {
                m_nextCmdSN++;
            }
            return cmdSN;
        }
    }
}
