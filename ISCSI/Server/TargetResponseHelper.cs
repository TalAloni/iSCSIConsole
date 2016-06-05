/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using DiskAccessLibrary;
using Utilities;

namespace ISCSI.Server
{
    public class TargetResponseHelper
    {
        private enum ErrorToReport { None, IncorrectLUN, CRCError, UnitAttention, OutOfRange, UnsupportedCommandCode };

        internal static List<ISCSIPDU> GetSCSIResponsePDU(SCSICommandPDU command, ISCSITarget target, SessionParameters session, ConnectionParameters connection)
        {
            ushort LUN = command.LUN;

            // We return either SCSIResponsePDU or SCSIDataInPDU
            List<ISCSIPDU> responseList = new List<ISCSIPDU>();
            
            ErrorToReport errorToReport = ErrorToReport.None;
            string connectionIdentifier = StateObject.GetConnectionIdentifier(session, connection);

            if (command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.TestUnitReady)
            {
                if (LUN < target.Disks.Count)
                {
                    SCSIResponsePDU response = new SCSIResponsePDU();
                    response.InitiatorTaskTag = command.InitiatorTaskTag;
                    response.Status = SCSIStatusCodeName.Good;
                    responseList.Add(response);
                }
                else
                {
                    errorToReport = ErrorToReport.IncorrectLUN;
                }
            }
            else if (command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.RequestSense)
            {
                // Some initiators (i.e. EFI iSCSI DXE) will send 'Request Sense' upon connection (likely just to verify the medium is ready)
                if (LUN < target.Disks.Count)
                {
                    SCSIResponsePDU response = new SCSIResponsePDU();
                    response.InitiatorTaskTag = command.InitiatorTaskTag;
                    response.Status = SCSIStatusCodeName.Good;
                    response.Data = FormatSenseData(SenseDataParameter.GetNoSenseSenseData());
                    responseList.Add(response);
                }
                else
                {
                    errorToReport = ErrorToReport.IncorrectLUN;
                }
            }
            else if (command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.Inquiry)
            {
                if (LUN < target.Disks.Count)
                {
                    SCSIDataInPDU response = Inquiry(command, target);
                    responseList.Add(response);
                }
                else
                {
                    errorToReport = ErrorToReport.IncorrectLUN;
                }
            }
            else if (command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.Reserve6)
            {
                if (LUN < target.Disks.Count)
                {
                    SCSIResponsePDU response = new SCSIResponsePDU();
                    response.InitiatorTaskTag = command.InitiatorTaskTag;
                    response.Status = SCSIStatusCodeName.Good;
                    responseList.Add(response);
                }
                else
                {
                    errorToReport = ErrorToReport.IncorrectLUN;
                }
            }
            else if (command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.Release6)
            {
                if (LUN < target.Disks.Count)
                {
                    SCSIResponsePDU response = new SCSIResponsePDU();
                    response.InitiatorTaskTag = command.InitiatorTaskTag;
                    response.Status = SCSIStatusCodeName.Good;
                    responseList.Add(response);
                }
                else
                {
                    errorToReport = ErrorToReport.IncorrectLUN;
                }
            }
            else if (command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.ModeSense6)
            {
                if (LUN < target.Disks.Count)
                {
                    SCSIDataInPDU response = ModeSense6(command, target);
                    responseList.Add(response);
                }
                else
                {
                    errorToReport = ErrorToReport.IncorrectLUN;
                }
            }
            else if (command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.ReadCapacity10)
            {
                if (LUN < target.Disks.Count)
                {
                    SCSIDataInPDU response = ReadCapacity10(command, target);
                    EnforceAllocationLength(response, command.ExpectedDataTransferLength);
                    responseList.Add(response);
                }
                else
                {
                    errorToReport = ErrorToReport.IncorrectLUN;
                }
            }
            else if (command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.Read6 ||
                     command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.Read10 ||
                     command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.Read16)
            {
                if (LUN < target.Disks.Count)
                {
                    try
                    {
                        // Note: we ignore ExpectedDataTransferLength, and assume it's always equal to the SCSI CDB's TransferLength * BlockLengthInBytes
                        List<SCSIDataInPDU> collection = Read(command, target, connection);
                        foreach (SCSIDataInPDU entry in collection)
                        {
                            responseList.Add(entry);
                        }
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        errorToReport = ErrorToReport.OutOfRange;
                    }
                    catch (IOException ex)
                    {
                        int error = Marshal.GetHRForException(ex);
                        if (error == (int)Win32Error.ERROR_CRC)
                        {
                            errorToReport = ErrorToReport.CRCError;
                        }
                        else
                        {
                            errorToReport = ErrorToReport.UnitAttention;
                            ISCSIServer.Log("[{0}][GetSCSIResponsePDU] Read error:", ex.ToString());
                        }
                    }
                }
                else
                {
                    errorToReport = ErrorToReport.IncorrectLUN;
                }
            }
            else if (command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.Write6 ||
                     command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.Write10 ||
                     command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.Write16)
            {
                if (LUN < target.Disks.Count)
                {
                    try
                    {
                        ISCSIPDU response = Write(command, target, session, connection);
                        responseList.Add(response);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        errorToReport = ErrorToReport.OutOfRange;
                    }
                    catch (IOException ex)
                    {
                        errorToReport = ErrorToReport.UnitAttention;
                        ISCSIServer.Log("[{0}][GetSCSIResponsePDU] Write error: ", ex.ToString());
                    }
                }
                else
                {
                    errorToReport = ErrorToReport.IncorrectLUN;
                }
            }
            else if (command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.Verify10 ||
                     command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.Verify16)
            {
                if (LUN < target.Disks.Count)
                {
                    SCSIResponsePDU response = Verify(command, target);
                    responseList.Add(response);
                }
                else
                {
                    errorToReport = ErrorToReport.IncorrectLUN;
                }
            }
            else if (command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.SynchronizeCache10)
            {
                if (LUN < target.Disks.Count)
                {
                    SCSIResponsePDU response = SynchronizeCache10(command, target);
                    responseList.Add(response);
                }
                else
                {
                    errorToReport = ErrorToReport.IncorrectLUN;
                }
            }
            else if (command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.ServiceActionIn &&
                     command.CommandDescriptorBlock.ServiceAction == ServiceAction.ReadCapacity16)
            {
                if (LUN < target.Disks.Count)
                {
                    SCSIDataInPDU response = ReadCapacity16(command, target);
                    EnforceAllocationLength(response, command.ExpectedDataTransferLength);
                    responseList.Add(response);
                }
                else
                {
                    errorToReport = ErrorToReport.IncorrectLUN;
                }
            }
            else if (command.CommandDescriptorBlock.OpCode == SCSIOpCodeName.ReportLUNs)
            {
                SCSIDataInPDU response = ReportLUNs(command, target);
                responseList.Add(response);
            }
            else
            {
                ISCSIServer.Log("[{0}][GetSCSIResponsePDU] Unsupported SCSI Command (0x{1})", connectionIdentifier, command.CommandDescriptorBlock.OpCode.ToString("X"));
                errorToReport = ErrorToReport.UnsupportedCommandCode;
            }

            if (errorToReport != ErrorToReport.None)
            {
                SCSIResponsePDU response = new SCSIResponsePDU();
                response.InitiatorTaskTag = command.InitiatorTaskTag;
                response.Status = SCSIStatusCodeName.CheckCondition;
                if (errorToReport == ErrorToReport.IncorrectLUN)
                {
                    response.Data = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidLUNSenseData());
                    ISCSIServer.Log("[{0}][GetSCSIResponsePDU] Incorrect LUN", connectionIdentifier);
                }
                else if (errorToReport == ErrorToReport.CRCError)
                {
                    response.Data = FormatSenseData(SenseDataParameter.GetWriteFaultSenseData());
                }
                else if (errorToReport == ErrorToReport.UnitAttention)
                {
                    response.Data = FormatSenseData(SenseDataParameter.GetUnitAttentionSenseData());
                }
                else if (errorToReport == ErrorToReport.OutOfRange)
                {
                    response.Data = FormatSenseData(SenseDataParameter.GetIllegalRequestLBAOutOfRangeSenseData());
                }
                else
                {
                    response.Data = FormatSenseData(SenseDataParameter.GetIllegalRequestUnsupportedCommandCodeSenseData());
                }
                responseList.Add(response);
            }

