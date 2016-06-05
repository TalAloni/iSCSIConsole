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

namespace ISCSI
{
    public class ISCSIPDU
    {
        public bool ImmediateDelivery;   // I-Bit - first byte
        public ISCSIOpCodeName OpCode;              // first byte
        public bool Final;               // F-Bit - first byte
        public byte[] OpCodeSpecificHeader = new byte[3]; // Final bit is removed!
        public byte TotalAHSLength;
        public uint DataSegmentLength;    // 3 bytes
        public byte[] LUNOrOpCodeSpecific = new byte[8];
        public uint InitiatorTaskTag;    // 4 bytes
        public byte[] OpCodeSpecific = new byte[28]; // 28 bytes
        public byte[] Data = new byte[0];

        protected ISCSIPDU()
        { 
        }

        protected ISCSIPDU(byte[] buffer)
        {
            ImmediateDelivery = (buffer[0] & 0x40) != 0;
            OpCode = (ISCSIOpCodeName)(buffer[0] & 0x3F);
            Final = (buffer[1] & 0x80) != 0;
            Array.Copy(buffer, 1, OpCodeSpecificHeader, 0, 3);
            OpCodeSpecificHeader[0] &= 0x7F; // remove the final bit

            TotalAHSLength = buffer[4];
            DataSegmentLength = (uint)(buffer[5] << 16 | buffer[6] << 8 | buffer[7]);
            Array.Copy(buffer, 8, LUNOrOpCodeSpecific, 0, 8);
            InitiatorTaskTag = BigEndianConverter.ToUInt32(buffer, 16);
            Array.Copy(buffer, 20, OpCodeSpecific, 0, 28);

            Data = new byte[DataSegmentLength];
            Array.Copy(buffer, 48, Data, 0, DataSegmentLength);
        }

        virtual public byte[] GetBytes()
        {
            DataSegmentLength = (uint)Data.Length; // We must update DataSegmentLength for all subsequest length calculations
            
            byte[] buffer = new byte[this.Length]; // include padding bytes
            buffer[0x00] = (byte)OpCode;
            if (ImmediateDelivery)
            {
                buffer[0] |= 0x40;
            }
            Array.Copy(OpCodeSpecificHeader, 0, buffer, 1, 3);
            if (Final) // Note: Login request / response use the the Final bit as the Transmit bit
            {
                buffer[1] |= 0x80;
            }
            buffer[4] = TotalAHSLength;
            buffer[5] = (byte)((DataSegmentLength >> 16) & 0xFF);
            buffer[6] = (byte)((DataSegmentLength >> 8) & 0xFF);
            buffer[7] = (byte)((DataSegmentLength >> 0) & 0xFF);
            Array.Copy(LUNOrOpCodeSpecific, 0, buffer, 8, 8);
            BigEndianWriter.WriteUInt32(buffer, 16, InitiatorTaskTag);
            Array.Copy(OpCodeSpecific, 0, buffer, 20, 28);
            Array.Copy(Data, 0, buffer, 48, DataSegmentLength);

            return buffer;
        }

        public int Length
        {
            get
            {
                int length = (int)(TotalAHSLength + DataSegmentLength + 48);
                length = (int)Math.Ceiling((double)length / 4) * 4; // iSCSIPDUs are padded to the closest integer number of four byte words.
                return length;
            }
        }

        public static ISCSIPDU GetPDU(byte[] buffer)
        {
            byte opCode = (byte)(buffer[0x00] & 0x3F);
            switch ((ISCSIOpCodeName)opCode)
            {
                case ISCSIOpCodeName.NOPOut:
                    return new NOPOutPDU(buffer);
                case ISCSIOpCodeName.SCSICommand:
                    return new SCSICommandPDU(buffer);
                case ISCSIOpCodeName.LoginRequest:
                    return new LoginRequestPDU(buffer);
                case ISCSIOpCodeName.TextRequest:
                    return new TextRequestPDU(buffer);
                case ISCSIOpCodeName.SCSIDataOut:
                    return new SCSIDataOutPDU(buffer);
                case ISCSIOpCodeName.LogoutRequest:
                    return new LogoutRequestPDU(buffer);
                case ISCSIOpCodeName.NOPIn:
                    return new NOPInPDU(buffer);
                case ISCSIOpCodeName.SCSIResponse:
                    return new SCSIResponsePDU(buffer);
                case ISCSIOpCodeName.LoginResponse:
                    return new LoginResponsePDU(buffer);
                case ISCSIOpCodeName.TextResponse:
                    return new TextResponsePDU(buffer);
                case ISCSIOpCodeName.SCSIDataIn:
                    return new SCSIDataInPDU(buffer);
                case ISCSIOpCodeName.LogoutResponse:
                    return new LogoutResponsePDU(buffer);
                case ISCSIOpCodeName.ReadyToTransfer:
                    return new ReadyToTransferPDU(buffer);
                default:
                    return new ISCSIPDU(buffer);
            }
        }

        public static int GetPDULength(byte[] buffer)
        {
            return GetPDULength(buffer, 0);
        }

        public static int GetPDULength(byte[] buffer, int offset)
        {
            byte totalAHSLength = buffer[offset + 4];
            int dataSegmentLength = buffer[offset + 5] << 16 | buffer[offset + 6] << 8 | buffer[offset + 7];

            // Basic Header segment size is 48 bytes
            int length = totalAHSLength + dataSegmentLength + 48;
            length = (int)Math.Ceiling((double)length / 4) * 4; // iSCSIPDUs are padded to the closest integer number of four byte words.

            return length;
        }
    }
}
