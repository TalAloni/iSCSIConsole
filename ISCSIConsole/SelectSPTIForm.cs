using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DiskAccessLibrary;

namespace ISCSIConsole
{
    public partial class SelectSPTIForm : Form
    {
        private SPTIDevice m_selectedDevice;

        public SelectSPTIForm()
        {
            InitializeComponent();
        }

        private void SelectSPTIForm_Load(object sender, EventArgs e)
        {
            List<SPTIDevice> devices = SPTIHelper.GetSPTIDevices();
            for (int index = 0; index < devices.Count; index++)
            {
                SPTIDevice device = devices[index];
                string title = String.Format("Device {0}", index);
                string description = device.Path;

                ListViewItem item = new ListViewItem(title);
                item.SubItems.Add(description);
                item.Tag = device;
                listSPTIDevices.Items.Add(item);
            }
        }

        public SPTIDevice SelectedDisk
        {
            get
            {
                return m_selectedDevice;
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            SPTIDevice selectedDisk;
            if (listSPTIDevices.SelectedItems.Count > 0)
            {
                selectedDisk = (SPTIDevice)listSPTIDevices.SelectedItems[0].Tag;
            }
            else
            {
                MessageBox.Show("No disk was selected", "Error");
                return;
            }
            m_selectedDevice = selectedDisk;
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
