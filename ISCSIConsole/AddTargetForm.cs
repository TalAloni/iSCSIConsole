using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using DiskAccessLibrary;
using ISCSI.Server;
using Utilities;

namespace ISCSIConsole
{
    public partial class AddTargetForm : Form
    {
        public static int m_targetNumber = 1;
        public const string DefaultTargetIQN = "iqn.1991-05.com.microsoft";

        private List<Disk> m_disks = new List<Disk>();
        private ISCSITarget m_target;

        public AddTargetForm()
        {
            InitializeComponent();
#if Win32
            btnAddPhysicalDisk.Visible = true;
            btnAddVolume.Visible = true;
            if (!SecurityHelper.IsAdministrator())
            {
                btnAddPhysicalDisk.Enabled = false;
                btnAddVolume.Enabled = false;
            }
#endif
        }

        private void AddTargetForm_Load(object sender, EventArgs e)
        {
            txtTargetIQN.Text = String.Format("{0}:target{1}", DefaultTargetIQN, m_targetNumber);
        }

        private void btnCreateDiskImage_Click(object sender, EventArgs e)
        {
            if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
            {
                CreateRAMDiskForm createRAMDisk = new CreateRAMDiskForm();
                DialogResult result = createRAMDisk.ShowDialog();
                if (result == DialogResult.OK)
                {
                    RAMDisk ramDisk = createRAMDisk.RAMDisk;
                    AddDisk(ramDisk);
                }
            }
            else
            {
                CreateDiskImageForm createDiskImage = new CreateDiskImageForm();
                DialogResult result = createDiskImage.ShowDialog();
                if (result == DialogResult.OK)
                {
                    DiskImage diskImage = createDiskImage.DiskImage;
                    AddDisk(diskImage);
                }
            }
        }

        private void btnAddDiskImage_Click(object sender, EventArgs e)
        {
            SelectDiskImageForm selectDiskImage = new SelectDiskImageForm();
            DialogResult result = selectDiskImage.ShowDialog();
            if (result == DialogResult.OK)
            {
                DiskImage diskImage = selectDiskImage.DiskImage;
                AddDisk(diskImage);
            }
        }

        private void btnAddPhysicalDisk_Click(object sender, EventArgs e)
        {
#if Win32
            SelectPhysicalDiskForm selectPhysicalDisk = new SelectPhysicalDiskForm();
            DialogResult result = selectPhysicalDisk.ShowDialog();
            if (result == DialogResult.OK)
            {
                AddDisk(selectPhysicalDisk.SelectedDisk);
            }
#endif
        }

        private void btnAddVolume_Click(object sender, EventArgs e)
        {
#if Win32
            SelectVolumeForm selectVolume = new SelectVolumeForm();
            DialogResult result = selectVolume.ShowDialog();
            if (result == DialogResult.OK)
            {
                VolumeDisk volumeDisk = new VolumeDisk(selectVolume.SelectedVolume, selectVolume.IsReadOnly);
                AddDisk(volumeDisk);
            }
#endif
        }

        private void AddDisk(Disk disk)
        {
            string description = String.Empty;
            string sizeString = FormattingHelper.GetStandardSizeString(disk.Size);
            if (disk is DiskImage)
            {
                description = ((DiskImage)disk).Path;
            }
            else if (disk is RAMDisk)
            {
                description = "RAM Disk";
            }
#if Win32
            else if (disk is PhysicalDisk)
            {
                description = String.Format("Physical Disk {0}", ((PhysicalDisk)disk).PhysicalDiskIndex);
            }
            else if (disk is VolumeDisk)
            {
                description = String.Format("Volume");
            }
#endif
            ListViewItem item = new ListViewItem(description);
            item.SubItems.Add(sizeString);
            listDisks.Items.Add(item);
            m_disks.Add(disk);
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (listDisks.SelectedIndices.Count > 0)
            {
                int selectedIndex = listDisks.SelectedIndices[0];
                LockUtils.ReleaseDisk(m_disks[selectedIndex]);
                m_disks.RemoveAt(selectedIndex);
                listDisks.Items.RemoveAt(selectedIndex);
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (!ISCSINameHelper.IsValidIQN(txtTargetIQN.Text))
            {
                MessageBox.Show("Target IQN is invalid", "Error");
                return;
            }
            m_target = new ISCSITarget(txtTargetIQN.Text, m_disks);
            m_targetNumber++;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        public ISCSITarget Target
        {
            get
            {
                return m_target;
            }
        }

        private void listDisks_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnRemove.Enabled = (listDisks.SelectedIndices.Count > 0);
        }

        private void listDisks_ColumnWidthChanging(object sender, ColumnWidthChangingEventArgs e)
        {
            e.NewWidth = ((ListView)sender).Columns[e.ColumnIndex].Width;
            e.Cancel = true;
        }

        private void AddTargetForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (this.DialogResult != DialogResult.OK)
            {
                LockUtils.ReleaseDisks(m_disks);
            }
        }

        private void AddTargetForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                btnCreateDiskImage.Text = "Create RAM Disk";
            }
        }

        private void AddTargetForm_KeyUp(object sender, KeyEventArgs e)
        {
            btnCreateDiskImage.Text = "Create Virtual Disk";
        }

        private void AddTargetForm_Deactivate(object sender, EventArgs e)
        {
            btnCreateDiskImage.Text = "Create Virtual Disk";
        }
    }
}