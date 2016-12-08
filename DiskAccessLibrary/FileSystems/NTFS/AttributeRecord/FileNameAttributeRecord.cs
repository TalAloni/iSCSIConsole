/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    // FileName attribute is always resident
    public class FileNameAttributeRecord : ResidentAttributeRecord // This is the record itself (the data that is contained in the attribute)
    {
        public const int FixedLength = 0x42;
        public FileNameRecord Record;

        public FileNameAttributeRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
            Record = new FileNameRecord(this.Data, 0);
        }

        public override byte[] GetBytes(int bytesPerCluster)
        {
            this.Data = Record.GetBytes();

            return base.GetBytes(bytesPerCluster);
        }
    }
}
