/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using SCSI;
using Utilities;

namespace ISCSI.Client
{
    public class ClientHelper
    {
        /// <param name="targetName">Set to null for discovery session</param>
        internal static LoginRequestPDU GetFirstStageLoginRequest(string initiatorName, string targetName, SessionParameters session, ConnectionParameters connection)
        {
            LoginRequestPDU request = new LoginRequestPDU();
            request.InitiatorTaskTag = session.GetNextTaskTag();
            request.ISID = session.ISID;
            request.TSIH = 0; // used on the first connection for a new session
            request.CID = connection.CID;
            request.CmdSN = session.GetNextCmdSN(false);
            request.ExpStatSN = 0; 
            // The stage codes are:
            // 0 - SecurityNegotiation
            // 1 - LoginOperationalNegotiation
            // 3 - FullFeaturePhase
            request.CurrentStage = 0;
            request.NextStage = 1;
            request.Transit = true;
            request.VersionMax = 0;
            request.VersionMin = 0;
            request.LoginParameters.Add("InitiatorName", initiatorName);
            request.LoginParameters.Add("AuthMethod", "None");
            if (targetName == null)
            {
                request.LoginParameters.Add("SessionType", "Discovery");
            }
            else
            {
                // RFC 3720: For any connection within a session whose type is not "Discovery", the first Login Request MUST also include the TargetName key=value pair.
                request.LoginParameters.Add("SessionType", "Normal");
                request.LoginParameters.Add("TargetName", targetName);
                
            }
            return request;
        }

        internal static LoginRequestPDU GetSecondStageLoginRequest(LoginResponsePDU firstStageResponse, SessionParameters session, ConnectionParameters connection, bool isDiscovery)
        {
            LoginRequestPDU request = new LoginRequestPDU();
            request.ISID = firstStageResponse.ISID;
            request.TSIH = firstStageResponse.TSIH;
            request.CID = connection.CID;
            request.InitiatorTaskTag = session.GetNextTaskTag();
            request.CmdSN = session.GetNextCmdSN(false);
            request.CurrentStage = firstStageResponse.NextStage;
            request.NextStage = 3;
            request.Transit = true;
            request.VersionMax = 0;
            request.VersionMin = 0;
            request.LoginParameters.Add("HeaderDigest", "None");
            request.LoginParameters.Add("DataDigest", "None");
            request.LoginParameters.Add("MaxRecvDataSegmentLength", connection.InitiatorMaxRecvDataSegmentLength.ToString());
            if (!isDiscovery)
            {
                request.LoginParameters.Add("MaxConnections", ISCSIClient.DesiredParameters.MaxConnections.ToString());
                request.LoginParameters.Add("InitialR2T", ISCSIClient.DesiredParameters.InitialR2T ? "Yes" : "No");
                request.LoginParameters.Add("ImmediateData", ISCSIClient.DesiredParameters.ImmediateData ? "Yes" : "No");
                request.LoginParameters.Add("MaxBurstLength", ISCSIClient.DesiredParameters.MaxBurstLength.ToString());
                request.LoginParameters.Add("FirstBurstLength", ISCSIClient.DesiredParameters.FirstBurstLength.ToString());
                request.LoginParameters.Add("MaxOutstandingR2T", ISCSIClient.DesiredParameters.MaxOutstandingR2T.ToString());
                request.LoginParameters.Add("DataPDUInOrder", ISCSIClient.DesiredParameters.DataPDUInOrder ? "Yes" : "No");
                request.LoginParameters.Add("DataSequenceInOrder", ISCSIClient.DesiredParameters.DataSequenceInOrder ? "Yes" : "No");
                request.LoginParameters.Add("ErrorRecoveryLevel", ISCSIClient.DesiredParameters.ErrorRecoveryLevel.ToString());
            }
            request.LoginParameters.Add("DefaultTime2Wait", ISCSIClient.DesiredParameters.DefaultTime2Wait.ToString());
            request.LoginParameters.Add("DefaultTime2Retain", ISCSIClient.DesiredParameters.DefaultTime2Retain.ToString());
            
            return request;
        }

