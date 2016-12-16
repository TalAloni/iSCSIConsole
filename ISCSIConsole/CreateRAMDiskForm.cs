using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using DiskAccessLibrary;

namespace ISCSIConsole
{
    public partial class CreateRAMDiskForm : Form
    {
        private RAMDisk m_ramDisk;

        public CreateRAMDiskForm()
        {
            InitializeComponent();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            int size = (int)numericDiskSize.Value * 1024 * 1024;
            RAMDisk disk;
            try
            {
                disk = new RAMDisk(size);
            }
            catch (OutOfMemoryException)
            {
                MessageBox.Show("Not enough memory available", "Error");
                return;
            }
            m_ramDisk = disk;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        public RAMDisk RAMDisk
        {
            get
            {
                return m_ramDisk;
            }
        }
    }
}