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
    internal class ISCSISession
    {
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

        public uint CommandQueueSize = ISCSIServer.DefaultCommandQueueSize;

        public readonly string InitiatorName;
        public readonly ulong ISID; // Initiator Session ID
        public readonly ushort TSIH; // Target Session Identifying Handle
        public bool IsDiscovery; // Indicate whether this is a discovery session
        public bool IsFullFeaturePhase; // Indicate whether login has been completed
        public bool CommandNumberingStarted;
        public uint ExpCmdSN;

        public ISCSITarget Target; // Across all connections within a session, an initiator sees one and the same target.
        public List<uint> CommandsInTransfer = new List<uint>();
        public List<SCSICommandPDU> DelayedCommands = new List<SCSICommandPDU>();

        /// <summary>
        /// Target Transfer Tag:
        /// There are no protocol specific requirements with regard to the value of these tags,
        /// but it is assumed that together with the LUN, they will enable the target to associate data with an R2T.
        /// </summary>
        private uint m_nextTransferTag = 0;
        private object m_transferTagLock = new object();

        public ISCSISession(string initiatorName, ulong isid, ushort tsih)
        {
            InitiatorName = initiatorName;
            ISID = isid;
            TSIH = tsih;
        }

        public uint GetNextTransferTag()
        {
            lock (m_transferTagLock)
            {
                uint transferTag = m_nextTransferTag;
                m_nextTransferTag++;
                return transferTag;
            }
        }

        public bool IsPrecedingCommandPending(uint cmdSN)
        {
            foreach (uint entry in CommandsInTransfer)
            {
                if (IsFirstCmdSNPreceding(entry, cmdSN))
                {
                    return true;
                }
            }
            return false;
        }

        public List<SCSICommandPDU> GetDelayedCommandsReadyForExecution()
        {
            List<SCSICommandPDU> result = new List<SCSICommandPDU>();
            if (CommandsInTransfer.Count == 0)
            {
                result.AddRange(DelayedCommands);
                DelayedCommands.Clear();
                return result;
            }

            // We find the earliest CmdSN of the commands in transfer
            uint earliestCmdSN = CommandsInTransfer[0];
            for(int index = 1; index < CommandsInTransfer.Count; index++)
            {
                if (IsFirstCmdSNPreceding(CommandsInTransfer[index], earliestCmdSN))
                {
                    earliestCmdSN = CommandsInTransfer[index];
                }
            }

            // Any command that is preceding earliestCmdSN should be executed
            for(int index = 0; index < DelayedCommands.Count; index++)
            {
                SCSICommandPDU delayedCommand = DelayedCommands[index];
                if (IsFirstCmdSNPreceding(delayedCommand.CmdSN, earliestCmdSN))
                {
                    result.Add(delayedCommand);
                    DelayedCommands.RemoveAt(index);
                    index--;
                }
            }
            return result;
        }

        public string SessionIdentifier
        {
            get
            {
                return String.Format("{0},ISID={1},TSIH={2}", InitiatorName, ISID.ToString("x"), TSIH.ToString("x"));
            }
        }

        /// <summary>
        /// Returns true if cmdSN1 should be executed before cmdSN2
        /// </summary>
        public static bool IsFirstCmdSNPreceding(uint cmdSN1, uint cmdSN2)
        {
            // The iSCSI protocol is designed to avoid having old, retried command instances appear in a valid command window after a command sequence number wrap around.
            const uint commandWindow = 2 ^ 31 - 1;
            if (cmdSN2 >= commandWindow)
            {
                if ((cmdSN1 > cmdSN2 - commandWindow) && (cmdSN1 < cmdSN2))
                {
                    return true;
                }
            }
            else
            {
                if ((cmdSN1 > cmdSN2 - commandWindow) || (cmdSN1 < cmdSN2))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
