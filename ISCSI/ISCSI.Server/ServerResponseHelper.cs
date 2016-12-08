/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace ISCSI.Server
{
    internal class ServerResponseHelper
    {
        internal static NOPInPDU GetNOPResponsePDU(NOPOutPDU request)
        {
            NOPInPDU response = new NOPInPDU();
            response.Data = request.Data;
            // When a target receives the NOP-Out with a valid Initiator Task Tag (not the reserved value 0xffffffff),
            // it MUST respond with a NOP-In with the same Initiator Task Tag that was provided in the NOP-Out request.
            // For such a response, the Target Transfer Tag MUST be 0xffffffff
            response.InitiatorTaskTag = request.InitiatorTaskTag;
            response.TargetTransferTag = 0xFFFFFFFF;
            return response;
        }

        internal static TextResponsePDU GetTextResponsePDU(TextRequestPDU request, List<ISCSITarget> targets)
        {
            TextResponsePDU response = new TextResponsePDU();
            response.Final = true;
            response.InitiatorTaskTag = request.InitiatorTaskTag;
            KeyValuePairList<string, string> entries = new KeyValuePairList<string, string>();
            foreach (ISCSITarget target in targets)
            {
                entries.Add("TargetName", target.TargetName);
            }
            response.Text = KeyValuePairUtils.ToNullDelimitedString(entries);
            return response;
        }

        internal static LogoutResponsePDU GetLogoutResponsePDU(LogoutRequestPDU request)
        {
            LogoutResponsePDU response = new LogoutResponsePDU();
            response.Response = LogoutResponse.ClosedSuccessfully;
            response.Final = true;
            response.InitiatorTaskTag = request.InitiatorTaskTag;
            return response;
        }
    }
}
