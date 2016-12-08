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
    public class ReportLUNsParameter
    {
        /// <summary>
        /// Minimum allocation length defined by the REPORT LUNS command
        /// </summary>
        public const int MinimumAllocationLength = 16;

        public List<LUNStructure> LUNList = new List<LUNStructure>();

        public ReportLUNsParameter()
        { 
        }

        public ReportLUNsParameter(int numberOfLUNs)
        {
            if (numberOfLUNs > LUNStructure.SingleLevelAddressingLimit)
            {
                throw new NotImplementedException("Unsupported Number of LUNs");
            }

            for (int index = 0; index < numberOfLUNs; index++)
            {
                LUNList.Add((ushort)index);
            }
        }

        public ReportLUNsParameter(byte[] buffer)
        {
            uint listLength = BigEndianConverter.ToUInt32(buffer, 0);
            // uint reserved = BigEndianConverter.ToUInt32(buffer, 4);
            int offset = 8;
            int lunCount = (int)(listLength / 8);
            for (int index = 0; index < lunCount; index++)
            {
                LUNStructure structure = new LUNStructure(buffer, offset);
                LUNList.Add(structure);
                offset += 8;
            }
        }

        public byte[] GetBytes()
        {
            uint LUNListLength = (uint)LUNList.Count * 8;
            byte[] buffer = new byte[8 + LUNListLength];
            BigEndianWriter.WriteUInt32(buffer, 0, LUNListLength);
            int offset = 8;
            for (int index = 0; index < LUNList.Count; index++)
            {
                byte[] structureBytes = LUNList[index].GetBytes();
                ByteWriter.WriteBytes(buffer, offset, structureBytes);
                offset += 8;
            }
            return buffer;
        }

        public static uint GetRequiredAllocationLength(byte[] buffer)
        {
            uint listLength = BigEndianConverter.ToUInt32(buffer, 0);
            return 8 + listLength;
        }
    }
}
