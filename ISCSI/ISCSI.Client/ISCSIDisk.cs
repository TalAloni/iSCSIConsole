/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ISCSI.Client
{
    /// <summary>
    /// Disk Adapter utilizing ISCSIClient
    /// </summary>
    public class ISCSIDisk : Disk
    {
        private ISCSIClient m_client;
        private int m_bytesPerSector;
        private long m_size;
        private bool m_isLoggedIn;
        private ushort m_lun;

        public ISCSIDisk()
        {
            string initiatorName = "iqn.1991-05.com.microsoft:" + Environment.MachineName;
            m_client = new ISCSIClient(initiatorName);
        }

        public ISCSIDisk(string initiatorName)
        {
            m_client = new ISCSIClient(initiatorName);
        }

        public bool Connect(IPAddress targetAddress, int targetPort, string targetName, ushort lun)
        {
            bool isConnected = m_client.Connect(targetAddress, targetPort);
            if (isConnected)
            {
                m_isLoggedIn = m_client.Login(targetName);
                if (m_isLoggedIn)
                {
                    List<ushort> luns = m_client.GetLUNList();
                    if (luns.Contains(lun))
                    {
                        m_lun = lun;
                        m_size = (long)m_client.ReadCapacity(lun, out m_bytesPerSector);
                        return true;
                    }
                    m_client.Logout();
                    m_isLoggedIn = false;
                }
            }
            return false;
        }

        public void Disconnect()
        {
            if (m_isLoggedIn)
            {
                m_client.Logout();
            }
            m_client.Disconnect();
        }

        public override byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            if (!m_isLoggedIn)
            {
                throw new InvalidOperationException("Not connected");
            }
            return m_client.Read(m_lun, (ulong)sectorIndex, (uint)sectorCount, m_bytesPerSector);
        }

        public override void WriteSectors(long sectorIndex, byte[] data)
        {
            if (!m_isLoggedIn)
            {
                throw new InvalidOperationException("Not connected");
            }
            if (IsReadOnly)
            {
                throw new UnauthorizedAccessException("Attempted to perform write on a readonly disk");
            }

            bool success = m_client.Write(m_lun, (ulong)sectorIndex, data, m_bytesPerSector);
            if (!success)
            {
                string message = String.Format("Failed to write to sector {0}", sectorIndex);
            }
        }

        public override int BytesPerSector
        {
            get
            {
                return m_bytesPerSector;
            }
        }

        public override long Size
        {
            get
            {
                return m_size;
            }
        }
    }
}
