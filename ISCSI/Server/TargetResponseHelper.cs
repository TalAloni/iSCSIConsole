/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using DiskAccessLibrary;
using Utilities;

namespace ISCSI.Server
{
    public class TargetResponseHelper
    {
        internal static List<ISCSIPDU> GetReadyToTransferPDUs(SCSICommandPDU command, ISCSITarget target, SessionParameters session, ConnectionParameters connection, out List<SCSICommandPDU> commandsToExecute)
        {
            // We return either SCSIResponsePDU or List<SCSIDataInPDU>
            List<ISCSIPDU> responseList = new List<ISCSIPDU>();
            commandsToExecute = new List<SCSICommandPDU>();
            
            string connectionIdentifier = ConnectionState.GetConnectionIdentifier(session, connection);

            if (command.Write && command.DataSegmentLength < command.ExpectedDataTransferLength)
            {
                uint transferTag = session.GetNextTransferTag();

                // Create buffer for next segments (we only execute the command after receiving all of its data)
                Array.Resize<byte>(ref command.Data, (int)command.ExpectedDataTransferLength);
                
                // Send R2T
                ReadyToTransferPDU response = new ReadyToTransferPDU();
                response.InitiatorTaskTag = command.InitiatorTaskTag;
                response.R2TSN = 0; // R2Ts are sequenced per command and must start with 0 for each new command;
                response.TargetTransferTag = transferTag;
                response.BufferOffset = command.DataSegmentLength;
                response.DesiredDataTransferLength = Math.Min((uint)connection.TargetMaxRecvDataSegmentLength, command.ExpectedDataTransferLength - response.BufferOffset);

                connection.AddTransfer(transferTag, command, 1);
                session.CommandsInTransfer.Add(command.CmdSN);

                responseList.Add(response);
                return responseList;
            }

            if (session.IsPrecedingCommandPending(command.CmdSN))
            {
                session.DelayedCommands.Add(command);
            }
            else
            {
                commandsToExecute.Add(command);
            }
            return responseList;
        }

        internal static List<ISCSIPDU> GetReadyToTransferPDUs(SCSIDataOutPDU request, ISCSITarget target, SessionParameters session, ConnectionParameters connection, out List<SCSICommandPDU> commandsToExecute)
        {
            List<ISCSIPDU> responseList = new List<ISCSIPDU>();
            commandsToExecute = new List<SCSICommandPDU>();

            string connectionIdentifier = ConnectionState.GetConnectionIdentifier(session, connection);
            TransferEntry transfer = connection.GetTransferEntry(request.TargetTransferTag);
            if (transfer == null)
            {
                ISCSIServer.Log("[{0}][GetSCSIDataOutResponsePDU] Invalid TargetTransferTag {1}", connectionIdentifier, request.TargetTransferTag);
                RejectPDU reject = new RejectPDU();
                reject.InitiatorTaskTag = request.InitiatorTaskTag;
                reject.Reason = RejectReason.InvalidPDUField;
                reject.Data = ByteReader.ReadBytes(request.GetBytes(), 0, 48);
                responseList.Add(reject);
                return responseList;
            }

            uint offset = request.BufferOffset;
            uint totalLength = (uint)transfer.Command.ExpectedDataTransferLength;

            // Store segment (we only execute the command after receiving all of its data)
            Array.Copy(request.Data, 0, transfer.Command.Data, offset, request.DataSegmentLength);
            
            ISCSIServer.Log(String.Format("[{0}][GetSCSIDataOutResponsePDU] Buffer offset: {1}, Total length: {2}", connectionIdentifier, offset, totalLength));

            if (offset + request.DataSegmentLength == totalLength)
            {
                // Last Data-out PDU
                if (session.IsPrecedingCommandPending(transfer.Command.CmdSN))
                {
                    session.DelayedCommands.Add(transfer.Command);
                }
                else
                {
                    commandsToExecute.Add(transfer.Command);
                    connection.RemoveTransfer(request.TargetTransferTag);
                    session.CommandsInTransfer.Remove(transfer.Command.CmdSN);
                    // Check if delayed commands are ready to be executed
                    List<SCSICommandPDU> pendingCommands = session.GetDelayedCommandsReadyForExecution();
                    foreach (SCSICommandPDU pendingCommand in pendingCommands)
                    {
                        commandsToExecute.Add(pendingCommand);
                    }
                }
                return responseList;
            }
            else
            {
                // Send R2T
                ReadyToTransferPDU response = new ReadyToTransferPDU();
                response.InitiatorTaskTag = request.InitiatorTaskTag;
                response.TargetTransferTag = request.TargetTransferTag;
                response.R2TSN = transfer.NextR2NSN;
                response.BufferOffset = offset + request.DataSegmentLength; // where we left off
                response.DesiredDataTransferLength = Math.Min((uint)connection.TargetMaxRecvDataSegmentLength, totalLength - response.BufferOffset);

                transfer.NextR2NSN++;

                responseList.Add(response);
                return responseList;
            }
        }

