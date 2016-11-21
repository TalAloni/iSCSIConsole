/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Utilities;

namespace ISCSI.Server
{
    public delegate ushort GetNextTSIH();

    public class ISCSIServer // Server may serve more than one target
    {
        public const int DefaultPort = 3260;

        // Offered Session Parameters:
        public static bool OfferedInitialR2T = true;
        public static bool OfferedImmediateData = true;
        public static int OfferedMaxBurstLength = SessionParameters.DefaultMaxBurstLength;
        public static int OfferedFirstBurstLength = SessionParameters.DefaultFirstBurstLength;
        public static int OfferedDefaultTime2Wait = 0;
        public static int OfferedDefaultTime2Retain = 20;
        public static int OfferedMaxOutstandingR2T = 1;
        public static bool OfferedDataPDUInOrder = true;
        public static bool OfferedDataSequenceInOrder = true;
        public static int OfferedErrorRecoveryLevel = 0;
        public static int OfferedMaxConnections = 1;

        private List<ISCSITarget> m_targets;
        private int m_port;
        private ushort m_nextTSIH = 1; // Next Target Session Identifying Handle

        private Socket m_listenerSocket;
        private bool m_listening;
        private static List<ConnectionState> m_activeConnections = new List<ConnectionState>();

        public static object m_logSyncLock = new object();
        private static FileStream m_logFile;
        
        public ISCSIServer(List<ISCSITarget> targets) : this(targets, DefaultPort)
        { }

        public ISCSIServer(List<ISCSITarget> targets, int port) : this(targets, port, String.Empty)
        { }

        /// <summary>
        /// Server needs to be started with Start()
        /// </summary>
        public ISCSIServer(List<ISCSITarget> targets, int port, string logFilePath)
        {
            m_port = port;
            m_targets = targets;

            if (logFilePath != String.Empty)
            {
                try
                {
                    // We must avoid using buffered writes, using it will negatively affect the performance and reliability.
                    // Note: once the file system write buffer is filled, Windows may delay any (buffer-dependent) pending write operations, which will create a deadlock.
                    m_logFile = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read, 0x1000, FileOptions.WriteThrough);
                }
                catch
                {
                    Console.WriteLine("Cannot open log file");
                }
            }
        }

        public void Start()
        {
            if (!m_listening)
            {
                m_listening = true;

                m_listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                m_listenerSocket.Bind(new IPEndPoint(IPAddress.Any, m_port));
                m_listenerSocket.Listen(1000);
                m_listenerSocket.BeginAccept(ConnectRequestCallback, m_listenerSocket);
            }
        }

