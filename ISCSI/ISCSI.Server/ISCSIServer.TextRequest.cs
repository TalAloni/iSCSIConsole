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
    public partial class ISCSIServer
    {
        private TextResponsePDU GetTextResponsePDU(TextRequestPDU request, ConnectionParameters connection)
        {
            TextResponsePDU response = new TextResponsePDU();
            response.Final = request.Final;
            response.InitiatorTaskTag = request.InitiatorTaskTag;
            if (request.Continue)
            {
                connection.AddTextToSequence(request.InitiatorTaskTag, request.Text);
            }
            else
            {
                string text = connection.AddTextToSequence(request.InitiatorTaskTag, request.Text);
                connection.RemoveTextSequence(request.InitiatorTaskTag);
                KeyValuePairList<string, string> requestParameters = KeyValuePairUtils.GetKeyValuePairList(text);
                // text keys are case sensitive
                if (requestParameters.ContainsKey("SendTargets"))
                {
                    KeyValuePairList<string, string> responseParameters = new KeyValuePairList<string, string>();
                    lock (m_targets.Lock)
                    {
                        foreach (ISCSITarget target in m_targets.GetList())
                        {
                            responseParameters.Add("TargetName", target.TargetName);
                        }
                    }
                    response.TextParameters = responseParameters;
                }
                else if (connection.Session.IsDiscovery || !IsVendorSpecificRequest(requestParameters))
                {
                    KeyValuePairList<string, string> responseParameters = new KeyValuePairList<string, string>();
                    foreach (KeyValuePair<string, string> entry in requestParameters)
                    {
                        responseParameters.Add(entry.Key, "Reject");
                    }
                    response.TextParameters = responseParameters;
                }
                else
                {
                    // RFC 3720: Vendor specific keys MUST ONLY be used in normal sessions
                    // Vendor specific text request, let the target handle it:
                    response.TextParameters = connection.Session.Target.GetTextResponse(requestParameters);
                }
            }
            return response;
        }
        
        private static bool IsVendorSpecificRequest(KeyValuePairList<string, string> requestParameters)
        {
            foreach(string key in requestParameters.Keys)
            {
                // RFC 3720: Implementers may introduce new keys by prefixing them with "X-" [..] or X# if registered with IANA.
                if (!(key.StartsWith("X-") || key.StartsWith("X#")))
                {
                    return false;   
                }
            }
            return (requestParameters.Count > 0);
        }
    }
}
