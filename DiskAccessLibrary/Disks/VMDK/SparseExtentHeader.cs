/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using Utilities;

namespace DiskAccessLibrary.VMDK
{
    public class SparseExtentHeader
    {
        private const string ValidSignature = "KDMV";

        public string Signature; // MagicNumber
        public uint Version;
        public uint Flags;
        public ulong Capacity; // multiple of the grain size
        public ulong GrainSize; // Expressed in sectors
        public ulong DescriptorOffset; // Expressed in sectors
        public ulong DescriptorSize; // Expressed in sectors
        public uint NumGTEsPerGT;
        public ulong RGDOffset; // Expressed in sectors
        public ulong GDOffset;  // Expressed in sectors
        public ulong OverHead;
        public bool UncleanShutdown; // Stored as byte 
        public char SingleEndLineChar;
        public char NonEndLineChar;
        public char DoubleEndLineChar1;
        public char DoubleEndLineChar2;
        public SparseExtentCompression CompressionAlgirithm;

        public SparseExtentHeader()
        {
        }

        public SparseExtentHeader(byte[] buffer)
        {
            Signature = ByteReader.ReadAnsiString(buffer, 0x00, 4);
            if (!String.Equals(Signature, ValidSignature))
            {
                throw new InvalidDataException("Sparse extent header signature is invalid");
            }
            Version = LittleEndianConverter.ToUInt32(buffer, 0x04);
            Flags = LittleEndianConverter.ToUInt32(buffer, 0x08);
            Capacity = LittleEndianConverter.ToUInt64(buffer, 0x0C);
            GrainSize = LittleEndianConverter.ToUInt64(buffer, 0x14);
            DescriptorOffset = LittleEndianConverter.ToUInt64(buffer, 0x1C);
            DescriptorSize = LittleEndianConverter.ToUInt64(buffer, 0x24);
            NumGTEsPerGT = LittleEndianConverter.ToUInt32(buffer, 0x2C);
            RGDOffset = LittleEndianConverter.ToUInt64(buffer, 0x30);
            GDOffset = LittleEndianConverter.ToUInt64(buffer, 0x38);
            OverHead = LittleEndianConverter.ToUInt64(buffer, 0x40);
            UncleanShutdown = ByteReader.ReadByte(buffer, 0x48) == 1;
            SingleEndLineChar = (char)ByteReader.ReadByte(buffer, 0x49);
            NonEndLineChar = (char)ByteReader.ReadByte(buffer, 0x4A);
            DoubleEndLineChar1 = (char)ByteReader.ReadByte(buffer, 0x4B);
            DoubleEndLineChar2 = (char)ByteReader.ReadByte(buffer, 0x4C);
            CompressionAlgirithm = (SparseExtentCompression)LittleEndianConverter.ToUInt16(buffer, 0x4D);
        }

        public bool IsSupported
        {
            get
            {
                return (Version == 1);
            }
        }
    }
}
