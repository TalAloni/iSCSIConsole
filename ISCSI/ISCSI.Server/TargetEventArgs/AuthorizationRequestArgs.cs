/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Net;

namespace ISCSI.Server
{
    public class AuthorizationRequestArgs : EventArgs
    {
        public ulong ISID;
        public string InitiatorName;
        public IPEndPoint InitiatorEndPoint;
        public bool Accept = true;

        public AuthorizationRequestArgs(ulong isid, string initiatorName, IPEndPoint initiatorEndPoint)
        {
            ISID = isid;
            InitiatorName = initiatorName;
            InitiatorEndPoint = initiatorEndPoint;
        }
    }
}