        internal static LoginRequestPDU GetSingleStageLoginRequest(string initiatorName, string targetName, SessionParameters session, ConnectionParameters connection)
        {
            LoginRequestPDU request = new LoginRequestPDU();
            request.ISID = session.ISID;
            request.TSIH = session.TSIH;
            request.CID = connection.CID;
            request.InitiatorTaskTag = session.GetNextTaskTag();
            request.CmdSN = session.GetNextCmdSN(false);
            request.CurrentStage = 1;
            request.NextStage = 3;
            request.Transit = true;
            request.VersionMax = 0;
            request.VersionMin = 0;
            request.LoginParameters.Add("InitiatorName", initiatorName);
            if (targetName == null)
            {
                request.LoginParameters.Add("SessionType", "Discovery");
            }
            else
            {
                request.LoginParameters.Add("SessionType", "Normal");
                request.LoginParameters.Add("TargetName", targetName);
            }
            request.LoginParameters.Add("DataDigest", "None");
            request.LoginParameters.Add("MaxRecvDataSegmentLength", connection.InitiatorMaxRecvDataSegmentLength.ToString());
            if (targetName != null)
            {
                request.LoginParameters.Add("MaxConnections", ISCSIClient.DesiredParameters.MaxConnections.ToString());
                request.LoginParameters.Add("InitialR2T", ISCSIClient.DesiredParameters.InitialR2T ? "Yes" : "No");
                request.LoginParameters.Add("ImmediateData", ISCSIClient.DesiredParameters.ImmediateData ? "Yes" : "No");
                request.LoginParameters.Add("MaxBurstLength", ISCSIClient.DesiredParameters.MaxBurstLength.ToString());
                request.LoginParameters.Add("FirstBurstLength", ISCSIClient.DesiredParameters.FirstBurstLength.ToString());
                request.LoginParameters.Add("MaxOutstandingR2T", ISCSIClient.DesiredParameters.MaxOutstandingR2T.ToString());
                request.LoginParameters.Add("DataPDUInOrder", ISCSIClient.DesiredParameters.DataPDUInOrder ? "Yes" : "No");
                request.LoginParameters.Add("DataSequenceInOrder", ISCSIClient.DesiredParameters.DataSequenceInOrder ? "Yes" : "No");
                request.LoginParameters.Add("ErrorRecoveryLevel", ISCSIClient.DesiredParameters.ErrorRecoveryLevel.ToString());
            }
            request.LoginParameters.Add("DefaultTime2Wait", ISCSIClient.DesiredParameters.DefaultTime2Wait.ToString());
            request.LoginParameters.Add("DefaultTime2Retain", ISCSIClient.DesiredParameters.DefaultTime2Retain.ToString());

            return request;
        }

