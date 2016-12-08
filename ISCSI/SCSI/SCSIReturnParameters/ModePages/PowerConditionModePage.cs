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

namespace SCSI
{
    public class PowerConditionModePage : ModePage0
    {
        public byte PmBgPrecedence;
        public bool StandbyY;
        public bool IdleC;
        public bool IdleB;
        public bool IdleA;
        public bool StandbyZ;
        public uint IdleATimer;
        public uint StandbyZTimer;
        public uint IdleBTimer;
        public uint IdleCTimer;
        public uint StandbyYTimer;
        public byte CcfIdle;
        public byte CcfStandby;
        public byte CcfStopped;

        public PowerConditionModePage() : base(ModePageCodeName.PowerConditionModePage, 38)
        { }

        public PowerConditionModePage(byte[] buffer, int offset) : base(buffer, offset)
        {
            throw new NotImplementedException();
        }

        public override byte[] GetBytes()
        {
            byte[] buffer = base.GetBytes();
            buffer[2] = (byte)(PmBgPrecedence << 5 | (Convert.ToByte(StandbyY) & 0x01));
            buffer[3] = (byte)((Convert.ToByte(IdleC) & 0x01) << 4 | (Convert.ToByte(IdleB) & 0x01) << 4 | (Convert.ToByte(IdleA) & 0x01) << 4 | (Convert.ToByte(StandbyZ) & 0x01));
            BigEndianWriter.WriteUInt32(buffer, 4, IdleATimer);
            BigEndianWriter.WriteUInt32(buffer, 8, StandbyZTimer);
            BigEndianWriter.WriteUInt32(buffer, 12, IdleBTimer);
            BigEndianWriter.WriteUInt32(buffer, 16, IdleCTimer);
            BigEndianWriter.WriteUInt32(buffer, 20, StandbyYTimer);
            buffer[39] = (byte)((Convert.ToByte(CcfIdle) & 0x03) << 6 | (Convert.ToByte(CcfStandby) & 0x01) << 4 | (Convert.ToByte(CcfStopped) & 0x01) << 2);
            return buffer;
        }
    }
}
