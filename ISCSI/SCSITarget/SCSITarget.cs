/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace SCSI
{
    public abstract class SCSITarget
    {
        public event EventHandler<StandardInquiryEventArgs> OnStandardInquiry;

        public event EventHandler<DeviceIdentificationInquiryEventArgs> OnDeviceIdentificationInquiry;

        public abstract SCSIStatusCodeName ExecuteCommand(byte[] commandBytes, LUNStructure lun, byte[] data, out byte[] response);

        public void NotifyStandardInquiry(object sender, StandardInquiryEventArgs args)
        {
            // To be thread-safe we must capture the delegate reference first
            EventHandler<StandardInquiryEventArgs> handler = OnStandardInquiry;
            if (handler != null)
            {
                handler(sender, args);
            }
        }

        public void NotifyDeviceIdentificationInquiry(object sender, DeviceIdentificationInquiryEventArgs args)
        {
            // To be thread-safe we must capture the delegate reference first
            EventHandler<DeviceIdentificationInquiryEventArgs> handler = OnDeviceIdentificationInquiry;
            if (handler != null)
            {
                handler(sender, args);
            }
        }
    }
}
