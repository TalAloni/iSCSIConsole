/* Copyright (C) 2012-2015 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace ISCSI
{
    public class ISCSIConnection // our server allows only one connection per session
    {
        public SessionParameters SessionParameters = new SessionParameters();
        public ConnectionParameters ConnectionParameters = new ConnectionParameters();
        public object WriteLock = new object();

        public string Identifier
        {
            get
            {
                return String.Format("ISID={0},TSIH={1},CID={2}", SessionParameters.ISID.ToString("x"), SessionParameters.TSIH.ToString("x"), ConnectionParameters.CID.ToString("x"));
            }
        }

        public bool IsDiscovery
        {
            get
            {
                return SessionParameters.IsDiscovery;
            }
            set
            {
                SessionParameters.IsDiscovery = value;
            }
        }

        public uint ExpCmdSN
        {
            get
            {
                return SessionParameters.ExpCmdSN;
            }
            set
            {
                SessionParameters.ExpCmdSN = value;
            }
        }

        public Dictionary<uint, uint> NextR2TSN
        {
            get
            {
                return SessionParameters.NextR2TSN;
            }
        }

        public uint StatSN
        {
            get
            {
                return ConnectionParameters.StatSN;
            }
            set
            {
                ConnectionParameters.StatSN = value;
            }
        }

        public Dictionary<uint, KeyValuePair<ulong, uint>> Transfers
        {
            get
            {
                return ConnectionParameters.Transfers;
            }
        }

        public Dictionary<uint, byte[]> TransferData
        {
            get
            {
                return ConnectionParameters.TransferData;
            }
        }
    }
}
