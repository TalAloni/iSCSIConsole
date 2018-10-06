/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public partial class NTFSVolume : IExtendableFileSystem
    {
        public long GetMaximumSizeToExtend()
        {
            // The sector following the NTFS volume is a backup to the boot sector, we want to leave room for a new backup boot sector
            return m_volume.Size - (this.Size + m_volume.BytesPerSector);
        }

        public void Extend(long numberOfAdditionalSectors)
        {
            long originalNumberOfSectors = (long)m_bootRecord.TotalSectors;
            long numberOfAdditionalClusters = numberOfAdditionalSectors / m_bootRecord.SectorsPerCluster;

            m_bitmap.Extend(numberOfAdditionalClusters);

            // We set TotalSectors only after extending the File system, or otherwise the $bitmap size will mismatch
            m_bootRecord.TotalSectors += (ulong)numberOfAdditionalClusters * m_bootRecord.SectorsPerCluster; // We only add usable sectors

            // Update boot sector
            byte[] bootRecordBytes = m_bootRecord.GetBytes();
            WriteSectors(0, bootRecordBytes);

            // Recreate the backup boot sector at the new end of the raw volume
            // Note: The backup boot sector does not count as part of the NTFS volume
            long backupBootSectorIndex = originalNumberOfSectors + numberOfAdditionalSectors;
            WriteSectors(backupBootSectorIndex, bootRecordBytes);
        }
    }
}
