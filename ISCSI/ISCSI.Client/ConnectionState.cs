/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace ISCSI.Client
{
    internal class ConnectionState
    {
        public const int ReceiveBufferSize = 131072; // Note: FirstBurstLength, MaxBurstLength and MaxRecvDataSegmentLength put a cap on DataSegmentLength, NOT on the PDU length.
        public byte[] ReceiveBuffer = new byte[ReceiveBufferSize]; // immediate receive buffer
        public byte[] ConnectionBuffer = new byte[0]; // we append the receive buffer here until we have a complete PDU
    }
}