            return responseList;
        }

        internal static void PrepareSCSIDataInPDU(SCSIDataInPDU response, SCSICommandPDU command, Nullable<SCSIStatusCodeName> status)
        {
            // StatSN, Status, and Residual Count only have meaningful content if the S bit is set to 1
            response.InitiatorTaskTag = command.InitiatorTaskTag;
            if (status.HasValue)
            {
                response.StatusPresent = true;
                response.Final = true; // If the S bit is set to 1, the F bit MUST also be set to 1
                response.Status = status.Value;
            }
        }

        public static void EnforceAllocationLength(SCSIDataInPDU response, uint allocationLength)
        {
            if (response.Data.Length > allocationLength)
            {
                // we must not return more bytes than inquiryCommand.AllocationLength
                byte[] data = response.Data;
                int dataLength = (int)Math.Min(data.Length, allocationLength);
                response.Data = new byte[dataLength];
                Array.Copy(data, response.Data, dataLength);
            }
            if (response.Data.Length < allocationLength)
            {
                response.ResidualUnderflow = true;
                response.ResidualCount = (uint)(allocationLength - response.Data.Length);
            }
        }

        internal static ISCSIPDU GetSCSIDataOutResponsePDU(SCSIDataOutPDU request, ISCSITarget target, SessionParameters session, ConnectionParameters connection)
        {
            string connectionIdentifier = StateObject.GetConnectionIdentifier(session, connection);
            if (connection.Transfers.ContainsKey(request.TargetTransferTag))
            { 
                byte LUN = (byte)request.LUN;
                if (LUN < target.Disks.Count)
                {
                    Disk disk = target.Disks[LUN];

                    uint offset = request.BufferOffset;
                    uint totalLength = connection.Transfers[request.TargetTransferTag].Value;

                    // Store segment (we only execute the command after receiving all of its data)
                    byte[] commandData = connection.TransferData[request.TargetTransferTag];
                    Array.Copy(request.Data, 0, commandData, offset, request.DataSegmentLength);
                    
                    ISCSIServer.Log(String.Format("[{0}][GetSCSIDataOutResponsePDU] Buffer offset: {1}, Total length: {2}", connectionIdentifier, offset, totalLength));

                    if (offset + request.DataSegmentLength == totalLength)
                    {
                        // Last Data-out PDU
                        ISCSIServer.Log("[{0}][GetSCSIDataOutResponsePDU] Last Data-out PDU", connectionIdentifier);
                        
                        if (!disk.IsReadOnly)
                        {
                            long sectorIndex = (long)connection.Transfers[request.TargetTransferTag].Key;
                            ISCSIServer.LogWrite(disk, sectorIndex, commandData); // must come before the actual write as it logs changes
                            lock (session.WriteLock)
                            {
                                disk.WriteSectors(sectorIndex, commandData);
                            }
                        }

                        SCSIResponsePDU response = new SCSIResponsePDU();
                        response.InitiatorTaskTag = request.InitiatorTaskTag;
                        if (disk.IsReadOnly)
                        {
                            response.Status = SCSIStatusCodeName.CheckCondition;
                            SenseDataParameter senseData = SenseDataParameter.GetDataProtectSenseData();
                            response.Data = FormatSenseData(senseData);
                        }
                        else
                        {
                            response.Status = SCSIStatusCodeName.Good;
                        }
                        connection.Transfers.Remove(request.TargetTransferTag);
                        connection.TransferData.Remove(request.TargetTransferTag);
                        session.NextR2TSN.Remove(request.TargetTransferTag);
                        return response;
                    }
                    else
                    {
                        // Send R2T
                        ReadyToTransferPDU response = new ReadyToTransferPDU();
                        response.InitiatorTaskTag = request.InitiatorTaskTag;
                        response.TargetTransferTag = request.TargetTransferTag;
                        response.R2TSN = session.GetNextR2TSN(request.TargetTransferTag);
                        response.BufferOffset = offset + request.DataSegmentLength; // where we left off
                        response.DesiredDataTransferLength = Math.Min((uint)connection.TargetMaxRecvDataSegmentLength, totalLength - response.BufferOffset);
                        
                        return response;
                    }
                }
                else
                {
                    ISCSIServer.Log("[{0}][GetSCSIDataOutResponsePDU] Incorrect LUN", connectionIdentifier);
                    SCSIResponsePDU response = new SCSIResponsePDU();
                    response.InitiatorTaskTag = request.InitiatorTaskTag;
                    response.Status = SCSIStatusCodeName.CheckCondition;
                    response.Data = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidLUNSenseData());
                    return response;
                }
            }
            else
            {
                ISCSIServer.Log("[{0}][GetSCSIDataOutResponsePDU] Unfamiliar TargetTransferTag", connectionIdentifier);
                SCSIResponsePDU response = new SCSIResponsePDU();
                response.InitiatorTaskTag = request.InitiatorTaskTag;
                response.Status = SCSIStatusCodeName.CheckCondition;
                response.Data = FormatSenseData(SenseDataParameter.GetIllegalRequestSenseData());
                return response;
            }
        }

        public static SCSIDataInPDU Inquiry(SCSICommandPDU command, ISCSITarget target)
        {
            ushort LUN = command.LUN;

            SCSIDataInPDU response = new SCSIDataInPDU();
            PrepareSCSIDataInPDU(response, command, SCSIStatusCodeName.Good);

            InquiryCommand inquiryCommand = (InquiryCommand)command.CommandDescriptorBlock;
            if (!inquiryCommand.EVPD)
            {
                StandardInquiryData inquiryData = new StandardInquiryData();
                inquiryData.PeripheralDeviceType = 0; // Direct access block device
                inquiryData.VendorIdentification = "TalAloni";
                inquiryData.ProductIdentification = "Disk";
                inquiryData.ProductRevisionLevel = "1.00";
                inquiryData.DriveSerialNumber = (uint)target.TargetName.GetHashCode() + command.LUN;
                inquiryData.CmdQue = true;
                inquiryData.Version = 5; // Microsoft iSCSI Target report version 5
                response.Data = inquiryData.GetBytes(); // we trim it later if necessary
            }
            else
            {
                switch (inquiryCommand.PageCode)
                {
                    case VitalProductDataPageName.SupportedVPDPages:
                        {
                            SupportedVitaLProductDataPages page = new SupportedVitaLProductDataPages();
                            page.SupportedPageList.Add((byte)VitalProductDataPageName.SupportedVPDPages);
                            page.SupportedPageList.Add((byte)VitalProductDataPageName.UnitSerialNumber);
                            page.SupportedPageList.Add((byte)VitalProductDataPageName.DeviceIdentification);
                            response.Data = page.GetBytes();
                            break;
                        }
                    case VitalProductDataPageName.UnitSerialNumber:
                        {
                            UnitSerialNumberVPDPage page = new UnitSerialNumberVPDPage();
                            // Older products that only support the Product Serial Number parameter will have a page length of 08h, while newer products that support both parameters (Vendor Unique field from the StandardInquiryData) will have a page length of 14h
                            // Microsoft iSCSI Target uses values such as "34E5A6FC-3ACC-452D-AEDA-6EE2EFF20FB4"
                            ulong serialNumber = (ulong)target.TargetName.GetHashCode() << 32 + command.LUN;
                            page.ProductSerialNumber = serialNumber.ToString("00000000");
                            response.Data = page.GetBytes();
                            break;
                        }
                    case VitalProductDataPageName.DeviceIdentification:
                        {
                            DeviceIdentificationVPDPage page = new DeviceIdentificationVPDPage();
                            // Identifiers necessity is preliminary, and has not been confirmed:
                            // WWN identifier is needed to prevent 0xF4 BSOD during Windows setup
                            // ISCSI identifier is needed for WinPE to pick up the disk during boot (after iPXE's sanhook)
                            page.IdentificationDescriptorList.Add(new IdentificationDescriptor(5, command.LUN));
                            page.IdentificationDescriptorList.Add(IdentificationDescriptor.GetISCSIIdentifier(target.TargetName));
                            response.Data = page.GetBytes();
                            break;
                        }
                    case VitalProductDataPageName.BlockLimits:
                        {
                            /* Provide only when requeste explicitly */
                            BlockLimitsVPDPage page = new BlockLimitsVPDPage();
                            page.OptimalTransferLengthGranularity = 128;
                            page.MaximumTransferLength = (uint)Settings.MaximumTransferSizeLBA;
                            page.OptimalTransferLength = 128;
                            response.Data = page.GetBytes();
                            break;
                        }
                    case VitalProductDataPageName.BlockDeviceCharacteristics:
                        {
                            /* Provide only when requeste explicitly */
                            BlockDeviceCharacteristicsVPDPage page = new BlockDeviceCharacteristicsVPDPage();
                            page.MediumRotationRate = 0; // Not reported
                            response.Data = page.GetBytes();
                            break;
                        }
                    default:
                        {
                            response.Status = SCSIStatusCodeName.CheckCondition;
                            response.Data = FormatSenseData(SenseDataParameter.GetIllegalRequestParameterNotSupportedSenseData());
                            ISCSIServer.Log("[Inquiry] Unsupported VPD Page request (0x{0})", inquiryCommand.PageCode.ToString("X"));
                            break;
                        }
                }
            }

            EnforceAllocationLength(response, inquiryCommand.AllocationLength);

            return response;
        }

        public static SCSIDataInPDU ModeSense6(SCSICommandPDU command, ISCSITarget target)
        {
            ushort LUN = command.LUN;

            SCSIDataInPDU response = new SCSIDataInPDU();
            PrepareSCSIDataInPDU(response, command, SCSIStatusCodeName.Good);

            ModeSense6CommandDescriptorBlock modeSense6Command = (ModeSense6CommandDescriptorBlock)command.CommandDescriptorBlock;
            
            ShortLBAModeParameterBlockDescriptor descriptor = new ShortLBAModeParameterBlockDescriptor();
            descriptor.LogicalBlockLength = (uint)target.Disks[LUN].BytesPerSector; 

            ModeParameterHeader6 header = new ModeParameterHeader6();
            header.WP = target.Disks[LUN].IsReadOnly;     // Write protected, even when set to true, Windows does not always prevent the disk from being written to.
            header.DPOFUA = true;  // Microsoft iSCSI Target support this
            header.BlockDescriptorLength = (byte)descriptor.Length;
            header.ModeDataLength += (byte)descriptor.Length;

            byte[] pageData = new byte[0];

            switch ((ModePageCodeName)modeSense6Command.PageCode)
            {
                case ModePageCodeName.CachingParametersPage:
                    {
                        CachingParametersPage page = new CachingParametersPage();
                        page.RCD = true;
                        header.ModeDataLength += (byte)page.Length;
                        pageData = new byte[page.Length];
                        Array.Copy(page.GetBytes(), pageData, page.Length);
                        break;
                    }
                case ModePageCodeName.ControlModePage:
                    {
                        ControlModePage page = new ControlModePage();
                        header.ModeDataLength += (byte)page.Length;
                        pageData = new byte[page.Length];
                        Array.Copy(page.GetBytes(), pageData, page.Length);
                        break;
                    }
                case ModePageCodeName.InformationalExceptionsControlModePage:
                    {
                        InformationalExceptionsControlModePage page = new InformationalExceptionsControlModePage();
                        header.ModeDataLength += (byte)page.Length;
                        pageData = new byte[page.Length];
                        Array.Copy(page.GetBytes(), pageData, page.Length);
                        break;
                    }
                case ModePageCodeName.ReturnAllPages:
                    {
                        CachingParametersPage page1 = new CachingParametersPage();
                        page1.RCD = true;
                        header.ModeDataLength += (byte)page1.Length;

                        InformationalExceptionsControlModePage page2 = new InformationalExceptionsControlModePage();
                        header.ModeDataLength += (byte)page2.Length;

                        pageData = new byte[page1.Length + page2.Length];
                        Array.Copy(page1.GetBytes(), pageData, page1.Length);
                        Array.Copy(page2.GetBytes(), 0, pageData, page1.Length, page2.Length);
                        break;
                    }
                case ModePageCodeName.VendorSpecificPage:
                    {
                        // Microsoft iSCSI Target running under Windows 2000 will request this page, we immitate Microsoft iSCSI Target by sending back an empty page
                        VendorSpecificPage page = new VendorSpecificPage();
                        header.ModeDataLength += (byte)page.Length;
                        pageData = new byte[page.Length];
                        Array.Copy(page.GetBytes(), pageData, page.Length);
                        break;
                    }
                default:
                    {
                        response.Status = SCSIStatusCodeName.CheckCondition;
                        response.Data = FormatSenseData(SenseDataParameter.GetIllegalRequestParameterNotSupportedSenseData());
                        ISCSIServer.Log("[ModeSense6] ModeSense6 page 0x{0} is not implemented", modeSense6Command.PageCode.ToString("x"));
                        break;
                    }
            }
            response.Data = new byte[1 + header.ModeDataLength];
            Array.Copy(header.GetBytes(), 0, response.Data, 0, header.Length);
            Array.Copy(descriptor.GetBytes(), 0, response.Data, header.Length, descriptor.Length);
            Array.Copy(pageData, 0, response.Data, header.Length + descriptor.Length, pageData.Length);

            EnforceAllocationLength(response, modeSense6Command.AllocationLength);
            return response;
        }

        public static SCSIDataInPDU ReadCapacity10(SCSICommandPDU command, ISCSITarget target)
        {
            ushort LUN = command.LUN;

            SCSIDataInPDU response = new SCSIDataInPDU();
            PrepareSCSIDataInPDU(response, command, SCSIStatusCodeName.Good);

            ReadCapacity10Parameter parameter = new ReadCapacity10Parameter(target.Disks[LUN].Size, (uint)target.Disks[LUN].BytesPerSector);
            response.Data = parameter.GetBytes();

            return response;
        }

        public static SCSIDataInPDU ReadCapacity16(SCSICommandPDU command, ISCSITarget target)
        {
            ushort LUN = command.LUN;

            SCSIDataInPDU response = new SCSIDataInPDU();
            PrepareSCSIDataInPDU(response, command, SCSIStatusCodeName.Good);

            ReadCapacity16Parameter parameter = new ReadCapacity16Parameter(target.Disks[LUN].Size, (uint)target.Disks[LUN].BytesPerSector);
            response.Data = parameter.GetBytes();

            return response;
        }

        public static SCSIDataInPDU ReportLUNs(SCSICommandPDU command, ISCSITarget target)
        {
            SCSIDataInPDU response = new SCSIDataInPDU();
            PrepareSCSIDataInPDU(response, command, SCSIStatusCodeName.Good);

            ReportLUNsParameter parameter = new ReportLUNsParameter(target.Disks.Count);
            response.Data = parameter.GetBytes();

            EnforceAllocationLength(response, command.CommandDescriptorBlock.TransferLength);

            return response;
        }

        public static List<SCSIDataInPDU> Read(SCSICommandPDU command, ISCSITarget target, ConnectionParameters connection)
        {
            ushort LUN = command.LUN;
            
            Disk disk = target.Disks[LUN];
            int sectorCount = (int)command.CommandDescriptorBlock.TransferLength;
            byte[] data = disk.ReadSectors((long)command.CommandDescriptorBlock.LogicalBlockAddress64, sectorCount);
            ISCSIServer.LogRead((long)command.CommandDescriptorBlock.LogicalBlockAddress64, sectorCount);
            List<SCSIDataInPDU> responseList = new List<SCSIDataInPDU>();

            if (data.Length <= connection.InitiatorMaxRecvDataSegmentLength)
            {
                SCSIDataInPDU response = new SCSIDataInPDU();
                PrepareSCSIDataInPDU(response, command, SCSIStatusCodeName.Good);
                response.Data = data;
                responseList.Add(response);
            }
            else // we have to split the response to multiple Data-In PDUs
            {
                int bytesLeftToSend = data.Length;

                uint dataSN = 0;
                while (bytesLeftToSend > 0)
                {
                    int dataSegmentLength;
                    if (bytesLeftToSend == data.Length)
                    {
                        // first segment in many
                        dataSegmentLength = connection.InitiatorMaxRecvDataSegmentLength;
                    }
                    else
                    {
                        dataSegmentLength = Math.Min(connection.InitiatorMaxRecvDataSegmentLength, bytesLeftToSend);
                    }

                    int dataOffset = data.Length - bytesLeftToSend;
                    bytesLeftToSend -= dataSegmentLength;

                    SCSIDataInPDU response = new SCSIDataInPDU();
                    
                    Nullable<SCSIStatusCodeName> status = null;
                    if (bytesLeftToSend == 0)
                    {
                        // last Data-In PDU
                        status = SCSIStatusCodeName.Good;
                    }
                    PrepareSCSIDataInPDU(response, command, status);
                    response.BufferOffset = (uint)dataOffset;
                    response.DataSN = dataSN;
                    dataSN++;

                    response.Data = new byte[dataSegmentLength];
                    Array.Copy(data, dataOffset, response.Data, 0, dataSegmentLength);
                    responseList.Add(response);
                }
            }

            return responseList;
        }

        public static ISCSIPDU Write(SCSICommandPDU command, ISCSITarget target, SessionParameters session, ConnectionParameters connection)
        {
            ushort LUN = command.LUN;

            Disk disk = target.Disks[LUN];
            // when InitialR2T = Yes, and ImmediateData = No, the initiators will wait for R2T before sending any data
            if (command.ExpectedDataTransferLength == command.DataSegmentLength)
            {
                if (!disk.IsReadOnly)
                {
                    ISCSIServer.LogWrite(disk, (long)command.CommandDescriptorBlock.LogicalBlockAddress64, command.Data); // must come before the actual write as it logs changes
                    lock (session.WriteLock)
                    {
                        disk.WriteSectors((long)command.CommandDescriptorBlock.LogicalBlockAddress64, command.Data);
                    }
                }

                SCSIResponsePDU response = new SCSIResponsePDU();
                response.InitiatorTaskTag = command.InitiatorTaskTag;
                if (disk.IsReadOnly)
                {
                    response.Status = SCSIStatusCodeName.CheckCondition;
                    SenseDataParameter senseData = SenseDataParameter.GetDataProtectSenseData();
                    response.Data = FormatSenseData(senseData);
                }
                else
                {
                    response.Status = SCSIStatusCodeName.Good;
                }
    
                return response;
            }
            else // the request is splitted to multiple PDUs
            {
                uint transferTag = session.GetNextTransferTag();
                
                // Store segment (we only execute the command after receiving all of its data)
                byte[] commandData = new byte[command.ExpectedDataTransferLength];
                Array.Copy(command.Data, 0, commandData, 0, command.DataSegmentLength);
                connection.Transfers.Add(transferTag, new KeyValuePair<ulong, uint>(command.CommandDescriptorBlock.LogicalBlockAddress64, command.ExpectedDataTransferLength));
                connection.TransferData.Add(transferTag, commandData);

                // Send R2T
                ReadyToTransferPDU response = new ReadyToTransferPDU();
                response.InitiatorTaskTag = command.InitiatorTaskTag;
                response.R2TSN = 0; // R2Ts are sequenced per command and must start with 0 for each new command;
                response.TargetTransferTag = transferTag;
                response.BufferOffset = command.DataSegmentLength;
                response.DesiredDataTransferLength = Math.Min((uint)connection.TargetMaxRecvDataSegmentLength, command.ExpectedDataTransferLength - response.BufferOffset);

                // We store the next R2TSN to be used
                session.NextR2TSN.Add(transferTag, 1);

                return response;
            }
        }

        public static SCSIResponsePDU Verify(SCSICommandPDU command, ISCSITarget target)
        {
            SCSIResponsePDU response = new SCSIResponsePDU();
            response.InitiatorTaskTag = command.InitiatorTaskTag;
            response.Status = SCSIStatusCodeName.Good;

            return response;
        }

        public static SCSIResponsePDU SynchronizeCache10(SCSICommandPDU command, ISCSITarget target)
        {
            SCSIResponsePDU response = new SCSIResponsePDU();
            response.InitiatorTaskTag = command.InitiatorTaskTag;
            response.Status = SCSIStatusCodeName.Good;

            return response;
        }

        public static byte[] FormatSenseData(SenseDataParameter senseData)
        {
            byte[] senseDataBytes = senseData.GetBytes();
            byte[] result = new byte[senseDataBytes.Length + 2];
            Array.Copy(BigEndianConverter.GetBytes((ushort)senseDataBytes.Length), 0, result, 0, 2);
            Array.Copy(senseDataBytes, 0, result, 2, senseDataBytes.Length);
            return result;
        }
    }
}
