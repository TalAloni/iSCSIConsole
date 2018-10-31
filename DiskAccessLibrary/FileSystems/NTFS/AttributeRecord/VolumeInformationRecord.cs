/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    /// <remarks>
    /// VolumeInformation attribute is always resident.
    /// </remarks>
    public class VolumeInformationRecord : ResidentAttributeRecord
    {
        public const int RecordDataLength = 12;

        // ulong Reserved;
        public byte MajorVersion;
        public byte MinorVersion;
        public VolumeFlags VolumeFlags;

        public VolumeInformationRecord(string name, ushort instance) : base(AttributeType.VolumeInformation, name, instance)
        {
            MajorVersion = 3;
            MinorVersion = 1;
        }

        public VolumeInformationRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
            MajorVersion = ByteReader.ReadByte(this.Data, 0x08);
            MinorVersion = ByteReader.ReadByte(this.Data, 0x09);
            VolumeFlags = (VolumeFlags)LittleEndianConverter.ToUInt16(this.Data, 0x0A);
        }

        public override byte[] GetBytes()
        {
            this.Data = new byte[this.DataLength];
            ByteWriter.WriteByte(this.Data, 0x08, MajorVersion);
            ByteWriter.WriteByte(this.Data, 0x09, MinorVersion);
            LittleEndianWriter.WriteUInt16(this.Data, 0x0A, (ushort)VolumeFlags);

            return base.GetBytes();
        }

        public override ulong DataLength
        {
            get
            {
                return RecordDataLength;
            }
        }

        public bool IsDirty
        {
            get
            {
                return (VolumeFlags & VolumeFlags.Dirty) != 0;
            }
            set
            {
                if (value)
                {
                    VolumeFlags |= VolumeFlags.Dirty;
                }
                else
                {
                    VolumeFlags &= ~VolumeFlags.Dirty;
                }
            }
        }
    }
}
