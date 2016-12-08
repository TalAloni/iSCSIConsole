/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace ISCSI.Client
{
    public class PDUHelper
    {
        public static uint? GetStatSN(ISCSIPDU pdu)
        {
            if (pdu is NOPInPDU)
            {
                return ((NOPInPDU)pdu).StatSN;
            }
            else if (pdu is SCSIResponsePDU)
            {
                return ((SCSIResponsePDU)pdu).StatSN;
            }
            else if (pdu is LoginResponsePDU)
            {
                return ((LoginResponsePDU)pdu).StatSN;
            }
            else if (pdu is TextResponsePDU)
            {
                return ((TextResponsePDU)pdu).StatSN;
            }
            else if (pdu is SCSIDataInPDU && ((SCSIDataInPDU)pdu).StatusPresent) // RFC 3720: StatSN [..] only have meaningful content if the S bit is set to 1
            {
                return ((SCSIDataInPDU)pdu).StatSN;
            }
            else if (pdu is LogoutResponsePDU)
            {
                return ((LogoutResponsePDU)pdu).StatSN;
            }
            else if (pdu is ReadyToTransferPDU)
            {
                return ((ReadyToTransferPDU)pdu).StatSN;
            }
            else if (pdu is RejectPDU)
            {
                return ((RejectPDU)pdu).StatSN;
            }
            return null;
        }

        public static void SetExpStatSN(ISCSIPDU pdu, uint expStatSN)
        {
            if (pdu is NOPOutPDU)
            {
                ((NOPOutPDU)pdu).ExpStatSN = expStatSN;
            }
            else if (pdu is SCSICommandPDU)
            {
                ((SCSICommandPDU)pdu).ExpStatSN = expStatSN;
            }
            else if (pdu is LoginRequestPDU)
            {
                ((LoginRequestPDU)pdu).ExpStatSN = expStatSN;
            }
            else if (pdu is TextRequestPDU)
            {
                ((TextRequestPDU)pdu).ExpStatSN = expStatSN;
            }
            else if (pdu is SCSIDataOutPDU)
            {
                ((SCSIDataOutPDU)pdu).ExpStatSN = expStatSN;
            }
            else if (pdu is LogoutRequestPDU)
            {
                ((LogoutRequestPDU)pdu).ExpStatSN = expStatSN;
            }
        }
    }
}
