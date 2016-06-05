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
    public class ISCSITarget
    {
        private string m_targetName; // ISCSI name
        private List<Disk> m_disks;

        public ISCSITarget(string targetName, List<Disk> disks)
        {
            m_targetName = targetName;
            m_disks = disks;
        }

        public string TargetName
        {
            get
            {
                return m_targetName;
            }
        }

        public List<Disk> Disks
        {
            get
            {
                return m_disks;
            }
        }
    }
}
