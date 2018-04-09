/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Utilities;

namespace ISCSI.Server
{
    public partial class ISCSIServer
    {
        private bool ValidateCommandNumbering(ISCSIPDU pdu, ConnectionState state)
        {
            if (state.Session == null)
            {
                return true;
            }

            uint? cmdSN = PDUHelper.GetCmdSN(pdu);
            Log(Severity.Verbose, "[{0}] Received PDU from initiator, Operation: {1}, Size: {2}, CmdSN: {3}", state.ConnectionIdentifier, (ISCSIOpCodeName)pdu.OpCode, pdu.Length, cmdSN);
            // RFC 3720: On any connection, the iSCSI initiator MUST send the commands in increasing order of CmdSN,
            // except for commands that are retransmitted due to digest error recovery and connection recovery.
            if (cmdSN.HasValue)
            {
                if (state.Session.CommandNumberingStarted)
                {
                    if (cmdSN != state.Session.ExpCmdSN)
                    {
                        return false;
                    }
                }
                else
                {
                    state.Session.ExpCmdSN = cmdSN.Value;
                    state.Session.CommandNumberingStarted = true;
                }

                if (pdu is LogoutRequestPDU || pdu is TextRequestPDU || pdu is SCSICommandPDU || pdu is RejectPDU)
                {
                    if (!pdu.ImmediateDelivery)
                    {
                        state.Session.ExpCmdSN++;
                    }
                }
            }
            return true;
        }