        internal static void UpdateOperationalParameters(KeyValuePairList<string, string> loginParameters, SessionParameters session, ConnectionParameters connection)
        {
            string value = loginParameters.ValueOf("MaxRecvDataSegmentLength");
            if (value != null)
            {
                connection.TargetMaxRecvDataSegmentLength = Convert.ToInt32(value);
            }

            value = loginParameters.ValueOf("MaxConnections");
            if (value != null)
            {
                session.MaxConnections = Convert.ToInt32(value);
            }
            else
            {
                session.MaxConnections = ISCSIClient.DesiredParameters.MaxConnections;
            }

            value = loginParameters.ValueOf("InitialR2T");
            if (value != null)
            {
                session.InitialR2T = (value == "Yes") ? true : false;
            }
            else
            {
                session.InitialR2T = ISCSIClient.DesiredParameters.InitialR2T;
            }

            value = loginParameters.ValueOf("ImmediateData");
            if (value != null)
            {
                session.ImmediateData = (value == "Yes") ? true : false;
            }
            else
            {
                session.ImmediateData = ISCSIClient.DesiredParameters.ImmediateData;
            }

            value = loginParameters.ValueOf("MaxBurstLength");
            if (value != null)
            {
                session.MaxBurstLength = Convert.ToInt32(value);
            }
            else
            {
                session.MaxBurstLength = ISCSIClient.DesiredParameters.MaxBurstLength;
            }

            value = loginParameters.ValueOf("FirstBurstLength");
            if (value != null)
            {
                session.FirstBurstLength = Convert.ToInt32(value);
            }
            else
            {
                session.FirstBurstLength = ISCSIClient.DesiredParameters.FirstBurstLength;
            }

            value = loginParameters.ValueOf("DefaultTime2Wait");
            if (value != null)
            {
                session.DefaultTime2Wait = Convert.ToInt32(value);
            }
            else
            {
                session.DefaultTime2Wait = ISCSIClient.DesiredParameters.DefaultTime2Wait;
            }

            value = loginParameters.ValueOf("DefaultTime2Retain");
            if (value != null)
            {
                session.DefaultTime2Retain = Convert.ToInt32(value);
            }
            else
            {
                session.DefaultTime2Retain = ISCSIClient.DesiredParameters.DefaultTime2Retain;
            }

            value = loginParameters.ValueOf("MaxOutstandingR2T");
            if (value != null)
            {
                session.MaxOutstandingR2T = Convert.ToInt32(value);
            }
            else
            {
                session.MaxOutstandingR2T = ISCSIClient.DesiredParameters.MaxOutstandingR2T;
            }

            value = loginParameters.ValueOf("DataPDUInOrder");
            if (value != null)
            {
                session.DataPDUInOrder = (value == "Yes") ? true : false;
            }
            else
            {
                session.DataPDUInOrder = ISCSIClient.DesiredParameters.DataPDUInOrder;
            }

            value = loginParameters.ValueOf("DataSequenceInOrder");
            if (value != null)
            {
                session.DataSequenceInOrder = (value == "Yes") ? true : false;
            }
            else
            {
                session.DataSequenceInOrder = ISCSIClient.DesiredParameters.DataSequenceInOrder;
            }

            value = loginParameters.ValueOf("ErrorRecoveryLevel");
            if (value != null)
            {
                session.ErrorRecoveryLevel = Convert.ToInt32(value);
            }
            else
            {
                session.ErrorRecoveryLevel = ISCSIClient.DesiredParameters.ErrorRecoveryLevel;
            }
        }

        internal static LogoutRequestPDU GetLogoutRequest(SessionParameters session, ConnectionParameters connection)
        {
            LogoutRequestPDU request = new LogoutRequestPDU();
            request.ReasonCode = LogoutReasonCode.CloseTheSession;
            request.InitiatorTaskTag = session.GetNextTaskTag();
            request.CID = connection.CID;
            request.CmdSN = session.GetNextCmdSN(true);

            return request;
        }

        internal static TextRequestPDU GetSendTargetsRequest(SessionParameters session, ConnectionParameters connection)
        {
            TextRequestPDU request = new TextRequestPDU();
            request.Text = "SendTargets=All";
            request.InitiatorTaskTag = session.GetNextTaskTag();
            request.CmdSN = session.GetNextCmdSN(true);
            request.Final = true;
            request.TargetTransferTag = 0xFFFFFFFF;
            return request;
        }

        internal static SCSICommandPDU GetReportLUNsCommand(SessionParameters session, ConnectionParameters connection, uint allocationLength)
        {
            SCSICommandDescriptorBlock reportLUNs = SCSICommandDescriptorBlock.Create(SCSIOpCodeName.ReportLUNs);
            reportLUNs.TransferLength = allocationLength;
            
            SCSICommandPDU scsiCommand = new SCSICommandPDU();
            scsiCommand.CommandDescriptorBlock = reportLUNs.GetBytes();
            scsiCommand.InitiatorTaskTag = session.GetNextTaskTag();
            scsiCommand.Final = true;
            scsiCommand.Read = true;
            scsiCommand.CmdSN = session.GetNextCmdSN(true);
            scsiCommand.ExpectedDataTransferLength = allocationLength;
            return scsiCommand;
        }

