/* Copyright (C) 2012-2015 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace ISCSI
{
    public class IdentificationDescriptor
    {
        public byte ProtocolIdentifier;
        public byte CodeSet;
        public bool PIV;
        public byte Association;
        public byte IdentifierType;
        public byte IdentifierLength;
        public byte[] Identifier = new byte[0];

        public IdentificationDescriptor()
        {
        }

        public IdentificationDescriptor(ulong eui64Identifier)
        {
            CodeSet = 0x01;
            IdentifierType = 0x02; // EUI-64
            Identifier = BigEndianConverter.GetBytes(eui64Identifier);
        }

        public IdentificationDescriptor(ulong eui64Identifier, ulong wwn)
        {
            /*
             * In Fibre Channel, the unique identity of a device is provided by a 64-bit WWN, whereas the network address is the 24-bit Fibre Channel address.
             * The WWN convention is also accommodated by iSCSI naming as an IEEE extended unique identifier (EUI) format or “eui.”
             * The resulting iSCSI name is simply “eui” followed by the hexidecimal WWN (for example, eui.0300732A32598D26). 
             * */
            CodeSet = 0x01;
            IdentifierType = 0x03;
            Identifier = new byte[16];
            Array.Copy(BigEndianConverter.GetBytes(eui64Identifier), 0, Identifier, 0, 8);
            Array.Copy(BigEndianConverter.GetBytes(wwn), 0, Identifier, 8, 8);
        }

        public IdentificationDescriptor(byte[] identifier)
        {
            CodeSet = 0x01; // The IDENTIFIER field shall contain binary values
            Identifier = identifier;
        }

        public IdentificationDescriptor(string identifier)
        {
            CodeSet = 0x02; // The IDENTIFIER field shall contain ASCII graphic codes
            Identifier = ASCIIEncoding.ASCII.GetBytes(identifier);
        }

        public IdentificationDescriptor(byte[] buffer, int offset)
        {
            ProtocolIdentifier = (byte)((buffer[offset + 0] >> 4) & 0x0F);
            CodeSet = (byte)(buffer[offset + 0] & 0x0F);
            PIV = (buffer[offset + 1] & 0x80) != 0;
            Association = (byte)((buffer[offset + 1] >> 4) & 0x03);
            IdentifierType = (byte)(buffer[offset + 1] & 0x0F);

            IdentifierLength = buffer[offset + 3];
            Identifier = new byte[IdentifierLength];
            Array.Copy(buffer, offset + 4, Identifier, 0, IdentifierLength);
        }

        public byte[] GetBytes()
        {
            IdentifierLength = (byte)Identifier.Length;

            byte[] buffer = new byte[4 + Identifier.Length];
            buffer[0] |= (byte)(ProtocolIdentifier << 4);
            buffer[0] |= (byte)(CodeSet & 0x0F);
            
            if (PIV)
            {
                buffer[1] |= 0x80;
            }
            buffer[1] |= (byte)((Association & 0x03) << 4);
            buffer[1] |= (byte)(IdentifierType & 0x0F);
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

        public static IdentificationDescriptor GetISCSIIdentifier(string iqn)
        {
            // RFC 3720: iSCSI names may be transported using both binary and ASCII-based protocols.
            // Note: Microsoft iSCSI Target uses binary CodeSet
            iqn += ",t,0x1"; // 't' for SCSI Target Port, 0x1 portal group tag
            //byte[] bytes = ASCIIEncoding.ASCII.GetBytes(iqn);
            IdentificationDescriptor result = new IdentificationDescriptor(iqn);
            result.ProtocolIdentifier = 0x05; // iSCSI
            return result;
        }
    }
}
