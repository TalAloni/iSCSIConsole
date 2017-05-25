using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DiskAccessLibrary;
using ISCSI.Server;
using SCSI.Win32;

namespace ISCSIConsole
{
    public partial class AddSPTITargetForm : Form
    {
        public const string DefaultTargetIQN = "iqn.1991-05.com.microsoft";

        public static int m_targetNumber = 1;
        private List<DeviceInfo> m_devices = new List<DeviceInfo>();
        private int m_storagePortStartIndex;
        private ISCSITarget m_target;

        public AddSPTITargetForm()
        {
            InitializeComponent();
        }

        private void AddSPTITargetForm_Load(object sender, EventArgs e)
        {
            txtTargetIQN.Text = String.Format("{0}:sptitarget{1}", DefaultTargetIQN, m_targetNumber);
            List<DeviceInfo> devices = GetSPTIDevices();
            for (int index = 0; index < devices.Count; index++)
            {
                AddDevice(devices[index]);
            }
            listDisks.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
        }

        public List<DeviceInfo> GetSPTIDevices()
        {
            List<DeviceInfo> result = new List<DeviceInfo>();
            result.AddRange(DeviceInterfaceUtils.GetDeviceList(DeviceInterfaceUtils.DiskClassGuid));
            result.AddRange(DeviceInterfaceUtils.GetDeviceList(DeviceInterfaceUtils.CDRomClassGuid));
            result.AddRange(DeviceInterfaceUtils.GetDeviceList(DeviceInterfaceUtils.TapeClassGuid));
            m_storagePortStartIndex = result.Count;
            result.AddRange(DeviceInterfaceUtils.GetDeviceList(DeviceInterfaceUtils.StoragePortClassGuid));
            return result;
        }

        private void AddDevice(DeviceInfo device)
        {
            string description = device.DeviceDescription;
            string path = device.DevicePath;

            ListViewItem item = new ListViewItem(description);
            item.SubItems.Add(path);
            item.Tag = device;
            listDisks.Items.Add(item);
            m_devices.Add(device);
        }

        private void btnAddDevice_Click(object sender, EventArgs e)
        {
            if (!ISCSINameHelper.IsValidIQN(txtTargetIQN.Text))
            {
                MessageBox.Show("Target IQN is invalid", "Error");
                return;
            }

            if (listDisks.SelectedIndices.Count == 0)
            {
                MessageBox.Show("No device was selected", "Error");
                return;
            }
            int selectedIndex = listDisks.SelectedIndices[0];
            bool emulateReportLUNs = (selectedIndex < m_storagePortStartIndex);
            SPTITarget sptiTarget = new SPTITarget(m_devices[selectedIndex].DevicePath, emulateReportLUNs);
            m_target = new ISCSITarget(txtTargetIQN.Text, sptiTarget);
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

        public ISCSITarget Target
        {
            get
            {
                return m_target;
            }
        }
    }
}