        internal static SCSICommandPDU GetReadCapacity10Command(SessionParameters session, ConnectionParameters connection, ushort LUN)
        {
            SCSICommandDescriptorBlock readCapacity10 = SCSICommandDescriptorBlock.Create(SCSIOpCodeName.ReadCapacity10);
            readCapacity10.TransferLength = ReadCapacity10Parameter.Length;

            SCSICommandPDU scsiCommand = new SCSICommandPDU();
            scsiCommand.CommandDescriptorBlock = readCapacity10.GetBytes();
            scsiCommand.InitiatorTaskTag = session.GetNextTaskTag();
            scsiCommand.Final = true;
            scsiCommand.Read = true;
            scsiCommand.LUN = LUN;
            scsiCommand.CmdSN = session.GetNextCmdSN(true);
            scsiCommand.ExpectedDataTransferLength = ReadCapacity10Parameter.Length;
            return scsiCommand;
        }

        internal static SCSICommandPDU GetReadCapacity16Command(SessionParameters session, ConnectionParameters connection, ushort LUN)
        {
            SCSICommandDescriptorBlock serviceActionIn = SCSICommandDescriptorBlock.Create(SCSIOpCodeName.ServiceActionIn);
            serviceActionIn.ServiceAction = ServiceAction.ReadCapacity16;
            serviceActionIn.TransferLength = ReadCapacity16Parameter.Length;

            SCSICommandPDU scsiCommand = new SCSICommandPDU();
            scsiCommand.CommandDescriptorBlock = serviceActionIn.GetBytes();
            scsiCommand.InitiatorTaskTag = session.GetNextTaskTag();
            scsiCommand.Final = true;
            scsiCommand.Read = true;
            scsiCommand.LUN = LUN;
            scsiCommand.CmdSN = session.GetNextCmdSN(true);
            scsiCommand.ExpectedDataTransferLength = ReadCapacity16Parameter.Length;
            return scsiCommand;
        }

        internal static SCSICommandPDU GetRead16Command(SessionParameters session, ConnectionParameters connection, ushort LUN, ulong sectorIndex, uint sectorCount, int bytesPerSector)
        {
            SCSICommandDescriptorBlock read16 = SCSICommandDescriptorBlock.Create(SCSIOpCodeName.Read16);
            read16.LogicalBlockAddress64 = sectorIndex;
            read16.TransferLength = sectorCount;

            SCSICommandPDU scsiCommand = new SCSICommandPDU();
            scsiCommand.CommandDescriptorBlock = read16.GetBytes();
            scsiCommand.LUN = LUN;
            scsiCommand.InitiatorTaskTag = session.GetNextTaskTag();
            scsiCommand.Final = true;
            scsiCommand.Read = true;
            scsiCommand.CmdSN = session.GetNextCmdSN(true);
            scsiCommand.ExpectedDataTransferLength = (uint)(sectorCount * bytesPerSector);
            return scsiCommand;
        }

        internal static SCSICommandPDU GetWrite16Command(SessionParameters session, ConnectionParameters connection, ushort LUN, ulong sectorIndex, byte[] data, int bytesPerSector)
        {
            SCSICommandDescriptorBlock write16 = SCSICommandDescriptorBlock.Create(SCSIOpCodeName.Write16);
            write16.LogicalBlockAddress64 = sectorIndex;
            write16.TransferLength = (uint)(data.Length / bytesPerSector);

            SCSICommandPDU scsiCommand = new SCSICommandPDU();
            scsiCommand.CommandDescriptorBlock = write16.GetBytes();
            if (session.ImmediateData)
            {
                int immediateDataLength = Math.Min(data.Length, session.FirstBurstLength);
                scsiCommand.Data = ByteReader.ReadBytes(data, 0, immediateDataLength);
            }
            scsiCommand.LUN = LUN;
            scsiCommand.InitiatorTaskTag = session.GetNextTaskTag();
            scsiCommand.Final = true;
            scsiCommand.Write = true;
            scsiCommand.CmdSN = session.GetNextCmdSN(true);
            scsiCommand.ExpectedDataTransferLength = (uint)(data.Length);
            return scsiCommand;
        }

