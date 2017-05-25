/* Copyright (C) 2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ISCSI.Server;
using DiskAccessLibrary;
using Utilities;

namespace ISCSIConsole
{
    public partial class MainForm : Form
    {
        private ISCSIServer m_server = new ISCSIServer();
        private List<ISCSITarget> m_targets = new List<ISCSITarget>();
        private UsageCounter m_usageCounter = new UsageCounter();
        private bool m_started = false;

        public MainForm()
        {
            InitializeComponent();
#if Win32
            btnAddSPTITarget.Visible = true;
#endif
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Text += " v" + version.ToString(3);
            m_server.OnLogEntry += Program.OnLogEntry;

            List<IPAddress> localIPs = GetHostIPAddresses();
            KeyValuePairList<string, IPAddress> list = new KeyValuePairList<string, IPAddress>();
            list.Add("Any", IPAddress.Any);
            foreach (IPAddress address in localIPs)
            {
                list.Add(address.ToString(), address);
            }
            comboIPAddress.DataSource = list;
            comboIPAddress.DisplayMember = "Key";
            comboIPAddress.ValueMember = "Value";
            lblStatus.Text = "Author: Tal Aloni (tal.aloni.il@gmail.com)";
#if Win32
            if (!SecurityHelper.IsAdministrator())
            {
                btnAddSPTITarget.Enabled = false;
                lblStatus.Text = "Some features require administrator privileges and have been disabled";
            }
#endif
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (!m_started)
            {
                IPAddress serverAddress = (IPAddress)comboIPAddress.SelectedValue;
                int port = Conversion.ToInt32(txtPort.Text, 0);
                if (port <= 0 || port > UInt16.MaxValue)
                {
                    MessageBox.Show("Invalid TCP port", "Error");
                    return;
                }
                IPEndPoint endpoint = new IPEndPoint(serverAddress, port);
                try
                {
                    m_server.Start(endpoint);
                }
                catch (SocketException ex)
                {
                    MessageBox.Show("Cannot start server, " + ex.Message, "Error");
                    return;
                }
                btnStart.Text = "Stop";
                txtPort.Enabled = false;
                comboIPAddress.Enabled = false;
                m_started = true;
                UpdateUI();
            }
            else
            {
                m_server.Stop();
                lblStatus.Text = String.Empty;
                m_started = false;
                btnStart.Text = "Start";
                txtPort.Enabled = true;
                comboIPAddress.Enabled = true;
            }
        }

        private void btnAddTarget_Click(object sender, EventArgs e)
        {
            AddTargetForm addTarget = new AddTargetForm();
            DialogResult addTargetResult = addTarget.ShowDialog();
            if (addTargetResult == DialogResult.OK)
            {
                ISCSITarget target = addTarget.Target;
                ((SCSI.VirtualSCSITarget)target.SCSITarget).OnLogEntry += Program.OnLogEntry;
                target.OnAuthorizationRequest += new EventHandler<AuthorizationRequestArgs>(ISCSITarget_OnAuthorizationRequest);
                target.OnSessionTermination += new EventHandler<SessionTerminationArgs>(ISCSITarget_OnSessionTermination);
                m_targets.Add(target);
                try
                {
                    m_server.AddTarget(target);
                }
                catch (ArgumentException ex)
                {
                    MessageBox.Show(ex.Message, "Error");
                    return;
                }
                listTargets.Items.Add(target.TargetName);
            }
        }

        private void btnAddSPTIDevice_Click(object sender, EventArgs e)
        {
            AddSPTITargetForm addTarget = new AddSPTITargetForm();
            DialogResult addTargetResult = addTarget.ShowDialog();
            if (addTargetResult == DialogResult.OK)
            {
                ISCSITarget target = addTarget.Target;
                ((SCSI.Win32.SPTITarget)target.SCSITarget).OnLogEntry += Program.OnLogEntry;
                target.OnAuthorizationRequest += new EventHandler<AuthorizationRequestArgs>(ISCSITarget_OnAuthorizationRequest);
                target.OnSessionTermination += new EventHandler<SessionTerminationArgs>(ISCSITarget_OnSessionTermination);
                m_targets.Add(target);
                try
                {
                    m_server.AddTarget(target);
                }
                catch (ArgumentException ex)
                {
                    MessageBox.Show(ex.Message, "Error");
                    return;
                }
                listTargets.Items.Add(target.TargetName);
            }
        }

        private void btnRemoveTarget_Click(object sender, EventArgs e)
        {
            if (listTargets.SelectedIndices.Count > 0)
            {
                int targetIndex = listTargets.SelectedIndices[0];
                ISCSITarget target = m_targets[targetIndex];
                bool isTargetRemoved = m_server.RemoveTarget(target.TargetName);
                if (!isTargetRemoved)
                {
                    MessageBox.Show("Could not remove iSCSI target", "Error");
                    return;
                }

                if (target.SCSITarget is SCSI.VirtualSCSITarget)
                {
                    List<Disk> disks = ((SCSI.VirtualSCSITarget)target.SCSITarget).Disks;
                    LockUtils.ReleaseDisks(disks);
                }
                else if (target.SCSITarget is SCSI.Win32.SPTITarget)
                {
                    ((SCSI.Win32.SPTITarget)target.SCSITarget).Close();
                }

                m_targets.RemoveAt(targetIndex);
                listTargets.Items.RemoveAt(targetIndex);
            }
        }

        private void ISCSITarget_OnAuthorizationRequest(object sender, AuthorizationRequestArgs e)
        {
            string targetName = ((ISCSITarget)sender).TargetName;
            m_usageCounter.NotifySessionStart(targetName);
            UpdateUI();
        }

        private void ISCSITarget_OnSessionTermination(object sender, SessionTerminationArgs e)
        {
            string targetName = ((ISCSITarget)sender).TargetName;
            m_usageCounter.NotifySessionTermination(targetName);
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)UpdateUI);
            }
            else
            {
                if (m_started)
                {
                    lblStatus.Text = String.Format("{0} Active Sessions", m_usageCounter.SessionCount);
                }

                if (listTargets.SelectedIndices.Count > 0)
                {
                    int targetIndex = listTargets.SelectedIndices[0];
                    ISCSITarget target = m_targets[targetIndex];
                    bool isInUse = m_usageCounter.IsTargetInUse(target.TargetName);
                    btnRemoveTarget.Enabled = !isInUse;
                }
                else
                {
                    btnRemoveTarget.Enabled = false;
                }
            }
        }

        private static List<IPAddress> GetHostIPAddresses()
        {
            List<IPAddress> result = new List<IPAddress>();
            foreach (NetworkInterface netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                IPInterfaceProperties ipProperties = netInterface.GetIPProperties();
                foreach (UnicastIPAddressInformation addressInfo in ipProperties.UnicastAddresses)
                {
                    if (addressInfo.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        result.Add(addressInfo.Address);
                    }
                }
            }
            return result;
        }

        private void listTargets_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateUI();
        }
    }
}