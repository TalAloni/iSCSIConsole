using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using ISCSI;
using Utilities;

namespace SCSI
{
    internal class GetConfigurationParameter
    {
        private const short MM_PROF_DVDROM = 0x0010;
        private readonly Disk _disk;
        private GetConfigurationCommand _cmd;

        public GetConfigurationParameter(Disk disk, GetConfigurationCommand command)
        {
            _disk = disk;
            _cmd = command;
        }

        internal byte[] GetBytes()
        {
            int hlen;
            int len = 0;
            int plen;
            ushort fc;

            var data = new byte[128];

            /* Reserved */
            data[4] = 0;
            /* Reserved */
            data[5] = 0;
            /* Current Profile */
            BigEndianWriter.WriteInt16(data, 6, MM_PROF_DVDROM);

            hlen = 8;

            switch (_cmd.RT)
            {
                case 0x00:
                    /* all of features */
                    for (fc = _cmd.StartingFeatureNumber; fc < 0xffff; fc++)
                    {
                        plen = istgt_lu_dvd_get_feature_descriptor(fc, data, hlen + len);
                        len += plen;
                    }
                    break;

                case 0x01:
                    /* current of features */
                    for (fc = _cmd.StartingFeatureNumber; fc < 0xffff; fc++)
                    {
                        plen = istgt_lu_dvd_get_feature_descriptor(fc, data, hlen + len);
                        if (data[hlen + 2] == 1)
                        {
                            len += plen;
                        }
                        else
                        {
                            /* drop non active descriptors */
                        }
                    }
                    break;

                case 0x02:
                    /* specified feature */
                    fc = _cmd.StartingFeatureNumber;
                    plen = istgt_lu_dvd_get_feature_descriptor(fc, data, hlen + len);
                    len += plen;
                    break;

                default:
                    /* not supported */
                    break;
            }

            /* Data Length */
            BigEndianWriter.WriteInt32(data, 0, len);

            return data;
        }

