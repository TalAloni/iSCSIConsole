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
    public class ControlModePage : ModePage0
    {
        public byte TST; // 3 bits
        public bool TmfOnly;
        public bool Reserved1;
        public bool DSense;
        public bool GLTSD;
        public bool RLEC;
        public byte QueueAlgorithmModifier; // 4 bits
        public bool Reserved2;
        public byte QErr; // 2 bits
        public bool DQUE; // obsolete
        public bool VS; // obsolete
        public bool RAC; // obsolete
        public bool UA_INTLCK_CTRL; // obsolete
        public bool SWP;
        public bool RAERP; // obsolete
        public bool UAAERP; // obsolete
        public bool EAERP; // obsolete
        public bool ATO;
        public bool TAS;
        public byte Reserved3; // 3 bits
        public byte AutoLoadMode; // 3 bits
        public ushort Obsolete1;
        public ushort Obsolete2;
        public ushort Obsolete3;

        public ControlModePage() : base(ModePageCodeName.ControlModePage, 10)
        {
        }

        public ControlModePage(byte[] buffer, int offset) : base(buffer, offset)
        {
            throw new NotImplementedException();
        }

        public override byte[] GetBytes()
        {
            byte[] buffer = base.GetBytes();
            buffer[2] = (byte)(TST << 5 | Convert.ToByte(TmfOnly) << 4 | Convert.ToByte(Reserved1) << 3 | Convert.ToByte(DSense) << 2 | Convert.ToByte(GLTSD) << 1 | Convert.ToByte(RLEC));
            buffer[3] = (byte)(QueueAlgorithmModifier << 4 | Convert.ToByte(Reserved2) << 3 | (Convert.ToByte(QErr) & 0x3) << 1 | Convert.ToByte(DQUE));
            buffer[4] = (byte)(Convert.ToByte(VS) << 7 | Convert.ToByte(RAC) << 6 | (Convert.ToByte(UA_INTLCK_CTRL) & 0x3) << 4 | Convert.ToByte(SWP) << 3 | Convert.ToByte(RAERP) << 2 | Convert.ToByte(UAAERP) << 1 | Convert.ToByte(EAERP));
            buffer[5] = (byte)(Convert.ToByte(ATO) << 7 | Convert.ToByte(TAS) << 6 | (Convert.ToByte(Reserved3) & 0x7) << 3 | (Convert.ToByte(AutoLoadMode) & 0x7));
            BigEndianWriter.WriteUInt16(buffer, 6, Obsolete1);
            BigEndianWriter.WriteUInt16(buffer, 8, Obsolete2);
            BigEndianWriter.WriteUInt16(buffer, 10, Obsolete3);

            return buffer;
        }
    }
}
