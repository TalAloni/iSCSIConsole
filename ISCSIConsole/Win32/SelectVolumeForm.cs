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
    public partial class SelectVolumeForm : Form
    {
        private Volume m_selectedVolume;
        private bool m_isReadOnly;

        public SelectVolumeForm()
        {
            InitializeComponent();
        }

        private void SelectPhysicalDiskForm_Load(object sender, EventArgs e)
        {
            List<Volume> volumes = WindowsVolumeHelper.GetVolumes();
            for(int index = 0; index < volumes.Count; index++)
            {
                Volume volume = volumes[index];
                string title = String.Format("Volume {0}", index);
                string type = VolumeHelper.GetVolumeTypeString(volume);
                string status = VolumeHelper.GetVolumeStatusString(volume);

                ulong volumeID = 0;
                string name = String.Empty;
                if (volume is DynamicVolume)
                {
                    volumeID = ((DynamicVolume)volume).VolumeID;
                    name = ((DynamicVolume)volume).Name;
                }
                else if (volume is GPTPartition)
                {
                    name = ((GPTPartition)volume).PartitionName;
                }
                ListViewItem item = new ListViewItem(title);
                item.SubItems.Add(name);
                item.SubItems.Add(type);
                item.SubItems.Add(status);
                item.SubItems.Add(FormattingHelper.GetStandardSizeString(volume.Size));
                item.Tag = volume;
                listVolumes.Items.Add(item);
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (listVolumes.SelectedItems.Count > 0)
            {
                Volume selectedVolume = (Volume)listVolumes.SelectedItems[0].Tag;
                if (selectedVolume is DynamicVolume && !((DynamicVolume)selectedVolume).IsOperational)
                {
                    MessageBox.Show("The selected volume is not operational", "Error");
                    return;
                }
                if (!chkReadOnly.Checked)
                {
                    Guid? volumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(selectedVolume);
                    if (Environment.OSVersion.Version.Major >= 6)
                    {
                        // Windows Vista / 7 enforce various limitations on direct write operations to volumes and disks.
                        // We either have to take the disk(s) offline or use the OS volume handle for write operations.
                        if (!volumeGuid.HasValue)
                        {
                            MessageBox.Show("The selected volume is not recognized by your operating system");
                            return;
                        }
                        selectedVolume = new OperatingSystemVolume(volumeGuid.Value, selectedVolume.BytesPerSector, selectedVolume.Size, chkReadOnly.Checked);
                    }

                    bool isLocked = false;
                    if (volumeGuid.HasValue)
                    {
                        isLocked = WindowsVolumeManager.ExclusiveLock(volumeGuid.Value);
                    }
                    if (!isLocked)
                    {
                        MessageBox.Show("Unable to lock the volume", "Error");
                        return;
                    }
                }
                m_selectedVolume = selectedVolume;
                m_isReadOnly = chkReadOnly.Checked;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("No volume was selected", "Error");
                return;
            }
        }

        private IList<PhysicalDisk> GetVolumeDisks(Volume volume)
        {
            SortedList<int, PhysicalDisk> disks = new SortedList<int,PhysicalDisk>();
            if (volume is DynamicVolume)
            {
                foreach (DiskExtent extent in ((DynamicVolume)volume).Extents)
                {
                    if (extent.Disk is PhysicalDisk)
                    {
                        PhysicalDisk disk = (PhysicalDisk)extent.Disk;
                        if (!disks.ContainsKey(disk.PhysicalDiskIndex))
                        {
                            disks.Add(disk.PhysicalDiskIndex, disk);
                        }
                    }
                }
            }
            else if (volume is Partition)
            {
                Partition partition = (Partition)volume;
                if (partition.Disk is PhysicalDisk)
                {
                    PhysicalDisk disk = (PhysicalDisk)partition.Disk;
                    disks.Add(disk.PhysicalDiskIndex, (PhysicalDisk)disk);
                }
            }
            return disks.Values;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        public Volume SelectedVolume
        {
            get
            {
                return m_selectedVolume;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return m_isReadOnly;
            }
        }

        private void listPhysicalDisks_ColumnWidthChanging(object sender, ColumnWidthChangingEventArgs e)
        {
            e.NewWidth = ((ListView)sender).Columns[e.ColumnIndex].Width;
            e.Cancel = true;
        }
    }
}