/* Copyright (C) 2012-2024 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SCSI;
using Utilities;

namespace ISCSI.Client
{
    public partial class ISCSIClient
    {
        private static readonly int DefaultResponseTimeoutInMilliseconds = 5000;

        private ConnectionParameters m_connection = new ConnectionParameters();

        private string m_initiatorName;
        private IPAddress m_targetAddress;
        private int m_targetPort;
        private int m_responseTimeoutInMilliseconds;
        private bool m_isConnected;
        private Socket m_clientSocket;
        
        private object m_incomingQueueLock = new object();
        private List<ISCSIPDU> m_incomingQueue = new List<ISCSIPDU>();
        private EventWaitHandle m_incomingQueueEventHandle = new EventWaitHandle(false, EventResetMode.AutoReset);

        public static object m_logSyncLock = new object();
        private static FileStream m_logFile = null;

        public ISCSIClient(string initiatorName)
        {
            m_initiatorName = initiatorName;
        }

        public bool Connect(IPAddress targetAddress, int targetPort)
        {
            return Connect(targetAddress, targetPort, DefaultResponseTimeoutInMilliseconds);
        }

        public bool Connect(IPAddress targetAddress, int targetPort, int responseTimeoutInMilliseconds)
        {
            m_targetAddress = targetAddress;
            m_targetPort = targetPort;
            m_responseTimeoutInMilliseconds = responseTimeoutInMilliseconds;
            if (!m_isConnected)
            {
                m_clientSocket = new Socket(m_targetAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                m_clientSocket.NoDelay = true;
                try
                {
                    m_clientSocket.Connect(m_targetAddress, m_targetPort);
                }
                catch (SocketException)
                {
                    return false;
                }
                ConnectionState state = new ConnectionState();
                ISCSIConnectionReceiveBuffer buffer = state.ReceiveBuffer;
                m_clientSocket.BeginReceive(buffer.Buffer, buffer.WriteOffset, buffer.AvailableLength, SocketFlags.None, new AsyncCallback(OnClientSocketReceive), state);
                m_isConnected = true;
            }
            return m_isConnected;
        }

        public void Disconnect()
        {
            if (m_isConnected)
            {
                m_clientSocket.Disconnect(false);
                m_isConnected = false;
            }
        }

        /// <param name="targetName">Set to null for discovery session</param>
        public bool Login(string targetName)
        {
            return Login(targetName, null, null, null);
        }

        /// <param name="targetName">Set to null for discovery session</param>
        protected internal bool Login(string targetName, ulong? isid, ushort? tsih, ushort? cid)
        {
            if (!m_isConnected)
            {
                throw new InvalidOperationException("iSCSI client is not connected");
            }
            m_connection.Session = new ISCSISession();
            m_connection.Session.ISID = isid ?? ClientHelper.GetRandomISID();
            m_connection.Session.TSIH = tsih ?? 0; // 0 is used on the first connection for a new session
            m_connection.CID = cid ?? m_connection.Session.GetNextCID();
            // p.s. It's possible to perform a single stage login (stage 1 to stage 3, tested against Microsoft iSCSI Target v3.1)
            LoginRequestPDU request = ClientHelper.GetFirstStageLoginRequest(m_initiatorName, targetName, m_connection);
            SendPDU(request);
            LoginResponsePDU response = WaitForPDU<LoginResponsePDU>(request.InitiatorTaskTag);
            if (response != null && response.Status == LoginResponseStatusName.Success)
            {
                m_connection.Session.TSIH = response.TSIH;
                // Status numbering starts with the Login response to the first Login request of the connection
                m_connection.StatusNumberingStarted = true;
                m_connection.ExpStatSN = response.StatSN + 1;

                request = ClientHelper.GetSecondStageLoginRequest(response, m_connection, targetName == null);
                SendPDU(request);
                response = WaitForPDU<LoginResponsePDU>(request.InitiatorTaskTag);
                if (response != null && response.Status == LoginResponseStatusName.Success)
                {
                    KeyValuePairList<string, string> loginParameters = KeyValuePairUtils.GetKeyValuePairList(response.LoginParametersText);
                    ClientHelper.UpdateOperationalParameters(loginParameters, m_connection);
                    return true;
                }
            }
            return false;
        }

        public bool Logout()
        {
            if (!m_isConnected)
            {
                throw new InvalidOperationException("iSCSI client is not connected");
            }

            if (m_connection.Session == null)
            {
                throw new InvalidOperationException("A login session must be successfully established before attempting to logout");
            }

            LogoutRequestPDU request = ClientHelper.GetLogoutRequest(m_connection);
            SendPDU(request);
            LogoutResponsePDU response = WaitForPDU<LogoutResponsePDU>(request.InitiatorTaskTag);
            return (response != null && response.Response == LogoutResponse.ClosedSuccessfully);
        }

        public List<string> ListTargets()
        {
            if (!m_isConnected)
            {
                throw new InvalidOperationException("iSCSI client is not connected");
            }

            if (m_connection.Session == null)
            {
                throw new InvalidOperationException("A login session must be successfully established before attempting to list targets");
            }

            TextRequestPDU request = ClientHelper.GetSendTargetsRequest(m_connection);
            SendPDU(request);
            TextResponsePDU response = WaitForPDU<TextResponsePDU>(request.InitiatorTaskTag);
            if (response != null && response.Final)
            {
                KeyValuePairList<string, string> entries = KeyValuePairUtils.GetKeyValuePairList(response.Text);
                List<string> result = new List<string>();
                foreach(KeyValuePair<string, string> entry in entries)
                {
                    if (entry.Key == "TargetName")
                    {
                        result.Add(entry.Value);
                    }
                }
                return result;
            }
            return null;
        }

        public List<ushort> GetLUNList()
        {
            if (!m_isConnected)
            {
                throw new InvalidOperationException("iSCSI client is not connected");
            }

            if (m_connection.Session == null)
            {
                throw new InvalidOperationException("A login session must be successfully established before attempting to retrieve LUN list");
            }

            SCSICommandPDU reportLUNs = ClientHelper.GetReportLUNsCommand(m_connection, ReportLUNsParameter.MinimumAllocationLength);
            SendPDU(reportLUNs);
            SCSIDataInPDU data = WaitForPDU<SCSIDataInPDU>(reportLUNs.InitiatorTaskTag);
            if (data != null && data.StatusPresent && data.Status == SCSIStatusCodeName.Good)
            {
                uint requiredAllocationLength = ReportLUNsParameter.GetRequiredAllocationLength(data.Data);
                if (requiredAllocationLength > ReportLUNsParameter.MinimumAllocationLength)
                {
                    reportLUNs = ClientHelper.GetReportLUNsCommand(m_connection, requiredAllocationLength);
                    m_clientSocket.Send(reportLUNs.GetBytes());
                    data = WaitForPDU<SCSIDataInPDU>(reportLUNs.InitiatorTaskTag);

                    if (data == null || !data.StatusPresent || data.Status != SCSIStatusCodeName.Good)
                    {
                        return null;
                    }
                }

                ReportLUNsParameter parameter = new ReportLUNsParameter(data.Data);
                List<ushort> result = new List<ushort>();
                foreach(LUNStructure lun in parameter.LUNList)
                {
                    if (lun.IsSingleLevelLUN)
                    {
                        result.Add(lun);
                    }
                }
                return result;
                
            }
            return null;
        }

        /// <returns>Capacity in bytes</returns>
        public ulong ReadCapacity(ushort LUN, out int bytesPerSector)
        {
            if (!m_isConnected)
            {
                throw new InvalidOperationException("iSCSI client is not connected");
            }

            if (m_connection.Session == null)
            {
                throw new InvalidOperationException("A login session must be successfully established before attempting to send SCSI commands");
            }

            SCSICommandPDU readCapacity = ClientHelper.GetReadCapacity10Command(m_connection, LUN);
            SendPDU(readCapacity);
            // SCSIResponsePDU with CheckCondition could be returned in case of an error
            SCSIDataInPDU data = WaitForPDU<SCSIDataInPDU>(readCapacity.InitiatorTaskTag);
            if (data != null && data.StatusPresent && data.Status == SCSIStatusCodeName.Good)
            {
                ReadCapacity10Parameter capacity = new ReadCapacity10Parameter(data.Data);
                if (capacity.ReturnedLBA != 0xFFFFFFFF)
                {
                    bytesPerSector = (int)capacity.BlockLengthInBytes;
                    return (ulong)(capacity.ReturnedLBA + 1) * capacity.BlockLengthInBytes;
                }

                readCapacity = ClientHelper.GetReadCapacity16Command(m_connection, LUN);
                m_clientSocket.Send(readCapacity.GetBytes());
                data = WaitForPDU<SCSIDataInPDU>(readCapacity.InitiatorTaskTag);
                if (data != null && data.StatusPresent && data.Status == SCSIStatusCodeName.Good)
                {
                    ReadCapacity16Parameter capacity16 = new ReadCapacity16Parameter(data.Data);
                    bytesPerSector = (int)capacity16.BlockLengthInBytes;
                    return (ulong)(capacity16.ReturnedLBA + 1) * capacity16.BlockLengthInBytes;
                }
            }

            bytesPerSector = 0;
            return 0;
        }

        public byte[] Read(ushort LUN, ulong sectorIndex, uint sectorCount, int bytesPerSector)
        {
            if (!m_isConnected)
            {
                throw new InvalidOperationException("iSCSI client is not connected");
            }

            if (m_connection.Session == null)
            {
                throw new InvalidOperationException("A login session must be successfully established before attempting to send SCSI commands");
            }

            SCSICommandPDU readCommand = ClientHelper.GetRead16Command(m_connection, LUN, sectorIndex, sectorCount, bytesPerSector);
            SendPDU(readCommand);
            // RFC 3720: Data payload is associated with a specific SCSI command through the Initiator Task Tag
            SCSIDataInPDU data = WaitForPDU<SCSIDataInPDU>(readCommand.InitiatorTaskTag);
            byte[] result = new byte[sectorCount * bytesPerSector];
            while (data != null)
            {
                Array.Copy(data.Data, 0, result, data.BufferOffset, data.DataSegmentLength);
                if (data.StatusPresent)
                {
                    break;
                }
                data = WaitForPDU<SCSIDataInPDU>(readCommand.InitiatorTaskTag);
            }
            if (data != null && data.Status == SCSIStatusCodeName.Good)
            {
                return result;
            }
            else
            {
                return null;
            }
        }

        public bool Write(ushort LUN, ulong sectorIndex, byte[] data, int bytesPerSector)
        {
            if (!m_isConnected)
            {
                throw new InvalidOperationException("iSCSI client is not connected");
            }

            if (m_connection.Session == null)
            {
                throw new InvalidOperationException("A login session must be successfully established before attempting to send SCSI commands");
            }

            SCSICommandPDU writeCommand = ClientHelper.GetWrite16Command(m_connection, LUN, sectorIndex, data, bytesPerSector);
            SendPDU(writeCommand);
            ISCSIPDU response = WaitForPDU(writeCommand.InitiatorTaskTag);
            while (response is ReadyToTransferPDU)
            {
                List<SCSIDataOutPDU> requestedData = ClientHelper.GetWriteData(m_connection, LUN, sectorIndex, data, bytesPerSector, (ReadyToTransferPDU)response);
                foreach (SCSIDataOutPDU dataOut in requestedData)
                {
                    SendPDU(dataOut);
                }
                response = WaitForPDU(writeCommand.InitiatorTaskTag);
            }

            if (response is SCSIResponsePDU)
            {
                if (((SCSIResponsePDU)response).Status == SCSIStatusCodeName.Good)
                {
                    return true;
                }
            }
            return false;
        }

        public bool PingTarget()
        {
            return PingTarget(new byte[0]);
        }

        public bool PingTarget(byte[] pingData)
        {
            return PingTarget(pingData, out _);
        }

        public bool PingTarget(byte[] pingData, out byte[] replyData)
        {
            if (!m_isConnected)
            {
                throw new InvalidOperationException("iSCSI client is not connected");
            }

            if (m_connection.Session == null)
            {
                throw new InvalidOperationException("A login session must be successfully established before attempting to ping target");
            }

            if (pingData.Length > m_connection.TargetMaxRecvDataSegmentLength)
            {
                throw new InvalidOperationException($"Attempted to send {pingData.Length} bytes which exceed the maximum allowed by the target ({m_connection.TargetMaxRecvDataSegmentLength})");
            }

            if (pingData.Length > m_connection.InitiatorMaxRecvDataSegmentLength)
            {
                throw new InvalidOperationException($"Attempted to request {pingData.Length} bytes which exceed the maximum allowed by the initiator ({m_connection.InitiatorMaxRecvDataSegmentLength})");
            }

            NOPOutPDU request = ClientHelper.GetPingRequest(m_connection);
            request.Data = pingData;
            SendPDU(request);
            NOPInPDU response = WaitForPDU<NOPInPDU>(request.InitiatorTaskTag);
            replyData = response?.Data;
            return response != null;
        }

        private void OnClientSocketReceive(IAsyncResult ar)
        {
            ConnectionState state = (ConnectionState)ar.AsyncState;

            int numberOfBytesReceived = 0;
            try
            {
                numberOfBytesReceived = m_clientSocket.EndReceive(ar);
            }
            catch (ArgumentException) // The IAsyncResult object was not returned from the corresponding synchronous method on this class.
            {
                m_isConnected = false;
                return;
            }
            catch (ObjectDisposedException)
            {
                Log("[ReceiveCallback] EndReceive ObjectDisposedException");
                m_isConnected = false;
                return;
            }
            catch (SocketException ex)
            {
                Log("[ReceiveCallback] EndReceive SocketException: " + ex.Message);
                m_isConnected = false;
                return;
            }

            if (numberOfBytesReceived == 0)
            {
                m_isConnected = false;
            }
            else
            {
                ISCSIConnectionReceiveBuffer buffer = state.ReceiveBuffer;
                buffer.SetNumberOfBytesReceived(numberOfBytesReceived);
                ProcessConnectionBuffer(state);

                try
                {
                    m_clientSocket.BeginReceive(buffer.Buffer, buffer.WriteOffset, buffer.AvailableLength, SocketFlags.None, new AsyncCallback(OnClientSocketReceive), state);
                }
                catch (ObjectDisposedException)
                {
                    m_isConnected = false;
                    Log("[ReceiveCallback] BeginReceive ObjectDisposedException");
                }
                catch (SocketException ex)
                {
                    m_isConnected = false;
                    Log("[ReceiveCallback] BeginReceive SocketException: " + ex.Message);
                }
            }
        }

        private void ProcessConnectionBuffer(ConnectionState state)
        {
            ISCSIConnectionReceiveBuffer buffer = state.ReceiveBuffer;
            while (buffer.HasCompletePDU())
            {
                ISCSIPDU pdu = null;
                try
                {
                    pdu = buffer.DequeuePDU();
                }
                catch (Exception)
                {
                    throw;
                }

                if (pdu.GetType() == typeof(ISCSIPDU))
                {
                    throw new Exception("Unsupported");
                }
                else
                {
                    ProcessPDU(pdu, state);
                }
            }
        }

        private void ProcessPDU(ISCSIPDU pdu, ConnectionState state)
        {
            if (pdu is NOPInPDU)
            {
                if (((NOPInPDU)pdu).TargetTransferTag != 0xFFFFFFFF)
                {
                    // Send NOP-OUT
                    NOPOutPDU response = ClientHelper.GetPingResponse((NOPInPDU)pdu, m_connection);
                    SendPDU(response);
                    return;
                }
            }
            
            if (m_connection.StatusNumberingStarted)
            {
                uint? responseStatSN = PDUHelper.GetStatSN(pdu);
                if (m_connection.ExpStatSN == responseStatSN)
                {
                    m_connection.ExpStatSN++;
                }
            }
            lock (m_incomingQueueLock)
            {
                m_incomingQueue.Add(pdu);
                m_incomingQueueEventHandle.Set();
            }
        }

        public T WaitForPDU<T>(uint initiatorTaskTag) where T : ISCSIPDU
        {
            ISCSIPDU response = WaitForPDU(initiatorTaskTag);
            if (response is RejectPDU rejectPDU)
            {
                throw new InvalidOperationException($"Request rejected, reason: {rejectPDU.Reason}");
            }
            else if (response != null && !(response is T))
            {
                throw new InvalidDataException($"Server returned invalid PDU type, OpCode returned: {response.OpCode}");
            }

            return response as T;
        }

        public ISCSIPDU WaitForPDU(uint initiatorTaskTag)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < m_responseTimeoutInMilliseconds)
            {
                lock (m_incomingQueueLock)
                {
                    if (!m_clientSocket.Connected && m_incomingQueue.Count == 0)
                    {
                        return null;
                    }

                    for (int index = 0; index < m_incomingQueue.Count; index++)
                    {
                        ISCSIPDU pdu = m_incomingQueue[index];
                        if (pdu is RejectPDU rejectPDU)
                        {
                            uint rejectedTaskTag = ISCSIPDU.GetInitiatorTaskTag(rejectPDU.Data, 0);
                            if (rejectedTaskTag == initiatorTaskTag)
                            {
                                m_incomingQueue.RemoveAt(index);
                                return pdu;
                            }
                        }
                        else if (pdu.InitiatorTaskTag == initiatorTaskTag)
                        {
                            m_incomingQueue.RemoveAt(index);
                            return pdu;
                        }
                    }
                }
                m_incomingQueueEventHandle.WaitOne(100);
            }
            return null;
        }

        public void SendPDU(ISCSIPDU request)
        {
            try
            {
                if (m_connection.StatusNumberingStarted)
                {
                    PDUHelper.SetExpStatSN(request, m_connection.ExpStatSN);
                }
                m_clientSocket.Send(request.GetBytes());
                Log("[{0}][SendPDU] Sent request to target, Operation: {1}, Size: {2}", m_connection.ConnectionIdentifier, (ISCSIOpCodeName)request.OpCode, request.Length);
            }
            catch (SocketException ex)
            {
                Log("[{0}][SendPDU] Failed to send PDU to target (Operation: {1}, Size: {2}), SocketException: {3}", m_connection.ConnectionIdentifier, (ISCSIOpCodeName)request.OpCode, request.Length, ex.Message);
                m_isConnected = false;
            }
            catch (ObjectDisposedException)
            {
                m_isConnected = false;
            }
        }

        public bool IsConnected
        {
            get
            {
                return m_isConnected;
            }
        }

        public ulong ISID
        {
            get
            {
                return m_connection.Session.ISID;
            }
        }

        public ushort TSIH
        {
            get
            {
                return m_connection.Session.TSIH;
            }
        }

        public ushort CID
        {
            get
            {
                return m_connection.CID;
            }
        }

        public static void Log(string message, params object[] args)
        {
            Log(String.Format(message, args));
        }

        public static void Log(string message)
        {
            if (m_logFile != null)
            {
                lock (m_logSyncLock)
                {
                    StreamWriter writer = new StreamWriter(m_logFile);
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ");
                    writer.WriteLine(timestamp + message);
                    writer.Flush();
                }
            }
        }
    }
}
