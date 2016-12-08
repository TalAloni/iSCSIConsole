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
using System.Threading;
using System.Runtime.InteropServices;
using DiskAccessLibrary;
using Utilities;

namespace SCSI
{
    public class VirtualSCSITarget : SCSITarget
    {
        private List<Disk> m_disks;

        public event EventHandler<LogEntry> OnLogEntry;

        public VirtualSCSITarget(List<Disk> disks)
        {
            m_disks = disks;
            Thread workerThread = new Thread(ProcessCommandQueue);
            workerThread.IsBackground = true;
            workerThread.Start();
        }

        /// <summary>
        /// This implementation is not thread-safe.
        /// </summary>
        public override SCSIStatusCodeName ExecuteCommand(byte[] commandBytes, LUNStructure lun, byte[] data, out byte[] response)
        {
            SCSICommandDescriptorBlock command;
            try
            {
                command = SCSICommandDescriptorBlock.FromBytes(commandBytes, 0);
            }
            catch(UnsupportedSCSICommandException)
            {
                Log(Severity.Error, "Unsupported SCSI Command (0x{0})", commandBytes[0].ToString("X"));
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestUnsupportedCommandCodeSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }

            return ExecuteCommand(command, lun, data, out response);
        }

        /// <summary>
        /// This implementation is not thread-safe.
        /// </summary>
        public SCSIStatusCodeName ExecuteCommand(SCSICommandDescriptorBlock command, LUNStructure lun, byte[] data, out byte[] response)
        {
            Log(Severity.Verbose, "Executing command: {0}", command.OpCode);
            if (command.OpCode == SCSIOpCodeName.ReportLUNs)
            {
                uint allocationLength = command.TransferLength;
                return ReportLUNs(allocationLength, out response);
            }
            else if (lun >= m_disks.Count)
            {
                Log(Severity.Warning, "Initiator error: tried to execute command on LUN {0} which does not exist", lun);
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidLUNSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }
            else if (command.OpCode == SCSIOpCodeName.TestUnitReady)
            {
                return TestUnitReady(lun, out response);
            }
            else if (command.OpCode == SCSIOpCodeName.RequestSense)
            {
                uint allocationLength = command.TransferLength;
                return RequestSense(lun, allocationLength, out response);
            }
            else if (command.OpCode == SCSIOpCodeName.Inquiry)
            {
                return Inquiry((InquiryCommand)command, lun, out response);
            }
            else if (command.OpCode == SCSIOpCodeName.Reserve6)
            {
                return Reserve6(lun, out response);
            }
            else if (command.OpCode == SCSIOpCodeName.Release6)
            {
                return Release6(lun, out response);
            }
            else if (command.OpCode == SCSIOpCodeName.ModeSense6)
            {
                return ModeSense6((ModeSense6CommandDescriptorBlock)command, lun, out response);
            }
            else if (command.OpCode == SCSIOpCodeName.ReadCapacity10)
            {
                return ReadCapacity10(lun, out response);
            }
            else if (command.OpCode == SCSIOpCodeName.Read6 ||
                     command.OpCode == SCSIOpCodeName.Read10 ||
                     command.OpCode == SCSIOpCodeName.Read16)
            {
                return Read(command, lun, out response);
            }
            else if (command.OpCode == SCSIOpCodeName.Write6 ||
                     command.OpCode == SCSIOpCodeName.Write10 ||
                     command.OpCode == SCSIOpCodeName.Write16)
            {
                return Write(command, lun, data, out response);
            }
            else if (command.OpCode == SCSIOpCodeName.Verify10 ||
                     command.OpCode == SCSIOpCodeName.Verify16)
            {
                return Verify(lun, out response);
            }
            else if (command.OpCode == SCSIOpCodeName.SynchronizeCache10)
            {
                return SynchronizeCache10(lun, out response);
            }
            else if (command.OpCode == SCSIOpCodeName.ServiceActionIn &&
                     command.ServiceAction == ServiceAction.ReadCapacity16)
            {
                uint allocationLength = command.TransferLength;
                return ReadCapacity16(lun, allocationLength, out response);
            }
            else
            {
                Log(Severity.Error, "Unsupported SCSI Command (0x{0})", command.OpCode.ToString("X"));
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestUnsupportedCommandCodeSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }
        }

