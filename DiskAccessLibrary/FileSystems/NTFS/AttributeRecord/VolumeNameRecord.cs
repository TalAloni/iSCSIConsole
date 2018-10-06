/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    /// VolumeName attribute is always resident.
    /// </remarks>
    public class VolumeNameRecord : ResidentAttributeRecord
    {
        public const int MaxVolumeNameLength = 32;

        private string m_volumeName;

        public VolumeNameRecord(string name, ushort instance) : base(AttributeType.VolumeName, name, instance)
        {
            m_volumeName = String.Empty;
        }

        public VolumeNameRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
            m_volumeName = Encoding.Unicode.GetString(this.Data);
        }

        public override byte[] GetBytes()
        {
            this.Data = Encoding.Unicode.GetBytes(m_volumeName);

            return base.GetBytes();
        }

        public override ulong DataLength
        {
            get
            {
                return (ulong)m_volumeName.Length * 2;
            }
        }

        public string VolumeName
        {
            get
            {
                return m_volumeName;
            }
            set
            {
                if (value.Length > MaxVolumeNameLength)
                {
                    throw new ArgumentException("Volume name length is limited to 32 characters");
                }
                m_volumeName = value;
            }
        }
    }
}
