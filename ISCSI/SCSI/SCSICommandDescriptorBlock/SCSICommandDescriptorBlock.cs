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
    public abstract class SCSICommandDescriptorBlock
    {
        public SCSIOpCodeName OpCode;
        public byte MiscellaneousCDBInformationHeader;
        public ServiceAction ServiceAction;
        public uint AdditionalCDBdata;
        public uint LogicalBlockAddress;
        public uint TransferLength; // number of blocks, also doubles as Parameter list length /  Allocation length
        public byte MiscellaneousCDBinformation;
        public byte Control;

        protected SCSICommandDescriptorBlock()
        { 
        }

        public abstract byte[] GetBytes();

        public static SCSICommandDescriptorBlock FromBytes(byte[] buffer, int offset)
        {
            byte opCode = buffer[offset + 0];
            switch ((SCSIOpCodeName)opCode)
            {
                case SCSIOpCodeName.TestUnitReady:
                    return new SCSICommandDescriptorBlock6(buffer, offset);
                case SCSIOpCodeName.RequestSense:
                    return new SCSICommandDescriptorBlock6(buffer, offset);
                case SCSIOpCodeName.Read6:
                    return new SCSICommandDescriptorBlock6(buffer, offset);
                case SCSIOpCodeName.Write6:
                    return new SCSICommandDescriptorBlock6(buffer, offset);
                case SCSIOpCodeName.Inquiry:
                    return new InquiryCommand(buffer, offset);
                case SCSIOpCodeName.Reserve6:
                    return new SCSICommandDescriptorBlock6(buffer, offset);
                case SCSIOpCodeName.Release6:
                    return new SCSICommandDescriptorBlock6(buffer, offset);
                case SCSIOpCodeName.ModeSense6:
                    return new ModeSense6CommandDescriptorBlock(buffer, offset);
                case SCSIOpCodeName.ReadCapacity10:
                    return new SCSICommandDescriptorBlock10(buffer, offset);
                case SCSIOpCodeName.Read10:
                    return new SCSICommandDescriptorBlock10(buffer, offset);
                case SCSIOpCodeName.Write10:
                    return new SCSICommandDescriptorBlock10(buffer, offset);
                case SCSIOpCodeName.Verify10:
                    return new SCSICommandDescriptorBlock10(buffer, offset);
                case SCSIOpCodeName.SynchronizeCache10:
                    return new SCSICommandDescriptorBlock10(buffer, offset);
                case SCSIOpCodeName.WriteSame10:
                    return new SCSICommandDescriptorBlock10(buffer, offset);
                case SCSIOpCodeName.Read16:
                    return new SCSICommandDescriptorBlock16(buffer, offset);
                case SCSIOpCodeName.Write16:
                    return new SCSICommandDescriptorBlock16(buffer, offset);
                case SCSIOpCodeName.Verify16:
                    return new SCSICommandDescriptorBlock16(buffer, offset);
                case SCSIOpCodeName.WriteSame16:
                    return new SCSICommandDescriptorBlock16(buffer, offset);
                case SCSIOpCodeName.ServiceActionIn16:
                    return new SCSICommandDescriptorBlock16(buffer, offset);
                case SCSIOpCodeName.ReportLUNs:
                    return new SCSICommandDescriptorBlock12(buffer, offset);
                default:
                    throw new UnsupportedSCSICommandException(String.Format("Unknown SCSI command: 0x{0}", opCode.ToString("x")));
            }
        }

        public static SCSICommandDescriptorBlock Create(SCSIOpCodeName opCode)
        {
            switch (opCode)
            {
                case SCSIOpCodeName.TestUnitReady:
                    return new SCSICommandDescriptorBlock6(opCode);
                case SCSIOpCodeName.RequestSense:
                    return new SCSICommandDescriptorBlock6(opCode);
                case SCSIOpCodeName.Read6:
                    return new SCSICommandDescriptorBlock6(opCode);
                case SCSIOpCodeName.Write6:
                    return new SCSICommandDescriptorBlock6(opCode);
                case SCSIOpCodeName.Inquiry:
                    return new InquiryCommand();
                case SCSIOpCodeName.Reserve6:
                    return new SCSICommandDescriptorBlock6(opCode);
                case SCSIOpCodeName.Release6:
                    return new SCSICommandDescriptorBlock6(opCode);
                case SCSIOpCodeName.ModeSense6:
                    return new ModeSense6CommandDescriptorBlock();
                case SCSIOpCodeName.ReadCapacity10:
                    return new SCSICommandDescriptorBlock10(opCode);
                case SCSIOpCodeName.Read10:
                    return new SCSICommandDescriptorBlock10(opCode);
                case SCSIOpCodeName.Write10:
                    return new SCSICommandDescriptorBlock10(opCode);
                case SCSIOpCodeName.Verify10:
                    return new SCSICommandDescriptorBlock10(opCode);
                case SCSIOpCodeName.SynchronizeCache10:
                    return new SCSICommandDescriptorBlock10(opCode);
                case SCSIOpCodeName.WriteSame10:
                    return new SCSICommandDescriptorBlock10(opCode);
                case SCSIOpCodeName.Read16:
                    return new SCSICommandDescriptorBlock16(opCode);
                case SCSIOpCodeName.Write16:
                    return new SCSICommandDescriptorBlock16(opCode);
                case SCSIOpCodeName.Verify16:
                    return new SCSICommandDescriptorBlock16(opCode);
                case SCSIOpCodeName.WriteSame16:
                    return new SCSICommandDescriptorBlock16(opCode);
                case SCSIOpCodeName.ServiceActionIn16:
                    return new SCSICommandDescriptorBlock16(opCode);
                case SCSIOpCodeName.ReportLUNs:
                    return new SCSICommandDescriptorBlock12(opCode);
                default:
                    throw new NotImplementedException("SCSI opcode not implemented");
            }
        }

        public ulong LogicalBlockAddress64
        {
            get
            {
                if (this is SCSICommandDescriptorBlock16)
                {
                    ulong result = (ulong)this.LogicalBlockAddress << 32;
                    result += this.AdditionalCDBdata;
                    return result;
                }
                else
                {
                    return this.LogicalBlockAddress;
                }
            }
            set
            {
                if (this is SCSICommandDescriptorBlock16)
                {
                    this.LogicalBlockAddress = (uint)(value >> 32);
                    this.AdditionalCDBdata = (uint)(value & 0xFFFFFFFF);
                }
                else
                {
                    this.LogicalBlockAddress = (uint)value;
                }
            }
        }
    }
}
