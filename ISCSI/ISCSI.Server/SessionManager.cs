/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Threading;
using Utilities;

namespace ISCSI.Server
{
    internal class SessionManager
    {
        private object m_nextTSIHLock = new object();
        private ushort m_nextTSIH = 1; // Next Target Session Identifying Handle
        private List<ISCSISession> m_activeSessions = new List<ISCSISession>();

        public ISCSISession StartSession(string initiatorName, ulong isid)
        {
            ushort tsih = GetNextTSIH();
            ISCSISession session = new ISCSISession(initiatorName, isid, tsih);
            lock (m_activeSessions)
            {
                m_activeSessions.Add(session);
            }
            return session;
        }

        public ISCSISession FindSession(string initiatorName, ulong isid, ushort tsih)
        {
            lock (m_activeSessions)
            {
                int index = GetSessionIndex(initiatorName, isid, tsih);
                if (index >= 0)
                {
                    return m_activeSessions[index];
                }
            }
            return null;
        }

        public ISCSISession FindSession(string initiatorName, ulong isid, string targetName)
        {
            lock (m_activeSessions)
            {
                for (int index = 0; index < m_activeSessions.Count; index++)
                {
                    ISCSISession session = m_activeSessions[index];
                    if (String.Equals(session.InitiatorName, initiatorName, StringComparison.OrdinalIgnoreCase) &&
                        session.ISID == isid &&
                        session.Target != null &&
                        String.Equals(session.Target.TargetName, targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        return session;
                    }
                }
            }
            return null;
        }

        public List<ISCSISession> FindTargetSessions(string targetName)
        {
            lock (m_activeSessions)
            {
                List<ISCSISession> result = new List<ISCSISession>();
                foreach (ISCSISession session in m_activeSessions)
                {
                    if (session.Target != null)
                    {
                        if (String.Equals(session.Target.TargetName, targetName, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add(session);
                        }
                    }
                }
                return result;
            }
        }

        public void RemoveSession(ISCSISession session, SessionTerminationReason reason)
        {
            lock (m_activeSessions)
            {
                int index = GetSessionIndex(session.InitiatorName, session.ISID, session.TSIH);
                if (index >= 0)
                {
                    ISCSITarget target = m_activeSessions[index].Target;
                    if (target != null)
                    {
                        target.NotifySessionTermination(session.InitiatorName, session.ISID, reason);
                    }
                    m_activeSessions.RemoveAt(index);
                }
            }
        }

        public bool IsTargetInUse(string targetName)
        {
            lock (m_activeSessions)
            {
                foreach (ISCSISession session in m_activeSessions)
                {
                    if (session.Target != null)
                    {
                        if (String.Equals(session.Target.TargetName, targetName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private int GetSessionIndex(string initiatorName, ulong isid, ushort tsih)
        {
            for (int index = 0; index < m_activeSessions.Count; index++)
            {
                if (String.Equals(initiatorName, m_activeSessions[index].InitiatorName, StringComparison.OrdinalIgnoreCase) &&
                    m_activeSessions[index].ISID == isid &&
                    m_activeSessions[index].TSIH == tsih)
                {
                    return index;
                }
            }
            return -1;
        }

        private ushort GetNextTSIH()
        {
            // The iSCSI Target selects a non-zero value for the TSIH at
            // session creation (when an initiator presents a 0 value at Login).
            // After being selected, the same TSIH value MUST be used whenever the
            // initiator or target refers to the session and a TSIH is required.
            lock (m_nextTSIHLock)
            {
                ushort nextTSIH = m_nextTSIH;
                m_nextTSIH++;
                if (m_nextTSIH == 0)
                {
                    m_nextTSIH++;
                }
                return nextTSIH;
            }
        }
    }
}
