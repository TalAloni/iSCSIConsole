/* Copyright (C) 2012-2017 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace SCSI
{
    public enum ProtocolName : byte
    {
        FibreChannel = 0,
        ParallelSCSI = 1,
        SSA = 2,
        IEEE1394 = 3,
        SCSIRDMA = 4,
        ISCSI = 5,
        SAS = 6,
        ADT = 7,
        ATA = 8,
    }

    public enum CodeSetName : byte
    {
        Binary = 1,
        ASCII = 2,
        UTF8 = 3,
    }

    public enum AssociationName : byte
    {
        LogicalUnit = 0,
        TargetPort = 1,
        TargetDevice = 2,
    }

    public class IdentificationDescriptor
    {
        public ProtocolName ProtocolIdentifier;
        public CodeSetName CodeSet;
        public bool PIV;
        public AssociationName Association;
        public IdentifierTypeName IdentifierType;
        public byte IdentifierLength;
        public byte[] Identifier = new byte[0];

        public IdentificationDescriptor()
        {
        }

        public IdentificationDescriptor(IdentifierTypeName identifierType, byte[] identifier)
        {
            CodeSet = CodeSetName.Binary;
            IdentifierType = identifierType;
            Identifier = identifier;
        }

        public IdentificationDescriptor(IdentifierTypeName identifierType, string identifier)
        {
            CodeSet = CodeSetName.ASCII;
            IdentifierType = identifierType;
            Identifier = ASCIIEncoding.ASCII.GetBytes(identifier);
        }

        public IdentificationDescriptor(byte[] buffer, int offset)
        {
            ProtocolIdentifier = (ProtocolName)((buffer[offset + 0] >> 4) & 0x0F);
            CodeSet = (CodeSetName)(buffer[offset + 0] & 0x0F);
            PIV = (buffer[offset + 1] & 0x80) != 0;
            Association = (AssociationName)((buffer[offset + 1] >> 4) & 0x03);
            IdentifierType = (IdentifierTypeName)(buffer[offset + 1] & 0x0F);

            IdentifierLength = buffer[offset + 3];
            Identifier = new byte[IdentifierLength];
            Array.Copy(buffer, offset + 4, Identifier, 0, IdentifierLength);
        }

        public byte[] GetBytes()
        {
            IdentifierLength = (byte)Identifier.Length;

            byte[] buffer = new byte[4 + Identifier.Length];
            buffer[0] |= (byte)((byte)ProtocolIdentifier << 4);
            buffer[0] |= (byte)((byte)CodeSet & 0x0F);
            
            if (PIV)
            {
                buffer[1] |= 0x80;
            }
            buffer[1] |= (byte)(((byte)Association & 0x03) << 4);
            buffer[1] |= (byte)((byte)IdentifierType & 0x0F);
            buffer[3] = (byte)Identifier.Length;
            Array.Copy(Identifier, 0, buffer, 4, Identifier.Length);

            return buffer;
        }

        public int Length
        {
            get
            {
                return 4 + Identifier.Length;
            }
        }

        /// <param name="oui">UInt24</param>
        public static IdentificationDescriptor GetEUI64Identifier(uint oui, uint vendorSpecific)
        {
            byte[] eui64 = new byte[8];
            BigEndianWriter.WriteUInt24(eui64, 0, oui);
            // we leave byte 3 zeroed-out
            BigEndianWriter.WriteUInt32(eui64, 4, vendorSpecific);
            return new IdentificationDescriptor(IdentifierTypeName.EUI64, eui64);
        }

        /// <param name="oui">UInt24</param>
        public static IdentificationDescriptor GetNAAExtendedIdentifier(uint oui, uint vendorSpecific)
        {
            byte[] identifier = new byte[8];
            identifier[0] = 0x02 << 4;
            BigEndianWriter.WriteUInt24(identifier, 2, oui);
            BigEndianWriter.WriteUInt24(identifier, 5, vendorSpecific);
            return new IdentificationDescriptor(IdentifierTypeName.NAA, identifier);
        }

        public static IdentificationDescriptor GetSCSINameStringIdentifier(string iqn)
        {
            IdentificationDescriptor result = new IdentificationDescriptor(IdentifierTypeName.ScsiNameString, iqn);
            result.Association = AssociationName.TargetDevice;
            result.ProtocolIdentifier = ProtocolName.ISCSI;
            return result;
        }
    }
}
