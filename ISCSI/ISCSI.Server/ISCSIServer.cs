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

        private Socket m_listenerSocket;
        private bool m_listening;
        private Thread m_keepAliveThread;
        private TargetList m_targets = new TargetList();
        private SessionManager m_sessionManager = new SessionManager();
        private ConnectionManager m_connectionManager = new ConnectionManager();

        public event EventHandler<LogEntry> OnLogEntry;

        /// <summary>
        /// Server needs to be started with Start()
        /// </summary>
        public ISCSIServer()
        {
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

        /// <exception cref="System.Net.Sockets.SocketException"></exception>
        public void Start()
        {
            Start(DefaultPort);
        }

        /// <param name="listenerPort">The port on which the iSCSI server will listen</param>
        /// <exception cref="System.Net.Sockets.SocketException"></exception>
        public void Start(int listenerPort)
        {
            Start(new IPEndPoint(IPAddress.Any, listenerPort));
        }

        /// <exception cref="System.Net.Sockets.SocketException"></exception>
        public void Start(IPEndPoint listenerEndPoint)
        {
            Start(listenerEndPoint, TimeSpan.FromMinutes(5));
        }

        /// <param name="listenerEP">The endpoint on which the iSCSI server will listen</param>
        /// <param name="keepAliveTime">The duration between keep-alive transmissions</param>        
        /// <exception cref="System.Net.Sockets.SocketException"></exception>
        public void Start(IPEndPoint listenerEndPoint, TimeSpan? keepAliveTime)
        {
            if (!m_listening)
            {
                Log(Severity.Information, "Starting Server");
                m_listenerSocket = new Socket(listenerEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                m_listenerSocket.Bind(listenerEndPoint);
                m_listenerSocket.Listen(1000);
                m_listenerSocket.BeginAccept(ConnectRequestCallback, m_listenerSocket);
                m_listening = true;

                if (keepAliveTime.HasValue)
                {
                    m_keepAliveThread = new Thread(delegate()
                    {
                        while (m_listening)
                        {
                            Thread.Sleep(keepAliveTime.Value);
                            m_connectionManager.SendKeepAlive();
                        }
                    });
                    m_keepAliveThread.IsBackground = true;
                    m_keepAliveThread.Start();
                }
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
            // Disable the Nagle Algorithm for this tcp socket:
            clientSocket.NoDelay = true;
            state.ClientSocket = clientSocket;
            Thread senderThread = new Thread(delegate()
            {
                ProcessSendQueue(state);
            });
            senderThread.IsBackground = true;
            senderThread.Start();

            ISCSIConnectionReceiveBuffer buffer = state.ReceiveBuffer;
            try
            {
                clientSocket.BeginReceive(buffer.Buffer, buffer.WriteOffset, buffer.AvailableLength, 0, ReceiveCallback, state);
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
            if (m_keepAliveThread != null)
            {
                m_keepAliveThread.Abort();
            }
            SocketUtils.ReleaseSocket(m_listenerSocket);
            lock (m_targets.Lock)
            {
                List<ISCSITarget> targets = m_targets.GetList();
                foreach (ISCSITarget target in targets)
                {
                    ResetTarget(target.TargetName);
                }
            }
        }

        /// <summary>
        /// Will terminate all TCP connections to all initiators (all sessions will be terminated)
        /// </summary>
        public void ResetTarget(string targetName)
        {
            List<ISCSISession> targetSessions = m_sessionManager.FindTargetSessions(targetName);
            foreach (ISCSISession session in targetSessions)
            {
                List<ConnectionState> sessionConnections = m_connectionManager.GetSessionConnections(session);
                foreach (ConnectionState sessionConnection in sessionConnections)
                {
                    m_connectionManager.ReleaseConnection(sessionConnection);
                }
                m_sessionManager.RemoveSession(session, SessionTerminationReason.TargetReset);
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
                HandleConnectionTermination(state);
                return;
            }

            int numberOfBytesReceived;
            try
            {
                numberOfBytesReceived = clientSocket.EndReceive(result);
            }
            catch (ObjectDisposedException)
            {
                HandleConnectionTermination(state);
                Log(Severity.Debug, "[ReceiveCallback] EndReceive ObjectDisposedException");
                return;
            }
            catch (SocketException ex)
            {
                HandleConnectionTermination(state);
                Log(Severity.Debug, "[ReceiveCallback] EndReceive SocketException: {0}", ex.Message);
                return;
            }

            if (numberOfBytesReceived == 0)
            {
                // The other side has closed the connection
                Log(Severity.Verbose, "[{0}] The initiator has closed the connection", state.ConnectionIdentifier);
                HandleConnectionTermination(state);
                return;
            }

            ISCSIConnectionReceiveBuffer buffer = state.ReceiveBuffer;
            buffer.SetNumberOfBytesReceived(numberOfBytesReceived);
            ProcessConnectionBuffer(state);

            try
            {
                clientSocket.BeginReceive(buffer.Buffer, buffer.WriteOffset, buffer.AvailableLength, 0, ReceiveCallback, state);
            }
            catch (ObjectDisposedException)
            {
                HandleConnectionTermination(state);
                Log(Severity.Debug, "[ReceiveCallback] BeginReceive ObjectDisposedException");
            }
            catch (SocketException ex)
            {
                HandleConnectionTermination(state);
                Log(Severity.Debug, "[ReceiveCallback] BeginReceive SocketException: {0}", ex.Message);
            }
        }

        private void HandleConnectionTermination(ConnectionState state)
        {
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
                            m_sessionManager.RemoveSession(state.Session, SessionTerminationReason.ConnectionFailure);
                        }
                    });
                    timeoutThread.IsBackground = true;
                    timeoutThread.Start();
                }
            }
        }

        private void ProcessConnectionBuffer(ConnectionState state)
        {
            Socket clientSocket = state.ClientSocket;

            ISCSIConnectionReceiveBuffer buffer = state.ReceiveBuffer;
            while (buffer.HasCompletePDU())
            {
                ISCSIPDU pdu = null;
                try
                {
                    pdu = buffer.DequeuePDU();
                }
                catch (Exception ex)
                {
                    byte[] pduBytes = buffer.DequeuePDUBytes();
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
                        reject.Data = ByteReader.ReadBytes(pdu.GetBytes(), 0, 48);
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
                    if (buffer.BytesInBuffer > 0)
                    {
                        Log(Severity.Debug, "[{0}] Buffer processing aborted, bytes left in receive buffer: {1}", state.ConnectionIdentifier, buffer.BytesInBuffer);
                    }
                    return;
                }
            }
        }

        private void ProcessSendQueue(ConnectionState state)
        {
            LogTrace("Entering ProcessSendQueue");
            while (true)
            {
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
                if ((response is NOPInPDU && ((NOPInPDU)response).InitiatorTaskTag != 0xffffffff) ||
                    response is SCSIResponsePDU ||
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
                        LogTrace("Leaving ProcessSendQueue");
                        return;
                    }
                    else if (response is LoginResponsePDU)
                    {
                        if (((LoginResponsePDU)response).Status != LoginResponseStatusName.Success)
                        {
                            // Login Response: If the Status Class is not 0, the initiator and target MUST close the TCP connection.
                            clientSocket.Close(); // We can close the connection now
                            LogTrace("Leaving ProcessSendQueue");
                            return;                            
                        }
                    }
                }
                catch (SocketException ex)
                {
                    Log(Severity.Verbose, "[{0}] Failed to send response to initator. Operation: {1}, Size: {2}, SocketException: {3}", state.ConnectionIdentifier, response.OpCode, response.Length, ex.Message);
                    LogTrace("Leaving ProcessSendQueue");
                    return;
                }
                catch (ObjectDisposedException)
                {
                    Log(Severity.Verbose, "[{0}] Failed to send response to initator. Operation: {1}, Size: {2}. ObjectDisposedException", state.ConnectionIdentifier, response.OpCode, response.Length);
                    LogTrace("Leaving ProcessSendQueue");
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

        [System.Diagnostics.Conditional("TRACE")]
        public void LogTrace(string message)
        {
            Log(Severity.Trace, message);
        }
    }
}
