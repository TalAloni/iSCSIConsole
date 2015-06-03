/* Copyright (C) 2012-2015 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Utilities;

namespace ISCSI
{
    public class ServerResponseHelper
    {
        // CmdSN is session wide
        // StatSN is per connection
        
        internal static NOPInPDU GetNOPResponsePDU(NOPOutPDU request, ISCSIConnection connection)
        {
            NOPInPDU response = new NOPInPDU();
            response.Data = request.Data;
            response.ExpCmdSN = request.CmdSN + 1;
            response.MaxCmdSN = response.ExpCmdSN + ISCSIServer.CommandQueueSize; // the queue excludes the next command, we make sure the next command can be always sent
            response.InitiatorTaskTag = request.InitiatorTaskTag;
            response.TargetTransferTag = request.TargetTransferTag;
            response.StatSN = connection.StatSN;

            connection.StatSN++;
            return response;
        }

        internal static LoginResponsePDU GetLoginResponsePDU(LoginRequestPDU request, List<ISCSITarget> availableTargets, ISCSIConnection connection, ref ISCSITarget target, ref ushort nextTSIH)
        {
            LoginResponsePDU response = new LoginResponsePDU();
            response.Transit = true;
            response.Continue = false;
            
            // The stage codes are:
            // 0 - SecurityNegotiation
            // 1 - LoginOperationalNegotiation
            // 3 - FullFeaturePhase

            response.CurrentStage = request.CurrentStage;
            response.NextStage = request.NextStage;

            response.VersionMax = request.VersionMax;
            response.VersionActive = request.VersionMin;
            response.ISID = request.ISID;

            response.Status = LoginResponseStatusName.Success;

            response.InitiatorTaskTag = request.InitiatorTaskTag;

            response.ExpCmdSN = request.CmdSN + 1;
            response.MaxCmdSN = response.ExpCmdSN + ISCSIServer.CommandQueueSize; // the queue excludes the next command, we make sure the next command can be always sent

            // For a new session, the request TSIH is zero,
            // As part of the response, the target generates a TSIH.
            // TSIH RULE: The iSCSI Target selects a non-zero value for the TSIH at
            // session creation (when an initiator presents a 0 value at Login).
            // After being selected, the same TSIH value MUST be used whenever the
            // initiator or target refers to the session and a TSIH is required
            if (request.TSIH == 0)
            {
                connection.SessionParameters.TSIH = nextTSIH;
                response.TSIH = nextTSIH;
                nextTSIH++;
                if (nextTSIH == 0)
                {
                    nextTSIH++;
                }
            }
            else
            {
                connection.SessionParameters.TSIH = request.TSIH;
                response.TSIH = request.TSIH;
            }

            // RFC 3720: For any connection within a session whose type is not "Discovery", the first Login Request MUST also include the TargetName key=value pair.
            if (request.LoginParameters.ContainsKey("TargetName"))
            {
                connection.IsDiscovery = false;

                string targetName = request.LoginParameters.ValueOf("TargetName");
                int targetIndex = GetTargetIndex(availableTargets, targetName);
                if (targetIndex >= 0)
                {
                    target = availableTargets[targetIndex];
                }
            }

            if (connection.IsDiscovery)
            {
                if (request.CurrentStage == 0)
                {
                    response.LoginParameters.Add("AuthMethod", "None");
                }
                else if (request.CurrentStage == 1)
                {
                    if (connection.StatSN == 2)
                    {
                        response.LoginParameters = GetLoginNegotiationParameters();
                    }
                }
                else
                {
                    // AFAIK not valid
                    response.Status = LoginResponseStatusName.InitiatorError;
                }
            }
            else
            {
                if (target == null)
                {
                    response.Status = LoginResponseStatusName.NotFound;
                }
                else
                {
                    if (request.CurrentStage == 0)
                    {
                        response.LoginParameters.Add("AuthMethod", "None");
                    }
                    else if (request.CurrentStage == 1)
                    {
                        // login to target
                        response.LoginParameters = GetLoginNegotiationParameters();
                        response.LoginParameters.Add("ErrorRecoveryLevel", ISCSIServer.ErrorRecoveryLevel.ToString());
                        response.LoginParameters.Add("MaxConnections", ISCSIServer.MaxConnections.ToString());
                    }
                    else
                    {
                        // AFAIK not valid
                        response.Status = LoginResponseStatusName.InitiatorError;
                    }
                }
            }

            if (response.Status == LoginResponseStatusName.Success)
            {
                response.StatSN = connection.StatSN;
                connection.StatSN++;
            }

            return response;
        }

        private static int GetTargetIndex(List<ISCSITarget> targets, string targetName)
        {
            for (int index = 0; index < targets.Count; index++)
            {
                if (String.Equals(targets[index].TargetName, targetName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return index;
                }
            }
            return -1;
        }

        public static KeyValuePairList<string, string> GetLoginNegotiationParameters()
        {
            KeyValuePairList<string, string> loginParameters = new KeyValuePairList<string, string>();
            loginParameters.Add("HeaderDigest", "None");
            loginParameters.Add("DataDigest", "None");
            loginParameters.Add("InitialR2T", ISCSIServer.InitialR2T ? "Yes" : "No");    // Microsoft iSCSI Target support InitialR2T = No
            loginParameters.Add("ImmediateData", ISCSIServer.ImmediateData ? "Yes" : "No");
            loginParameters.Add("MaxRecvDataSegmentLength", ISCSIServer.MaxRecvDataSegmentLength.ToString());
            loginParameters.Add("MaxBurstLength", ISCSIServer.OfferedMaxBurstLength.ToString());
            loginParameters.Add("FirstBurstLength", ISCSIServer.OfferedFirstBurstLength.ToString());
            loginParameters.Add("DefaultTime2Wait", ISCSIServer.DefaultTime2Wait.ToString());
            loginParameters.Add("DefaultTime2Retain", ISCSIServer.DefaultTime2Retain.ToString());
            loginParameters.Add("MaxOutstandingR2T", ISCSIServer.MaxOutstandingR2T.ToString());
            loginParameters.Add("DataPDUInOrder", ISCSIServer.DataPDUInOrder ? "Yes" : "No");
            loginParameters.Add("DataSequenceInOrder", ISCSIServer.DataSequenceInOrder ? "Yes" : "No");
            return loginParameters;
        }

        internal static TextResponsePDU GetTextResponsePDU(TextRequestPDU request, List<ISCSITarget> targets)
        {
            // Format: 
            TextResponsePDU response = new TextResponsePDU();
            response.Final = true;
            response.InitiatorTaskTag = request.InitiatorTaskTag;
            StringBuilder builder = new StringBuilder();
            foreach(ISCSITarget target in targets)
            {
                builder.Append(GetTargetTextDescription(target.TargetName));
            }
            response.Text = builder.ToString();

            return response;
        }

        // multiple descriptions can be concatenated
        private static string GetTargetTextDescription(string targetName)
        {
            // We can settle for TargetName only, the initiator will assume the IP address and TCP port for this target are the same as used on the current connection
            return String.Format("TargetName={0}\0", targetName);
        }

        /// <param name="portalGroupTag">Usually 1</param>
        private static string GetTargetTextDescription(string targetIQN, string targetName, IPAddress targetAddress, int port, int portalGroupTag)
        {
            return String.Format("TargetName={0}:{1}\0TargetAddress={2}:{3},{4}\0", targetIQN, targetName, targetAddress.ToString(), port, portalGroupTag);
        }

        internal static LogoutResponsePDU GetLogoutResponsePDU(LogoutRequestPDU request, ISCSIConnection connection)
        {
            LogoutResponsePDU response = new LogoutResponsePDU();
            response.Response = 0;
            response.Final = true;
            response.InitiatorTaskTag = request.InitiatorTaskTag;
            response.ExpCmdSN = request.CmdSN;
            response.StatSN = connection.StatSN;
            response.MaxCmdSN = request.CmdSN + ISCSIServer.CommandQueueSize;

            connection.StatSN++;
            return response;
        }
    }
}
