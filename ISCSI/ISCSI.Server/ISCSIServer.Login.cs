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
        private LoginResponsePDU GetLoginResponsePDU(LoginRequestPDU request, ConnectionParameters connection)
        {
            // RFC 3720: The numbering fields (StatSN, ExpCmdSN, MaxCmdSN) are only valid if status-Class is 0.
            // RFC 3720: Command numbering starts with the first login request on the first connection of a session
            if (request.Continue)
            {
                connection.AddTextToSequence(request.InitiatorTaskTag, request.LoginParametersText);
                return GetPartialLoginResponsePDU(request, connection);
            }
            else
            {
                string text = connection.AddTextToSequence(request.InitiatorTaskTag, request.LoginParametersText);
                connection.RemoveTextSequence(request.InitiatorTaskTag);
                KeyValuePairList<string, string> loginParameters = KeyValuePairUtils.GetKeyValuePairList(text);
                if (connection.Session == null)
                {
                    LoginResponseStatusName status = SetUpSession(request, loginParameters, connection);
                    if (status != LoginResponseStatusName.Success)
                    {
                        LoginResponsePDU response = GetLoginResponseTemplate(request);
                        response.Transit = request.Transit;
                        response.Status = status;
                        return response;
                    }
                }
                return GetFinalLoginResponsePDU(request, loginParameters, connection);
            }
        }

        private LoginResponsePDU GetPartialLoginResponsePDU(LoginRequestPDU request, ConnectionParameters connection)
        {
            LoginResponsePDU response = GetLoginResponseTemplate(request);
            response.Transit = false;
            response.InitiatorTaskTag = request.InitiatorTaskTag;
            response.ExpCmdSN = request.CmdSN; // We must set ExpCmdSN ourselves because we haven't set up the session yet.
            if (request.Transit)
            {
                Log(Severity.Warning, "[{0}] Initiator error: Received login request with both Transit and Continue set to true", connection.ConnectionIdentifier);
                response.Status = LoginResponseStatusName.InitiatorError;
                return response;
            }
            response.Status = LoginResponseStatusName.Success;
            return response;
        }

        private LoginResponseStatusName SetUpSession(LoginRequestPDU request, KeyValuePairList<string, string> requestParameters, ConnectionParameters connection)
        {
            string initiatorName = requestParameters.ValueOf("InitiatorName");
            if (String.IsNullOrEmpty(initiatorName))
            {
                // RFC 3720: InitiatorName: The initiator of the TCP connection MUST provide this key [..]
                // at the first Login of the Login Phase for every connection.
                string loginIdentifier = String.Format("ISID={0},TSIH={1},CID={2}", request.ISID.ToString("x"), request.TSIH.ToString("x"), request.CID.ToString("x"));
                Log(Severity.Warning, "[{0}] Initiator error: InitiatorName was not included in the login request", loginIdentifier);
                return LoginResponseStatusName.InitiatorError;
            }

            if (request.TSIH == 0)
            {
                // Note: An initiator could login with the same ISID to a different target (another session),
                // We should only perform session reinstatement when an initiator is logging in to the same target.

                // For a new session, the request TSIH is zero,
                // As part of the response, the target generates a TSIH.
                connection.Session = m_sessionManager.StartSession(initiatorName, request.ISID);
                connection.CID = request.CID;
                Log(Severity.Verbose, "[{0}] Session has been started", connection.Session.SessionIdentifier);
                connection.Session.CommandNumberingStarted = true;
                connection.Session.ExpCmdSN = request.CmdSN;

                string sessionType = requestParameters.ValueOf("SessionType");
                if (sessionType == "Discovery")
                {
                    connection.Session.IsDiscovery = true;
                }
                else //sessionType == "Normal" or unspecified (default is Normal)
                {
                    if (requestParameters.ContainsKey("TargetName"))
                    {
                        string targetName = requestParameters.ValueOf("TargetName");
                        return SetUpNormalSession(request, targetName, connection);
                    }
                    else
                    {
                        // RFC 3720: For any connection within a session whose type is not "Discovery", the first Login Request MUST also include the TargetName key=value pair.
                        Log(Severity.Warning, "[{0}] Initiator error: TargetName was not included in a non-discovery session", connection.ConnectionIdentifier);
                        return LoginResponseStatusName.InitiatorError;
                    }
                }
            }
            else
            {
                ISCSISession existingSession = m_sessionManager.FindSession(initiatorName, request.ISID, request.TSIH);
                if (existingSession == null)
                {
                    return LoginResponseStatusName.SessionDoesNotExist;
                }
                else
                {
                    connection.Session = existingSession;
                    ConnectionState existingConnection = m_connectionManager.FindConnection(existingSession, request.CID);
                    if (existingConnection != null)
                    {
                        // do connection reinstatement
                        Log(Severity.Verbose, "[{0}] Initiating implicit logout", existingConnection.ConnectionIdentifier);
                        m_connectionManager.ReleaseConnection(existingConnection);
                    }
                    else
                    {
                        // add a new connection to the session
                        if (m_connectionManager.GetSessionConnections(existingSession).Count > existingSession.MaxConnections)
                        {
                            return LoginResponseStatusName.TooManyConnections;
                        }
                    }
                    connection.CID = request.CID;
                }
            }
            return LoginResponseStatusName.Success;
        }

        private LoginResponseStatusName SetUpNormalSession(LoginRequestPDU request, string targetName, ConnectionParameters connection)
        {
            ISCSISession session = connection.Session;
            session.IsDiscovery = false;
            // If there's an existing session between this initiator and the target, we should terminate the
            // old session before reinstating a new iSCSI session in its place.
            ISCSISession existingSession = m_sessionManager.FindSession(session.InitiatorName, request.ISID, targetName);
            if (existingSession != null)
            {
                Log(Severity.Verbose, "[{0}] Terminating old session with target: {1}", connection.ConnectionIdentifier, targetName);
                List<ConnectionState> existingConnections = m_connectionManager.GetSessionConnections(existingSession);
                foreach (ConnectionState existingConnection in existingConnections)
                {
                    m_connectionManager.ReleaseConnection(existingConnection);
                }
                m_sessionManager.RemoveSession(existingSession, SessionTerminationReason.ImplicitLogout);
            }
            // We use m_targets.Lock to synchronize between the login logic and the target removal logic.
            lock (m_targets.Lock)
            {
                ISCSITarget target = m_targets.FindTarget(targetName);
                if (target != null)
                {
                    if (!target.AuthorizeInitiator(session.InitiatorName, session.ISID, connection.InitiatorEndPoint))
                    {
                        Log(Severity.Warning, "[{0}] Initiator was not authorized to access {1}", connection.ConnectionIdentifier, targetName);
                        return LoginResponseStatusName.AuthorizationFailure;
                    }
                    session.Target = target;
                }
                else
                {
                    Log(Severity.Warning, "[{0}] Initiator requested an unknown target: {1}", connection.ConnectionIdentifier, targetName);
                    return LoginResponseStatusName.NotFound;
                }
            }

            return LoginResponseStatusName.Success;
        }

        private LoginResponsePDU GetFinalLoginResponsePDU(LoginRequestPDU request, KeyValuePairList<string, string> requestParameters, ConnectionParameters connection)
        {
            LoginResponsePDU response = GetLoginResponseTemplate(request);
            response.Transit = request.Transit;
            response.TSIH = connection.Session.TSIH;

            string connectionIdentifier = connection.ConnectionIdentifier;
            response.Status = LoginResponseStatusName.Success;

            ISCSISession session = connection.Session;

            // RFC 3720:  The login process proceeds in two stages - the security negotiation
            // stage and the operational parameter negotiation stage.  Both stages are optional
            // but at least one of them has to be present.
            
            // The stage codes are:
            // 0 - SecurityNegotiation
            // 1 - LoginOperationalNegotiation
            // 3 - FullFeaturePhase
            if (request.CurrentStage == 0)
            {
                KeyValuePairList<string, string> responseParameters = new KeyValuePairList<string, string>();
                responseParameters.Add("AuthMethod", "None");
                if (session.Target != null)
                {
                    // RFC 3720: During the Login Phase the iSCSI target MUST return the TargetPortalGroupTag key with the first Login Response PDU with which it is allowed to do so
                    responseParameters.Add("TargetPortalGroupTag", "1");
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
                response.LoginParameters = responseParameters;
            }
            else if (request.CurrentStage == 1)
            {
                UpdateOperationalParameters(requestParameters, connection);
                response.LoginParameters = GetLoginResponseOperationalParameters(connection);

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

        private LoginResponsePDU GetLoginResponseTemplate(LoginRequestPDU request)
        {
            LoginResponsePDU response = new LoginResponsePDU();
            response.Continue = false;
            response.CurrentStage = request.CurrentStage;
            response.NextStage = request.NextStage;
            response.VersionMax = request.VersionMax;
            response.VersionActive = request.VersionMin;
            response.ISID = request.ISID;
            // TSIH: With the exception of the Login Final-Response in a new session, this field should
            // be set to the TSIH provided by the initiator in the Login Request.
            response.TSIH = request.TSIH;
            response.InitiatorTaskTag = request.InitiatorTaskTag;
            return response;
        }

        private static void UpdateOperationalParameters(KeyValuePairList<string, string> loginParameters, ConnectionParameters connection)
        {
            ISCSISession session = connection.Session;
            string value = loginParameters.ValueOf("MaxRecvDataSegmentLength");
            if (value != null)
            {
                connection.InitiatorMaxRecvDataSegmentLength = Convert.ToInt32(value);
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

        private static KeyValuePairList<string, string> GetLoginResponseOperationalParameters(ConnectionParameters connection)
        {
            ISCSISession session = connection.Session;
            KeyValuePairList<string, string> loginParameters = new KeyValuePairList<string, string>();
            loginParameters.Add("HeaderDigest", "None");
            loginParameters.Add("DataDigest", "None");
            loginParameters.Add("MaxRecvDataSegmentLength", connection.TargetMaxRecvDataSegmentLength.ToString());
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
            }
            loginParameters.Add("DefaultTime2Wait", session.DefaultTime2Wait.ToString());
            loginParameters.Add("DefaultTime2Retain", session.DefaultTime2Retain.ToString());
            loginParameters.Add("ErrorRecoveryLevel", session.ErrorRecoveryLevel.ToString());
            
            return loginParameters;
        }
    }
}
