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

namespace DiskAccessLibrary.LogicalDiskManager
{
    // DatabaseRecord can span multiple VBLK blocks
    public abstract class DatabaseRecord
    {
        public const int RecordHeaderLength = 8;
        private List<DatabaseRecordFragment> m_fragments = new List<DatabaseRecordFragment>();

        public DatabaseRecordUpdateStatus UpdateStatus;
        public byte RecordRevision;
        public RecordType RecordType;
        public byte Flags;
        // private uint dataLength
        public ulong Id;
        public string Name;

        private byte[] m_data;

        public DatabaseRecord()
        { 
        }

        /// <param name="recordFragments">Fragments must be sorted!</param>
        protected DatabaseRecord(List<DatabaseRecordFragment> recordFragments)
        {
            m_fragments = recordFragments;
        }

        protected void ReadCommonFields(byte[] data, ref int offset)
        {
            UpdateStatus = (DatabaseRecordUpdateStatus)BigEndianReader.ReadUInt16(data, ref offset);
            Flags = ByteReader.ReadByte(data, ref offset);
            byte temp = ByteReader.ReadByte(data, ref offset);
            RecordRevision = (byte)(temp >> 4);
            RecordType = (RecordType)(temp & 0xF);
            offset += 4; // data length
            Id = ReadVarULong(data, ref offset);
            Name = ReadVarString(data, ref offset);
        }

        public abstract byte[] GetDataBytes();

        /// <summary>
        /// Update UpdateStatus field
        /// </summary>
        public void UpdateHeader()
        {
            if (m_fragments.Count > 0)
            {
                int offset = 0x00;
                BigEndianWriter.WriteUInt16(m_fragments[0].Data, ref offset, (ushort)UpdateStatus);
            }
        }

        /// <summary>
        /// store new record fragments containing updated record data.
        /// SequenceNumber and GroupNumber have to be set before writing these fragments to the database.
        /// </summary>
        public void UpdateFragments(int blockSize)
        {
            m_fragments.Clear();

            byte[] data = GetDataBytes();
            m_fragments = GetUpdatedFragments(blockSize, data);
        }

        protected void WriteCommonFields(byte[] data, ref int offset)
        {
            BigEndianWriter.WriteUInt16(data, ref offset, (ushort)UpdateStatus);
            ByteWriter.WriteByte(data, ref offset, Flags);
            byte temp = (byte)(((RecordRevision & 0xF) << 4) | ((byte)RecordType & 0xF));
            ByteWriter.WriteByte(data, ref offset, temp);
            BigEndianWriter.WriteUInt32(data, ref offset, (uint)data.Length - RecordHeaderLength); // record data length does not include the record header (first 8 bytes)
            WriteVarULong(data, ref offset, Id);
            WriteVarString(data, ref offset, Name);
        }

        public List<DatabaseRecordFragment> Fragments
        {
            get
            {
                return m_fragments;
            }
        }

