/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <remarks>
    /// FileName attribute is always resident.
    /// </remarks>
    public class FileNameAttributeRecord : ResidentAttributeRecord
    {
        public FileNameRecord Record;

        public FileNameAttributeRecord(string name) : base(AttributeType.FileName, name)
        {
        }

        public FileNameAttributeRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
            Record = new FileNameRecord(this.Data, 0);
        }

        public override byte[] GetBytes()
        {
            this.Data = Record.GetBytes();

            return base.GetBytes();
        }

        public override AttributeRecord Clone()
        {
            FileNameAttributeRecord clone = (FileNameAttributeRecord)base.Clone();
            clone.Record = this.Record.Clone();
            return clone;
        }

        public override ulong DataLength
        {
            get
            {
                return (ulong)Record.Length;
            }
        }
    }
}
