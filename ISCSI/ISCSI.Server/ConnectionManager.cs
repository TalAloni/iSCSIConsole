/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace ISCSI.Server
{
    public class ConnectionManager
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
            return RemoveConnection(connection.SessionParameters.ISID, connection.SessionParameters.TSIH, connection.ConnectionParameters.CID);
        }

        public bool RemoveConnection(ulong isid, ushort tsih, ushort cid)
        {
            lock (m_activeConnections)
            {
                int connectionIndex = GetConnectionStateIndex(isid, tsih, cid);
                if (connectionIndex >= 0)
                {
                    m_activeConnections.RemoveAt(connectionIndex);
                    return true;
                }
                return false;
            }
        }

        public ConnectionState FindConnection(ConnectionState connection)
        {
            return FindConnection(connection.SessionParameters.ISID, connection.SessionParameters.TSIH, connection.ConnectionParameters.CID);
        }

        public ConnectionState FindConnection(ulong isid, ushort tsih, ushort cid)
        {
            lock (m_activeConnections)
            {
                int index = GetConnectionStateIndex(isid, tsih, cid);
                if (index >= 0)
                {
                    return m_activeConnections[index];
                }
                return null;
            }
        }

        public List<ConnectionState> GetSessionConnections(ulong isid, ushort tsih)
        {
            List<ConnectionState> result = new List<ConnectionState>();
            lock (m_activeConnections)
            {
                for (int index = 0; index < m_activeConnections.Count; index++)
                {
                    if (m_activeConnections[index].SessionParameters.ISID == isid &&
                        m_activeConnections[index].SessionParameters.TSIH == tsih)
                    {
                        result.Add(m_activeConnections[index]);
                    }
                }
            }
            return result;
        }

        private int GetConnectionStateIndex(ulong isid, ushort tsih, ushort cid)
        {
            for (int index = 0; index < m_activeConnections.Count; index++)
            {
                if (m_activeConnections[index].SessionParameters.ISID == isid &&
                    m_activeConnections[index].SessionParameters.TSIH == tsih &&
                    m_activeConnections[index].ConnectionParameters.CID == cid)
                {
                    return index;
                }
            }
            return -1;
        }
    }
}