        public DatabaseRecordFragment FirstFragment
        {
            get
            {
                if (m_fragments.Count > 0)
                {
                    return m_fragments[0];
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Data stored in the record fragments
        /// </summary>
        public byte[] Data
        {
            get
            {
                if (m_data == null)
                {
                    // Data begins at 0x10 (VBLK header is at 0x00)
                    uint dataLength = BigEndianConverter.ToUInt32(this.FirstFragment.Data, 0x04); // this is the length excluding the record header (8 bytes)
                    m_data = GetDataFromFragments(this.Fragments, RecordHeaderLength + dataLength);
                }
                return m_data;
            }
        }

        /// <param name="recordLength">Record header length + record data length</param>
        protected static byte[] GetDataFromFragments(List<DatabaseRecordFragment> recordFragments, uint recordLength)
        {
            byte[] result = new byte[recordLength];  // record header length + record data length
            int leftToCopy = (int)recordLength;
            foreach (DatabaseRecordFragment fragment in recordFragments)
            { 
                int length = Math.Min(leftToCopy, fragment.Data.Length);
                Array.Copy(fragment.Data, 0, result, recordLength - leftToCopy, length);
                leftToCopy -= length;
            }
            return result;
        }

        /// <param name="fragments">Must be sorted</param>
        public static DatabaseRecord GetDatabaseRecord(List<DatabaseRecordFragment> fragments)
        {
            DatabaseRecord result = null;
            if (fragments.Count != 0)
            {
                // Make sure we have all the records and that the first record is at the top of the fragment list
                if (fragments[0].NumberInGroup == 0 && fragments[0].FragmentCount == fragments.Count)
                {
                    RecordType recordType = (RecordType)(fragments[0].Data[0x03] & 0xF);
                    switch (recordType)
                    {
                        case RecordType.Volume:
                            result = new VolumeRecord(fragments);
                            break;

                        case RecordType.Component:
                            result = new ComponentRecord(fragments);
                            break;

                        case RecordType.Extent:
                            result = new ExtentRecord(fragments);
                            break;

                        case RecordType.Disk:
                            result = new DiskRecord(fragments);
                            break;

                        case RecordType.DiskGroup:
                            result = new DiskGroupRecord(fragments);
                            break;

                        default:
                            throw new NotImplementedException("Unrecognized record type: " + recordType);
                    }
                }
                else
                {
                    throw new InvalidDataException("Incomplete or unsorted record");
                }
            }
            return result;
        }

        /// <summary>
        /// Return record fragments containing updated record data.
        /// SequenceNumber and GroupNumber have to be set before writing these fragments to the database.
        /// </summary>
        private static List<DatabaseRecordFragment> GetUpdatedFragments(int blockSize, byte[] data)
        {
            int fragmentDataLength = blockSize - DatabaseRecordFragment.HeaderLength;
            int fragmentCount = (int)Math.Ceiling((double)data.Length / fragmentDataLength);

            List<DatabaseRecordFragment> result = new List<DatabaseRecordFragment>();
            int dataOffset = 0;
            for (int numberInGroup = 0; numberInGroup < fragmentCount; numberInGroup++)
            {
                DatabaseRecordFragment fragment = new DatabaseRecordFragment();
                fragment.NumberInGroup = (ushort)numberInGroup;
                fragment.FragmentCount = (ushort)fragmentCount;

                fragment.Data = new byte[fragmentDataLength];
                int currentDataLength = Math.Min((int)fragmentDataLength, data.Length - dataOffset);
                Array.Copy(data, dataOffset, fragment.Data, 0, currentDataLength);
                dataOffset += currentDataLength;
                result.Add(fragment);
            }
            return result;
        }

        /// <summary>
        /// DMDiag reports some variable fields as invalid if they occupy more than 4 bytes (excluding the length byte prefix)
        /// </summary>
        protected static uint ReadVarUInt(byte[] buffer, ref int offset)
        {
            return (uint)ReadVarULong(buffer, ref offset);
        }

        protected static ulong ReadVarULong(byte[] buffer, ref int offset)
        {
            int length = buffer[offset];

            ulong result = 0;
            for (int i = 0; i < length; ++i)
            {
                result = (result << 8) | buffer[offset + i + 1];
            }

            offset += length + 1;

            return result;
        }

        protected static long ReadVarLong(byte[] buffer, ref int offset)
        {
            return (long)ReadVarULong(buffer, ref offset);
        }

        protected static string ReadVarString(byte[] buffer, ref int offset)
        {
            int length = buffer[offset];

            string result = ByteReader.ReadAnsiString(buffer, offset + 1, length);
            offset += length + 1;
            return result;
        }

        protected static void WriteVarUInt(byte[] buffer, ref int offset, uint value)
        {
            WriteVarULong(buffer, ref offset, value);
        }

        protected static void WriteVarULong(byte[] buffer, ref int offset, ulong value)
        {
            List<byte> components = new List<byte>();
            while (value > 0)
            { 
                byte component = (byte)(value & 0xFF);
                components.Add(component);
                value = value >> 8;
            }
            components.Reverse();

            byte length = (byte)components.Count;
            buffer[offset] = length;
            for (int index = 0; index < components.Count; index++)
            {
                buffer[offset + index + 1] = components[index];
            }
            offset += length + 1;
        }

        protected static void WritePaddedVarULong(byte[] buffer, ref int offset, ulong value)
        {
            List<byte> components = new List<byte>();
            while (value > 0)
            {
                byte component = (byte)(value & 0xFF);
                components.Add(component);
                value = value >> 8;
            }
            components.Reverse();

            // PaddedVarULong that is not within the range of UInt32, must have length of 8
            if (components.Count > 4)
            {
                while (components.Count < 8)
                {
                    components.Insert(0, 0);
                }
            }

            byte length = (byte)components.Count;
            buffer[offset] = length;
            for (int index = 0; index < components.Count; index++)
            {
                buffer[offset + index + 1] = components[index];
            }
            offset += length + 1;
        }

        protected static void WriteVarString(byte[] buffer, ref int offset, string value)
        {
            buffer[offset] = (byte)value.Length;
            offset++;
            ByteWriter.WriteAnsiString(buffer, offset, value, value.Length);
            offset += value.Length;
        }

        protected static int VarUIntSize(uint value)
        {
            return VarULongSize(value);
        }

        protected static int VarULongSize(ulong value)
        {
            int size = 1;
            while (value > 0)
            {
                value = value >> 8;
                size++;
            }
            return size;
        }

        protected static int PaddedVarULongSize(ulong value)
        {
            int size = VarULongSize(value);
            // PaddedVarULong that is not within the range of UInt32, must have length of 8
            if (size > 5)
            {
                size = 9;
            }
            return size;
        }

        public override bool Equals(object obj)
        {
            if (obj is DatabaseRecord)
            {
                return ((DatabaseRecord)obj).Id == this.Id;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }

        public virtual DatabaseRecord Clone()
        {
            // We do not want to clone the original fragments
            List<DatabaseRecordFragment> fragments = m_fragments;
            m_fragments = new List<DatabaseRecordFragment>();
            DatabaseRecord clone = (DatabaseRecord)MemberwiseClone();
            m_fragments = fragments;
            return clone;
        }
    }
}