        internal static List<ISCSIPDU> GetSCSICommandResponse(SCSICommandPDU command, ISCSITarget target, SessionParameters session, ConnectionParameters connection)
        {
            string connectionIdentifier = ConnectionState.GetConnectionIdentifier(session, connection);
            ISCSIServer.Log("[{0}] Executing Command: CmdSN: {1}", connectionIdentifier, command.CmdSN);
            byte[] scsiResponse;
            SCSIStatusCodeName status = target.ExecuteCommand(command.CommandDescriptorBlock, command.LUN, command.Data, out scsiResponse);
            return PrepareSCSICommandResponse(command, status, scsiResponse, connection);
        }

        internal static List<ISCSIPDU> PrepareSCSICommandResponse(SCSICommandPDU command, SCSIStatusCodeName status, byte[] scsiResponse, ConnectionParameters connection)
        {
            List<ISCSIPDU> responseList = new List<ISCSIPDU>();
            if (!command.Read || status != SCSIStatusCodeName.Good)
            {
                // RFC 3720: if the command is completed with an error, then the response and sense data MUST be sent in a SCSI Response PDU
                SCSIResponsePDU response = new SCSIResponsePDU();
                response.InitiatorTaskTag = command.InitiatorTaskTag;
                response.Status = status;
                response.Data = scsiResponse;
                if (command.Read)
                {
                    EnforceExpectedDataTransferLength(response, command.ExpectedDataTransferLength);
                }
                responseList.Add(response);
            }
            else if (scsiResponse.Length <= connection.InitiatorMaxRecvDataSegmentLength)
            {
                SCSIDataInPDU response = new SCSIDataInPDU();
                response.InitiatorTaskTag = command.InitiatorTaskTag;
                response.Status = status;
                response.StatusPresent = true;
                response.Final = true;
                response.Data = scsiResponse;
                EnforceExpectedDataTransferLength(response, command.ExpectedDataTransferLength);
                responseList.Add(response);
            }
            else // we have to split the response to multiple Data-In PDUs
            {
                int bytesLeftToSend = scsiResponse.Length;

                uint dataSN = 0;
                while (bytesLeftToSend > 0)
                {
                    int dataSegmentLength = Math.Min(connection.InitiatorMaxRecvDataSegmentLength, bytesLeftToSend);
                    int dataOffset = scsiResponse.Length - bytesLeftToSend;

                    SCSIDataInPDU response = new SCSIDataInPDU();
                    response.InitiatorTaskTag = command.InitiatorTaskTag;
                    if (bytesLeftToSend == dataSegmentLength)
                    {
                        // last Data-In PDU
                        response.Status = status;
                        response.StatusPresent = true;
                        response.Final = true;
                    }
                    response.BufferOffset = (uint)dataOffset;
                    response.DataSN = dataSN;
                    dataSN++;

                    response.Data = new byte[dataSegmentLength];
                    Array.Copy(scsiResponse, dataOffset, response.Data, 0, dataSegmentLength);
                    responseList.Add(response);

                    bytesLeftToSend -= dataSegmentLength;
                }
            }

            return responseList;
        }

        public static void EnforceExpectedDataTransferLength(SCSIResponsePDU response, uint expectedDataTransferLength)
        {
            if (response.Data.Length > expectedDataTransferLength)
            {
                response.ResidualOverflow = true;
                response.ResidualCount = (uint)(response.Data.Length - expectedDataTransferLength);
                response.Data = ByteReader.ReadBytes(response.Data, 0, (int)expectedDataTransferLength);
            }
            else if (response.Data.Length < expectedDataTransferLength)
            {
                response.ResidualUnderflow = true;
                response.ResidualCount = (uint)(expectedDataTransferLength - response.Data.Length);
            }
        }

        public static void EnforceExpectedDataTransferLength(SCSIDataInPDU response, uint expectedDataTransferLength)
        {
            if (response.Data.Length > expectedDataTransferLength)
            {
                response.ResidualOverflow = true;
                response.ResidualCount = (uint)(response.Data.Length - expectedDataTransferLength);
                response.Data = ByteReader.ReadBytes(response.Data, 0, (int)expectedDataTransferLength);
            }
            else if (response.Data.Length < expectedDataTransferLength)
            {
                response.ResidualUnderflow = true;
                response.ResidualCount = (uint)(expectedDataTransferLength - response.Data.Length);
            }
        }
    }
}
