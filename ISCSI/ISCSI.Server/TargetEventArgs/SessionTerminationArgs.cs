/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;

namespace ISCSI.Server
{
    public enum SessionTerminationReason
    {
        Logout,
        ImplicitLogout, // Session reinstatement
        ConnectionFailure,
        TargetReset,
    }

    public class SessionTerminationArgs : EventArgs
    {
        public string InitiatorName;
        public ulong ISID;
        public SessionTerminationReason Reason;

        public SessionTerminationArgs(string initiatorName, ulong isid, SessionTerminationReason reason)
        {
            InitiatorName = initiatorName;
            ISID = isid;
            Reason = reason;
        }
    }
}
