/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace DiskAccessLibrary
{
    public partial class CHSAddress
    {
        public byte Head;       // the range for head is 0 through 255 inclusive
        public byte Sector;     // The range for sector is 1 through 63
        public ushort Cylinder; // the range for cylinder is 0 through 1023

        public CHSAddress()
        { 
        }

        public CHSAddress(byte[] buffer, int offset)
        {
            Head = buffer[offset + 0];
            Sector = (byte)(buffer[offset + 1] & 0x3F);
            // bits 7–6 of address[offset + 1] are high bits of cylinder
            Cylinder = (ushort)(((buffer[offset + 1] >> 6) << 8) | buffer[offset + 2]);
        }

        public void WriteBytes(byte[] buffer, int offset)
        {
            buffer[offset + 0] = Head;
            buffer[offset + 1] = (byte)((Sector & 0x3F) | ((Cylinder >> 8) << 6));
            buffer[offset + 2] = (byte)(Cylinder & 0xFF);
        }

        public static CHSAddress FromLBA(ulong lba, Disk disk)
        {
            if (disk is IDiskGeometry)
            {
                return FromLBA(lba, (IDiskGeometry)disk);
            }
            else
            {
                throw new NotImplementedException("Disk image lba to chs");
            }
        }

        // tracksPerCylinder a.k.a. heads per cylinder
        public static CHSAddress FromLBA(ulong lba, IDiskGeometry disk)
        {
            CHSAddress chs = new CHSAddress();
            chs.Cylinder = (ushort)(lba / (ulong)(disk.SectorsPerTrack * disk.TracksPerCylinder));
            chs.Head = (byte)((lba / (ulong)disk.SectorsPerTrack) % (ulong)disk.TracksPerCylinder);
            chs.Sector = (byte)(lba % (ulong)disk.SectorsPerTrack + 1);
            return chs;
        }
    }
}
