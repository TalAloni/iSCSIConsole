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
    public enum RAID5ManagerOperation : ushort
    {
        //MoveExtent = 0x0100, // MoveExtent v0
        MoveExtent = 0x0101, // MoveExtent v1
        AddDiskToArray = 0x0200,
    }

    public abstract class RAID5ManagerBootRecord
    {
        public const int Length = 512;
        public const string ValidSignature = "RAID5MGR";

        public string Signature = ValidSignature;
        public byte RecordRevision = 1; // Must be 1
        protected RAID5ManagerOperation Operation; // 2 bytes
        // reserved 5 bytes

        public RAID5ManagerBootRecord()
        { 

        }

        public RAID5ManagerBootRecord(byte[] buffer)
        {
            Signature = ByteReader.ReadAnsiString(buffer, 0, 8);
            RecordRevision = ByteReader.ReadByte(buffer, 8);
            Operation = (RAID5ManagerOperation)BigEndianConverter.ToUInt16(buffer, 9);

            ReadOperationParameters(buffer, 16);
        }

        protected abstract void ReadOperationParameters(byte[] buffer, int offset);

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Length];
            ByteWriter.WriteAnsiString(buffer, 0, Signature, 8);
            ByteWriter.WriteByte(buffer, 8, RecordRevision);
            BigEndianWriter.WriteUInt16(buffer, 9, (ushort)Operation);

            WriteOperationParameters(buffer, 16);

            return buffer;
        }

        protected abstract void WriteOperationParameters(byte[] buffer, int offset);

        public bool IsValid
        {
            get
            {
                return this.Signature == ValidSignature;
            }
        }

        public static RAID5ManagerBootRecord FromBytes(byte[] buffer)
        {
            string signature = ByteReader.ReadAnsiString(buffer, 0, 8);
            byte recordRevision = ByteReader.ReadByte(buffer, 8);
            RAID5ManagerOperation operation = (RAID5ManagerOperation)BigEndianConverter.ToUInt16(buffer, 9);
            if (signature == ValidSignature && recordRevision == 1)
            {
                if (operation == RAID5ManagerOperation.AddDiskToArray)
                {
                    return new AddDiskOperationBootRecord(buffer);
                }
                else if (operation == RAID5ManagerOperation.MoveExtent)
                {
                    return new MoveExtentOperationBootRecord(buffer);
                }
            }
            return null;
        }

        public static bool HasValidSignature(byte[] buffer)
        {
            string signature = ByteReader.ReadAnsiString(buffer, 0, 8);
            return (signature == ValidSignature);
        }
    }
}