        // This method Accepts new connections
        private void ConnectRequestCallback(IAsyncResult ar)
        {
            Socket listenerSocket = (Socket)ar.AsyncState;

            Socket clientSocket;
            try
            {
                clientSocket = listenerSocket.EndAccept(ar);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            Log("[OnConnectRequest] New connection has been accepted");

            ConnectionState state = new ConnectionState();
            state.ReceiveBuffer = new byte[ConnectionState.ReceiveBufferSize];
            // Disable the Nagle Algorithm for this tcp socket:
            clientSocket.NoDelay = true;
            state.ClientSocket = clientSocket;
            try
            {
                clientSocket.BeginReceive(state.ReceiveBuffer, 0, ConnectionState.ReceiveBufferSize, 0, ReceiveCallback, state);
            }
            catch (ObjectDisposedException)
            {
                Log("[OnConnectRequest] BeginReceive ObjectDisposedException");
            }
            catch (SocketException ex)
            {
                Log("[OnConnectRequest] BeginReceive SocketException: " + ex.Message);
            }
            m_listenerSocket.BeginAccept(ConnectRequestCallback, m_listenerSocket);
        }

        public void Stop()
        {
            m_listening = false;
            SocketUtils.ReleaseSocket(m_listenerSocket);

            if (m_logFile != null)
            {
                m_logFile.Close();
                m_logFile = null;
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            if (!m_listening)
            {
                return;
            }

            ConnectionState state = (ConnectionState)result.AsyncState;
            Socket clientSocket = state.ClientSocket;
            if (!clientSocket.Connected)
            {
                return;
            }

            int numberOfBytesReceived;
            try
            {
                numberOfBytesReceived = clientSocket.EndReceive(result);
            }
            catch (ObjectDisposedException)
            {
                Log("[ReceiveCallback] EndReceive ObjectDisposedException");
                return;
            }
            catch (SocketException ex)
            {
                Log("[ReceiveCallback] EndReceive SocketException: " + ex.Message);
                return;
            }

            if (numberOfBytesReceived == 0)
            {
                // The other side has closed the connection
                clientSocket.Close();
                Log("[ReceiveCallback] The initiator has closed the connection");
                // Wait for pending I/O to complete.
                state.RunningSCSICommands.WaitUntilZero();
                lock (m_activeConnections)
                {
                    int connectionIndex = GetConnectionStateIndex(m_activeConnections, state.SessionParameters.ISID, state.SessionParameters.TSIH, state.ConnectionParameters.CID);
                    if (connectionIndex >= 0)
                    {
                        m_activeConnections.RemoveAt(connectionIndex);
                    }
                }
                return;
            }

            byte[] currentBuffer = ByteReader.ReadBytes(state.ReceiveBuffer, 0, numberOfBytesReceived);
            ProcessCurrentBuffer(currentBuffer, state);

            try
            {
                clientSocket.BeginReceive(state.ReceiveBuffer, 0, ConnectionState.ReceiveBufferSize, 0, ReceiveCallback, state);
            }
            catch (ObjectDisposedException)
            {
                Log("[ReceiveCallback] BeginReceive ObjectDisposedException");
            }
            catch (SocketException ex)
            {
                Log("[ReceiveCallback] BeginReceive SocketException: " + ex.Message);
            }
        }

        private void ProcessCurrentBuffer(byte[] currentBuffer, ConnectionState state)
        {
            Socket clientSocket = state.ClientSocket;

            if (state.ConnectionBuffer.Length == 0)
            {
                state.ConnectionBuffer = currentBuffer;
            }
            else
            {
                state.ConnectionBuffer = ByteUtils.Concatenate(state.ConnectionBuffer, currentBuffer);
            }

            // we now have all PDU bytes received so far in state.ConnectionBuffer
            int bytesLeftInBuffer = state.ConnectionBuffer.Length;

            while (bytesLeftInBuffer >= 8)
            {
                int bufferOffset = state.ConnectionBuffer.Length - bytesLeftInBuffer;
                int pduLength = ISCSIPDU.GetPDULength(state.ConnectionBuffer, bufferOffset);
                if (pduLength > bytesLeftInBuffer)
                {
                    Log("[{0}][ProcessCurrentBuffer] Bytes left in receive buffer: {1}", state.ConnectionIdentifier, bytesLeftInBuffer);
                    break;
                }
                else
                {
                    byte[] pduBytes = ByteReader.ReadBytes(state.ConnectionBuffer, bufferOffset, pduLength);
                    bytesLeftInBuffer -= pduLength;
                    ISCSIPDU pdu = null;
                    try
                    {
                        pdu = ISCSIPDU.GetPDU(pduBytes);
                    }
                    catch (Exception ex)
                    {
                        Log("[{0}][ProcessCurrentBuffer] Failed to read PDU (Exception: {1})", state.ConnectionIdentifier, ex.Message);
                        RejectPDU reject = new RejectPDU();
                        reject.Reason = RejectReason.InvalidPDUField;
                        reject.Data = ByteReader.ReadBytes(pduBytes, 0, 48);

                        TrySendPDU(state, reject);
                    }

                    if (pdu != null)
                    {
                        if (pdu.GetType() == typeof(ISCSIPDU))
                        {
                            Log("[{0}][ProcessCurrentBuffer] Unsupported PDU (0x{1})", state.ConnectionIdentifier, pdu.OpCode.ToString("X"));
                            // Unsupported PDU
                            RejectPDU reject = new RejectPDU();
                            reject.InitiatorTaskTag = pdu.InitiatorTaskTag;
                            reject.Reason = RejectReason.CommandNotSupported;
                            reject.Data = ByteReader.ReadBytes(pduBytes, 0, 48);
                            TrySendPDU(state, reject);
                        }
                        else
                        {
                            ProcessPDU(pdu, state);
                        }
                    }
                    
                    if (!clientSocket.Connected)
                    {
                        // Do not continue to process the buffer if the other side closed the connection
                        if (bytesLeftInBuffer > 0)
                        {
                            Log("[{0}][ProcessCurrentBuffer] Buffer processing aborted, bytes left in receive buffer: {1}", state.ConnectionIdentifier, bytesLeftInBuffer);
                        }
                        return;
                    }
                }
            }

            if (bytesLeftInBuffer > 0)
            {
                state.ConnectionBuffer = ByteReader.ReadBytes(state.ConnectionBuffer, state.ConnectionBuffer.Length - bytesLeftInBuffer, bytesLeftInBuffer);
            }
            else
            {
                state.ConnectionBuffer = new byte[0];
            }
        }

        private void ProcessPDU(ISCSIPDU pdu, ConnectionState state)
        {
            Socket clientSocket = state.ClientSocket;
            
            uint? cmdSN = PDUHelper.GetCmdSN(pdu);
            Log("[{0}][ProcessPDU] Received PDU from initiator, Operation: {1}, Size: {2}, CmdSN: {3}", state.ConnectionIdentifier, (ISCSIOpCodeName)pdu.OpCode, pdu.Length, cmdSN);
            // RFC 3720: On any connection, the iSCSI initiator MUST send the commands in increasing order of CmdSN,
            // except for commands that are retransmitted due to digest error recovery and connection recovery.
            if (cmdSN.HasValue)
            {
                if (state.SessionParameters.CommandNumberingStarted)
                {
                    if (cmdSN != state.SessionParameters.ExpCmdSN)
                    {
                        Log("[{0}][ProcessPDU] CmdSN outside of expected range", state.ConnectionIdentifier);
                        // We ignore this PDU
                        return;
                    }
                }
                else
                {
                    state.SessionParameters.ExpCmdSN = cmdSN.Value;
                    state.SessionParameters.CommandNumberingStarted = true;
                }

                if (pdu is LogoutRequestPDU || pdu is TextRequestPDU || pdu is SCSICommandPDU || pdu is RejectPDU)
                {
                    if (!pdu.ImmediateDelivery)
                    {
                        state.SessionParameters.ExpCmdSN++;
                    }
                }
            }

            if (!state.SessionParameters.IsFullFeaturePhase)
            {
                if (pdu is LoginRequestPDU)
                {
                    LoginRequestPDU request = (LoginRequestPDU)pdu;
                    Log("[{0}][ReceiveCallback] Login Request, current stage: {1}, next stage: {2}, parameters: {3}", state.ConnectionIdentifier, request.CurrentStage, request.NextStage, KeyValuePairUtils.ToString(request.LoginParameters));
                    if (request.TSIH != 0)
                    {
                        // RFC 3720: A Login Request with a non-zero TSIH and a CID equal to that of an existing
                        // connection implies a logout of the connection followed by a Login
                        lock (m_activeConnections)
                        {
                            int existingConnectionIndex = GetConnectionStateIndex(m_activeConnections, request.ISID, request.TSIH, request.CID);
                            if (existingConnectionIndex >= 0)
                            {
                                // Perform implicit logout
                                Log("[{0}][ProcessPDU] Initiating implicit logout", state.ConnectionIdentifier);
                                ConnectionState existingConnection = m_activeConnections[existingConnectionIndex];
                                // Wait for pending I/O to complete.
                                existingConnection.RunningSCSICommands.WaitUntilZero();
                                SocketUtils.ReleaseSocket(existingConnection.ClientSocket);
                                m_activeConnections.RemoveAt(existingConnectionIndex);
                                Log("[{0}][ProcessPDU] Implicit logout completed", state.ConnectionIdentifier);
                            }
                        }
                    }
                    LoginResponsePDU response = ServerResponseHelper.GetLoginResponsePDU(request, m_targets, state.SessionParameters, state.ConnectionParameters, ref state.Target, GetNextTSIH);
                    if (state.SessionParameters.IsFullFeaturePhase)
                    {
                        state.SessionParameters.ISID = request.ISID;
                        state.ConnectionParameters.CID = request.CID;
                        lock (m_activeConnections)
                        {
                            m_activeConnections.Add(state);
                        }
                    }
                    Log("[{0}][ReceiveCallback] Login Response parameters: {1}", state.ConnectionIdentifier, KeyValuePairUtils.ToString(response.LoginParameters));
                    TrySendPDU(state, response);
                }
                else
                {
                    // Before the Full Feature Phase is established, only Login Request and Login Response PDUs are allowed.
                    Log("[{0}][ProcessPDU] Improper command during login phase, OpCode: 0x{1}", state.ConnectionIdentifier, pdu.OpCode.ToString("x"));
                    if (state.SessionParameters.TSIH == 0)
                    {
                        // A target receiving any PDU except a Login request before the Login phase is started MUST
                        // immediately terminate the connection on which the PDU was received.
                        clientSocket.Close();
                    }
                    else
                    {
                        // Once the Login phase has started, if the target receives any PDU except a Login request,
                        // it MUST send a Login reject (with Status "invalid during login") and then disconnect.
                        LoginResponsePDU loginResponse = new LoginResponsePDU();
                        loginResponse.TSIH = state.SessionParameters.TSIH;
                        loginResponse.Status = LoginResponseStatusName.InvalidDuringLogon;
                        TrySendPDU(state, loginResponse);
                        clientSocket.Close();
                    }
                }
            }
            else // Logged in
            {
                if (pdu is TextRequestPDU)
                {
                    TextRequestPDU request = (TextRequestPDU)pdu;
                    TextResponsePDU response = ServerResponseHelper.GetTextResponsePDU(request, m_targets);
                    TrySendPDU(state, response);
                }
                else if (pdu is LogoutRequestPDU)
                {
                    lock (m_activeConnections)
                    {
                        int connectionIndex = GetConnectionStateIndex(m_activeConnections, state.SessionParameters.ISID, state.SessionParameters.TSIH, state.ConnectionParameters.CID);
                        if (connectionIndex >= 0)
                        {
                            ConnectionState existingConnection = m_activeConnections[connectionIndex];
                            // RFC 3720: A Logout for a CID may be performed on a different transport connection when the TCP connection for the CID has already been terminated.
                            if (existingConnection != state)
                            {
                                // Wait for pending I/O to complete.
                                existingConnection.RunningSCSICommands.WaitUntilZero();
                            }
                            m_activeConnections.RemoveAt(connectionIndex);
                        }
                    }
                    // Wait for pending I/O to complete.
                    state.RunningSCSICommands.WaitUntilZero();
                    LogoutRequestPDU request = (LogoutRequestPDU)pdu;
                    LogoutResponsePDU response = ServerResponseHelper.GetLogoutResponsePDU(request);
                    TrySendPDU(state, response);
                    clientSocket.Close(); // We can close the connection now
                }
                else if (state.SessionParameters.IsDiscovery)
                {
                    // The target MUST ONLY accept text requests with the SendTargets key and a logout
                    // request with the reason "close the session".  All other requests MUST be rejected.
                    Log("[{0}][ProcessPDU] Improper command during discovery session, OpCode: 0x{1}", state.ConnectionIdentifier, pdu.OpCode.ToString("x"));
                    RejectPDU reject = new RejectPDU();
                    reject.Reason = RejectReason.ProtocolError;
                    reject.Data = ByteReader.ReadBytes(pdu.GetBytes(), 0, 48);

                    TrySendPDU(state, reject);
                }
                else if (pdu is NOPOutPDU)
                {
                    NOPOutPDU request = (NOPOutPDU)pdu;
                    if (request.InitiatorTaskTag != 0xFFFFFFFF)
                    {
                        NOPInPDU response = ServerResponseHelper.GetNOPResponsePDU(request);
                        TrySendPDU(state, response);
                    }
                }
                else if (pdu is SCSIDataOutPDU || pdu is SCSICommandPDU)
                {
                    // RFC 3720: the iSCSI target layer MUST deliver the commands for execution (to the SCSI execution engine) in the order specified by CmdSN.
                    // e.g. read requests should not be executed while previous write request data is being received (via R2T)
                    List<SCSICommandPDU> commandsToExecute;
                    List<ISCSIPDU> responseList;
                    if (pdu is SCSIDataOutPDU)
                    {
                        SCSIDataOutPDU request = (SCSIDataOutPDU)pdu;
                        ISCSIServer.Log("[{0}][ProcessPDU] SCSIDataOutPDU: Target transfer tag: {1}, LUN: {2}, Buffer offset: {3}, Data segment length: {4}, DataSN: {5}, Final: {6}", state.ConnectionIdentifier, request.TargetTransferTag, (ushort)request.LUN, request.BufferOffset, request.DataSegmentLength, request.DataSN, request.Final);
                        responseList = TargetResponseHelper.GetReadyToTransferPDUs(request, state.Target, state.SessionParameters, state.ConnectionParameters, out commandsToExecute);
                    }
                    else
                    {
                        SCSICommandPDU command = (SCSICommandPDU)pdu;
                        ISCSIServer.Log("[{0}][ProcessPDU] SCSICommandPDU: CmdSN: {1}, LUN: {2}, Data segment length: {3}, Expected Data Transfer Length: {4}, Final: {5}", state.ConnectionIdentifier, command.CmdSN, (ushort)command.LUN, command.DataSegmentLength, command.ExpectedDataTransferLength, command.Final);
                        responseList = TargetResponseHelper.GetReadyToTransferPDUs(command, state.Target, state.SessionParameters, state.ConnectionParameters, out commandsToExecute);
                    }

                    state.RunningSCSICommands.Add(commandsToExecute.Count);
                    foreach(SCSICommandPDU command in commandsToExecute)
                    {
                        List<ISCSIPDU> commandResponseList = TargetResponseHelper.GetSCSICommandResponse(command, state.Target, state.SessionParameters, state.ConnectionParameters);
                        state.RunningSCSICommands.Decrement();
                        responseList.AddRange(commandResponseList);
                    }

                    foreach (ISCSIPDU response in responseList)
                    {
                        TrySendPDU(state, response);
                        if (!clientSocket.Connected)
                        {
                            return;
                        }
                    }
                }
                else if (pdu is LoginRequestPDU)
                {
                    Log("[{0}][ProcessPDU] Protocol Error (Login request during full feature phase)", state.ConnectionIdentifier);
                    // RFC 3720: Login requests and responses MUST be used exclusively during Login.
                    // On any connection, the login phase MUST immediately follow TCP connection establishment and
                    // a subsequent Login Phase MUST NOT occur before tearing down a connection
                    RejectPDU reject = new RejectPDU();
                    reject.Reason = RejectReason.ProtocolError;
                    reject.Data = ByteReader.ReadBytes(pdu.GetBytes(), 0, 48);

                    TrySendPDU(state, reject);
                }
                else
                {
                    Log("[{0}][ProcessPDU] Unsupported command, OpCode: 0x{1}", state.ConnectionIdentifier, pdu.OpCode.ToString("x"));
                    RejectPDU reject = new RejectPDU();
                    reject.Reason = RejectReason.CommandNotSupported;
                    reject.Data = ByteReader.ReadBytes(pdu.GetBytes(), 0, 48);

                    TrySendPDU(state, reject);
                }
            }
        }

        private static int GetConnectionStateIndex(List<ConnectionState> Connections, ulong isid, ushort tsih, ushort cid)
        {
            for (int index = 0; index < Connections.Count; index++)
            {
                if (Connections[index].SessionParameters.ISID == isid &&
                    Connections[index].SessionParameters.TSIH == tsih &&
                    Connections[index].ConnectionParameters.CID == cid)
                {
                    return index;
                }
            }
            return -1;
        }

        private static void TrySendPDU(ConnectionState state, ISCSIPDU response)
        {
            Socket clientSocket = state.ClientSocket;
            try
            {
                PDUHelper.SetStatSN(response, state.ConnectionParameters.StatSN);
                PDUHelper.SetExpCmdSN(response, state.SessionParameters.ExpCmdSN, state.SessionParameters.ExpCmdSN + state.SessionParameters.CommandQueueSize);
                if (response is SCSIResponsePDU ||
                    response is LoginResponsePDU ||
                    response is TextResponsePDU ||
                    (response is SCSIDataInPDU && ((SCSIDataInPDU)response).StatusPresent) ||
                    response is RejectPDU)
                {
                    state.ConnectionParameters.StatSN++;
                }
                clientSocket.Send(response.GetBytes());
                Log("[{0}][TrySendPDU] Sent response to initator, Operation: {1}, Size: {2}", state.ConnectionIdentifier, response.OpCode, response.Length);
            }
            catch (SocketException ex)
            {
                Log("[{0}][TrySendPDU] Failed to send response to initator (Operation: {1}, Size: {2}), SocketException: {3}", state.ConnectionIdentifier, response.OpCode, response.Length, ex.Message);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public ushort GetNextTSIH()
        {
            // The iSCSI Target selects a non-zero value for the TSIH at
            // session creation (when an initiator presents a 0 value at Login).
            // After being selected, the same TSIH value MUST be used whenever the
            // initiator or target refers to the session and a TSIH is required
            ushort nextTSIH = m_nextTSIH;
            m_nextTSIH++;
            if (m_nextTSIH == 0)
            {
                m_nextTSIH++;
            }
            return nextTSIH;
        }

        public static void Log(string message)
        {
            if (m_logFile != null)
            {
                lock (m_logSyncLock)
                {
                    StreamWriter writer = new StreamWriter(m_logFile);
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ");
                    writer.WriteLine(timestamp + message);
                    writer.Flush();
                }
            }
        }

        public static void Log(string message, params object[] args)
        {
            Log(String.Format(message, args));
        }
    }
}
