using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using DiskAccessLibrary;
using Utilities;

namespace ISCSI.Server
{
    public class SCSITarget
    {
        private List<Disk> m_disks;
        public object WriteLock = new object();

        public SCSITarget(List<Disk> disks)
        {
            m_disks = disks;
        }

        public SCSIStatusCodeName ExecuteCommand(SCSICommandDescriptorBlock command, LUNStructure lun, byte[] data, out byte[] response)
        {
            if (command.OpCode == SCSIOpCodeName.TestUnitReady)
            {
                return TestUnitReady(lun, out response);
            }
            else if (command.OpCode == SCSIOpCodeName.RequestSense)
            {
                return RequestSense(lun, out response);
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
                return ReadCapacity16(lun, out response);
            }
            else if (command.OpCode == SCSIOpCodeName.ReportLUNs)
            {
                return ReportLUNs(out response);
            }
            else
            {
                ISCSIServer.Log("[ExecuteCommand] Unsupported SCSI Command (0x{0})", command.OpCode.ToString("X"));
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestUnsupportedCommandCodeSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }
        }

        public SCSIStatusCodeName Inquiry(InquiryCommand command, LUNStructure lun, out byte[] response)
        {
            if (lun >= m_disks.Count)
            {
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidLUNSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }

            if (!command.EVPD)
            {
                StandardInquiryData inquiryData = new StandardInquiryData();
                inquiryData.PeripheralDeviceType = 0; // Direct access block device
                inquiryData.VendorIdentification = "iSCSIConsole";
                inquiryData.ProductIdentification = "Disk_" + lun.ToString();
                inquiryData.ProductRevisionLevel = "1.00";
                inquiryData.DriveSerialNumber = 0;
                inquiryData.CmdQue = true;
                inquiryData.Version = 5; // Microsoft iSCSI Target report version 5
                response = inquiryData.GetBytes();
            }
            else
            {
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
                            // Identifiers necessity is preliminary, and has not been confirmed:
                            // WWN identifier is needed to prevent 0xF4 BSOD during Windows setup
                            // ISCSI identifier is needed for WinPE to pick up the disk during boot (after iPXE's sanhook)
                            page.IdentificationDescriptorList.Add(new IdentificationDescriptor(5, lun));
                            if (this is ISCSITarget)
                            {
                                page.IdentificationDescriptorList.Add(IdentificationDescriptor.GetISCSIIdentifier(((ISCSITarget)this).TargetName));
                            }
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
                            response = FormatSenseData(SenseDataParameter.GetIllegalRequestParameterNotSupportedSenseData());
                            ISCSIServer.Log("[Inquiry] Unsupported VPD Page request (0x{0})", command.PageCode.ToString("X"));
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
            if (lun >= m_disks.Count)
            {
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidLUNSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }

            ShortLBAModeParameterBlockDescriptor descriptor = new ShortLBAModeParameterBlockDescriptor();
            descriptor.LogicalBlockLength = (uint)m_disks[lun].BytesPerSector;

            ModeParameterHeader6 header = new ModeParameterHeader6();
            header.WP = m_disks[lun].IsReadOnly; // Write protected, even when set to true, Windows does not always prevent the disk from being written to.
            header.DPOFUA = true;  // Microsoft iSCSI Target support this
            header.BlockDescriptorLength = (byte)descriptor.Length;
            header.ModeDataLength += (byte)descriptor.Length;

            byte[] pageData = new byte[0];

            switch ((ModePageCodeName)command.PageCode)
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
                        response = FormatSenseData(SenseDataParameter.GetIllegalRequestParameterNotSupportedSenseData());
                        ISCSIServer.Log("[ModeSense6] ModeSense6 page 0x{0} is not implemented", command.PageCode.ToString("x"));
                        return SCSIStatusCodeName.CheckCondition;
                    }
            }
            response = new byte[1 + header.ModeDataLength];
            Array.Copy(header.GetBytes(), 0, response, 0, header.Length);
            Array.Copy(descriptor.GetBytes(), 0, response, header.Length, descriptor.Length);
            Array.Copy(pageData, 0, response, header.Length + descriptor.Length, pageData.Length);
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName ReadCapacity10(LUNStructure lun, out byte[] response)
        {
            if (lun >= m_disks.Count)
            {
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidLUNSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }

            ReadCapacity10Parameter parameter = new ReadCapacity10Parameter(m_disks[lun].Size, (uint)m_disks[lun].BytesPerSector);
            response = parameter.GetBytes();
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName ReadCapacity16(LUNStructure lun, out byte[] response)
        {
            if (lun >= m_disks.Count)
            {
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidLUNSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }

            ReadCapacity16Parameter parameter = new ReadCapacity16Parameter(m_disks[lun].Size, (uint)m_disks[lun].BytesPerSector);
            response = parameter.GetBytes();
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName ReportLUNs(out byte[] response)
        {
            ReportLUNsParameter parameter = new ReportLUNsParameter(m_disks.Count);
            response = parameter.GetBytes();
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName Read(SCSICommandDescriptorBlock command, LUNStructure lun, out byte[] response)
        {
            if (lun >= m_disks.Count)
            {
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidLUNSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }

            Disk disk = m_disks[lun];
            int sectorCount = (int)command.TransferLength;
            try
            {
                response = disk.ReadSectors((long)command.LogicalBlockAddress64, sectorCount);
                return SCSIStatusCodeName.Good;
            }
            catch (ArgumentOutOfRangeException)
            {
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestLBAOutOfRangeSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }
            catch (IOException ex)
            {
                int error = Marshal.GetHRForException(ex);
                if (error == (int)Win32Error.ERROR_CRC)
                {
                    response = FormatSenseData(SenseDataParameter.GetWriteFaultSenseData());
                    return SCSIStatusCodeName.CheckCondition;
                }
                else
                {
                    ISCSIServer.Log("[{0}][Read] Read error:", ex.ToString());
                    response = FormatSenseData(SenseDataParameter.GetUnitAttentionSenseData());
                    return SCSIStatusCodeName.CheckCondition;
                }
            }
        }

        // Some initiators (i.e. EFI iSCSI DXE) will send 'Request Sense' upon connection (likely just to verify the medium is ready)
        public SCSIStatusCodeName RequestSense(LUNStructure lun, out byte[] response)
        {
            if (lun >= m_disks.Count)
            {
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidLUNSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }

            response = FormatSenseData(SenseDataParameter.GetNoSenseSenseData());
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName Reserve6(LUNStructure lun, out byte[] response)
        {
            if (lun >= m_disks.Count)
            {
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidLUNSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }

            response = new byte[0];
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName SynchronizeCache10(LUNStructure lun, out byte[] response)
        {
            if (lun >= m_disks.Count)
            {
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidLUNSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }

            response = new byte[0];
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName Release6(LUNStructure lun, out byte[] response)
        {
            if (lun >= m_disks.Count)
            {
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidLUNSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }

            response = new byte[0];
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName TestUnitReady(LUNStructure lun, out byte[] response)
        {
            if (lun >= m_disks.Count)
            {
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidLUNSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }

            response = new byte[0];
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName Verify(LUNStructure lun, out byte[] response)
        {
            if (lun >= m_disks.Count)
            {
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidLUNSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }

            response = new byte[0];
            return SCSIStatusCodeName.Good;
        }

        public SCSIStatusCodeName Write(SCSICommandDescriptorBlock command, LUNStructure lun, byte[] data, out byte[] response)
        {
            return Write(lun, (long)command.LogicalBlockAddress64, data, out response);
        }

        public SCSIStatusCodeName Write(LUNStructure lun, long sectorIndex, byte[] data, out byte[] response)
        {
            if (lun >= m_disks.Count)
            {
                response = FormatSenseData(SenseDataParameter.GetIllegalRequestInvalidLUNSenseData());
                return SCSIStatusCodeName.CheckCondition;
            }

            Disk disk = m_disks[lun];
            if (disk.IsReadOnly)
            {
                SenseDataParameter senseData = SenseDataParameter.GetDataProtectSenseData();
                response = senseData.GetBytes();
                return SCSIStatusCodeName.CheckCondition;
            }

            lock (WriteLock)
            {
                try
                {
                    disk.WriteSectors(sectorIndex, data);
                    response = new byte[0];
                    return SCSIStatusCodeName.Good;
                }
                catch (ArgumentOutOfRangeException)
                {
                    response = FormatSenseData(SenseDataParameter.GetIllegalRequestLBAOutOfRangeSenseData());
                    return SCSIStatusCodeName.CheckCondition;
                }
                catch (IOException ex)
                {
                    ISCSIServer.Log("[{0}][Write] Write error:", ex.ToString());
                    response = FormatSenseData(SenseDataParameter.GetUnitAttentionSenseData());
                    return SCSIStatusCodeName.CheckCondition;
                }
            }
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
