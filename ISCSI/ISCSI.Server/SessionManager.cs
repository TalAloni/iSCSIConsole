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
        private object m_syncLock = new object();
        private ushort m_nextTSIH = 1; // Next Target Session Identifying Handle
        
        public ushort GetNextTSIH()
        {
            // The iSCSI Target selects a non-zero value for the TSIH at
            // session creation (when an initiator presents a 0 value at Login).
            // After being selected, the same TSIH value MUST be used whenever the
            // initiator or target refers to the session and a TSIH is required.
            lock (m_syncLock)
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
