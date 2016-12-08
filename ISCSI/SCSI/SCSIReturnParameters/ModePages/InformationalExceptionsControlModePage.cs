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
    public class InformationalExceptionsControlModePage : ModePage0
    {
        public bool PERF;     // Performance
        public bool EBF;      // Enable Background Function
        public bool EWASC;    // Enable Warning
        public bool DExcpt;   // Disable Exception Control
        public bool Test;
        public bool EBackErr; // enable background error
        public bool LogErr;
        
        public byte MRIE;
        public uint IntervalTimer;
        public uint ReportCount;

        public InformationalExceptionsControlModePage() : base(ModePageCodeName.InformationalExceptionsControlModePage, 10)
        {
        }

        public InformationalExceptionsControlModePage(byte[] buffer, int offset) : base(buffer, offset)
        {
            PERF = (buffer[offset + 2] & 0x80) != 0;
            EBF = (buffer[offset + 2] & 0x20) != 0;
            EWASC = (buffer[offset + 2] & 0x10) != 0;
            DExcpt = (buffer[offset + 2] & 0x08) != 0;
            Test = (buffer[offset + 2] & 0x04) != 0;
            EBackErr = (buffer[offset + 2] & 0x02) != 0;
            LogErr = (buffer[offset + 2] & 0x01) != 0;

            MRIE = (byte)(buffer[offset + 3] & 0x0F);
            IntervalTimer = BigEndianConverter.ToUInt32(buffer, 4);
            ReportCount = BigEndianConverter.ToUInt32(buffer, 8);
        }

        public override byte[] GetBytes()
        {
            byte[] buffer = base.GetBytes();
            if (PERF)
            {
                buffer[2] |= 0x80;
            }
            if (EBF)
            {
                buffer[2] |= 0x20;
            }
            if (EWASC)
            {
                buffer[2] |= 0x10;
            }
            if (DExcpt)
            {
                buffer[2] |= 0x08;
            }
            if (Test)
            {
                buffer[2] |= 0x04;
            }
            if (EBackErr)
            {
                buffer[2] |= 0x02;
            }
            if (LogErr)
            {
                buffer[2] |= 0x01;
            }

            buffer[3] = (byte)(MRIE & 0x0F);
            BigEndianWriter.WriteUInt32(buffer, 4, IntervalTimer);
            BigEndianWriter.WriteUInt32(buffer, 8, ReportCount);

            return buffer;
        }
    }
}

