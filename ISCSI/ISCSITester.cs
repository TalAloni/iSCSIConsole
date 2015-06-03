/* Copyright (C) 2012-2015 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Utilities;

namespace ISCSI
{
    public class ISCSITester
    {
        private Socket m_targetSocket;
        private Socket m_initiatorSocket;
        private bool m_waitingForResponse = false;
        public List<ISCSIPDU> m_pduSent = new List<ISCSIPDU>();
        public List<ISCSIPDU> m_pduReceived = new List<ISCSIPDU>();
        private bool m_isTargetConnected = false;

        public ISCSITester(Socket initiatorSocket)
        {
            m_initiatorSocket = initiatorSocket;
            
        }

        public void Transmit(byte[] data)
        {
            ISCSIPDU pdu = ISCSIPDU.GetPDU(data);
            if (pdu is LoginRequestPDU && !m_isTargetConnected)
            {
                m_isTargetConnected = true;
                m_targetSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                m_targetSocket.Connect("tal2", 3260);
            }

            //m_pduToSend.Add(data);
            //if (!m_waitingForResponse)
            //{
                //m_waitingForResponse = true;
                //byte[] toSend = m_pduToSend[0];
                //m_pduToSend.RemoveAt(0);
                StateObject state = new StateObject();
                state.ReceiveBuffer = new byte[StateObject.ReceiveBufferSize];
                m_targetSocket.BeginReceive(state.ReceiveBuffer, 0, StateObject.ReceiveBufferSize, 0, ReceiveCallback, state);

                Console.WriteLine("waiting for respone: " + m_waitingForResponse.ToString());
                m_targetSocket.Send(data);
                m_pduSent.Add(ISCSIPDU.GetPDU(data));
                Console.WriteLine("Transmitted PDU to real target");
            //}
            
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            StateObject state = (StateObject)result.AsyncState;
            //Socket clientSocket = state.WorkerSocket;
            byte[] buffer = state.ReceiveBuffer;

            int bytesReceived;
            
            try
            {
                bytesReceived = m_targetSocket.EndReceive(result);
                m_waitingForResponse = false;
            }
            catch (Exception)
            {
                //An error has occured when reading
                Console.WriteLine("An error has occured when reading from real target");
                return;
            }

            if (bytesReceived == 0)
            {
                //The connection has been closed.
                Console.WriteLine("The connection with the real target has been closed");
                return;
            }

            //iSCSIPDU pdu1 = GetResponcePDU((LoginRequestPDU)pdu);

            Console.WriteLine("Received PDU from real target");
            byte[] pduBytes = new byte[bytesReceived];
            Array.Copy(buffer, pduBytes, bytesReceived);

            ISCSIPDU pdu = ISCSIPDU.GetPDU(pduBytes);
            m_pduReceived.Add(pdu);

            m_initiatorSocket.Send(pduBytes);
            Console.WriteLine("Sent PDU to real initiator, OpCode: " + pdu.OpCode);
            if (pdu is LogoutResponsePDU)
            {
                m_initiatorSocket.Close();
                m_targetSocket.Close();
                m_isTargetConnected = false;
                return;
            }
            
            //Do something with the data object here.
            //Then start reading from the network again.
            m_targetSocket.BeginReceive(state.ReceiveBuffer, 0, StateObject.ReceiveBufferSize, 0, ReceiveCallback, state);
        }
    }
}
