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
    public class PowerConsumptionModePage : SubModePage
    {
        public byte PowerConsumptionIdentifier;

        public PowerConsumptionModePage() : base(ModePageCodeName.PowerConditionModePage, 0x01, 12)
        {
        }

        public PowerConsumptionModePage(byte[] buffer, int offset) : base(buffer, offset)
        {
            PowerConsumptionIdentifier = buffer[offset + 7];
        }

        public override byte[] GetBytes()
        {
            byte[] buffer = base.GetBytes();
            buffer[7] = PowerConsumptionIdentifier;

            return buffer;
        }
    }
}
