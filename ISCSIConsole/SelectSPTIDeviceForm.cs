using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DiskAccessLibrary;

namespace ISCSIConsole
{
    public partial class SelectSPTIDeviceForm : Form
    {
        private DeviceInfo m_selectedDevice;

        public SelectSPTIDeviceForm()
        {
            InitializeComponent();
        }

        private void SelectSPTIForm_Load(object sender, EventArgs e)
        {
            List<DeviceInfo> devices = SPTIHelper.GetSPTIDevices();
            for (int index = 0; index < devices.Count; index++)
            {
                DeviceInfo device = devices[index];
                string title = devices[index].DeviceName;
                string description = devices[index].DevicePath;

                ListViewItem item = new ListViewItem(title);
                item.SubItems.Add(description);
                item.Tag = device;
                listSPTIDevices.Items.Add(item);
            }
        }

        public DeviceInfo SelectedDisk
        {
            get
            {
                return m_selectedDevice;
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (listSPTIDevices.SelectedItems.Count == 1)
            {
                m_selectedDevice = (DeviceInfo)listSPTIDevices.SelectedItems[0].Tag;
            }
            else
            {
                MessageBox.Show("Please select a device", "Error");
                return;
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
