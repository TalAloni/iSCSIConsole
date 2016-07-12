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
        internal static List<ISCSIPDU> GetSCSIResponsePDU(SCSICommandPDU command, ISCSITarget target, SessionParameters session, ConnectionParameters connection)
        {
            // We return either SCSIResponsePDU or List<SCSIDataInPDU>
            List<ISCSIPDU> responseList = new List<ISCSIPDU>();
            
            string connectionIdentifier = StateObject.GetConnectionIdentifier(session, connection);

            if (command.Write && command.DataSegmentLength < command.ExpectedDataTransferLength)
            {
                uint transferTag = session.GetNextTransferTag();

                // Store segment (we only execute the command after receiving all of its data)
                byte[] commandData = new byte[command.ExpectedDataTransferLength];
                Array.Copy(command.Data, 0, commandData, 0, command.DataSegmentLength);
                connection.Transfers.Add(transferTag, new KeyValuePair<byte[], uint>(command.CommandDescriptorBlock, command.ExpectedDataTransferLength));
                connection.TransferData.Add(transferTag, commandData);

                // Send R2T
                ReadyToTransferPDU response = new ReadyToTransferPDU();
                response.InitiatorTaskTag = command.InitiatorTaskTag;
                response.R2TSN = 0; // R2Ts are sequenced per command and must start with 0 for each new command;
                response.TargetTransferTag = transferTag;
                response.BufferOffset = command.DataSegmentLength;
                response.DesiredDataTransferLength = Math.Min((uint)connection.TargetMaxRecvDataSegmentLength, command.ExpectedDataTransferLength - response.BufferOffset);

                // We store the next R2TSN to be used
                session.NextR2TSN.Add(transferTag, 1);

                responseList.Add(response);
                return responseList;
            }

            byte[] scsiResponse;
            SCSIStatusCodeName status = target.ExecuteCommand(command.CommandDescriptorBlock, command.LUN, command.Data, out scsiResponse);
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

        internal static ISCSIPDU GetSCSIDataOutResponsePDU(SCSIDataOutPDU request, ISCSITarget target, SessionParameters session, ConnectionParameters connection)
        {
            string connectionIdentifier = StateObject.GetConnectionIdentifier(session, connection);
            if (connection.Transfers.ContainsKey(request.TargetTransferTag))
            {
                ushort LUN = (ushort)request.LUN;
                
                Disk disk = target.Disks[LUN];

                uint offset = request.BufferOffset;
                uint totalLength = connection.Transfers[request.TargetTransferTag].Value;

                // Store segment (we only execute the command after receiving all of its data)
                byte[] commandData = connection.TransferData[request.TargetTransferTag];
                Array.Copy(request.Data, 0, commandData, offset, request.DataSegmentLength);
                
                ISCSIServer.Log(String.Format("[{0}][GetSCSIDataOutResponsePDU] Buffer offset: {1}, Total length: {2}", connectionIdentifier, offset, totalLength));

                if (offset + request.DataSegmentLength == totalLength)
                {
                    // Last Data-out PDU
                    ISCSIServer.Log("[{0}][GetSCSIDataOutResponsePDU] Last Data-out PDU", connectionIdentifier);
                    
                    byte[] commandBytes = connection.Transfers[request.TargetTransferTag].Key;
                    byte[] scsiResponse;
                    SCSIStatusCodeName status = target.ExecuteCommand(commandBytes, request.LUN, commandData, out scsiResponse);
                    SCSIResponsePDU response = new SCSIResponsePDU();
                    response.InitiatorTaskTag = request.InitiatorTaskTag;
                    response.Status = status;
                    response.Data = scsiResponse;
                    connection.Transfers.Remove(request.TargetTransferTag);
                    connection.TransferData.Remove(request.TargetTransferTag);
                    session.NextR2TSN.Remove(request.TargetTransferTag);
                    return response;
                }
                else
                {
                    // Send R2T
                    ReadyToTransferPDU response = new ReadyToTransferPDU();
                    response.InitiatorTaskTag = request.InitiatorTaskTag;
                    response.TargetTransferTag = request.TargetTransferTag;
                    response.R2TSN = session.GetNextR2TSN(request.TargetTransferTag);
                    response.BufferOffset = offset + request.DataSegmentLength; // where we left off
                    response.DesiredDataTransferLength = Math.Min((uint)connection.TargetMaxRecvDataSegmentLength, totalLength - response.BufferOffset);
                    
                    return response;
                }
                
            }
            else
            {
                ISCSIServer.Log("[{0}][GetSCSIDataOutResponsePDU] Invalid TargetTransferTag", connectionIdentifier);
                RejectPDU reject = new RejectPDU();
                reject.InitiatorTaskTag = request.InitiatorTaskTag;
                reject.Reason = RejectReason.InvalidPDUField;
                reject.Data = ByteReader.ReadBytes(request.GetBytes(), 0, 48);
                return reject;
            }
        }

        public static void EnforceExpectedDataTransferLength(SCSIResponsePDU response, uint expectedDataTransferLength)
        {
            if (response.Data.Length > expectedDataTransferLength)
            {
                response.ResidualOverflow = true;
                response.ResidualCount = (uint)(expectedDataTransferLength - response.Data.Length);
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
                response.ResidualCount = (uint)(expectedDataTransferLength - response.Data.Length);
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
