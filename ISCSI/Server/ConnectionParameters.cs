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
    public class ConnectionParameters
    {
        public ushort CID; // connection ID
        public int InitiatorMaxRecvDataSegmentLength = ISCSIServer.DefaultMaxRecvDataSegmentLength; //this is the MaxRecvDataSegmentLength declared by the initator

        public uint StatSN = 0; // Initial StatSN, any number will do
        // Dictionary of current transfers: <transfer-tag, <offset, length>>
        // offset - logical block address (sector)
        // length - data transfer length in bytes
        // Note: here incoming means data write operations to the target
        public Dictionary<uint, KeyValuePair<ulong, uint>> Transfers = new Dictionary<uint, KeyValuePair<ulong, uint>>();

        // Dictionary of transfer data: <transfer-tag, command-data>
        public Dictionary<uint, byte[]> TransferData = new Dictionary<uint, byte[]>();
    }
}
