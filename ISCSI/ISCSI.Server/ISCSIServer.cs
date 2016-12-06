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
    /// <summary>
    /// an iSCSI server that can serve multiple iSCSI targets
    /// </summary>
    public partial class ISCSIServer
    {
        public const int DefaultPort = 3260;

        private IPEndPoint m_listenerEP;

        private Socket m_listenerSocket;
        private bool m_listening;
        private TargetList m_targets = new TargetList();
        private SessionManager m_sessionManager = new SessionManager();
        private ConnectionManager m_connectionManager = new ConnectionManager();

        public event EventHandler<LogEntry> OnLogEntry;
        
        public ISCSIServer() : this(DefaultPort)
        { }

        /// <summary>
        /// Server needs to be started with Start()
        /// </summary>
        /// <param name="port">The port on which the iSCSI server will listen</param>
        public ISCSIServer(int port) : this(new IPEndPoint(IPAddress.Any, port))
        {
        }

        /// <summary>
        /// Server needs to be started with Start()
        /// </summary>
        /// <param name="listenerEP">The endpoint on which the iSCSI server will listen</param>
        public ISCSIServer(IPEndPoint listenerEP)
        {
            m_listenerEP = listenerEP;
        }

        public void AddTarget(ISCSITarget target)
        {
            m_targets.AddTarget(target);
        }

        public void AddTargets(List<ISCSITarget> targets)
        {
            foreach (ISCSITarget target in targets)
            {
                m_targets.AddTarget(target);
            }
        }

        public bool RemoveTarget(string targetName)
        {
            // We use m_targets.Lock to synchronize between the login logic and the target removal logic.
            // We must obtain the lock before calling IsTargetInUse() to prevent a successful login to a target followed by its removal.
            lock (m_targets.Lock)
            {
                if (!m_sessionManager.IsTargetInUse(targetName))
                {
                    return m_targets.RemoveTarget(targetName);
                }
            }
            return false;
        }

        public void Start()
        {
            if (!m_listening)
            {
                Log(Severity.Information, "Starting Server");
                m_listening = true;

                m_listenerSocket = new Socket(m_listenerEP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                m_listenerSocket.Bind(m_listenerEP);
                m_listenerSocket.Listen(1000);
                m_listenerSocket.BeginAccept(ConnectRequestCallback, m_listenerSocket);
            }
        }

        // This method accepts new connections
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

            Log(Severity.Information, "New connection has been accepted");

            ConnectionState state = new ConnectionState();
            state.ConnectionParameters.InitiatorEndPoint = clientSocket.RemoteEndPoint as IPEndPoint;
            state.ReceiveBuffer = new byte[ConnectionState.ReceiveBufferSize];
            // Disable the Nagle Algorithm for this tcp socket:
            clientSocket.NoDelay = true;
            state.ClientSocket = clientSocket;
            Thread senderThread = new Thread(delegate()
            {
                ProcessSendQueue(state);
            });
            senderThread.IsBackground = true;
            senderThread.Start();

            try
            {
                clientSocket.BeginReceive(state.ReceiveBuffer, 0, ConnectionState.ReceiveBufferSize, 0, ReceiveCallback, state);
            }
            catch (ObjectDisposedException)
            {
                Log(Severity.Debug, "[OnConnectRequest] BeginReceive ObjectDisposedException");
            }
            catch (SocketException ex)
            {
                Log(Severity.Debug, "[OnConnectRequest] BeginReceive SocketException: {0}", ex.Message);
            }
            m_listenerSocket.BeginAccept(ConnectRequestCallback, m_listenerSocket);
        }

        public void Stop()
        {
            Log(Severity.Information, "Stopping Server");
            m_listening = false;
            SocketUtils.ReleaseSocket(m_listenerSocket);
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
                Log(Severity.Debug, "[ReceiveCallback] EndReceive ObjectDisposedException");
                return;
            }
            catch (SocketException ex)
            {
                Log(Severity.Debug, "[ReceiveCallback] EndReceive SocketException: {0}", ex.Message);
                return;
            }

            if (numberOfBytesReceived == 0)
            {
                // The other side has closed the connection
                Log(Severity.Verbose, "The initiator has closed the connection");
                m_connectionManager.ReleaseConnection(state);
                if (state.Session != null)
                {
                    List<ConnectionState> connections = m_connectionManager.GetSessionConnections(state.Session);
                    if (connections.Count == 0)
                    {
                        Thread timeoutThread = new Thread(delegate()
                        {
                            // Session timeout is an event defined to occur when the last connection [..] timeout expires
                            int timeout = state.Session.DefaultTime2Wait + state.Session.DefaultTime2Retain;
                            Thread.Sleep(timeout * 1000);
                            // Check if there are still no connections in this session
                            connections = m_connectionManager.GetSessionConnections(state.Session);
                            if (connections.Count == 0)
                            {
                                m_sessionManager.RemoveSession(state.Session);
                            }
                        });
                        timeoutThread.IsBackground = true;
                        timeoutThread.Start();
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
                Log(Severity.Debug, "[ReceiveCallback] BeginReceive ObjectDisposedException");
            }
            catch (SocketException ex)
            {
                Log(Severity.Debug, "[ReceiveCallback] BeginReceive SocketException: {0}", ex.Message);
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
                    Log(Severity.Debug, "[{0}][ProcessCurrentBuffer] Bytes left in receive buffer: {1}", state.ConnectionIdentifier, bytesLeftInBuffer);
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
                        Log(Severity.Error, "[{0}] Failed to read PDU (Exception: {1})", state.ConnectionIdentifier, ex.Message);
                        RejectPDU reject = new RejectPDU();
                        reject.Reason = RejectReason.InvalidPDUField;
                        reject.Data = ByteReader.ReadBytes(pduBytes, 0, 48);

                        state.SendQueue.Enqueue(reject);
                    }

                    if (pdu != null)
                    {
                        if (pdu.GetType() == typeof(ISCSIPDU))
                        {
                            Log(Severity.Error, "[{0}][ProcessCurrentBuffer] Unsupported PDU (0x{1})", state.ConnectionIdentifier, pdu.OpCode.ToString("X"));
                            // Unsupported PDU
                            RejectPDU reject = new RejectPDU();
                            reject.InitiatorTaskTag = pdu.InitiatorTaskTag;
                            reject.Reason = RejectReason.CommandNotSupported;
                            reject.Data = ByteReader.ReadBytes(pduBytes, 0, 48);
                            state.SendQueue.Enqueue(reject);
                        }
                        else
                        {
                            bool valid = ValidateCommandNumbering(pdu, state);
                            if (valid)
                            {
                                ProcessPDU(pdu, state);
                            }
                            else
                            {
                                // We ignore this PDU
                                Log(Severity.Warning, "[{0}] Ignoring PDU with CmdSN outside of expected range", state.ConnectionIdentifier);
                            }
                        }
                    }
                    
                    if (!clientSocket.Connected)
                    {
                        // Do not continue to process the buffer if the other side closed the connection
                        if (bytesLeftInBuffer > 0)
                        {
                            Log(Severity.Debug, "[{0}] Buffer processing aborted, bytes left in receive buffer: {1}", state.ConnectionIdentifier, bytesLeftInBuffer);
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

        private void ProcessSendQueue(ConnectionState state)
        {
            while (true)
            {
                Log(Severity.Trace, "Entering ProcessSendQueue");
                ISCSIPDU response;
                bool stopped = !state.SendQueue.TryDequeue(out response);
                if (stopped)
                {
                    return;
                }
                Socket clientSocket = state.ClientSocket;
                PDUHelper.SetStatSN(response, state.ConnectionParameters.StatSN);
                if (state.Session != null)
                {
                    PDUHelper.SetExpCmdSN(response, state.Session.ExpCmdSN, state.Session.ExpCmdSN + state.Session.CommandQueueSize);
                }
                if (response is SCSIResponsePDU ||
                    response is LoginResponsePDU ||
                    response is TextResponsePDU ||
                    (response is SCSIDataInPDU && ((SCSIDataInPDU)response).StatusPresent) ||
                    response is RejectPDU)
                {
                    state.ConnectionParameters.StatSN++;
                }
                try
                {
                    clientSocket.Send(response.GetBytes());
                    Log(Severity.Verbose, "[{0}] Sent response to initator, Operation: {1}, Size: {2}", state.ConnectionIdentifier, response.OpCode, response.Length);
                    if (response is LogoutResponsePDU)
                    {
                        clientSocket.Close(); // We can close the connection now
                        Log(Severity.Trace, "Leaving ProcessSendQueue");
                        return;
                    }
                    else if (response is LoginResponsePDU)
                    {
                        if (((LoginResponsePDU)response).Status != LoginResponseStatusName.Success)
                        {
                            // Login Response: If the Status Class is not 0, the initiator and target MUST close the TCP connection.
                            clientSocket.Close(); // We can close the connection now
                            Log(Severity.Trace, "Leaving ProcessSendQueue");
                            return;                            
                        }
                    }
                }
                catch (SocketException ex)
                {
                    Log(Severity.Verbose, "[{0}] Failed to send response to initator. Operation: {1}, Size: {2}, SocketException: {3}", state.ConnectionIdentifier, response.OpCode, response.Length, ex.Message);
                    Log(Severity.Trace, "Leaving ProcessSendQueue");
                    return;
                }
                catch (ObjectDisposedException)
                {
                    Log(Severity.Verbose, "[{0}] Failed to send response to initator. Operation: {1}, Size: {2}. ObjectDisposedException", state.ConnectionIdentifier, response.OpCode, response.Length);
                    Log(Severity.Trace, "Leaving ProcessSendQueue");
                    return;
                }
            }
        }

        public void Log(Severity severity, string message)
        {
            // To be thread-safe we must capture the delegate reference first
            EventHandler<LogEntry> handler = OnLogEntry;
            if (handler != null)
            {
                handler(this, new LogEntry(DateTime.Now, severity, "iSCSI Server", message));
            }
        }

        public void Log(Severity severity, string message, params object[] args)
        {
            Log(severity, String.Format(message, args));
        }
    }
}