        private int istgt_lu_dvd_get_feature_descriptor(ushort fc, byte[] data, int offset)
        {
            byte hlen = 0, len = 0, plen;

            switch (fc)
            {
                case 0x0000:
                    /* Profile List */
                    plen = 2 * 4 + 4;
                    FEATURE_DESCRIPTOR_INIT(data, offset, plen, fc);
                    /* Version(5-2) Persistent(1) Current(0) */
                    // BDSET8W(&data[2], 0, 5, 4);
                    // BSET8(&data[2], 1);         /* Persistent=1 */
                    // BSET8(&data[2], 0);         /* Current=1 */
                    data[offset + 2] = 0b_0000_0011;

                    hlen = 4;

                    /* Profile Descriptor */
                    /* Profile 1 (CDROM) */
                    // DSET16(&cp[0], 0x0008);
                    BigEndianWriter.WriteInt16(data, offset + hlen + len, 0x0008);

                    plen = 4;
                    len += plen;

                    /* Profile 2 (DVDROM) */
                    // DSET16(&cp[0], MM_PROF_DVDROM);
                    BigEndianWriter.WriteInt16(data, offset + hlen + len, MM_PROF_DVDROM);

// BSET8(&cp[2], 0);       /* CurrentP(0)=1 */
                    data[offset + hlen + len + 2] = 1;
                    plen = 4;
                    len += plen;
                    break;

                case 0x0001:
                    /* Core Feature */
                    /* GET CONFIGURATION/GET EVENT STATUS NOTIFICATION/INQUIRY */
                    /* MODE SELECT (10)/MODE SENSE (10)/REQUEST SENSE/TEST UNIT READY */
                    plen = 8 + 4;
                    FEATURE_DESCRIPTOR_INIT(data, offset, plen, fc);
                    /* Version(5-2) Persistent(1) Current(0) */
//                     BDSET8W(&data[2], 0x01, 5, 4);          /* MMC4 */
//                     BSET8(&data[2], 1);         /* Persistent=1 */
//                     BSET8(&data[2], 0);         /* Current=1 */
                    data[offset + 2] = 0b_0000_0111;
                    hlen = 4;

                    /* Physical Interface Standard */
                    // DSET32(&data[4], 0x00000000);           /* Unspecified */
                    BigEndianWriter.WriteInt32(data, 4, 0x00000000);
                    /* DBE(0) */
                    // BCLR8(&data[8], 0);         /* DBE=0*/
                    data[offset + 8] = 0;
                    len = 8;
                    break;

                case 0x0003:
                    /* Removable Medium */
                    /* MECHANISM STATUS/PREVENT ALLOW MEDIUM REMOVAL/START STOP UNIT */
                    plen = 0x04 + 4;
                    FEATURE_DESCRIPTOR_INIT(data, offset, plen, fc);
                    /* Version(5-2) Persistent(1) Current(0) */
//                     BDSET8W(&data[2], 0x01, 5, 4);
//                     BSET8(&data[2], 1);         /* Persistent=1 */
//                     BSET8(&data[2], 0);         /* Current=1 */
                    data[offset + 2] = 0b_0000_0111;
                    hlen = 4;

                    /* Loading Mechanism Type(7-5) Eject(3) Pvnt Jmpr(2) Lock(0) */
//                     BDSET8W(&data[4], 0x01, 7, 3); /* Tray type loading mechanism */
//                     BSET8(&data[4], 3);         /* eject via START/STOP YES */
//                     BSET8(&data[4], 0);         /* locking YES */
                    data[offset + 4] = 0b_0001_1001;

                    len = 8;
                    break;

                case 0x0010:
                    /* Random Readable */
                    /* READ CAPACITY/READ (10) */
                    plen = 4 + 4;
                    FEATURE_DESCRIPTOR_INIT(data, offset, plen, fc);
                    /* Version(5-2) Persistent(1) Current(0) */
//                     BDSET8W(&data[2], 0x00, 5, 4);
//                     BSET8(&data[2], 1);         /* Persistent=1 */
//                     BSET8(&data[2], 0);         /* Current=1 */
                    data[offset + 2] = 0b_0000_0011;

                    hlen = 4;

                    /* Logical Block Size */
                    // DSET32(&data[4], (uint32_t)spec->blocklen);
                    BigEndianWriter.WriteInt32(data, 4, (int)_disk.TotalSectors);
                    /* Blocking */
                    // DSET16(&data[8], 1);
                    BigEndianWriter.WriteUInt16(data, 8, 0x2);
                    /* PP(0) */
                    // BCLR8(&data[10], 0);            /* PP=0 */
                    data[offset + 10] = 0;
                    len = 4;
                    break;

                case 0x001d:
                    /* Multi-Read Feature */
                    /* READ (10)/READ CD/READ DISC INFORMATION/READ TRACK INFORMATION */
                    plen = 4;
                    FEATURE_DESCRIPTOR_INIT(data, offset, plen, fc);
                    /* Version(5-2) Persistent(1) Current(0) */
//                     BDSET8W(&data[2], 0x00, 5, 4);
//                     BSET8(&data[2], 1);         /* Persistent=1 */
//                     BSET8(&data[2], 0);         /* Current=1 */
                    data[offset + 2] = 0b_0000_0011;

                    hlen = 4;
                    len = 0;
                    break;

                case 0x001e:
                    /* CD Read */
                    /* READ CD/READ CD MSF/READ TOC/PMA/ATIP */
                    plen = 4 + 4;
                    FEATURE_DESCRIPTOR_INIT(data, offset, plen, fc);
                    /* Version(5-2) Persistent(1) Current(0) */
//                     BDSET8W(&data[2], 0x02, 5, 4); /* MMC4 */
//                     BCLR8(&data[2], 1);         /* Persistent=0 */
//                     if (spec->profile == MM_PROF_CDROM)
//                     {
//                         BSET8(&data[2], 0);     /* Current=1 */
//                     }
//                     else
//                     {
//                         BCLR8(&data[2], 0);     /* Current=0 */
//                     }
                    data[offset + 2] = 0b_0000_1000;

                    hlen = 4;

                    /* DAP(7) C2 Flags(1) CD-Text(0) */
//                     BCLR8(&data[4], 7);     /* not support DAP */
//                     BCLR8(&data[4], 1);     /* not support C2 */
//                     BCLR8(&data[4], 0);     /* not support CD-Text */
                    data[offset + 4] = 0;
                    len = 4;
                    break;

                case 0x001f:
                    /* DVD Read */
                    /* READ (10)/READ (12)/READ DVD STRUCTURE/READ TOC/PMA/ATIP */
                    plen = 4;
                    FEATURE_DESCRIPTOR_INIT(data, offset, plen, fc);
                    /* Version(5-2) Persistent(1) Current(0) */
//                     BDSET8W(&data[2], 0x00, 5, 4);
//                     BCLR8(&data[2], 1);         /* Persistent=0 */
//                     if (spec->profile == MM_PROF_DVDROM)
//                     {
//                         BSET8(&data[2], 0);     /* Current=1 */
//                     }
//                     else
//                     {
//                         BCLR8(&data[2], 0);     /* Current=0 */
//                     }
                    data[offset + 2] = 0b_0000_0001;

                    hlen = 4;
                    len = 0;
                    break;

                default:
                    /* not supported */
                    break;
            }

            return hlen + len;
        }

        private void FEATURE_DESCRIPTOR_INIT(byte[] data, int offset, byte plen, ushort fc)
        {
            BigEndianWriter.WriteUInt16(data, offset, fc);
            data[offset + 3] = (byte)(plen - 4);
        }
    }
}
