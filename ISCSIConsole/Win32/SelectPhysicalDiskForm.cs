using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DiskAccessLibrary;
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace ISCSIConsole
{
    public partial class SelectPhysicalDiskForm : Form
    {
        private PhysicalDisk m_selectedDisk;

        public SelectPhysicalDiskForm()
        {
            InitializeComponent();
        }

        private void SelectPhysicalDiskForm_Load(object sender, EventArgs e)
        {
            List<PhysicalDisk> physicalDisks = PhysicalDiskHelper.GetPhysicalDisks();
            if (Environment.OSVersion.Version.Major >= 6)
            {
                listPhysicalDisks.Columns.Add("Status", 60);
                columnDescription.Width -= 60;
            }
            foreach (PhysicalDisk physicalDisk in physicalDisks)
            {
                string title = String.Format("Disk {0}", physicalDisk.PhysicalDiskIndex);
                string description = physicalDisk.Description;
                string serialNumber = physicalDisk.SerialNumber;
                string sizeString = FormattingHelper.GetStandardSizeString(physicalDisk.Size);
                ListViewItem item = new ListViewItem(title);
                item.SubItems.Add(description);
                item.SubItems.Add(serialNumber);
                item.SubItems.Add(sizeString);
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    bool isOnline = physicalDisk.GetOnlineStatus();
                    string status = isOnline ? "Online" : "Offline";
                    item.SubItems.Add(status);
                }
                item.Tag = physicalDisk;
                listPhysicalDisks.Items.Add(item);
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            PhysicalDisk selectedDisk;
            if (listPhysicalDisks.SelectedItems.Count > 0)
            {
                selectedDisk = (PhysicalDisk)listPhysicalDisks.SelectedItems[0].Tag;
            }
            else
            {
                MessageBox.Show("No disk was selected", "Error");
                return;
            }
            if (!chkReadOnly.Checked)
            {
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    bool isDiskReadOnly;
                    bool isOnline = selectedDisk.GetOnlineStatus(out isDiskReadOnly);
                    if (isDiskReadOnly)
                    {
                        MessageBox.Show("The selected disk is set to readonly", "Error");
                        return;
                    }

                    if (isOnline)
                    {
                        DialogResult result = MessageBox.Show("The selected disk will now be taken offline. OK?", String.Empty, MessageBoxButtons.OKCancel);
                        if (result == DialogResult.OK)
                        {
                            bool success = selectedDisk.SetOnlineStatus(false);
                            if (!success)
                            {
                                MessageBox.Show("Was not able to take the disk offline", "Error");
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                }
                else
                {
                    if (DynamicDisk.IsDynamicDisk(selectedDisk))
                    {
                        // The user will probably want to stop the Logical Disk Manager services (vds, dmadmin, dmserver)
                        // and lock all dynamic disks and dynamic volumes before whatever he's doing.
                        // Modifications the the LDM database should be applied to all dynamic disks.
                        DialogResult result = MessageBox.Show("The dynamic disk database will likely be corrupted, Continue?", "Warning", MessageBoxButtons.YesNo);
                        if (result != DialogResult.Yes)
                        {
                            return;
                        }
                    }
                    else
                    {
                        // Locking a disk does not prevent Windows from accessing mounted volumes on it. (it does prevent creation of new volumes).
                        // For basic disks we need to lock the Disk and Volumes, and we should also call UpdateDiskProperties() after releasing the lock.
                        LockStatus status = LockHelper.LockBasicDiskAndVolumesOrNone(selectedDisk);
                        if (status == LockStatus.CannotLockDisk)
                        {
                            MessageBox.Show("Unable to lock the disk", "Error");
                            return;
                        }
                        else if (status == LockStatus.CannotLockVolume)
                        {
                            MessageBox.Show("Unable to lock one of the volumes on the disk", "Error");
                            return;
                        }
                    }
                }
            }
            if (chkReadOnly.Checked)
            {
                selectedDisk = new PhysicalDisk(selectedDisk.PhysicalDiskIndex, true);
            }
            m_selectedDisk = selectedDisk;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        public PhysicalDisk SelectedDisk
        {
            get
            {
                return m_selectedDisk;
            }
        }

        private void listPhysicalDisks_ColumnWidthChanging(object sender, ColumnWidthChangingEventArgs e)
        {
            e.NewWidth = ((ListView)sender).Columns[e.ColumnIndex].Width;
            e.Cancel = true;
        }
    }
}