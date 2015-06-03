/* Copyright (C) 2012-2015 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    public class ReportLUNsParameter
    {
        public List<byte> LUNList = new List<byte>();

        public ReportLUNsParameter()
        { 
        }

        public ReportLUNsParameter(int numberOfLUNs)
        {
            if (numberOfLUNs > 256)
            {
                throw new NotImplementedException("Unsupported Number of LUNs");
            }

            for (int index = 0; index < numberOfLUNs; index++)
            {
                LUNList.Add((byte)index);
            }
        }

        public byte[] GetBytes()
        {
            uint LUNListLength = (uint)LUNList.Count * 8;
            byte[] buffer = new byte[8 + LUNListLength];
            Array.Copy(BigEndianConverter.GetBytes(LUNListLength), 0, buffer, 0, 4);
            int offset = 8;
            for (int index = 0; index < LUNList.Count; index++)
            {
                byte LUNIndex = LUNList[index];
                // Single Level LUN Structure as per SAM-2 (i.e, byte 0 is zero, byte 1 contains the LUN value, and the remaining 6 bytes are zero)
                buffer[offset + 1] = LUNIndex;
                offset += 8;
            }
            return buffer;
        }
    }
}
