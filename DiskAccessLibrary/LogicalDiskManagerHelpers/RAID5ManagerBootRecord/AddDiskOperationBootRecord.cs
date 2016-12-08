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

namespace DiskAccessLibrary
{
    public class AddDiskOperationBootRecord : RAID5ManagerBootRecord
    {
        public Guid VolumeGuid; // offset 16
        public ulong NumberOfCommittedSectors; // for an array, this would be the total of all sectors that can be now read from the new array

        public AddDiskOperationBootRecord()
        {
            Operation = RAID5ManagerOperation.AddDiskToArray;
        }

        public AddDiskOperationBootRecord(byte[] buffer) : base(buffer)
        { 
        }

        protected override void ReadOperationParameters(byte[] buffer, int offset)
        {
            VolumeGuid = BigEndianConverter.ToGuid(buffer, offset + 0);
            NumberOfCommittedSectors = BigEndianConverter.ToUInt64(buffer, offset + 16);
        }

        protected override void WriteOperationParameters(byte[] buffer, int offset)
        {
            BigEndianWriter.WriteGuidBytes(buffer, offset + 0, VolumeGuid);
            BigEndianWriter.WriteUInt64(buffer, offset + 16, NumberOfCommittedSectors);
        }
    }
}
