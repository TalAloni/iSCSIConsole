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

namespace DiskAccessLibrary.LogicalDiskManager
{
    // This class represents a single VBLK block (record can span multiple VBLK blocks)
    public class DatabaseRecordFragment
    {
        public const int HeaderLength = 16;

        public string Signature = "VBLK"; // VBLK
        public uint SequenceNumber;       // each fragment have different SequenceNumber, SequenceNumber starts from 4 ( 0-3 are taken by the VMDB)
        public uint GroupNumber;          // same for all fragments of the same record
        public ushort NumberInGroup; // (x of y), Zero-based 
        public ushort FragmentCount;         // Number of fragments in group
        public byte[] Data;

        public DatabaseRecordFragment()
        { 

        }

        protected DatabaseRecordFragment(byte[] buffer)
        {
            Signature = ByteReader.ReadAnsiString(buffer, 0x00, 4);
            SequenceNumber = BigEndianConverter.ToUInt32(buffer, 0x04);
            GroupNumber = BigEndianConverter.ToUInt32(buffer, 0x08);
            NumberInGroup = BigEndianConverter.ToUInt16(buffer, 0x0C);
            FragmentCount = BigEndianConverter.ToUInt16(buffer, 0x0E);
            Data = new byte[buffer.Length - HeaderLength];
            Array.Copy(buffer, 0x10, Data, 0, buffer.Length - HeaderLength);
        }

        public byte[] GetBytes(int blockSize)
        {
            byte[] buffer = new byte[blockSize];
            ByteWriter.WriteAnsiString(buffer, 0, Signature, 4);
            BigEndianWriter.WriteUInt32(buffer, 0x04, SequenceNumber);
            BigEndianWriter.WriteUInt32(buffer, 0x08, GroupNumber);
            BigEndianWriter.WriteUInt16(buffer, 0x0C, NumberInGroup);
            BigEndianWriter.WriteUInt16(buffer, 0x0E, FragmentCount);
            ByteWriter.WriteBytes(buffer, 0x10, Data, Math.Min(Data.Length, blockSize - HeaderLength));
            return buffer;
        }

        public static DatabaseRecordFragment GetDatabaseRecordFragment(byte[] buffer)
        {
            string signature = ByteReader.ReadAnsiString(buffer, 0x00, 4);
            ushort fragmentCount = BigEndianConverter.ToUInt16(buffer, 0x0E);
            if (fragmentCount == 0 || signature != "VBLK")
            {
                return null;
            }
            else
            {
                return new DatabaseRecordFragment(buffer);
            }
        }

        /// <summary>
        /// Free fragment from record data (the result can be used to overwrite this fragment)
        /// </summary>
        public void Clear()
        {
            this.GroupNumber = 0;
            this.NumberInGroup = 0;
            this.FragmentCount = 0;
            this.Data = new byte[0];
        }
    }
}
