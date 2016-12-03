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
            Log(Severity.Trace, "Entering ProcessPDU");
            
            if (!state.Session.IsFullFeaturePhase)
            {
                if (pdu is LoginRequestPDU)
                {
                    LoginRequestPDU request = (LoginRequestPDU)pdu;
                    Log(Severity.Verbose, "[{0}] Login Request, current stage: {1}, next stage: {2}, parameters: {3}", state.ConnectionIdentifier, request.CurrentStage, request.NextStage, KeyValuePairUtils.ToString(request.LoginParameters));
                    if (request.TSIH != 0)
                    {
                        // RFC 3720: A Login Request with a non-zero TSIH and a CID equal to that of an existing
                        // connection implies a logout of the connection followed by a Login
                        ConnectionState existingConnection = m_connectionManager.FindConnection(request.ISID, request.TSIH, request.CID);
                        if (existingConnection != null)
                        {
                            // Perform implicit logout
                            Log(Severity.Verbose, "[{0}] Initiating implicit logout", state.ConnectionIdentifier);
                            // Wait for pending I/O to complete.
                            existingConnection.RunningSCSICommands.WaitUntilZero();
                            SocketUtils.ReleaseSocket(existingConnection.ClientSocket);
                            existingConnection.SendQueue.Stop();
                            m_connectionManager.RemoveConnection(existingConnection);
                            Log(Severity.Verbose, "[{0}] Implicit logout completed", state.ConnectionIdentifier);
                        }
                    }
                    LoginResponsePDU response = GetLoginResponsePDU(request, state.Session, state.ConnectionParameters);
                    if (state.Session.IsFullFeaturePhase)
                    {
                        state.ConnectionParameters.CID = request.CID;
                        m_connectionManager.AddConnection(state);
                    }
                    Log(Severity.Verbose, "[{0}] Login Response parameters: {1}", state.ConnectionIdentifier, KeyValuePairUtils.ToString(response.LoginParameters));
                    state.SendQueue.Enqueue(response);
                }
                else
                {
                    // Before the Full Feature Phase is established, only Login Request and Login Response PDUs are allowed.
                    Log(Severity.Warning, "[{0}] Initiator error: Improper command during login phase, OpCode: 0x{1}", state.ConnectionIdentifier, pdu.OpCode.ToString("x"));
                    if (state.Session.TSIH == 0)
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
                    TextResponsePDU response;
                    lock (m_targets.Lock)
                    {
                        response = ServerResponseHelper.GetTextResponsePDU(request, m_targets.GetList());
                    }
                    state.SendQueue.Enqueue(response);
                }
                else if (pdu is LogoutRequestPDU)
                {
                    Log(Severity.Verbose, "[{0}] Logour Request", state.ConnectionIdentifier);
                    LogoutRequestPDU request = (LogoutRequestPDU)pdu;
                    if (state.Session.IsDiscovery && request.ReasonCode != LogoutReasonCode.CloseTheSession)
                    {
                        // RFC 3720: Discovery-session: The target MUST ONLY accept [..] logout request with the reason "close the session"
                        RejectPDU reject = new RejectPDU();
                        reject.Reason = RejectReason.ProtocolError;
                        reject.Data = ByteReader.ReadBytes(pdu.GetBytes(), 0, 48);
                        state.SendQueue.Enqueue(reject);
                    }
                    else
                    {
                        List<ConnectionState> connectionsToClose = new List<ConnectionState>();
                        if (request.ReasonCode == LogoutReasonCode.CloseTheSession)
                        {
                            connectionsToClose = m_connectionManager.GetSessionConnections(state.Session.ISID, state.Session.TSIH);
                        }
                        else
                        {
                            // RFC 3720: A Logout for a CID may be performed on a different transport connection when the TCP connection for the CID has already been terminated.
                            ConnectionState existingConnection = m_connectionManager.FindConnection(state.Session.ISID, state.Session.TSIH, request.CID);
                            if (existingConnection != null && existingConnection != state)
                            {
                                connectionsToClose.Add(existingConnection);
                            }
                            connectionsToClose.Add(state);
                        }

                        foreach (ConnectionState connection in connectionsToClose)
                        {
                            // Wait for pending I/O to complete.
                            connection.RunningSCSICommands.WaitUntilZero();
                            if (connection != state)
                            {
                                SocketUtils.ReleaseSocket(connection.ClientSocket);
                            }
                            m_connectionManager.RemoveConnection(connection);
                        }
                        LogoutResponsePDU response = ServerResponseHelper.GetLogoutResponsePDU(request);
                        state.SendQueue.Enqueue(response);
                        // connection will be closed after a LogoutResponsePDU has been sent.
                    }
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
                        Log(Severity.Debug, "[{0}] SCSIDataOutPDU: Target transfer tag: {1}, LUN: {2}, Buffer offset: {3}, Data segment length: {4}, DataSN: {5}, Final: {6}", state.ConnectionIdentifier, request.TargetTransferTag, (ushort)request.LUN, request.BufferOffset, request.DataSegmentLength, request.DataSN, request.Final);
                        try
                        {
                            readyToTransferPDUs = TargetResponseHelper.GetReadyToTransferPDUs(request, state.Target, state.Session, state.ConnectionParameters, out commandsToExecute);
                        }
                        catch (InvalidTargetTransferTagException ex)
                        {
                            Log(Severity.Warning, "[{0}] Initiator error: Invalid TargetTransferTag: {1}", state.ConnectionIdentifier, ex.TargetTransferTag);
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
                        Log(Severity.Debug, "[{0}] SCSICommandPDU: CmdSN: {1}, LUN: {2}, Data segment length: {3}, Expected Data Transfer Length: {4}, Final: {5}", state.ConnectionIdentifier, command.CmdSN, (ushort)command.LUN, command.DataSegmentLength, command.ExpectedDataTransferLength, command.Final);
                        readyToTransferPDUs = TargetResponseHelper.GetReadyToTransferPDUs(command, state.Target, state.Session, state.ConnectionParameters, out commandsToExecute);
                    }
                    foreach (ReadyToTransferPDU readyToTransferPDU in readyToTransferPDUs)
                    {
                        state.SendQueue.Enqueue(readyToTransferPDU);
                    }
                    if (commandsToExecute != null)
                    {
                        state.RunningSCSICommands.Add(commandsToExecute.Count);
                    }
                    foreach (SCSICommandPDU commandPDU in commandsToExecute)
                    {
                        Log(Severity.Debug, "[{0}] Queuing command: CmdSN: {1}", state.ConnectionIdentifier, commandPDU.CmdSN);
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
            Log(Severity.Trace, "Leaving ProcessPDU");
        }
    }
}
