/* Copyright (C) 2012-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using Utilities;

namespace ISCSI.Server
{
    public partial class ISCSIServer
    {
        private ISCSIPDU GetLogoutResponsePDU(LogoutRequestPDU request, ConnectionParameters connection)
        {
            Log(Severity.Verbose, "[{0}] Logout Request", connection.ConnectionIdentifier);
            if (connection.Session.IsDiscovery && request.ReasonCode != LogoutReasonCode.CloseTheSession)
            {
                // RFC 3720: Discovery-session: The target MUST ONLY accept [..] logout request with the reason "close the session"
                RejectPDU reject = new RejectPDU();
                reject.Reason = RejectReason.ProtocolError;
                reject.Data = ByteReader.ReadBytes(request.GetBytes(), 0, 48);
                return reject;
            }
            else
            {
                List<ConnectionState> connectionsToClose = new List<ConnectionState>();
                if (request.ReasonCode == LogoutReasonCode.CloseTheSession)
                {
                    connectionsToClose = m_connectionManager.GetSessionConnections(connection.Session);
                }
                else if (request.ReasonCode == LogoutReasonCode.CloseTheConnection)
                {
                    // RFC 3720: A Logout for a CID may be performed on a different transport connection when the TCP connection for the CID has already been terminated.
                    ConnectionState existingConnection = m_connectionManager.FindConnection(connection.Session, request.CID);
                    if (existingConnection != null)
                    {
                        connectionsToClose.Add(existingConnection);
                    }
                    else
                    {
                        return ServerResponseHelper.GetLogoutResponsePDU(request, LogoutResponse.CIDNotFound);
                    }
                }
                else if (request.ReasonCode == LogoutReasonCode.RemoveTheConnectionForRecovery)
                {
                    return ServerResponseHelper.GetLogoutResponsePDU(request, LogoutResponse.ConnectionRecoveryNotSupported);
                }
                else
                {
                    // Unknown LogoutRequest ReasonCode
                    RejectPDU reject = new RejectPDU();
                    reject.Reason = RejectReason.ProtocolError;
                    reject.Data = ByteReader.ReadBytes(request.GetBytes(), 0, 48);
                    return reject;
                }

                foreach (ConnectionState connectionToClose in connectionsToClose)
                {
                    // Wait for pending I/O to complete.
                    connectionToClose.RunningSCSICommands.WaitUntilZero();
                    if (connectionToClose.ConnectionParameters != connection)
                    {
                        SocketUtils.ReleaseSocket(connectionToClose.ClientSocket);
                    }
                    m_connectionManager.RemoveConnection(connectionToClose);
                }

                if (request.ReasonCode == LogoutReasonCode.CloseTheSession)
                {
                    Log(Severity.Verbose, "[{0}] Session has been closed", connection.Session.SessionIdentifier);
                    m_sessionManager.RemoveSession(connection.Session, SessionTerminationReason.Logout);
                }

                return ServerResponseHelper.GetLogoutResponsePDU(request, LogoutResponse.ClosedSuccessfully);
                // connection will be closed after a LogoutResponsePDU has been sent.
            }
        }
    }
}
