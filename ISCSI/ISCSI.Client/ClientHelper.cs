/* Copyright (C) 2012-2024 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
        internal static LoginRequestPDU GetFirstStageLoginRequest(string initiatorName, string targetName, ConnectionParameters connection)
        {
            ISCSISession session = connection.Session;
            LoginRequestPDU request = new LoginRequestPDU();
            request.InitiatorTaskTag = session.GetNextTaskTag();
            request.ISID = session.ISID;
            request.TSIH = session.TSIH;
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
            KeyValuePairList<string, string> loginParameters = new KeyValuePairList<string, string>();
            loginParameters.Add("InitiatorName", initiatorName);
            loginParameters.Add("AuthMethod", "None");
            if (targetName == null)
            {
                loginParameters.Add("SessionType", "Discovery");
            }
            else
            {
                // RFC 3720: For any connection within a session whose type is not "Discovery", the first Login Request MUST also include the TargetName key=value pair.
                loginParameters.Add("SessionType", "Normal");
                loginParameters.Add("TargetName", targetName);
            }
            request.LoginParameters = loginParameters;
            return request;
        }

        internal static LoginRequestPDU GetSecondStageLoginRequest(LoginResponsePDU firstStageResponse, ConnectionParameters connection, bool isDiscovery)
        {
            ISCSISession session = connection.Session;
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
            KeyValuePairList<string, string> loginParameters = new KeyValuePairList<string, string>();
            loginParameters.Add("HeaderDigest", "None");
            loginParameters.Add("DataDigest", "None");
            loginParameters.Add("MaxRecvDataSegmentLength", connection.InitiatorMaxRecvDataSegmentLength.ToString());
            if (!isDiscovery)
            {
                loginParameters.Add("MaxConnections", ISCSIClient.DesiredParameters.MaxConnections.ToString());
                loginParameters.Add("InitialR2T", ISCSIClient.DesiredParameters.InitialR2T ? "Yes" : "No");
                loginParameters.Add("ImmediateData", ISCSIClient.DesiredParameters.ImmediateData ? "Yes" : "No");
                loginParameters.Add("MaxBurstLength", ISCSIClient.DesiredParameters.MaxBurstLength.ToString());
                loginParameters.Add("FirstBurstLength", ISCSIClient.DesiredParameters.FirstBurstLength.ToString());
                loginParameters.Add("MaxOutstandingR2T", ISCSIClient.DesiredParameters.MaxOutstandingR2T.ToString());
                loginParameters.Add("DataPDUInOrder", ISCSIClient.DesiredParameters.DataPDUInOrder ? "Yes" : "No");
                loginParameters.Add("DataSequenceInOrder", ISCSIClient.DesiredParameters.DataSequenceInOrder ? "Yes" : "No");
                loginParameters.Add("ErrorRecoveryLevel", ISCSIClient.DesiredParameters.ErrorRecoveryLevel.ToString());
            }
            loginParameters.Add("DefaultTime2Wait", ISCSIClient.DesiredParameters.DefaultTime2Wait.ToString());
            loginParameters.Add("DefaultTime2Retain", ISCSIClient.DesiredParameters.DefaultTime2Retain.ToString());
            request.LoginParameters = loginParameters;
            return request;
        }

        internal static LoginRequestPDU GetSingleStageLoginRequest(string initiatorName, string targetName, ConnectionParameters connection)
        {
            ISCSISession session = connection.Session;
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
            KeyValuePairList<string, string> loginParameters = new KeyValuePairList<string, string>();
            loginParameters.Add("InitiatorName", initiatorName);
            if (targetName == null)
            {
                loginParameters.Add("SessionType", "Discovery");
            }
            else
            {
                loginParameters.Add("SessionType", "Normal");
                loginParameters.Add("TargetName", targetName);
            }
            loginParameters.Add("DataDigest", "None");
            loginParameters.Add("MaxRecvDataSegmentLength", connection.InitiatorMaxRecvDataSegmentLength.ToString());
            if (targetName != null)
            {
                loginParameters.Add("MaxConnections", ISCSIClient.DesiredParameters.MaxConnections.ToString());
                loginParameters.Add("InitialR2T", ISCSIClient.DesiredParameters.InitialR2T ? "Yes" : "No");
                loginParameters.Add("ImmediateData", ISCSIClient.DesiredParameters.ImmediateData ? "Yes" : "No");
                loginParameters.Add("MaxBurstLength", ISCSIClient.DesiredParameters.MaxBurstLength.ToString());
                loginParameters.Add("FirstBurstLength", ISCSIClient.DesiredParameters.FirstBurstLength.ToString());
                loginParameters.Add("MaxOutstandingR2T", ISCSIClient.DesiredParameters.MaxOutstandingR2T.ToString());
                loginParameters.Add("DataPDUInOrder", ISCSIClient.DesiredParameters.DataPDUInOrder ? "Yes" : "No");
                loginParameters.Add("DataSequenceInOrder", ISCSIClient.DesiredParameters.DataSequenceInOrder ? "Yes" : "No");
                loginParameters.Add("ErrorRecoveryLevel", ISCSIClient.DesiredParameters.ErrorRecoveryLevel.ToString());
            }
            loginParameters.Add("DefaultTime2Wait", ISCSIClient.DesiredParameters.DefaultTime2Wait.ToString());
            loginParameters.Add("DefaultTime2Retain", ISCSIClient.DesiredParameters.DefaultTime2Retain.ToString());
            request.LoginParameters = loginParameters;
            return request;
        }

        internal static void UpdateOperationalParameters(KeyValuePairList<string, string> loginParameters, ConnectionParameters connection)
        {
            ISCSISession session = connection.Session;
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

        internal static LogoutRequestPDU GetLogoutRequest(ConnectionParameters connection)
        {
            ISCSISession session = connection.Session;
            LogoutRequestPDU request = new LogoutRequestPDU();
            request.ReasonCode = LogoutReasonCode.CloseTheSession;
            request.InitiatorTaskTag = session.GetNextTaskTag();
            request.CID = connection.CID;
            request.CmdSN = session.GetNextCmdSN(true);

            return request;
        }

        internal static TextRequestPDU GetSendTargetsRequest(ConnectionParameters connection)
        {
            ISCSISession session = connection.Session;
            TextRequestPDU request = new TextRequestPDU();
            request.Text = "SendTargets=All";
            request.InitiatorTaskTag = session.GetNextTaskTag();
            request.CmdSN = session.GetNextCmdSN(true);
            request.Final = true;
            request.TargetTransferTag = 0xFFFFFFFF;
            return request;
        }

        internal static SCSICommandPDU GetReportLUNsCommand(ConnectionParameters connection, uint allocationLength)
        {
            ISCSISession session = connection.Session;
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

        internal static SCSICommandPDU GetReadCapacity10Command(ConnectionParameters connection, ushort LUN)
        {
            ISCSISession session = connection.Session;
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

        internal static SCSICommandPDU GetReadCapacity16Command(ConnectionParameters connection, ushort LUN)
        {
            ISCSISession session = connection.Session;
            SCSICommandDescriptorBlock serviceActionIn = SCSICommandDescriptorBlock.Create(SCSIOpCodeName.ServiceActionIn16);
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

        internal static SCSICommandPDU GetRead16Command(ConnectionParameters connection, ushort LUN, ulong sectorIndex, uint sectorCount, int bytesPerSector)
        {
            ISCSISession session = connection.Session;
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

        internal static SCSICommandPDU GetWrite16Command(ConnectionParameters connection, ushort LUN, ulong sectorIndex, byte[] data, int bytesPerSector)
        {
            ISCSISession session = connection.Session;
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

        internal static List<SCSIDataOutPDU> GetWriteData(ConnectionParameters connection, ushort LUN, ulong sectorIndex, byte[] data, int bytesPerSector, ReadyToTransferPDU readyToTransfer)
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

        internal static NOPOutPDU GetPingRequest(ConnectionParameters connection)
        {
            ISCSISession session = connection.Session;
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

        internal static NOPOutPDU GetPingResponse(NOPInPDU request, ConnectionParameters connection)
        {
            ISCSISession session = connection.Session;
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