        public SCSIStatusCodeName Inquiry(InquiryCommand command, LUNStructure lun, out byte[] response)
        {
            if (!command.EVPD)
            {
                if ((int)command.PageCode == 0)
                {
                    StandardInquiryData inquiryData = new StandardInquiryData();
                    inquiryData.PeripheralDeviceType = 0; // Direct access block device
                    inquiryData.VendorIdentification = "TalAloni";
                    inquiryData.ProductIdentification = "SCSI Disk " + ((ushort)lun).ToString();
                    inquiryData.ProductRevisionLevel = "1.00";
                    inquiryData.DriveSerialNumber = 0;
                    inquiryData.CmdQue = true;
                    inquiryData.Version = 5; // SPC-3
                    NotifyStandardInquiry(this, new StandardInquiryEventArgs(lun, inquiryData));
                    response = inquiryData.GetBytes();
                }
                else
                {
                    Log(Severity.Warning, "Initiator error: Invalid Inquiry request");
                    response = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidFieldInCDBSenseData());
                    return SCSIStatusCodeName.CheckCondition;
                }
            }
            else
            {
                Log(Severity.Verbose, "Inquiry: Page code: 0x{0}", command.PageCode.ToString("X"));
                switch (command.PageCode)
                {
                    case VitalProductDataPageName.SupportedVPDPages:
                        {
                            SupportedVitaLProductDataPages page = new SupportedVitaLProductDataPages();
                            page.SupportedPageList.Add((byte)VitalProductDataPageName.SupportedVPDPages);
                            page.SupportedPageList.Add((byte)VitalProductDataPageName.UnitSerialNumber);
                            page.SupportedPageList.Add((byte)VitalProductDataPageName.DeviceIdentification);
                            response = page.GetBytes();
                            break;
                        }
                    case VitalProductDataPageName.UnitSerialNumber:
                        {
                            UnitSerialNumberVPDPage page = new UnitSerialNumberVPDPage();
                            // Older products that only support the Product Serial Number parameter will have a page length of 08h, while newer products that support both parameters (Vendor Unique field from the StandardInquiryData) will have a page length of 14h
                            // Microsoft iSCSI Target uses values such as "34E5A6FC-3ACC-452D-AEDA-6EE2EFF20FB4"
                            ulong serialNumber = 0;
                            page.ProductSerialNumber = serialNumber.ToString("00000000");
                            response = page.GetBytes();
                            break;
                        }
                    case VitalProductDataPageName.DeviceIdentification:
                        {
                            DeviceIdentificationVPDPage page = new DeviceIdentificationVPDPage();
                            // NAA identifier is needed to prevent 0xF4 BSOD during Windows setup
                            page.IdentificationDescriptorList.Add(IdentificationDescriptor.GetNAAExtendedIdentifier(5, lun));
                            NotifyDeviceIdentificationInquiry(this, new DeviceIdentificationInquiryEventArgs(lun, page));
                            response = page.GetBytes();
                            break;
                        }
                    case VitalProductDataPageName.BlockLimits:
                        {
                            /* Provide only when requeste explicitly */
                            BlockLimitsVPDPage page = new BlockLimitsVPDPage();
                            page.OptimalTransferLengthGranularity = 128;
                            page.MaximumTransferLength = (uint)DiskAccessLibrary.Settings.MaximumTransferSizeLBA;
                            page.OptimalTransferLength = 128;
                            response = page.GetBytes();
                            break;
                        }
                    case VitalProductDataPageName.BlockDeviceCharacteristics:
                        {
                            /* Provide only when requeste explicitly */
                            BlockDeviceCharacteristicsVPDPage page = new BlockDeviceCharacteristicsVPDPage();
                            page.MediumRotationRate = 0; // Not reported
                            response = page.GetBytes();
                            break;
                        }
                    default:
                        {
                            Log(Severity.Error, "Inquiry: Unsupported VPD Page request (0x{0})", command.PageCode.ToString("X"));
                            response = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidFieldInCDBSenseData());
                            return SCSIStatusCodeName.CheckCondition;
                        }
                }
            }

