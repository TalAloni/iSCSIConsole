/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace ISCSI.Server
{
    internal class ConnectionManager
    {
        private List<ConnectionState> m_activeConnections = new List<ConnectionState>();

        public void AddConnection(ConnectionState connection)
        {
            lock (m_activeConnections)
            {
                m_activeConnections.Add(connection);
            }
        }

        public bool RemoveConnection(ConnectionState connection)
        {
            lock (m_activeConnections)
            {
                int connectionIndex = GetConnectionStateIndex(connection.Session, connection.ConnectionParameters.CID);
                if (connectionIndex >= 0)
                {
                    m_activeConnections.RemoveAt(connectionIndex);
                    return true;
                }
                return false;
            }
        }

        public void ReleaseConnection(ConnectionState connection)
        {
            // Wait for pending I/O to complete.
            connection.RunningSCSICommands.WaitUntilZero();
            connection.SendQueue.Stop();
            SocketUtils.ReleaseSocket(connection.ClientSocket);
            if (connection.Session != null)
            {
                RemoveConnection(connection);
            }
        }

        public ConnectionState FindConnection(ISCSISession session, ushort cid)
        {
            lock (m_activeConnections)
            {
                int index = GetConnectionStateIndex(session, cid);
                if (index >= 0)
                {
                    return m_activeConnections[index];
                }
                return null;
            }
        }

        public List<ConnectionState> GetSessionConnections(ISCSISession session)
        {
            List<ConnectionState> result = new List<ConnectionState>();
            lock (m_activeConnections)
            {
                for (int index = 0; index < m_activeConnections.Count; index++)
                {
                    if (String.Equals(m_activeConnections[index].Session.InitiatorName, session.InitiatorName, StringComparison.OrdinalIgnoreCase) &&
                        m_activeConnections[index].Session.ISID == session.ISID &&
                        m_activeConnections[index].Session.TSIH == session.TSIH)
                    {
                        result.Add(m_activeConnections[index]);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// This method will check for dead peers (initiators).
        /// Because TCP broken connections can only be detected by sending data, we send a NOP-In PDU,
        /// If the connection is dead, the send failure will trigger the necessary connection termination logic.
        /// See: http://blog.stephencleary.com/2009/05/detection-of-half-open-dropped.html
        /// </summary>
        public void SendKeepAlive()
        {
            lock (m_activeConnections)
            {
                foreach (ConnectionState connection in m_activeConnections)
                {
                    connection.SendQueue.Enqueue(ServerResponseHelper.GetKeepAlivePDU());
                }
            }
        }

        private int GetConnectionStateIndex(ISCSISession session, ushort cid)
        {
            for (int index = 0; index < m_activeConnections.Count; index++)
            {
                if (String.Equals(m_activeConnections[index].Session.InitiatorName, session.InitiatorName, StringComparison.OrdinalIgnoreCase) &&
                    m_activeConnections[index].Session.ISID == session.ISID &&
                    m_activeConnections[index].Session.TSIH == session.TSIH &&
                    m_activeConnections[index].ConnectionParameters.CID == cid)
                {
                    return index;
                }
            }
            return -1;
        }
    }
}
