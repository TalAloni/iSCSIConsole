/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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

namespace ISCSI.Server
{
    public class ServerResponseHelper
    {
        // CmdSN is session wide
        // StatSN is per connection
        
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

        internal static LoginResponsePDU GetLoginResponsePDU(LoginRequestPDU request, List<ISCSITarget> availableTargets, SessionParameters session, ConnectionParameters connection, ref ISCSITarget target, GetNextTSIH GetNextTSIH)
        {
            LoginResponsePDU response = new LoginResponsePDU();
            response.Transit = request.Transit;
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

            if (request.TSIH == 0)
            {
                // For a new session, the request TSIH is zero,
                // As part of the response, the target generates a TSIH.
                session.TSIH = GetNextTSIH();
            }
            response.TSIH = session.TSIH;

            if (request.Transit && request.Continue)
            {
                response.Status = LoginResponseStatusName.InitiatorError;
                return response;
            }
            else if (request.Continue)
            {
                response.Status = LoginResponseStatusName.Success;
                return response;
            }

            // RFC 3720:  The login process proceeds in two stages - the security negotiation
            // stage and the operational parameter negotiation stage.  Both stages are optional
            // but at least one of them has to be present.
            bool firstLoginRequest = (!session.IsDiscovery && target == null);
            if (firstLoginRequest)
            {
                connection.InitiatorName = request.LoginParameters.ValueOf("InitiatorName");
                if (String.IsNullOrEmpty(connection.InitiatorName))
                {
                    // RFC 3720: InitiatorName: The initiator of the TCP connection MUST provide this key [..]
                    // at the first Login of the Login Phase for every connection.
                    response.Status = LoginResponseStatusName.InitiatorError;
                    return response;
                }
                string sessionType = request.LoginParameters.ValueOf("SessionType");
                if (sessionType == "Discovery")
                {
                    session.IsDiscovery = true;
                }
                else //sessionType == "Normal" or unspecified (default is Normal)
                {
                    session.IsDiscovery = false;
                    if (request.LoginParameters.ContainsKey("TargetName"))
                    {
                        string targetName = request.LoginParameters.ValueOf("TargetName");
                        int targetIndex = GetTargetIndex(availableTargets, targetName);
                        if (targetIndex >= 0)
                        {
                            target = availableTargets[targetIndex];
                            if (!target.AuthorizeInitiator(connection.InitiatorEndPoint, connection.InitiatorName))
                            {
                                response.Status = LoginResponseStatusName.AuthorizationFailure;
                                return response;
                            }
                        }
                        else
                        {
                            response.Status = LoginResponseStatusName.NotFound;
                            return response;
                        }
                    }
                    else
                    {
                        // RFC 3720: For any connection within a session whose type is not "Discovery", the first Login Request MUST also include the TargetName key=value pair.
                        response.Status = LoginResponseStatusName.InitiatorError;
                        return response;
                    }
                }
            }

            if (request.CurrentStage == 0)
            {
                response.LoginParameters.Add("AuthMethod", "None");
                if (target != null)
                {
                    // RFC 3720: During the Login Phase the iSCSI target MUST return the TargetPortalGroupTag key with the first Login Response PDU with which it is allowed to do so
                    response.LoginParameters.Add("TargetPortalGroupTag", "1");
                }

                if (request.Transit)
                {
                    if (request.NextStage == 3)
                    {
                        session.IsFullFeaturePhase = true;
                    }
                    else if (request.NextStage != 1)
                    {
                        response.Status = LoginResponseStatusName.InitiatorError;
                    }
                }
            }
            else if (request.CurrentStage == 1)
            {
                UpdateOperationalParameters(request.LoginParameters, session, connection);
                response.LoginParameters = GetLoginOperationalParameters(session, connection);

                if (request.Transit)
                {
                    if (request.NextStage == 3)
                    {
                        session.IsFullFeaturePhase = true;
                    }
                    else
                    {
                        response.Status = LoginResponseStatusName.InitiatorError;
                    }
                }
            }
            else
            {
                // Not valid
                response.Status = LoginResponseStatusName.InitiatorError;
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

        public static void UpdateOperationalParameters(KeyValuePairList<string, string> loginParameters, SessionParameters sessionParameters, ConnectionParameters connectionParameters)
        {
            string value = loginParameters.ValueOf("MaxRecvDataSegmentLength");
            if (value != null)
            {
                connectionParameters.InitiatorMaxRecvDataSegmentLength = Convert.ToInt32(value);
            }

            value = loginParameters.ValueOf("MaxConnections");
            if (value != null)
            {
                sessionParameters.MaxConnections = Math.Min(Convert.ToInt32(value), ISCSIServer.DesiredParameters.MaxConnections);
            }

            value = loginParameters.ValueOf("InitialR2T");
            if (value != null)
            {
                sessionParameters.InitialR2T = (value == "Yes") || ISCSIServer.DesiredParameters.InitialR2T;
            }

            value = loginParameters.ValueOf("ImmediateData");
            if (value != null)
            {
                sessionParameters.ImmediateData = (value == "Yes") && ISCSIServer.DesiredParameters.ImmediateData;
            }

            value = loginParameters.ValueOf("MaxBurstLength");
            if (value != null)
            {
                sessionParameters.MaxBurstLength = Math.Min(Convert.ToInt32(value), ISCSIServer.DesiredParameters.MaxBurstLength);
            }

            value = loginParameters.ValueOf("FirstBurstLength");
            if (value != null)
            {
                sessionParameters.FirstBurstLength = Math.Min(Convert.ToInt32(value), ISCSIServer.DesiredParameters.FirstBurstLength);
            }

            value = loginParameters.ValueOf("DataPDUInOrder");
            if (value != null)
            {
                sessionParameters.DataPDUInOrder = (value == "Yes") || ISCSIServer.DesiredParameters.DataPDUInOrder;
            }

            value = loginParameters.ValueOf("DataSequenceInOrder");
            if (value != null)
            {
                sessionParameters.DataSequenceInOrder = (value == "Yes") || ISCSIServer.DesiredParameters.DataSequenceInOrder;
            }

            value = loginParameters.ValueOf("DefaultTime2Wait");
            if (value != null)
            {
                sessionParameters.DefaultTime2Wait = Math.Max(Convert.ToInt32(value), ISCSIServer.DesiredParameters.DefaultTime2Wait);
            }

            value = loginParameters.ValueOf("DefaultTime2Retain");
            if (value != null)
            {
                sessionParameters.DefaultTime2Retain = Math.Min(Convert.ToInt32(value), ISCSIServer.DesiredParameters.DefaultTime2Retain);
            }

            value = loginParameters.ValueOf("MaxOutstandingR2T");
            if (value != null)
            {
                sessionParameters.MaxOutstandingR2T = Math.Min(Convert.ToInt32(value), ISCSIServer.DesiredParameters.MaxOutstandingR2T);
            }
        }

        public static KeyValuePairList<string, string> GetLoginOperationalParameters(SessionParameters sessionParameters, ConnectionParameters connectionParameters)
        {
            KeyValuePairList<string, string> loginParameters = new KeyValuePairList<string, string>();
            loginParameters.Add("HeaderDigest", "None");
            loginParameters.Add("DataDigest", "None");
            loginParameters.Add("MaxRecvDataSegmentLength", connectionParameters.TargetMaxRecvDataSegmentLength.ToString());
            if (!sessionParameters.IsDiscovery)
            {
                loginParameters.Add("MaxConnections", sessionParameters.MaxConnections.ToString());
                loginParameters.Add("InitialR2T", sessionParameters.InitialR2T ? "Yes" : "No");    // Microsoft iSCSI Target support InitialR2T = No
                loginParameters.Add("ImmediateData", sessionParameters.ImmediateData ? "Yes" : "No");
                loginParameters.Add("MaxBurstLength", sessionParameters.MaxBurstLength.ToString());
                loginParameters.Add("FirstBurstLength", sessionParameters.FirstBurstLength.ToString());
                loginParameters.Add("MaxOutstandingR2T", sessionParameters.MaxOutstandingR2T.ToString());
                loginParameters.Add("DataPDUInOrder", sessionParameters.DataPDUInOrder ? "Yes" : "No");
                loginParameters.Add("DataSequenceInOrder", sessionParameters.DataSequenceInOrder ? "Yes" : "No");
                loginParameters.Add("ErrorRecoveryLevel", sessionParameters.ErrorRecoveryLevel.ToString());
            }
            loginParameters.Add("DefaultTime2Wait", sessionParameters.DefaultTime2Wait.ToString());
            loginParameters.Add("DefaultTime2Retain", sessionParameters.DefaultTime2Retain.ToString());
            
            return loginParameters;
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
