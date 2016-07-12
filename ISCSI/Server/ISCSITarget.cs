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
    public class ISCSITarget : SCSITarget
    {
        private string m_targetName; // ISCSI name

        public ISCSITarget(string targetName, List<Disk> disks) : base(disks)
        {
            m_targetName = targetName;
        }

        public string TargetName
        {
            get
            {
                return m_targetName;
            }
        }
    }
}
