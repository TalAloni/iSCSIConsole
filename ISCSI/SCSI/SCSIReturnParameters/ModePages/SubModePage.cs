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
    public class SubModePage : ModePage // SUB_PAGE mode page format
    {
        public bool PS;    // Parameter Savable
        public bool SPF;   // SubPage Format
        public ModePageCodeName PageCode;
        public byte SubPageCode;
        public ushort PageLength; // excluding this and previous bytes

        protected SubModePage(ModePageCodeName pageCode, byte subPageCode, ushort pageLength)
        {
            PageCode = pageCode;
            PageLength = pageLength;
        }

        public SubModePage(byte[] buffer, int offset)
        {
            PS = (buffer[offset + 0] & 0x80) != 0;
            SPF = (buffer[offset + 0] & 0x40) != 0;
            PageCode = (ModePageCodeName)(buffer[offset + 0] & 0x3F);
            SubPageCode = buffer[offset + 1];
            PageLength = BigEndianConverter.ToUInt16(buffer, 2);
        }

        override public byte[] GetBytes()
        {
            byte[] buffer = new byte[4 + PageLength];
            if (PS)
            {
                buffer[0] |= 0x80;
            }
            if (SPF)
            {
                buffer[0] |= 0x40;
            }
            buffer[0] |= (byte)((byte)PageCode & 0x3F);
            buffer[1] = SubPageCode;
            BigEndianWriter.WriteUInt16(buffer, 2, PageLength);

            return buffer;
        }

        public override int Length
        {
            get
            {
                return 4 + PageLength;
            }
        }
    }
}
