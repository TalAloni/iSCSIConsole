/* Copyright (C) 2012-2017 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using SCSI;
using Utilities;

namespace ISCSI.Server
{
    public class ISCSITarget : SCSITargetInterface
    {
        private string m_targetName; // ISCSI name
        private SCSITargetInterface m_target;
        // SCSI events:
        public event EventHandler<StandardInquiryEventArgs> OnStandardInquiry;
        public event EventHandler<UnitSerialNumberInquiryEventArgs> OnUnitSerialNumberInquiry;
        public event EventHandler<DeviceIdentificationInquiryEventArgs> OnDeviceIdentificationInquiry;
        // iSCSI events:
        public event EventHandler<AuthorizationRequestArgs> OnAuthorizationRequest;
        public event EventHandler<TextRequestArgs> OnTextRequest;
        public event EventHandler<SessionTerminationArgs> OnSessionTermination;

        public ISCSITarget(string targetName, List<Disk> disks) : this(targetName, new VirtualSCSITarget(disks))
        {
        }

        public ISCSITarget(string targetName, SCSITargetInterface scsiTarget)
        {
            m_targetName = targetName;
            m_target = scsiTarget;
            m_target.OnStandardInquiry += new EventHandler<StandardInquiryEventArgs>(Target_OnStandardInquiry);
            m_target.OnDeviceIdentificationInquiry += new EventHandler<DeviceIdentificationInquiryEventArgs>(Target_OnDeviceIdentificationInquiry);
            m_target.OnUnitSerialNumberInquiry += new EventHandler<UnitSerialNumberInquiryEventArgs>(Target_OnUnitSerialNumberInquiry);
        }

        public void QueueCommand(byte[] commandBytes, LUNStructure lun, byte[] data, object task, OnCommandCompleted OnCommandCompleted)
        {
            m_target.QueueCommand(commandBytes, lun, data, task, OnCommandCompleted);
        }

        public SCSIStatusCodeName ExecuteCommand(byte[] commandBytes, LUNStructure lun, byte[] data, out byte[] response)
        {
            return m_target.ExecuteCommand(commandBytes, lun, data, out response);
        }

        public void Target_OnStandardInquiry(object sender, StandardInquiryEventArgs args)
        {
            args.Data.VersionDescriptors.Add(VersionDescriptorName.iSCSI);
            // To be thread-safe we must capture the delegate reference first
            EventHandler<StandardInquiryEventArgs> handler = OnStandardInquiry;
            if (handler != null)
            {
                handler(this, args);
            }
        }

        void Target_OnUnitSerialNumberInquiry(object sender, UnitSerialNumberInquiryEventArgs args)
        {
            // To be thread-safe we must capture the delegate reference first
            EventHandler<UnitSerialNumberInquiryEventArgs> handler = OnUnitSerialNumberInquiry;
            if (handler != null)
            {
                handler(this, args);
            }
        }

        public void Target_OnDeviceIdentificationInquiry(object sender, DeviceIdentificationInquiryEventArgs args)
        {
            // ISCSI identifier is needed for WinPE to pick up the disk during boot (after iPXE's sanhook)
            args.Page.IdentificationDescriptorList.Add(IdentificationDescriptor.GetSCSINameStringIdentifier(m_targetName));
            // To be thread-safe we must capture the delegate reference first
            EventHandler<DeviceIdentificationInquiryEventArgs> handler = OnDeviceIdentificationInquiry;
            if (handler != null)
            {
                handler(this, args);
            }
        }

        internal bool AuthorizeInitiator(string initiatorName, ulong isid, IPEndPoint initiatorEndPoint)
        {
            // To be thread-safe we must capture the delegate reference first
            EventHandler<AuthorizationRequestArgs> handler = OnAuthorizationRequest;
            if (handler != null)
            {
                AuthorizationRequestArgs args = new AuthorizationRequestArgs(initiatorName, isid, initiatorEndPoint);
                handler(this, args);
                return args.Accept;
            }
            return true;
        }

        internal KeyValuePairList<string, string> GetTextResponse(KeyValuePairList<string, string> requestParameters)
        {
            EventHandler<TextRequestArgs> handler = OnTextRequest;
            if (handler != null)
            {
                TextRequestArgs args = new TextRequestArgs(requestParameters);
                handler(this, args);
                return args.ResponseParaemeters;
            }
            return new KeyValuePairList<string, string>();
        }

        internal void NotifySessionTermination(string initiatorName, ulong isid, SessionTerminationReason reason)
        {
            EventHandler<SessionTerminationArgs> handler = OnSessionTermination;
            if (handler != null)
            {
                SessionTerminationArgs args = new SessionTerminationArgs(initiatorName, isid, reason);
                handler(this, args);
            }
        }

        public string TargetName
        {
            get
            {
                return m_targetName;
            }
        }

        public SCSITargetInterface SCSITarget
        {
            get
            {
                return m_target;
            }
        }
    }
}
