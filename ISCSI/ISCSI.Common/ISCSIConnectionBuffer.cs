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

namespace ISCSI
{
    /// <summary>
    /// iSCSI Connection Receive Buffer
    /// </summary>
    public class ISCSIConnectionReceiveBuffer
    {
        private byte[] m_buffer;
        private int m_readOffset = 0;
        private int m_bytesInBuffer = 0;
        private int? m_pduLength;

        /// <param name="bufferLength">
        /// bufferLength should be set to BasicHeaderSegmentLength + TotalAHSLength + HeaderDigestLength + MaxRecvDataSegmentLength + DataDigestLength.
        /// (because DataSegmentLength MUST not exceed MaxRecvDataSegmentLength for the direction it is sent)
        /// </param>
        public ISCSIConnectionReceiveBuffer(int bufferLength)
        {
            m_buffer = new byte[bufferLength];
        }

        public void SetNumberOfBytesReceived(int numberOfBytesReceived)
        {
            m_bytesInBuffer += numberOfBytesReceived;
        }

        public bool HasCompletePDU()
        {
            if (m_bytesInBuffer >= 8)
            {
                if (!m_pduLength.HasValue)
                {
                    m_pduLength = ISCSIPDU.GetPDULength(m_buffer, m_readOffset);
                }
                return m_bytesInBuffer >= m_pduLength.Value;
            }
            return false;
        }

        /// <summary>
        /// HasCompletePDU must be called and return true before calling DequeuePDU
        /// </summary>
        /// <exception cref="System.IO.InvalidDataException"></exception>
        public ISCSIPDU DequeuePDU()
        {
            ISCSIPDU pdu;
            try
            {
                pdu = ISCSIPDU.GetPDU(m_buffer, m_readOffset);
            }
            catch (IndexOutOfRangeException ex)
            {
                throw new System.IO.InvalidDataException("Invalid PDU", ex);
            }
            RemovePDUBytes();
            return pdu;
        }

        /// <summary>
        /// HasCompletePDU must be called and return true before calling DequeuePDUBytes
        /// </summary>
        public byte[] DequeuePDUBytes()
        {
            byte[] pduBytes = ByteReader.ReadBytes(m_buffer, m_readOffset, m_pduLength.Value);
            RemovePDUBytes();
            return pduBytes;
        }

        private void RemovePDUBytes()
        {
            m_bytesInBuffer -= m_pduLength.Value;
            if (m_bytesInBuffer == 0)
            {
                m_readOffset = 0;
                m_pduLength = null;
            }
            else
            {
                m_readOffset += m_pduLength.Value;
                m_pduLength = null;
                if (!HasCompletePDU())
                {
                    Array.Copy(m_buffer, m_readOffset, m_buffer, 0, m_bytesInBuffer);
                    m_readOffset = 0;
                }
            }
        }

        public byte[] Buffer
        {
            get
            {
                return m_buffer;
            }
        }

        public int WriteOffset
        {
            get
            {
                return m_readOffset + m_bytesInBuffer;
            }
        }

        public int BytesInBuffer
        {
            get
            {
                return m_bytesInBuffer;
            }
        }

        public int AvailableLength
        {
            get
            {
                return m_buffer.Length - (m_readOffset + m_bytesInBuffer);
            }
        }
    }
}