        internal static List<SCSIDataOutPDU> GetWriteData(SessionParameters session, ConnectionParameters connection, ushort LUN, ulong sectorIndex, byte[] data, int bytesPerSector, ReadyToTransferPDU readyToTransfer)
        {
            List<SCSIDataOutPDU> result = new List<SCSIDataOutPDU>();
            // if readyToTransfer.DesiredDataTransferLength <= connection.TargetMaxRecvDataSegmentLength we must send multiple Data-Out PDUs
            // We assume DesiredDataTransferLength does not violate session.MaxBurstLength
            int numberOfChunks = (int)Math.Ceiling((double)readyToTransfer.DesiredDataTransferLength / connection.TargetMaxRecvDataSegmentLength);
            for (int chunkIndex = 0; chunkIndex < numberOfChunks; chunkIndex++)
            {
                int chunkOffset = chunkIndex * connection.TargetMaxRecvDataSegmentLength;
                int chunkLength = (int)Math.Min(connection.TargetMaxRecvDataSegmentLength, readyToTransfer.DesiredDataTransferLength - chunkOffset);
                SCSIDataOutPDU dataOut = new SCSIDataOutPDU();
                dataOut.BufferOffset = readyToTransfer.BufferOffset + (uint)chunkOffset;
                dataOut.Data = ByteReader.ReadBytes(data, (int)dataOut.BufferOffset, chunkLength);
                dataOut.TargetTransferTag = readyToTransfer.TargetTransferTag;
                dataOut.InitiatorTaskTag = readyToTransfer.InitiatorTaskTag;
                if (chunkIndex == numberOfChunks - 1)
                {
                    dataOut.Final = true;
                }
                result.Add(dataOut);
            }
            return result;
        }

        internal static NOPOutPDU GetPingRequest(SessionParameters session, ConnectionParameters connection)
        {
            // Microsoft iSCSI Target v3.1 expects that CmdSN won't be incremented after this request regardless of whether the ImmediateDelivery bit is set or not,
            // So we set the ImmediateDelivery bit to work around the issue.
            NOPOutPDU request = new NOPOutPDU();
            request.ImmediateDelivery = true;
            request.InitiatorTaskTag = session.GetNextTaskTag();
            request.CmdSN = session.GetNextCmdSN(false);
            // RFC 3720: The NOP-Out MUST only have the Target Transfer Tag set if it is issued in response to a NOP-In (with a valid Target Transfer Tag).
            // Otherwise, the Target Transfer Tag MUST be set to 0xffffffff.
            request.TargetTransferTag = 0xFFFFFFFF;
            return request;
        }

        internal static NOPOutPDU GetPingResponse(NOPInPDU request, SessionParameters session, ConnectionParameters connection)
        {
            NOPOutPDU response = new NOPOutPDU();
            // If the Initiator Task Tag contains 0xffffffff, the I bit MUST be set to 1 and the CmdSN is not advanced after this PDU is sent.
            response.ImmediateDelivery = true;
            // RFC 3720: The NOP-Out MUST have the Initiator Task Tag set to a valid value only if a response in the form of NOP-In is requested.
            // Otherwise, the Initiator Task Tag MUST be set to 0xffffffff
            response.InitiatorTaskTag = 0xFFFFFFFF;
            response.CmdSN = session.GetNextCmdSN(false);
            response.TargetTransferTag = request.TargetTransferTag;
            // p.s. the Data Segment (of the request sent by the target) MUST NOT contain any data
            return response;
        }

        public static ulong GetRandomISID()
        {
            byte a = 0x80; // Random
            ushort b = (ushort)(new Random().Next(UInt16.MaxValue + 1));
            byte c = (byte)(new Random().Next(Byte.MaxValue + 1));
            ushort d = 0;
            ulong isid = (ulong)a << 40 | (ulong)b << 24 | (ulong)c << 16 | (ulong)d;
            return isid;
        }
    }
}
