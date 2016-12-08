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

namespace ISCSI.Server
{
    internal class TargetList
    {
        public object Lock = new object();
        private List<ISCSITarget> m_targets = new List<ISCSITarget>();

        public void AddTarget(ISCSITarget target)
        {
            lock (Lock)
            {
                int index = IndexOfTarget(target.TargetName);
                if (index >= 0)
                {
                    throw new ArgumentException("A target with the same iSCSI Target Name already exists");
                }
                m_targets.Add(target);
            }
        }

        public ISCSITarget FindTarget(string targetName)
        {
            lock (Lock)
            {
                int index = IndexOfTarget(targetName);
                if (index >= 0)
                {
                    return m_targets[index];
                }
                return null;
            }
        }

        public bool RemoveTarget(string targetName)
        {
            lock (Lock)
            {
                int index = IndexOfTarget(targetName);
                if (index >= 0)
                {
                    m_targets.RemoveAt(index);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Caller MUST obtain a lock on TargetList.Lock before calling this method
        /// </summary>
        public List<ISCSITarget> GetList()
        {
            return new List<ISCSITarget>(m_targets);
        }

        private int IndexOfTarget(string targetName)
        {
            for (int index = 0; index < m_targets.Count; index++)
            {
                if (String.Equals(m_targets[index].TargetName, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
            return -1;
        }
    }
}
