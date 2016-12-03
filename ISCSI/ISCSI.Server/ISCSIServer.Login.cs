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
    public partial class ISCSIServer
    {
        private LoginResponsePDU GetLoginResponsePDU(LoginRequestPDU request, ISCSISession session, ConnectionParameters connection)
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
            // TSIH: With the exception of the Login Final-Response in a new session, this field should
            // be set to the TSIH provided by the initiator in the Login Request.
            response.TSIH = request.TSIH;
            response.InitiatorTaskTag = request.InitiatorTaskTag;

            string connectionIdentifier = ConnectionState.GetConnectionIdentifier(session, connection);

            if (request.Transit && request.Continue)
            {
                Log(Severity.Warning, "[{0}] Initiator error: Received login request with both Transit and Continue set to true", connectionIdentifier);
                response.Status = LoginResponseStatusName.InitiatorError;
                return response;
            }
            else if (request.Continue)
            {
                response.Status = LoginResponseStatusName.Success;
                return response;
            }

            if (request.TSIH == 0)
            {
                // For a new session, the request TSIH is zero,
                // As part of the response, the target generates a TSIH.
                session.TSIH = m_sessionManager.GetNextTSIH();
                session.ISID = request.ISID;
            }
            response.TSIH = session.TSIH;

            response.Status = LoginResponseStatusName.Success;

            // RFC 3720:  The login process proceeds in two stages - the security negotiation
            // stage and the operational parameter negotiation stage.  Both stages are optional
            // but at least one of them has to be present.
            bool firstLoginRequest = (!session.IsDiscovery && session.Target == null);
            if (firstLoginRequest)
            {
                connection.InitiatorName = request.LoginParameters.ValueOf("InitiatorName");
                if (String.IsNullOrEmpty(connection.InitiatorName))
                {
                    // RFC 3720: InitiatorName: The initiator of the TCP connection MUST provide this key [..]
                    // at the first Login of the Login Phase for every connection.
                    Log(Severity.Warning, "[{0}] Initiator error: InitiatorName was not included in the login request", connectionIdentifier);
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
                        ISCSITarget target = m_targets.FindTarget(targetName);
                        if (target != null)
                        {
                            if (!target.AuthorizeInitiator(connection.InitiatorName, connection.InitiatorEndPoint))
                            {
                                Log(Severity.Warning, "[{0}] Initiator was not authorized to access {1}", connectionIdentifier, targetName);
                                response.Status = LoginResponseStatusName.AuthorizationFailure;
                                return response;
                            }
                            session.Target = target;
                        }
                        else
                        {
                            Log(Severity.Warning, "[{0}] Initiator requested an unknown target: {1}", connectionIdentifier, targetName);
                            response.Status = LoginResponseStatusName.NotFound;
                            return response;
                        }
                    }
                    else
                    {
                        // RFC 3720: For any connection within a session whose type is not "Discovery", the first Login Request MUST also include the TargetName key=value pair.
                        Log(Severity.Warning, "[{0}] Initiator error: TargetName was not included in a non-discovery session", connectionIdentifier);
                        response.Status = LoginResponseStatusName.InitiatorError;
                        return response;
                    }
                }
            }

            if (request.CurrentStage == 0)
            {
                response.LoginParameters.Add("AuthMethod", "None");
                if (session.Target != null)
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
                        Log(Severity.Warning, "[{0}] Initiator error: Received login request with Invalid NextStage", connectionIdentifier);
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
                        Log(Severity.Warning, "[{0}] Initiator error: Received login request with Invalid NextStage", connectionIdentifier);
                        response.Status = LoginResponseStatusName.InitiatorError;
                    }
                }
            }
            else
            {
                // Not valid
                Log(Severity.Warning, "[{0}] Initiator error: Received login request with Invalid CurrentStage", connectionIdentifier);
                response.Status = LoginResponseStatusName.InitiatorError;
            }

            return response;
        }

        private static void UpdateOperationalParameters(KeyValuePairList<string, string> loginParameters, ISCSISession session, ConnectionParameters connectionParameters)
        {
            string value = loginParameters.ValueOf("MaxRecvDataSegmentLength");
            if (value != null)
            {
                connectionParameters.InitiatorMaxRecvDataSegmentLength = Convert.ToInt32(value);
            }

            value = loginParameters.ValueOf("MaxConnections");
            if (value != null)
            {
                session.MaxConnections = Math.Min(Convert.ToInt32(value), ISCSIServer.DesiredParameters.MaxConnections);
            }

            value = loginParameters.ValueOf("InitialR2T");
            if (value != null)
            {
                session.InitialR2T = (value == "Yes") || ISCSIServer.DesiredParameters.InitialR2T;
            }

            value = loginParameters.ValueOf("ImmediateData");
            if (value != null)
            {
                session.ImmediateData = (value == "Yes") && ISCSIServer.DesiredParameters.ImmediateData;
            }

            value = loginParameters.ValueOf("MaxBurstLength");
            if (value != null)
            {
                session.MaxBurstLength = Math.Min(Convert.ToInt32(value), ISCSIServer.DesiredParameters.MaxBurstLength);
            }

            value = loginParameters.ValueOf("FirstBurstLength");
            if (value != null)
            {
                session.FirstBurstLength = Math.Min(Convert.ToInt32(value), ISCSIServer.DesiredParameters.FirstBurstLength);
            }

            value = loginParameters.ValueOf("DataPDUInOrder");
            if (value != null)
            {
                session.DataPDUInOrder = (value == "Yes") || ISCSIServer.DesiredParameters.DataPDUInOrder;
            }

            value = loginParameters.ValueOf("DataSequenceInOrder");
            if (value != null)
            {
                session.DataSequenceInOrder = (value == "Yes") || ISCSIServer.DesiredParameters.DataSequenceInOrder;
            }

            value = loginParameters.ValueOf("DefaultTime2Wait");
            if (value != null)
            {
                session.DefaultTime2Wait = Math.Max(Convert.ToInt32(value), ISCSIServer.DesiredParameters.DefaultTime2Wait);
            }

            value = loginParameters.ValueOf("DefaultTime2Retain");
            if (value != null)
            {
                session.DefaultTime2Retain = Math.Min(Convert.ToInt32(value), ISCSIServer.DesiredParameters.DefaultTime2Retain);
            }

            value = loginParameters.ValueOf("MaxOutstandingR2T");
            if (value != null)
            {
                session.MaxOutstandingR2T = Math.Min(Convert.ToInt32(value), ISCSIServer.DesiredParameters.MaxOutstandingR2T);
            }
        }

        private static KeyValuePairList<string, string> GetLoginOperationalParameters(ISCSISession session, ConnectionParameters connectionParameters)
        {
            KeyValuePairList<string, string> loginParameters = new KeyValuePairList<string, string>();
            loginParameters.Add("HeaderDigest", "None");
            loginParameters.Add("DataDigest", "None");
            loginParameters.Add("MaxRecvDataSegmentLength", connectionParameters.TargetMaxRecvDataSegmentLength.ToString());
            if (!session.IsDiscovery)
            {
                loginParameters.Add("MaxConnections", session.MaxConnections.ToString());
                loginParameters.Add("InitialR2T", session.InitialR2T ? "Yes" : "No");    // Microsoft iSCSI Target support InitialR2T = No
                loginParameters.Add("ImmediateData", session.ImmediateData ? "Yes" : "No");
                loginParameters.Add("MaxBurstLength", session.MaxBurstLength.ToString());
                loginParameters.Add("FirstBurstLength", session.FirstBurstLength.ToString());
                loginParameters.Add("MaxOutstandingR2T", session.MaxOutstandingR2T.ToString());
                loginParameters.Add("DataPDUInOrder", session.DataPDUInOrder ? "Yes" : "No");
                loginParameters.Add("DataSequenceInOrder", session.DataSequenceInOrder ? "Yes" : "No");
                loginParameters.Add("ErrorRecoveryLevel", session.ErrorRecoveryLevel.ToString());
            }
            loginParameters.Add("DefaultTime2Wait", session.DefaultTime2Wait.ToString());
            loginParameters.Add("DefaultTime2Retain", session.DefaultTime2Retain.ToString());
            
            return loginParameters;
        }
    }
}
