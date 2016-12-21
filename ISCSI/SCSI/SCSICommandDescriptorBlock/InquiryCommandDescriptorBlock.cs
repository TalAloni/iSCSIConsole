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
    public class InquiryCommand : SCSICommandDescriptorBlock
    {
        public bool EVPD; // Enable Vital Product Data
        public VitalProductDataPageName PageCode;

        public InquiryCommand() : base()
        {
            OpCode = SCSIOpCodeName.Inquiry;
        }

        public InquiryCommand(byte[] buffer, int offset)
        { 
            OpCode = (SCSIOpCodeName)buffer[offset + 0];
            EVPD = (buffer[offset + 1] & 0x01) != 0;
            PageCode = (VitalProductDataPageName)buffer[offset + 2];
            AllocationLength = BigEndianConverter.ToUInt16(buffer, offset + 3);
            Control = buffer[offset + 5];
        }

        public override byte[] GetBytes()
        {
            byte[] buffer = new byte[6];
            buffer[0] = (byte)OpCode;
            if (EVPD)
            {
                buffer[1] |= 0x01;
            }
            buffer[2] = (byte)PageCode;
            BigEndianWriter.WriteUInt16(buffer, 3, AllocationLength);
            buffer[5] = Control;
            return buffer;
        }

        public ushort AllocationLength
        {
            get
            {
                return (ushort)TransferLength;
            }
            set
            {
                TransferLength = value;
            }
        }
    }
}
