/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace ISCSI.Server
{
    public class PDUHelper
    {
        public static uint? GetCmdSN(ISCSIPDU pdu)
        {
            if (pdu is NOPOutPDU)
            {
                return ((NOPOutPDU)pdu).CmdSN;
            }
            else if (pdu is SCSICommandPDU)
            {
                return ((SCSICommandPDU)pdu).CmdSN;
            }
            else if (pdu is LoginRequestPDU)
            {
                return ((LoginRequestPDU)pdu).CmdSN;
            }
            else if (pdu is TextRequestPDU)
            {
                return ((TextRequestPDU)pdu).CmdSN;
            }
            else if (pdu is LogoutRequestPDU)
            {
                return ((LogoutRequestPDU)pdu).CmdSN;
            }
            return null;
        }

        public static void SetStatSN(ISCSIPDU pdu, uint statSN)
        {
            if (pdu is NOPInPDU)
            {
                ((NOPInPDU)pdu).StatSN = statSN;
            }
            else if (pdu is SCSIResponsePDU)
            {
                ((SCSIResponsePDU)pdu).StatSN = statSN;
            }
            else if (pdu is LoginResponsePDU)
            {
                ((LoginResponsePDU)pdu).StatSN = statSN;
            }
            else if (pdu is TextResponsePDU)
            {
                ((TextResponsePDU)pdu).StatSN = statSN;
            }
            else if (pdu is SCSIDataInPDU && ((SCSIDataInPDU)pdu).StatusPresent) // RFC 3720: StatSN [..] only have meaningful content if the S bit is set to 1
            {
                ((SCSIDataInPDU)pdu).StatSN = statSN;
            }
            else if (pdu is LogoutResponsePDU)
            {
                ((LogoutResponsePDU)pdu).StatSN = statSN;
            }
            else if (pdu is ReadyToTransferPDU)
            {
                ((ReadyToTransferPDU)pdu).StatSN = statSN;
            }
            else if (pdu is RejectPDU)
            {
                ((RejectPDU)pdu).StatSN = statSN;
            }
        }

        public static void SetExpCmdSN(ISCSIPDU pdu, uint expCmdSN, uint maxCmdSN)
        {
            if (pdu is NOPInPDU)
            {
                ((NOPInPDU)pdu).ExpCmdSN = expCmdSN;
                ((NOPInPDU)pdu).MaxCmdSN = maxCmdSN;
            }
            else if (pdu is SCSIResponsePDU)
            {
                ((SCSIResponsePDU)pdu).ExpCmdSN = expCmdSN;
                ((SCSIResponsePDU)pdu).MaxCmdSN = maxCmdSN;
            }
            else if (pdu is LoginResponsePDU)
            {
                ((LoginResponsePDU)pdu).ExpCmdSN = expCmdSN;
                ((LoginResponsePDU)pdu).MaxCmdSN = maxCmdSN;
            }
            else if (pdu is TextResponsePDU)
            {
                ((TextResponsePDU)pdu).ExpCmdSN = expCmdSN;
                ((TextResponsePDU)pdu).MaxCmdSN = maxCmdSN;
            }
            else if (pdu is SCSIDataInPDU)
            {
                ((SCSIDataInPDU)pdu).ExpCmdSN = expCmdSN;
                ((SCSIDataInPDU)pdu).MaxCmdSN = maxCmdSN;
            }
            else if (pdu is LogoutResponsePDU)
            {
                ((LogoutResponsePDU)pdu).ExpCmdSN = expCmdSN;
                ((LogoutResponsePDU)pdu).MaxCmdSN = maxCmdSN;
            }
            else if (pdu is ReadyToTransferPDU)
            {
                ((ReadyToTransferPDU)pdu).ExpCmdSN = expCmdSN;
                ((ReadyToTransferPDU)pdu).MaxCmdSN = maxCmdSN;
            }
            else if (pdu is RejectPDU)
            {
                ((RejectPDU)pdu).ExpCmdSN = expCmdSN;
                ((RejectPDU)pdu).MaxCmdSN = maxCmdSN;
            }
        }
    }
}
