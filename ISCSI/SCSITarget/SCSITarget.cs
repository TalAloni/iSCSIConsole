/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Utilities;

namespace SCSI
{
    public abstract class SCSITarget : SCSITargetInterface
    {
        private class SCSICommand
        {
            public byte[] CommandBytes;
            public LUNStructure LUN;
            public byte[] Data;
            public object Task;
            public OnCommandCompleted OnCommandCompleted;
        }

        private BlockingQueue<SCSICommand> m_commandQueue = new BlockingQueue<SCSICommand>();

        public event EventHandler<StandardInquiryEventArgs> OnStandardInquiry;

        public event EventHandler<UnitSerialNumberInquiryEventArgs> OnUnitSerialNumberInquiry;

        public event EventHandler<DeviceIdentificationInquiryEventArgs> OnDeviceIdentificationInquiry;

        public SCSITarget()
        {
            Thread workerThread = new Thread(ProcessCommandQueue);
            workerThread.IsBackground = true;
            workerThread.Start();
        }

        private void ProcessCommandQueue()
        {
            while (true)
            {
                SCSICommand command;
                bool stopping = !m_commandQueue.TryDequeue(out command);
                if (stopping)
                {
                    return;
                }

                byte[] responseBytes;
                SCSIStatusCodeName status = ExecuteCommand(command.CommandBytes, command.LUN, command.Data, out responseBytes);
                command.OnCommandCompleted(status, responseBytes, command.Task);
            }
        }

        public void QueueCommand(byte[] commandBytes, LUNStructure lun, byte[] data, object task, OnCommandCompleted OnCommandCompleted)
        {
            SCSICommand command = new SCSICommand();
            command.CommandBytes = commandBytes;
            command.LUN = lun;
            command.Data = data;
            command.OnCommandCompleted = OnCommandCompleted;
            command.Task = task;
            m_commandQueue.Enqueue(command);
        }

        public abstract SCSIStatusCodeName ExecuteCommand(byte[] commandBytes, LUNStructure lun, byte[] data, out byte[] response);

        protected void NotifyStandardInquiry(object sender, StandardInquiryEventArgs args)
        {
            // To be thread-safe we must capture the delegate reference first
            EventHandler<StandardInquiryEventArgs> handler = OnStandardInquiry;
            if (handler != null)
            {
                handler(sender, args);
            }
        }

        protected void NotifyUnitSerialNumberInquiry(object sender, UnitSerialNumberInquiryEventArgs args)
        {
            // To be thread-safe we must capture the delegate reference first
            EventHandler<UnitSerialNumberInquiryEventArgs> handler = OnUnitSerialNumberInquiry;
            if (handler != null)
            {
                handler(sender, args);
            }
        }

        protected void NotifyDeviceIdentificationInquiry(object sender, DeviceIdentificationInquiryEventArgs args)
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
