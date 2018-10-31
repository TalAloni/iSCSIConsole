using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using DiskAccessLibrary;

namespace ISCSIConsole
{
    public partial class SelectDiskImageForm : Form
    {
        private DiskImage m_diskImage;

        public SelectDiskImageForm()
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
            string path = txtFilePath.Text;
            if (path == String.Empty)
            {
                MessageBox.Show("Please choose file location.", "Error");
                return;
            }
            DiskImage diskImage;
            try
            {
                diskImage = DiskImage.GetDiskImage(path, chkReadOnly.Checked);
            }
            catch (IOException ex)
            {
                MessageBox.Show("Can't open disk image: " + ex.Message, "Error");
                return;
            }
            catch (InvalidDataException ex)
            {
                MessageBox.Show("Invalid disk image: " + ex.Message, "Error");
                return;
            }
            catch (NotImplementedException)
            {
                MessageBox.Show("Unsupported Disk Image Format", "Error");
                return;
            }

            bool isLocked = false;
            try
            {
                isLocked = diskImage.ExclusiveLock();
            }
            catch (IOException)
            {
            }
            if (!isLocked)
            {
                MessageBox.Show("Cannot lock the disk image for exclusive access.", "Error");
                return;
            }
            m_diskImage = diskImage;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            DialogResult result = openDiskImageDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                txtFilePath.Text = openDiskImageDialog.FileName;
            }
        }

        public DiskImage DiskImage
        {
            get
            {
                return m_diskImage;
            }
        }
    }
}