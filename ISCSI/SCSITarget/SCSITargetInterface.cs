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
    public delegate void OnCommandCompleted(SCSIStatusCodeName status, byte[] responseBytes, object task);

    public interface SCSITargetInterface
    {
        event EventHandler<StandardInquiryEventArgs> OnStandardInquiry;

        event EventHandler<UnitSerialNumberInquiryEventArgs> OnUnitSerialNumberInquiry;

        event EventHandler<DeviceIdentificationInquiryEventArgs> OnDeviceIdentificationInquiry;

        void QueueCommand(byte[] commandBytes, LUNStructure lun, byte[] data, object task, OnCommandCompleted OnCommandCompleted);

        SCSIStatusCodeName ExecuteCommand(byte[] commandBytes, LUNStructure lun, byte[] data, out byte[] response);
    }
}
