/* Copyright (C) 2012-2015 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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

namespace ISCSI
{
    public class ISCSIServer // Server may serve more than one target
    {
        // We use MaxCmdSN = ExpCmdSN + CommandQueueSize, so CommandQueueSize = 0 means the initiator can send one command at a time (i.e. there won't be any queue following the currently processed command)
        public static uint CommandQueueSize = 64; // over a low-latency connection, most of the gain comes from increasing the queue size from 0 to 1
        public const int DefaultPort = 3260;

        /// <summary>
        /// scope: session, allow the initiator to start sending data to a target as if it has received an initial R2T,
        /// Microsoft iSCSI Target support InitialR2T = No
        /// </summary>
        public static readonly bool InitialR2T = true;

        public static readonly bool ImmediateData = true;
        public static object m_logSyncLock = new object();

        /// <summary>
        /// The default MaxRecvDataSegmentLength is used during Login
        /// </summary>
        public const int DefaultMaxRecvDataSegmentLength = 8192;
        public const int DefaultMaxBurstLength = 262144;
        public const int DefaultFirstBurstLength = 65536;
        
        /// <summary>
        /// scope: connection (a per connection and per direction parameter that the target or initator declares),
        /// maximum data segment length that the target (or initator) can receive in a single iSCSI PDU.
        /// </summary>
        public static uint MaxRecvDataSegmentLength = 262144;

        /// <summary>
        /// scope: session, the total of all the DataSegmentLength of all PDUs in a sequence MUST not exceed MaxBurstLength.
        /// </summary>
        public static uint OfferedMaxBurstLength = DefaultMaxBurstLength;

        /// <summary>
        /// scope: session, the total of all the DataSegmentLength of all PDUs in a sequence MUST not exceed FirstBurstLength for unsolicited data.
        /// </summary>
        public static uint OfferedFirstBurstLength = DefaultFirstBurstLength;

        /// <summary>
        /// scope: session, minimum time, in seconds, to wait before attempting an explicit/implicit logout after connection termination / reset.
        /// </summary>
        public const int DefaultTime2Wait = 0;

        /// <summary>
        /// scope: session, maximum time, in seconds after an initial wait (Time2Wait), before which an active task reassignment
        /// is still possible after an unexpected connection termination or a connection reset.
        /// </summary>
        public const int DefaultTime2Retain = 20;

        public const int MaxOutstandingR2T = 1;             // scope: session
        public static readonly bool DataPDUInOrder = true;  // scope: session
        public static readonly bool DataSequenceInOrder = true; // scope: session

        public const int ErrorRecoveryLevel = 0;            // scope: session
        /// <summary>
        /// scope: session, the maximum number of connections per session.
        /// </summary>
        public const int MaxConnections = 1;

        private ushort m_nextTSIH = 1; // Next Target Session Identifying Handle
        private List<ISCSITarget> m_targets;
        private int m_port;
        private static FileStream m_logFile;
        private static bool m_enableDiskIOLogging = true;
        
        private Socket m_listenerSocket;
        private bool m_listening;
        private static object m_activeConnectionsLock = new object();
        private static List<StateObject> m_activeConnections = new List<StateObject>();
        
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
                    // We must avoid using the file system cache for writing, using it will negatively affect the performance and reliability.
                    // Note: once the file system cache is filled, Windows may delay any (cache-dependent) pending write operations, which will create a deadlock.
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

            StateObject state = new StateObject();
            state.Connection.SessionParameters.IsDiscovery = true;
            state.ReceiveBuffer = new byte[StateObject.ReceiveBufferSize];
            // Disable the Nagle Algorithm for this tcp socket:
            clientSocket.NoDelay = true;
            state.ClientSocket = clientSocket;
            try
            {
                clientSocket.BeginReceive(state.ReceiveBuffer, 0, StateObject.ReceiveBufferSize, 0, ReceiveCallback, state);
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

            StateObject state = (StateObject)result.AsyncState;
            Socket clientSocket = state.ClientSocket;
            byte[] receiveBuffer = state.ReceiveBuffer;

            int bytesReceived;

            try
            {
                bytesReceived = clientSocket.EndReceive(result);
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

            if (bytesReceived == 0)
            {
                // The other side has closed the connection
                clientSocket.Close();
                Log("[ReceiveCallback] The initiator has closed the connection");
                lock (m_activeConnectionsLock)
                {
                    int connectionIndex = GetStateObjectIndex(m_activeConnections, state.Connection.SessionParameters.ISID, state.Connection.SessionParameters.TSIH, state.Connection.ConnectionParameters.CID);
                    if (connectionIndex >= 0)
                    {
                        lock (m_activeConnections[connectionIndex].Connection.WriteLock)
                        {
                            // Wait for pending I/O to complete.
                        }
                        m_activeConnections.RemoveAt(connectionIndex);
                    }
                }
                return;
            }

            Log("[{0}][ReceiveCallback] Received {1} bytes", state.Connection.Identifier, bytesReceived);
            byte[] currentBuffer = new byte[bytesReceived];
            Array.Copy(receiveBuffer, currentBuffer, bytesReceived);
            
            ProcessCurrentBuffer(currentBuffer, state);

            if (clientSocket.Connected)
            {
                try
                {
                    clientSocket.BeginReceive(state.ReceiveBuffer, 0, StateObject.ReceiveBufferSize, 0, ReceiveCallback, state);
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
        }

        public void ProcessCurrentBuffer(byte[] currentBuffer, StateObject state)
        {
            Socket clientSocket = state.ClientSocket;

            if (state.ConnectionBuffer.Length == 0)
            {
                state.ConnectionBuffer = currentBuffer;
            }
            else
            {
                byte[] oldConnectionBuffer = state.ConnectionBuffer;
                state.ConnectionBuffer = new byte[oldConnectionBuffer.Length + currentBuffer.Length];
                Array.Copy(oldConnectionBuffer, state.ConnectionBuffer, oldConnectionBuffer.Length);
                Array.Copy(currentBuffer, 0, state.ConnectionBuffer, oldConnectionBuffer.Length, currentBuffer.Length);
            }

            // we now have all PDU bytes received so far in state.ConnectionBuffer
            int bytesLeftInBuffer = state.ConnectionBuffer.Length;

            while (bytesLeftInBuffer >= 8)
            {
                int bufferOffset = state.ConnectionBuffer.Length - bytesLeftInBuffer;
                int pduLength = ISCSIPDU.GetPDULength(state.ConnectionBuffer, bufferOffset);
                if (pduLength > bytesLeftInBuffer)
                {
                    Log("[{0}][ProcessCurrentBuffer] Bytes left in receive buffer: {1}", state.Connection.Identifier, bytesLeftInBuffer);
                    break;
                }
                else
                {
                    Log("[{0}][ProcessCurrentBuffer] PDU is being processed, Length: {1}", state.Connection.Identifier, pduLength);
                    byte[] pduBytes = new byte[pduLength];
                    Array.Copy(state.ConnectionBuffer, bufferOffset, pduBytes, 0, pduLength);
                    bytesLeftInBuffer -= pduLength;
                    ISCSIPDU pdu = null;
                    try
                    {
                        pdu = ISCSIPDU.GetPDU(pduBytes);
                    }
                    catch (UnsupportedSCSICommandException)
                    {
                        SCSICommandPDU command = new SCSICommandPDU(pduBytes, false);
                        Log("[{0}][ProcessCurrentBuffer] Unsupported SCSI Command (0x{1})", state.Connection.Identifier, command.OpCodeSpecific[12].ToString("X"));
                        SCSIResponsePDU response = new SCSIResponsePDU();
                        TargetResponseHelper.PrepareSCSIResponsePDU(response, command, state.Connection);
                        response.Status = SCSIStatusCodeName.CheckCondition;
                        response.Data = TargetResponseHelper.FormatSenseData(SenseDataParameter.GetIllegalRequestUnsupportedCommandCodeSenseData());
                        TrySendPDU(state, response);
                    }
                    catch (Exception ex)
                    {
                        Log("[{0}][ProcessCurrentBuffer] Failed to read PDU (Exception: {1})", state.Connection.Identifier, ex.Message);
                        RejectPDU reject = new RejectPDU();
                        reject.Reason = RejectReason.InvalidPDUField;
                        reject.StatSN = state.Connection.StatSN;
                        reject.ExpCmdSN = state.Connection.ExpCmdSN;
                        reject.MaxCmdSN = state.Connection.ExpCmdSN + ISCSIServer.CommandQueueSize;
                        reject.Data = ByteReader.ReadBytes(pduBytes, 0, 48);

                        // StatSN is advanced after a Reject
                        state.Connection.StatSN++;

                        TrySendPDU(state, reject);
                    }

                    if (pdu != null)
                    {
                        if (pdu.GetType() == typeof(ISCSIPDU))
                        {
                            Log("[{0}][ProcessCurrentBuffer] Unsupported PDU (0x{1})", state.Connection.Identifier, pdu.OpCode.ToString("X"));
                            // Unsupported PDU
                            RejectPDU reject = new RejectPDU();
                            reject.Reason = RejectReason.CommandNotSupported;
                            reject.StatSN = state.Connection.StatSN;
                            reject.ExpCmdSN = state.Connection.ExpCmdSN;
                            reject.MaxCmdSN = state.Connection.ExpCmdSN + ISCSIServer.CommandQueueSize;
                            reject.Data = ByteReader.ReadBytes(pduBytes, 0, 48);

                            // StatSN is advanced after a Reject
                            state.Connection.StatSN++;

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
                        Log("[{0}][ProcessCurrentBuffer] Buffer processing aborted, bytes left in receive buffer: {1}", state.Connection.Identifier, bytesLeftInBuffer);
                        return;
                    }
                }
            }

            if (bytesLeftInBuffer > 0)
            {
                byte[] newReceiveBuffer = new byte[bytesLeftInBuffer];
                Array.Copy(state.ConnectionBuffer, state.ConnectionBuffer.Length - bytesLeftInBuffer, newReceiveBuffer, 0, bytesLeftInBuffer);
                state.ConnectionBuffer = newReceiveBuffer;
            }
            else
            {
                state.ConnectionBuffer = new byte[0];
            }
        }

        public void ProcessPDU(ISCSIPDU pdu, StateObject state)
        {
            Socket clientSocket = state.ClientSocket;

            Log("[{0}][ProcessPDU] Received PDU from initiator, Operation: {1}, Size: {2}", state.Connection.Identifier, (ISCSIOpCodeName)pdu.OpCode, pdu.Length);
            
            if (pdu is NOPOutPDU)
            {
                NOPOutPDU request = (NOPOutPDU)pdu;
                NOPInPDU response = ServerResponseHelper.GetNOPResponsePDU(request, state.Connection);
                TrySendPDU(state, response);
            }
            else if (pdu is TextRequestPDU)
            {
                TextRequestPDU request = (TextRequestPDU)pdu;
                TextResponsePDU response = ServerResponseHelper.GetTextResponsePDU(request, m_targets);
                TrySendPDU(state, response);
            }
            else if (pdu is LoginRequestPDU)
            {
                LoginRequestPDU request = (LoginRequestPDU)pdu;
                Log("[{0}][ReceiveCallback] Login Request parameters: {1}", state.Connection.Identifier, KeyValuePairUtils.ToString(request.LoginParameters));
                if (request.TSIH != 0)
                {
                    // RFC 3720: A Login Request with a non-zero TSIH and a CID equal to that of an existing
                    // connection implies a logout of the connection followed by a Login
                    lock (m_activeConnectionsLock)
                    {
                        int existingConnectionIndex = GetStateObjectIndex(m_activeConnections, request.ISID, request.TSIH, request.CID);
                        if (existingConnectionIndex >= 0)
                        {
                            // Perform implicit logout
                            Log("[{0}][ProcessPDU] Initiating implicit logout", state.Connection.Identifier);
                            SocketUtils.ReleaseSocket(m_activeConnections[existingConnectionIndex].ClientSocket);
                            lock (m_activeConnections[existingConnectionIndex].Connection.WriteLock)
                            {
                                // Wait for pending I/O to complete.
                            }
                            m_activeConnections.RemoveAt(existingConnectionIndex);
                            Log("[{0}][ProcessPDU] Implicit logout completed", state.Connection.Identifier);
                        }
                    }
                }
                LoginResponsePDU response = ServerResponseHelper.GetLoginResponsePDU(request, m_targets, state.Connection, ref state.Target, ref m_nextTSIH);
                if (state.Target != null)
                {
                    if (request.LoginParameters.ContainsKey("MaxRecvDataSegmentLength"))
                    {
                        state.Connection.ConnectionParameters.InitiatorMaxRecvDataSegmentLength = Convert.ToInt32(request.LoginParameters.ValueOf("MaxRecvDataSegmentLength"));
                        Log("[{0}][ProcessPDU] Initiator's MaxRecvDataSegmentLength: {1}", state.Connection.Identifier, state.Connection.ConnectionParameters.InitiatorMaxRecvDataSegmentLength);
                    }

                    // We ignore the initator's offer, and make our own counter offer
                    state.Connection.SessionParameters.MaxBurstLength = OfferedMaxBurstLength;
                    state.Connection.SessionParameters.FirstBurstLength = OfferedFirstBurstLength;

                    state.Connection.SessionParameters.ISID = request.ISID;
                    state.Connection.ConnectionParameters.CID = request.CID;

                    if (response.NextStage == 3)
                    {
                        m_activeConnections.Add(state);
                    }
                }
                Log("[{0}][ReceiveCallback] Login Response parameters: {1}", state.Connection.Identifier, KeyValuePairUtils.ToString(response.LoginParameters));
                TrySendPDU(state, response);
            }
            else if (pdu is LogoutRequestPDU)
            {
                lock (m_activeConnectionsLock)
                {
                    int connectionIndex = GetStateObjectIndex(m_activeConnections, state.Connection.SessionParameters.ISID, state.Connection.SessionParameters.TSIH, state.Connection.ConnectionParameters.CID);
                    if (connectionIndex >= 0)
                    {
                        lock (m_activeConnections[connectionIndex].Connection.WriteLock)
                        {
                            // Wait for pending I/O to complete.
                        }
                        m_activeConnections.RemoveAt(connectionIndex);
                    }
                }
                LogoutRequestPDU request = (LogoutRequestPDU)pdu;
                LogoutResponsePDU response = ServerResponseHelper.GetLogoutResponsePDU(request, state.Connection);
                TrySendPDU(state, response);
                clientSocket.Close(); // We can close the connection now
            }
            else // Target commands
            {
                if (state.Target == null)
                {
                    Log("[{0}][ProcessPDU] Unknown or improper command, OpCode: 0x{1}", state.Connection.Identifier, pdu.OpCode.ToString("x"));
                }
                else // Logged in to target 
                {
                    if (pdu is SCSIDataOutPDU)
                    {
                        SCSIDataOutPDU request = (SCSIDataOutPDU)pdu;
                        ISCSIServer.Log("[{0}][ProcessPDU] SCSIDataOutPDU: Target transfer tag: {1}, LUN: {2}, Buffer offset: {3}, Data segment length: {4}, DataSN: {5}, Final: {6}", state.Connection.Identifier, request.TargetTransferTag, request.LUN, request.BufferOffset, request.DataSegmentLength, request.DataSN, request.Final);
                        ISCSIPDU response = TargetResponseHelper.GetSCSIDataOutResponsePDU(request, state.Target, state.Connection);
                        TrySendPDU(state, response);
                    }
                    else if (pdu is SCSICommandPDU)
                    {
                        SCSICommandPDU command = (SCSICommandPDU)pdu;
                        ISCSIServer.Log("[{0}][ProcessPDU] SCSICommandPDU: CmdSN: {1}, SCSI command: {2}, LUN: {3}, Data segment length: {4}, Expected Data Transfer Length: {5}, Final: {6}", state.Connection.Identifier, command.CmdSN, (SCSIOpCodeName)command.CommandDescriptorBlock.OpCode, command.LUN, command.DataSegmentLength, command.ExpectedDataTransferLength, command.Final);
                        List<ISCSIPDU> scsiResponseList = TargetResponseHelper.GetSCSIResponsePDU(command, state.Target, state.Connection);
                        foreach (ISCSIPDU response in scsiResponseList)
                        {
                            TrySendPDU(state, response);
                            if (!clientSocket.Connected)
                            {
                                return;
                            }
                        }
                    }
                    else
                    {
                        Log("[{0}][ProcessPDU] Unknown or improper command, OpCode: 0x{1}", state.Connection.Identifier, pdu.OpCode.ToString("x"));
                    }
                }
            }
        }

        private static int GetStateObjectIndex(List<StateObject> stateObjects, ulong isid, ushort tsih, ushort cid)
        {
            for (int index = 0; index < stateObjects.Count; index++)
            {
                if (stateObjects[index].Connection.SessionParameters.ISID == isid &&
                    stateObjects[index].Connection.SessionParameters.TSIH == tsih &&
                    stateObjects[index].Connection.ConnectionParameters.CID == cid)
                {
                    return index;
                }
            }
            return -1;
        }

        public static void TrySendPDU(StateObject state, ISCSIPDU response)
        {
            Socket clientSocket = state.ClientSocket;
            try
            {
                clientSocket.Send(response.GetBytes());
                Log("[{0}][ProcessPDU] Sent response to initator, Operation: {1}, Size: {2}", state.Connection.Identifier, (ISCSIOpCodeName)response.OpCode, response.Length);
            }
            catch (SocketException ex)
            {
                Log("[{0}][ProcessPDU] Failed to send response to initator (Operation: {1}, Size: {2}), SocketException: {3}", state.Connection.Identifier, (ISCSIOpCodeName)response.OpCode, response.Length, ex.Message);
            }
            catch (ObjectDisposedException)
            {
            }
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

        public static void LogRead(long sectorIndex, int sectorCount)
        {
            if (m_enableDiskIOLogging)
            {
                Log("[LogRead] Sector: {0}, Sector count: {1}", sectorIndex, sectorCount);
            }
        }

        public static void LogWrite(Disk disk, long sectorIndex, byte[] data)
        {
            if (m_logFile != null && m_enableDiskIOLogging)
            {
                Log("[LogWrite] Sector: {0}, Data Length: {1}", sectorIndex, data.Length);
            }
        }

        public static string GetByteArrayString(byte[] array)
        {
            StringBuilder builder = new StringBuilder();
            foreach (byte b in array)
            {
                builder.Append(b.ToString("X2")); // 2 digit hex
                builder.Append(" ");
            }
            return builder.ToString();
        }
    }
}
