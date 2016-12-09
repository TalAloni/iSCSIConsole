/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    public class ISCSITarget : SCSITarget
    {
        private string m_targetName; // ISCSI name
        private SCSITarget m_target;
        public event EventHandler<AuthorizationRequestArgs> OnAuthorizationRequest;
        public event EventHandler<SessionTerminationArgs> OnSessionTermination;

        public ISCSITarget(string targetName, List<Disk> disks) : this(targetName, new VirtualSCSITarget(disks))
        {
        }

        public ISCSITarget(string targetName, SCSITarget scsiTarget)
        {
            m_targetName = targetName;
            m_target = scsiTarget;
            m_target.OnStandardInquiry += new EventHandler<StandardInquiryEventArgs>(Target_OnStandardInquiry);
            m_target.OnDeviceIdentificationInquiry += new EventHandler<DeviceIdentificationInquiryEventArgs>(Target_OnDeviceIdentificationInquiry);
        }

        public override SCSIStatusCodeName ExecuteCommand(byte[] commandBytes, LUNStructure lun, byte[] data, out byte[] response)
        {
            return m_target.ExecuteCommand(commandBytes, lun, data, out response);
        }

        public void Target_OnStandardInquiry(object sender, StandardInquiryEventArgs args)
        {
            args.Data.VersionDescriptors.Add(VersionDescriptorName.iSCSI);
            NotifyStandardInquiry(this, args);
        }

        public void Target_OnDeviceIdentificationInquiry(object sender, DeviceIdentificationInquiryEventArgs args)
        {
            // ISCSI identifier is needed for WinPE to pick up the disk during boot (after iPXE's sanhook)
            args.Page.IdentificationDescriptorList.Add(IdentificationDescriptor.GetSCSINameStringIdentifier(m_targetName));
            NotifyDeviceIdentificationInquiry(this, args);
        }

        public bool AuthorizeInitiator(string initiatorName, ulong isid, IPEndPoint initiatorEndPoint)
        {
            // To be thread-safe we must capture the delegate reference first
            EventHandler<AuthorizationRequestArgs> handler = OnAuthorizationRequest;
            if (handler != null)
            {
                AuthorizationRequestArgs args = new AuthorizationRequestArgs(initiatorName, isid, initiatorEndPoint);
                OnAuthorizationRequest(this, args);
                return args.Accept;
            }
            return true;
        }

        internal void NotifySessionTermination(string initiatorName, ulong isid, SessionTerminationReason reason)
        {
            EventHandler<SessionTerminationArgs> handler = OnSessionTermination;
            if (handler != null)
            {
                SessionTerminationArgs args = new SessionTerminationArgs(initiatorName, isid, reason);
                OnSessionTermination(this, args);
            }
        }

        public string TargetName
        {
            get
            {
                return m_targetName;
            }
        }

        public SCSITarget SCSITarget
        {
            get
            {
                return m_target;
            }
        }
    }
}
