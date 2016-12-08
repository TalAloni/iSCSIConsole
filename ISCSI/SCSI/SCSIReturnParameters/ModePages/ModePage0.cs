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
    public class ModePage0 : ModePage // page_0 mode page format
    {
        public bool PS;    // Parameter Savable
        public bool SPF;   // SubPage Format
        public ModePageCodeName PageCode;
        public byte PageLength; // excluding this and previous bytes

        protected ModePage0(ModePageCodeName pageCode, byte pageLength)
        {
            PageCode = pageCode;
            PageLength = pageLength;
        }

        public ModePage0(byte[] buffer, int offset)
        {
            PS = (buffer[offset + 0] & 0x80) != 0;
            SPF = (buffer[offset + 0] & 0x40) != 0;
            PageCode = (ModePageCodeName)(buffer[offset + 0] & 0x3F);
            PageLength = buffer[offset + 1];
        }

        override public byte[] GetBytes()
        {
            byte[] buffer = new byte[2 + PageLength];
            if (PS)
            {
                buffer[0] |= 0x80;
            }
            if (SPF)
            {
                buffer[0] |= 0x40;
            }
            buffer[0] |= (byte)((byte)PageCode & 0x3F);
            buffer[1] = PageLength;

            return buffer;
        }

        public override int Length
        {
            get
            {
                return 2 + PageLength;
            }
        }
    }
}
