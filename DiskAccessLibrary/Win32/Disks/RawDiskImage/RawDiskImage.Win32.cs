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

namespace DiskAccessLibrary
{
    public partial class RawDiskImage : DiskImage
    {
        public override void ExtendFast(long additionalNumberOfBytes)
        {
            if (additionalNumberOfBytes % this.BytesPerSector > 0)
            {
                throw new ArgumentException("additionalNumberOfBytes must be a multiple of BytesPerSector");
            }

            long length = this.Size;
            bool hasManageVolumePrivilege = SecurityUtils.ObtainManageVolumePrivilege();
            FileStream stream = new FileStream(this.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 0x1000, FILE_FLAG_NO_BUFFERING | FileOptions.WriteThrough);
            try
            {
                stream.SetLength(length + additionalNumberOfBytes);
            }
            catch
            {
                stream.Close();
                throw;
            }
            if (hasManageVolumePrivilege)
            {
                FileStreamUtils.SetValidLength(stream, length + additionalNumberOfBytes);
            }
            stream.Close();
        }
    }
}