        private void ProcessPDU(ISCSIPDU pdu, ConnectionState state)
        {
            LogTrace("Entering ProcessPDU");
            
            if (state.Session == null || !state.Session.IsFullFeaturePhase)
            {
                if (pdu is LoginRequestPDU)
                {
                    LoginRequestPDU request = (LoginRequestPDU)pdu;
                    string loginIdentifier = String.Format("ISID={0},TSIH={1},CID={2}", request.ISID.ToString("x"), request.TSIH.ToString("x"), request.CID.ToString("x"));
                    Log(Severity.Verbose, "[{0}] Login Request, current stage: {1}, next stage: {2}, parameters: {3}", loginIdentifier, request.CurrentStage, request.NextStage, FormatNullDelimitedText(request.LoginParametersText));
                    LoginResponsePDU response = GetLoginResponsePDU(request, state.ConnectionParameters);
                    if (state.Session != null && state.Session.IsFullFeaturePhase)
                    {
                        m_connectionManager.AddConnection(state);
                    }
                    Log(Severity.Verbose, "[{0}] Login Response parameters: {1}", state.ConnectionIdentifier, FormatNullDelimitedText(response.LoginParametersText));
                    state.SendQueue.Enqueue(response);
                }
                else
                {
                    // Before the Full Feature Phase is established, only Login Request and Login Response PDUs are allowed.
                    Log(Severity.Warning, "[{0}] Initiator error: Improper command during login phase, OpCode: 0x{1}", state.ConnectionIdentifier, pdu.OpCode.ToString("x"));
                    if (state.Session == null)
                    {
                        // A target receiving any PDU except a Login request before the Login phase is started MUST
                        // immediately terminate the connection on which the PDU was received.
                        state.ClientSocket.Close();
                    }
                    else
                    {
                        // Once the Login phase has started, if the target receives any PDU except a Login request,
                        // it MUST send a Login reject (with Status "invalid during login") and then disconnect.
                        LoginResponsePDU loginResponse = new LoginResponsePDU();
                        loginResponse.TSIH = state.Session.TSIH;
                        loginResponse.Status = LoginResponseStatusName.InvalidDuringLogon;
                        state.SendQueue.Enqueue(loginResponse);
                    }
                }
            }
            else // Logged in
            {
                if (pdu is TextRequestPDU)
                {
                    TextRequestPDU request = (TextRequestPDU)pdu;
                    TextResponsePDU response = GetTextResponsePDU(request, state.ConnectionParameters);
                    state.SendQueue.Enqueue(response);
                }
                else if (pdu is LogoutRequestPDU)
                {
                    LogoutRequestPDU request = (LogoutRequestPDU)pdu;
                    ISCSIPDU response = GetLogoutResponsePDU(request, state.ConnectionParameters);
                    state.SendQueue.Enqueue(response);
                }
                else if (state.Session.IsDiscovery)
                {
                    // The target MUST ONLY accept text requests with the SendTargets key and a logout
                    // request with the reason "close the session".  All other requests MUST be rejected.
                    Log(Severity.Warning, "[{0}] Initiator error: Improper command during discovery session, OpCode: 0x{1}", state.ConnectionIdentifier, pdu.OpCode.ToString("x"));
                    RejectPDU reject = new RejectPDU();
                    reject.Reason = RejectReason.ProtocolError;
                    reject.Data = ByteReader.ReadBytes(pdu.GetBytes(), 0, 48);

                    state.SendQueue.Enqueue(reject);
                }
                else if (pdu is NOPOutPDU)
                {
                    NOPOutPDU request = (NOPOutPDU)pdu;
                    if (request.InitiatorTaskTag != 0xFFFFFFFF)
                    {
                        NOPInPDU response = ServerResponseHelper.GetNOPResponsePDU(request);
                        state.SendQueue.Enqueue(response);
                    }
                }
                else if (pdu is SCSIDataOutPDU || pdu is SCSICommandPDU)
                {
                    // RFC 3720: the iSCSI target layer MUST deliver the commands for execution (to the SCSI execution engine) in the order specified by CmdSN.
                    // e.g. read requests should not be executed while previous write request data is being received (via R2T)
                    List<SCSICommandPDU> commandsToExecute = null;
                    List<ReadyToTransferPDU> readyToTransferPDUs = new List<ReadyToTransferPDU>();
                    if (pdu is SCSIDataOutPDU)
                    {
                        SCSIDataOutPDU request = (SCSIDataOutPDU)pdu;
                        Log(Severity.Debug, "[{0}] SCSIDataOutPDU: InitiatorTaskTag: {1}, TargetTransferTag: {2}, LUN: {3}, BufferOffset: {4}, DataSegmentLength: {5}, DataSN: {6}, Final: {7}", state.ConnectionIdentifier, request.InitiatorTaskTag.ToString("x"), request.TargetTransferTag.ToString("x"), (ushort)request.LUN, request.BufferOffset, request.DataSegmentLength, request.DataSN, request.Final);
                        try
                        {
                            readyToTransferPDUs = TargetResponseHelper.GetReadyToTransferPDUs(request, state.ConnectionParameters, out commandsToExecute);
                        }
                        catch (InvalidTargetTransferTagException ex)
                        {
                            Log(Severity.Warning, "[{0}] Initiator error: Invalid TargetTransferTag: {1}", state.ConnectionIdentifier, ex.TargetTransferTag.ToString("x"));
                            RejectPDU reject = new RejectPDU();
                            reject.InitiatorTaskTag = request.InitiatorTaskTag;
                            reject.Reason = RejectReason.InvalidPDUField;
                            reject.Data = ByteReader.ReadBytes(request.GetBytes(), 0, 48);
                            state.SendQueue.Enqueue(reject);
                        }
                    }
                    else
                    {
                        SCSICommandPDU command = (SCSICommandPDU)pdu;
                        Log(Severity.Debug, "[{0}] SCSICommandPDU: CmdSN: {1}, LUN: {2}, InitiatorTaskTag: {3}, DataSegmentLength: {4}, ExpectedDataTransferLength: {5}, Final: {6}", state.ConnectionIdentifier, command.CmdSN, (ushort)command.LUN, command.InitiatorTaskTag.ToString("x"), command.DataSegmentLength, command.ExpectedDataTransferLength, command.Final);
                        readyToTransferPDUs = TargetResponseHelper.GetReadyToTransferPDUs(command, state.ConnectionParameters, out commandsToExecute);
                    }
                    foreach (ReadyToTransferPDU readyToTransferPDU in readyToTransferPDUs)
                    {
                        state.SendQueue.Enqueue(readyToTransferPDU);
                        Log(Severity.Debug, "[{0}] Enqueued R2T, InitiatorTaskTag: {1}, TargetTransferTag: {2}, BufferOffset: {3}, DesiredDataTransferLength: {4}, R2TSN: {5}", state.ConnectionIdentifier, readyToTransferPDU.InitiatorTaskTag.ToString("x"), readyToTransferPDU.TargetTransferTag.ToString("x"), readyToTransferPDU.BufferOffset, readyToTransferPDU.DesiredDataTransferLength, readyToTransferPDU.R2TSN);
                    }
                    if (commandsToExecute != null)
                    {
                        state.RunningSCSICommands.Add(commandsToExecute.Count);
                    }
                    foreach (SCSICommandPDU commandPDU in commandsToExecute)
                    {
                        Log(Severity.Debug, "[{0}] Queuing command: CmdSN: {1}, InitiatorTaskTag: {2}", state.ConnectionIdentifier, commandPDU.CmdSN, commandPDU.InitiatorTaskTag.ToString("x"));
                        state.Target.QueueCommand(commandPDU.CommandDescriptorBlock, commandPDU.LUN, commandPDU.Data, commandPDU, state.OnCommandCompleted);
                    }
                }
                else if (pdu is LoginRequestPDU)
                {
                    Log(Severity.Warning, "[{0}] Initiator Error: Login request during full feature phase", state.ConnectionIdentifier);
                    // RFC 3720: Login requests and responses MUST be used exclusively during Login.
                    // On any connection, the login phase MUST immediately follow TCP connection establishment and
                    // a subsequent Login Phase MUST NOT occur before tearing down a connection
                    RejectPDU reject = new RejectPDU();
                    reject.Reason = RejectReason.ProtocolError;
                    reject.Data = ByteReader.ReadBytes(pdu.GetBytes(), 0, 48);

                    state.SendQueue.Enqueue(reject);
                }
                else
                {
                    Log(Severity.Error, "[{0}] Unsupported command, OpCode: 0x{1}", state.ConnectionIdentifier, pdu.OpCode.ToString("x"));
                    RejectPDU reject = new RejectPDU();
                    reject.Reason = RejectReason.CommandNotSupported;
                    reject.Data = ByteReader.ReadBytes(pdu.GetBytes(), 0, 48);

                    state.SendQueue.Enqueue(reject);
                }
            }
            LogTrace("Leaving ProcessPDU");
        }

        private static string FormatNullDelimitedText(string text)
        {
            string result = String.Join(", ", text.Split('\0'));
            if (result.EndsWith(", "))
            {
                result = result.Remove(result.Length - 2);
            }
            return result;
        }
    }
}
