/* Copyright (C) 2012-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
using SCSI;
using Utilities;

namespace ISCSI.Server
{
    internal class TargetResponseHelper
    {
        internal static List<ReadyToTransferPDU> GetReadyToTransferPDUs(SCSICommandPDU command, ConnectionParameters connection, out List<SCSICommandPDU> commandsToExecute)
        {
            // We return either SCSIResponsePDU or List<SCSIDataInPDU>
            List<ReadyToTransferPDU> responseList = new List<ReadyToTransferPDU>();
            commandsToExecute = new List<SCSICommandPDU>();

            ISCSISession session = connection.Session;

            if (command.Write && command.DataSegmentLength < command.ExpectedDataTransferLength)
            {
                uint transferTag = session.GetNextTransferTag();

                // Create buffer for next segments (we only execute the command after receiving all of its data)
                Array.Resize<byte>(ref command.Data, (int)command.ExpectedDataTransferLength);
                
                // Send R2Ts:
                uint bytesLeft = command.ExpectedDataTransferLength - command.DataSegmentLength;
                uint nextOffset = command.DataSegmentLength;
                if (!session.InitialR2T)
                {
                    uint firstDataPDULength = Math.Min((uint)session.FirstBurstLength, command.ExpectedDataTransferLength) - command.DataSegmentLength;
                    bytesLeft -= firstDataPDULength;
                    nextOffset += firstDataPDULength;
                }
                int totalR2Ts = (int)Math.Ceiling((double)bytesLeft / connection.TargetMaxRecvDataSegmentLength);
                int outgoingR2Ts = Math.Min(session.MaxOutstandingR2T, totalR2Ts);

                for (uint index = 0; index < outgoingR2Ts; index++)
                {
                    ReadyToTransferPDU response = new ReadyToTransferPDU();
                    response.InitiatorTaskTag = command.InitiatorTaskTag;
                    response.R2TSN = index; // R2Ts are sequenced per command and must start with 0 for each new command;
                    response.TargetTransferTag = transferTag;
                    response.BufferOffset = nextOffset;
                    response.DesiredDataTransferLength = Math.Min((uint)connection.TargetMaxRecvDataSegmentLength, command.ExpectedDataTransferLength - response.BufferOffset);
                    responseList.Add(response);
                    nextOffset += (uint)connection.TargetMaxRecvDataSegmentLength;
                }
                connection.AddTransfer(command.InitiatorTaskTag, transferTag, command, (uint)outgoingR2Ts, nextOffset, (uint)totalR2Ts);
                session.CommandsInTransfer.Add(command.CmdSN);
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

        internal static List<ReadyToTransferPDU> GetReadyToTransferPDUs(SCSIDataOutPDU request, ConnectionParameters connection, out List<SCSICommandPDU> commandsToExecute)
        {
            List<ReadyToTransferPDU> responseList = new List<ReadyToTransferPDU>();
            commandsToExecute = new List<SCSICommandPDU>();

            ISCSISession session = connection.Session;
            TransferEntry transfer = null;
            if (request.TargetTransferTag != 0xFFFFFFFF) // 0xFFFFFFFF means Target Transfer Tag is not supplied
            {
                transfer = connection.GetTransferEntry(request.TargetTransferTag);
            }
            else if (!session.InitialR2T)
            {
                transfer = connection.GetTransferEntryUsingTaskTag(request.InitiatorTaskTag);
            }

            if (transfer == null)
            {
                throw new InvalidTargetTransferTagException(request.TargetTransferTag);
            }

            uint offset = request.BufferOffset;
            uint totalLength = (uint)transfer.Command.ExpectedDataTransferLength;

            // Store segment (we only execute the command after receiving all of its data)
            Array.Copy(request.Data, 0, transfer.Command.Data, offset, request.DataSegmentLength);

            if (offset + request.DataSegmentLength == totalLength)
            {
                // Last Data-out PDU
                connection.RemoveTransfer(request.InitiatorTaskTag, request.TargetTransferTag);
                session.CommandsInTransfer.Remove(transfer.Command.CmdSN);
                if (session.IsPrecedingCommandPending(transfer.Command.CmdSN))
                {
                    session.DelayedCommands.Add(transfer.Command);
                }
                else
                {
                    commandsToExecute.Add(transfer.Command);
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
                // RFC 3720: An R2T MAY be answered with one or more SCSI Data-Out PDUs with a matching Target Transfer Tag.
                // If an R2T is answered with a single Data-Out PDU, the Buffer Offset in the Data PDU MUST be the same as the one specified
                // by the R2T, and the data length of the Data PDU MUST be the same as the Desired Data Transfer Length specified in the R2T.
                // If the R2T is answered with a sequence of Data PDUs, the Buffer Offset and Length MUST be within
                // the range of those specified by R2T, and the last PDU MUST have the F bit set to 1.
                // An R2T is considered outstanding until the last data PDU is transferred.
                if (request.Final)
                {
                    // We already sent as many R2T as we could, we will only send R2T if any remained.
                    if (transfer.NextR2TSN < transfer.TotalR2Ts)
                    {
                        // Send R2T
                        ReadyToTransferPDU response = new ReadyToTransferPDU();
                        response.InitiatorTaskTag = request.InitiatorTaskTag;
                        response.TargetTransferTag = request.TargetTransferTag;
                        response.R2TSN = transfer.NextR2TSN;
                        response.BufferOffset = transfer.NextOffset; // where we left off
                        response.DesiredDataTransferLength = Math.Min((uint)connection.TargetMaxRecvDataSegmentLength, totalLength - response.BufferOffset);
                        responseList.Add(response);

                        transfer.NextR2TSN++;
                        transfer.NextOffset += (uint)connection.TargetMaxRecvDataSegmentLength;
                    }
                }
                return responseList;
            }
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
