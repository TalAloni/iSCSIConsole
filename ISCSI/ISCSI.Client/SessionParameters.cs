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
    public class SessionParameters
    {
        public const int DefaultMaxBurstLength = 262144;
        public const int DefaultFirstBurstLength = 65536;

        public ulong ISID; // Initiator Session ID
        public ushort TSIH; // Target Session Identifying Handle

        public bool InitialR2T;
        public bool ImmediateData;
        public int MaxBurstLength = DefaultMaxBurstLength;
        public int FirstBurstLength = DefaultFirstBurstLength;
        public int DefaultTime2Wait;
        public int DefaultTime2Retain;
        public int MaxOutstandingR2T;
        public bool DataPDUInOrder;
        public bool DataSequenceInOrder;
        public int ErrorRecoveryLevel;

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
