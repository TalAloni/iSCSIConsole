/* Copyright (C) 2017 Tal Aloni <tal.aloni.il@gmail.com>.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;

namespace SCSI.Win32
{
    internal class LogicalUnit
    {
        public byte AssociatedLun;
        public byte PathId;
        public byte TargetId;
        public byte TargetLun;
        public PeripheralDeviceType DeviceType;
        public uint? BlockSize;
    }

    internal class LogicalUnitManager
    {
        private IDictionary<byte, LogicalUnit> m_luns = new Dictionary<byte, LogicalUnit>();

        public LogicalUnitManager()
        {
        }

        public void AddLogicalUnit(LogicalUnit logicalUnit)
        {
            m_luns.Add(logicalUnit.AssociatedLun, logicalUnit);
        }

        public LogicalUnit FindLogicalUnit(byte lun)
        {
            LogicalUnit result;
            m_luns.TryGetValue(lun, out result);
            return result;
        }

        public byte? FindAssociatedLUN(byte pathId, byte targetId, byte targetLun)
        {
            foreach (byte associatedLUN in m_luns.Keys)
            {
                if (m_luns[associatedLUN].PathId == pathId &&
                    m_luns[associatedLUN].TargetId == targetId &&
                    m_luns[associatedLUN].TargetLun == targetLun)
                {
                    return associatedLUN;
                }
            }
            return null;
        }

        public byte? FindUnusedLUN()
        {
            // Windows supports 0x00 - 0xFE
            for (byte lun = 0; lun < 255; lun++)
            {
                if (!m_luns.ContainsKey(lun))
                {
                    return lun;
                }
            }
            return null;
        }
    }
}
