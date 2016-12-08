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
    public class VendorSpecificPage : ModePage
    {
        public VendorSpecificPage()
        {
        }

        public VendorSpecificPage(byte[] buffer, int offset)
        {
        }

        public override byte[] GetBytes()
        {
            byte[] buffer = new byte[0];
            return buffer;
        }

        // Note: Vendor specific page does not require page format
        public override int Length
        {
            get
            {
                return 0;
            }
        }
    }
}
