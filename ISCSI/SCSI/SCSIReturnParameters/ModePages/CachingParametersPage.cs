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
    public class CachingParametersPage : ModePage0
    {
        public bool IC;    // Initiator Control
        public bool ABPF;  // Abort Prefetch
        public bool CAP;   // Caching Analysis Permitted
        public bool DISC;  // Discontinuity
        public bool SIZE;  // Size Enable
        public bool WCE;   // Write Cache Enable
        public bool MF;    // Multiplication Factor
        public bool RCD;   // READ Cache Disable
        public byte DemandReadRetentionPriority;
        public byte WriteRetentionPriority;
        public ushort DisablePrefetchTransferLength;
        public ushort MinimumPrefetch;
        public ushort MaximumPrefetch;
        public ushort MaximumPrefetchCeiling;
        public bool FSW;   // Force Sequential Write
        public bool LBCSS; // Not used
        public bool DRA;   // Disable READ-Ahead
        public bool NV_DIS;
        public byte NumberOfCacheSegments;
        public ushort CacheSegmentSize;

        public CachingParametersPage() : base(ModePageCodeName.CachingParametersPage, 18)
        {
        }

        public CachingParametersPage(byte[] buffer, int offset) : base(buffer, offset)
        {
            IC = (buffer[offset + 2] & 0x80) != 0;
            ABPF = (buffer[offset + 2] & 0x40) != 0;
            CAP = (buffer[offset + 2] & 0x20) != 0;
            DISC = (buffer[offset + 2] & 0x10) != 0;
            SIZE = (buffer[offset + 2] & 0x08) != 0;
            WCE = (buffer[offset + 2] & 0x04) != 0;
            MF = (buffer[offset + 2] & 0x02) != 0;
            RCD = (buffer[offset + 2] & 0x01) != 0;

            DemandReadRetentionPriority = (byte)((buffer[offset + 3] >> 4) & 0x0F);
            WriteRetentionPriority = (byte)(buffer[offset + 3] & 0x0F);

            DisablePrefetchTransferLength = BigEndianConverter.ToUInt16(buffer, offset + 4);
            MinimumPrefetch = BigEndianConverter.ToUInt16(buffer, offset + 6);
            MaximumPrefetch = BigEndianConverter.ToUInt16(buffer, offset + 8);
            MaximumPrefetchCeiling = BigEndianConverter.ToUInt16(buffer, offset + 10);

            FSW = (buffer[offset + 12] & 0x80) != 0;
            LBCSS = (buffer[offset + 12] & 0x40) != 0;
            DRA = (buffer[offset + 12] & 0x20) != 0;
            NV_DIS = (buffer[offset + 12] & 0x01) != 0;

            NumberOfCacheSegments = buffer[offset + 13];
            CacheSegmentSize = BigEndianConverter.ToUInt16(buffer, offset + 14);
        }
    
        public override byte[] GetBytes()
        {
            byte[] buffer = base.GetBytes();
            
            if (IC)
            {
                buffer[2] |= 0x80;
            }
            if (ABPF)
            {
                buffer[2] |= 0x40;
            }
            if (CAP)
            {
                buffer[2] |= 0x20;
            }
            if (DISC)
            {
                buffer[2] |= 0x10;
            }
            if (SIZE)
            {
                buffer[2] |= 0x08;
            }
            if (WCE)
            {
                buffer[2] |= 0x04;
            }
            if (MF)
            {
                buffer[2] |= 0x02;
            }
            if (RCD)
            {
                buffer[2] |= 0x01;
            }

            buffer[3] = (byte)((DemandReadRetentionPriority & 0x0F) << 4);
            buffer[3] = (byte)(WriteRetentionPriority & 0x0F);

            BigEndianWriter.WriteUInt16(buffer, 4, DisablePrefetchTransferLength);
            BigEndianWriter.WriteUInt16(buffer, 6, MinimumPrefetch);
            BigEndianWriter.WriteUInt16(buffer, 8, MaximumPrefetch);
            BigEndianWriter.WriteUInt16(buffer, 10, MaximumPrefetchCeiling);

            if (FSW)
            {
                buffer[12] |= 0x80;
            }
            if (LBCSS)
            {
                buffer[12] |= 0x40;
            }
            if (DRA)
            {
                buffer[12] |= 0x20;
            }
            if (NV_DIS)
            {
                buffer[12] |= 0x01;
            }

            buffer[13] = NumberOfCacheSegments;
            BigEndianWriter.WriteUInt16(buffer, 14, CacheSegmentSize);

            return buffer;
        }
    }
}
