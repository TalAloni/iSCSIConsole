/* Copyright (C) 2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ISCSIConsole
{
    public class UsageCounter
    {
        private SortedList<string, int> m_targetsInUse = new SortedList<string, int>();
        private int m_sessionCount = 0;

        public void NotifySessionStart(string targetName)
        {
            Interlocked.Increment(ref m_sessionCount);
            lock (m_targetsInUse)
            {
                int index = m_targetsInUse.IndexOfKey(targetName);
                if (index >= 0)
                {
                    m_targetsInUse[targetName]++;
                }
                else
                {
                    m_targetsInUse.Add(targetName, 1);
                }
            }
        }

        public void NotifySessionTermination(string targetName)
        {
            Interlocked.Decrement(ref m_sessionCount);
            lock (m_targetsInUse)
            {
                int index = m_targetsInUse.IndexOfKey(targetName);
                if (index >= 0)
                {
                    int useCount = m_targetsInUse[targetName];
                    useCount--;
                    if (useCount == 0)
                    {
                        m_targetsInUse.Remove(targetName);
                    }
                    else
                    {
                        m_targetsInUse[targetName] = useCount;
                    }
                }
            }
        }

        public bool IsTargetInUse(string targetName)
        {
            lock (m_targetsInUse)
            {
                return m_targetsInUse.ContainsKey(targetName);
            }
        }

        public int SessionCount
        {
            get
            {
                return m_sessionCount;
            }
        }
    }
}
