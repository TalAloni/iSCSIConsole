/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;
using DiskAccessLibrary.VHD;

namespace DiskAccessLibrary
{
    public partial class VirtualHardDisk : DiskImage, IDiskGeometry
    {
        public override void ExtendFast(long additionalNumberOfBytes)
        {
            if (additionalNumberOfBytes % this.BytesPerSector > 0)
            {
                throw new ArgumentException("additionalNumberOfBytes must be a multiple of BytesPerSector");
            }

            if (m_vhdFooter.DiskType == VirtualHardDiskType.Fixed)
            {
                long length = this.Size; // does not include the footer
                bool hasManageVolumePrivilege = SecurityUtils.ObtainManageVolumePrivilege();
                FileStream stream = new FileStream(this.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 0x1000, FileOptions.WriteThrough);
                try
                {
                    stream.SetLength(length + additionalNumberOfBytes + VHDFooter.Length);
                }
                catch
                {
                    stream.Close();
                    throw;
                }
                if (hasManageVolumePrivilege)
                {
                    FileStreamUtils.SetValidLength(stream, length + additionalNumberOfBytes + VHDFooter.Length);
                }
                stream.Seek(length + additionalNumberOfBytes, SeekOrigin.Begin);
                m_vhdFooter.CurrentSize += (ulong)additionalNumberOfBytes;
                stream.Write(m_vhdFooter.GetBytes(), 0, VHDFooter.Length);
                stream.Close();
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