            // we must not return more bytes than InquiryCommand.AllocationLength
            if (response.Length > command.AllocationLength)
            {
                response = ByteReader.ReadBytes(response, 0, command.AllocationLength);
            }
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName ModeSense6(ModeSense6CommandDescriptorBlock command, LUNStructure lun, out byte[] response)
        {
            Log(Severity.Verbose, "ModeSense6: Page code: 0x{0}, Sub page code: 0x{1}", command.PageCode.ToString("X"), command.SubpageCode.ToString("X"));
            byte[] pageData;

            switch ((ModePageCodeName)command.PageCode)
            {
                case ModePageCodeName.CachingParametersPage:
                    {
                        CachingParametersPage page = new CachingParametersPage();
                        page.RCD = true;
                        pageData = page.GetBytes();
                        break;
                    }
                case ModePageCodeName.ControlModePage:
                    {
                        ControlModePage page = new ControlModePage();
                        pageData = page.GetBytes();
                        break;
                    }
                case ModePageCodeName.PowerConditionModePage:
                    {
                        if (command.SubpageCode == 0x00)
                        {
                            PowerConditionModePage page = new PowerConditionModePage();
                            pageData = page.GetBytes();
                            break;
                        }
                        else if (command.SubpageCode == 0x01)
                        {
                            PowerConsumptionModePage page = new PowerConsumptionModePage();
                            pageData = page.GetBytes();
                            break;
                        }
                        else
                        {
                            Log(Severity.Error, "ModeSense6: Power condition subpage 0x{0} is not implemented", command.SubpageCode.ToString("x"));
                            response = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidFieldInCDBSenseData());
                            return SCSIStatusCodeName.CheckCondition;
                        }
                    }
                case ModePageCodeName.InformationalExceptionsControlModePage:
                    {
                        InformationalExceptionsControlModePage page = new InformationalExceptionsControlModePage();
                        pageData = page.GetBytes();
                        break;
                    }
                case ModePageCodeName.ReturnAllPages:
                    {
                        CachingParametersPage page1 = new CachingParametersPage();
                        page1.RCD = true;
                        InformationalExceptionsControlModePage page2 = new InformationalExceptionsControlModePage();

                        pageData = new byte[page1.Length + page2.Length];
                        Array.Copy(page1.GetBytes(), pageData, page1.Length);
                        Array.Copy(page2.GetBytes(), 0, pageData, page1.Length, page2.Length);
                        break;
                    }
                case ModePageCodeName.VendorSpecificPage:
                    {
                        // Windows 2000 will request this page, we immitate Microsoft iSCSI Target by sending back an empty page
                        VendorSpecificPage page = new VendorSpecificPage();
                        pageData = page.GetBytes();
                        break;
                    }
                default:
                    {
                        Log(Severity.Error, "ModeSense6: ModeSense6 page 0x{0} is not implemented", command.PageCode.ToString("x"));
                        response = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidFieldInCDBSenseData());
                        return SCSIStatusCodeName.CheckCondition;
                    }
            }

            ModeParameterHeader6 header = new ModeParameterHeader6();
            header.WP = m_disks[lun].IsReadOnly; // Write protected, even when set to true, Windows does not always prevent the disk from being written to.
            header.DPOFUA = true;  // Microsoft iSCSI Target support this
            byte[] descriptorBytes = new byte[0];
            if (!command.DBD)
            {
                ShortLBAModeParameterBlockDescriptor descriptor = new ShortLBAModeParameterBlockDescriptor();
                descriptor.LogicalBlockLength = (uint)m_disks[lun].BytesPerSector;
                descriptorBytes = descriptor.GetBytes();
            }
            header.BlockDescriptorLength = (byte)descriptorBytes.Length;
            header.ModeDataLength += (byte)(descriptorBytes.Length + pageData.Length);

            response = new byte[1 + header.ModeDataLength];
            Array.Copy(header.GetBytes(), 0, response, 0, header.Length);
            Array.Copy(descriptorBytes, 0, response, header.Length, descriptorBytes.Length);
            Array.Copy(pageData, 0, response, header.Length + descriptorBytes.Length, pageData.Length);

            // we must not return more bytes than ModeSense6Command.AllocationLength
            if (response.Length > command.AllocationLength)
            {
                response = ByteReader.ReadBytes(response, 0, command.AllocationLength);
            }
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName ReadCapacity10(LUNStructure lun, out byte[] response)
        {
            ReadCapacity10Parameter parameter = new ReadCapacity10Parameter(m_disks[lun].Size, (uint)m_disks[lun].BytesPerSector);
            response = parameter.GetBytes();
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName ReadCapacity16(LUNStructure lun, uint allocationLength, out byte[] response)
        {
            ReadCapacity16Parameter parameter = new ReadCapacity16Parameter(m_disks[lun].Size, (uint)m_disks[lun].BytesPerSector);
            response = parameter.GetBytes();
            // we must not return more bytes than ReadCapacity16.AllocationLength
            if (response.Length > allocationLength)
            {
                response = ByteReader.ReadBytes(response, 0, (int)allocationLength);
            }
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName ReportLUNs(uint allocationLength, out byte[] response)
        {
            ReportLUNsParameter parameter = new ReportLUNsParameter(m_disks.Count);
            response = parameter.GetBytes();
            // we must not return more bytes than ReportLUNs.AllocationLength
            if (response.Length > allocationLength)
            {
                response = ByteReader.ReadBytes(response, 0, (int)allocationLength);
            }
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName Read(SCSICommandDescriptorBlock command, LUNStructure lun, out byte[] response)
        {
            Disk disk = m_disks[lun];
            int sectorCount = (int)command.TransferLength;
            Log(Severity.Verbose, "LUN {0}: Reading {1} blocks starting from LBA {2}", (ushort)lun, sectorCount, (long)command.LogicalBlockAddress64);
            try
            {
                response = disk.ReadSectors((long)command.LogicalBlockAddress64, sectorCount);
                return SCSIStatusCodeName.Good;
            }
            catch (ArgumentOutOfRangeException)
            {
                Log(Severity.Error, "Read error: LBA out of range");
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestLBAOutOfRangeSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }
            catch (CyclicRedundancyCheckException)
            {
                Log(Severity.Error, "Read error: CRC error");
                response = FormatSenseData(SenseDataParameter.GetWriteFaultSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }
            catch (IOException ex)
            {
                Log(Severity.Error, "Read error: {0}", ex.ToString());
                response = FormatSenseData(SenseDataParameter.GetMediumErrorUnrecoverableReadErrorSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }
        }

        // Some initiators (i.e. EFI iSCSI DXE) will send 'Request Sense' upon connection (likely just to verify the medium is ready)
        public SCSIStatusCodeName RequestSense(LUNStructure lun, uint allocationLength, out byte[] response)
        {
            response = FormatSenseData(SenseDataParameter.GetNoSenseSenseData());
            // we must not return more bytes than RequestSense.AllocationLength
            if (response.Length > allocationLength)
            {
                response = ByteReader.ReadBytes(response, 0, (int)allocationLength);
            }
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName Reserve6(LUNStructure lun, out byte[] response)
        {
            response = new byte[0];
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName SynchronizeCache10(LUNStructure lun, out byte[] response)
        {
            response = new byte[0];
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName Release6(LUNStructure lun, out byte[] response)
        {
            response = new byte[0];
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName TestUnitReady(LUNStructure lun, out byte[] response)
        {
            response = new byte[0];
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName Verify(LUNStructure lun, out byte[] response)
        {
            response = new byte[0];
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName Write(SCSICommandDescriptorBlock command, LUNStructure lun, byte[] data, out byte[] response)
        {
            Disk disk = m_disks[lun];
            if (disk.IsReadOnly)
            {
                Log(Severity.Verbose, "LUN {0}: Refused attempt to write to a read-only disk", lun);
                SenseDataParameter senseData = SenseDataParameter.GetDataProtectSenseData();
                response = senseData.GetBytes();
                return SCSIStatusCodeName.CheckCondition;
            }

            Log(Severity.Verbose, "LUN {0}: Writing {1} blocks starting from LBA {2}", (ushort)lun, command.TransferLength, (long)command.LogicalBlockAddress64);
            try
            {
                disk.WriteSectors((long)command.LogicalBlockAddress64, data);
                response = new byte[0];
                return SCSIStatusCodeName.Good;
            }
            catch (ArgumentOutOfRangeException)
            {
                Log(Severity.Error, "Write error: LBA out of range");
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestLBAOutOfRangeSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }
            catch (IOException ex)
            {
                Log(Severity.Error, "Write error: {0}", ex.ToString());
                response = FormatSenseData(SenseDataParameter.GetMediumErrorWriteFaultSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }
        }

        public void Log(Severity severity, string message)
        {
            // To be thread-safe we must capture the delegate reference first
            EventHandler<LogEntry> handler = OnLogEntry;
            if (handler != null)
            {
                handler(this, new LogEntry(DateTime.Now, severity, "Virtual SCSI Target", message));
            }
        }

        public void Log(Severity severity, string message, params object[] args)
        {
            Log(severity, String.Format(message, args));
        }

        public List<Disk> Disks
        {
            get
            {
                return m_disks;
            }
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
