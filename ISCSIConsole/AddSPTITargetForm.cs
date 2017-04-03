using DiskAccessLibrary;
using ISCSI.Server;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ISCSIConsole
{
    public partial class AddSPTITargetForm : Form
    {
        public static int m_targetNumber = 1;
        public const string DefaultTargetIQN = "iqn.1991-05.com.microsoft";

        private List<DeviceInfo> m_devices = new List<DeviceInfo>();
        private ISCSITarget m_target;

        public AddSPTITargetForm()
        {
            InitializeComponent();
        }

        public ISCSITarget Target
        {
            get
            {
                return m_target;
            }
        }

        private void AddSPTITargetForm_Load(object sender, EventArgs e)
        {
            txtTargetIQN.Text = String.Format("{0}:sptitarget{1}", DefaultTargetIQN, m_targetNumber);
        }

        private void btnAddSPTI_Click(object sender, EventArgs e)
        {
            SelectSPTIDeviceForm selectSPTI = new SelectSPTIDeviceForm();
            DialogResult result = selectSPTI.ShowDialog();
            if (result == DialogResult.OK)
            {
                AddDevice(selectSPTI.SelectedDisk);
            }
        }

        private void AddDevice(DeviceInfo device)
        {
            string description = device.DeviceDescription;
            string path = device.DevicePath;

            ListViewItem item = new ListViewItem(description);
            item.SubItems.Add(path);
            listDisks.Items.Add(item);
            m_devices.Add(device);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (!ISCSINameHelper.IsValidIQN(txtTargetIQN.Text))
            {
                MessageBox.Show("Target IQN is invalid", "Error");
                return;
            }

            if (m_devices.Count == 0)
            {
                MessageBox.Show("No devices added", "Error");
                return;
            }

            m_target = new ISCSITarget(txtTargetIQN.Text, m_devices[0].DevicePath);
            m_targetNumber++;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (listDisks.SelectedIndices.Count > 0)
            {
                int selectedIndex = listDisks.SelectedIndices[0];
                m_devices.RemoveAt(selectedIndex);
                listDisks.Items.RemoveAt(selectedIndex);
            }
        }

    }
}
