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
    /// <summary>
    /// Eight byte logical unit number structure from SAM-2
    /// </summary>
    public struct LUNStructure
    {
        public const int Length = 8;
        public const int SingleLevelAddressingLimit = 16384;

        private ushort FirstLevelAddressing;
        private ushort SecondLevelAddressing;
        private ushort ThirdLevelAddressing;
        private ushort FourthLevelAddressing;

        public LUNStructure(ushort lun)
        {
            FirstLevelAddressing = GetFirstLevelAddressing(lun);
            SecondLevelAddressing = 0;
            ThirdLevelAddressing = 0;
            FourthLevelAddressing = 0;
        }

        public LUNStructure(byte[] buffer, int offset)
        {
            FirstLevelAddressing = BigEndianConverter.ToUInt16(buffer, offset + 0);
            SecondLevelAddressing = BigEndianConverter.ToUInt16(buffer, offset + 2);
            ThirdLevelAddressing = BigEndianConverter.ToUInt16(buffer, offset + 4);
            FourthLevelAddressing = BigEndianConverter.ToUInt16(buffer, offset + 6);
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Length];
            BigEndianWriter.WriteUInt16(buffer, 0, FirstLevelAddressing);
            BigEndianWriter.WriteUInt16(buffer, 2, SecondLevelAddressing);
            BigEndianWriter.WriteUInt16(buffer, 4, ThirdLevelAddressing);
            BigEndianWriter.WriteUInt16(buffer, 6, FourthLevelAddressing);
            return buffer;
        }

        public AddressingMethod AddressingMethod
        {
            get
            {
                return (AddressingMethod)(FirstLevelAddressing >> 14);
            }
        }

        public bool IsSingleLevelLUN
        {
            get
            {
                if (AddressingMethod == AddressingMethod.PeripheralDeviceAddressing)
                {
                    byte bus = (byte)((FirstLevelAddressing >> 8) & 0x3F);
                    return (bus == 0);
                }
                else if (AddressingMethod == AddressingMethod.FlatSpaceAddressing)
                {
                    return true;
                }
                return false;
            }
        }
        
        public ushort SingleLevelLUN
        {
            get
            {
                AddressingMethod addressing = this.AddressingMethod;
                if (addressing == AddressingMethod.PeripheralDeviceAddressing)
                {
                    byte bus = (byte)((FirstLevelAddressing >> 8) & 0x3F);
                    if (bus == 0)
                    {
                        return (byte)(FirstLevelAddressing & 0xFF);
                    }
                }
                else if (addressing == AddressingMethod.FlatSpaceAddressing)
                {
                    return (ushort)(FirstLevelAddressing & 0x3FFF);
                }
                throw new Exception("Not a single level LUN address");
            }
            set
            {
                if (value > SingleLevelAddressingLimit)
                {
                    throw new ArgumentException("Cannot address more than 16384 logical units using single level addressing");
                }
                FirstLevelAddressing = GetFirstLevelAddressing(value);
                SecondLevelAddressing = 0;
                ThirdLevelAddressing = 0;
                FourthLevelAddressing = 0;
            }
        }

        public static ushort GetPeripheralDeviceAddressing(byte bus, byte lun)
        {
            if (bus > 63)
            {
                throw new ArgumentException("Cannot address more than 64 buses using peripheral device addressing");
            }

            return (ushort)((byte)AddressingMethod.PeripheralDeviceAddressing << 14 | bus << 8 | lun);
        }

        public static ushort GetFirstLevelAddressing(ushort lun)
        {
            if (lun > SingleLevelAddressingLimit)
            {
                throw new ArgumentException("Cannot address more than 16384 logical units using single level addressing");
            }

            if (lun <= 255)
            {
                return GetPeripheralDeviceAddressing(0, (byte)lun);
            }
            else
            {
                return (ushort)((byte)AddressingMethod.FlatSpaceAddressing << 14 | lun);
            }
        }

        public static implicit operator ushort(LUNStructure structure)
        {
            if (structure.IsSingleLevelLUN)
            {
                return structure.SingleLevelLUN;
            }
            else
            {
                throw new NotImplementedException("Unsupported LUN structure");
            }
        }

        public static implicit operator LUNStructure(ushort LUN)
        {
            return new LUNStructure(LUN);
        }
    }
}
