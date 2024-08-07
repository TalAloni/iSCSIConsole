using Utilities;

namespace SCSI
{
    internal class ReadTocParameter
    {
        const int Length = 64;
        private Disk _disk;
        private bool _msf;
        private readonly byte _format;

        public ReadTocParameter(Disk disk, bool msf, byte format)
        {
            _disk = disk;
            _msf = msf;
            _format = format;
        }

        internal byte[] GetBytes()
        {
            switch(_format)
            {
                case 0:
                    {
                        FormattedToc toc = new FormattedToc(_msf);
                        toc.FirstTrackNumber = 1;
                        toc.LastTrackNumber = 1;
                        toc.SetTocTrackDescriptor(0, 1, 4, 1, 0);
                        // Lead-Out
                        toc.SetTocTrackDescriptor(1, 1, 4, 0xAA, (uint)_disk.TotalSectors);
                        toc.UpdateDataLength(2);

                        return toc.GetBytes();
                    }
                case 1: return MultiSessionInformation();
                case 2: return RawTOC();
                default: return null;
            }
        }

        private byte[] RawTOC()
        {
            sbyte hlen;
            sbyte len = 0, plen;

            byte[] data = new byte[Length];

            /* First Complete Session Number */
            data[2] = 1;
            /* Last Complete Session Number */
            data[3] = 1;
            hlen = 4;

            /* TOC Track Descriptor */
            /* First Track number in the program area */
            int offset = hlen + len;

            /* Session Number */
            data[offset + 0] = 1;
            /* ADR(7-4) CONTROL(3-0) */
            data[offset + 1] = 0x14;
            /* TNO */
            data[offset + 2] = 0;
            /* POINT */
            data[offset + 3] = 0xa0;
            /* Min */
            data[offset + 4] = 0;
            /* Sec */
            data[offset + 5] = 0;
            /* Frame */
            data[offset + 6] = 0;
            /* Zero */
            data[offset + 7] = 0;
            /* PMIN / First Track Number */
            data[offset + 8] = 1;
            /* PSEC / Disc Type */
            data[offset + 9] = 0x00; /* CD-DA or CD Data with first track in Mode 1 */
            /* PFRAME */
            data[offset + 10] = 0;
            plen = 11;
            len += plen;

            /* Last Track number in the program area */
            offset = hlen + len;

            /* Session Number */
            data[offset + 0] = 1;
            /* ADR(7-4) CONTROL(3-0) */
            data[offset + 1] = 0x14;
            /* TNO */
            data[offset + 2] = 0;
            /* POINT */
            data[offset + 3] = 0xa1;
            /* Min */
            data[offset + 4] = 0;
            /* Sec */
            data[offset + 5] = 0;
            /* Frame */
            data[offset + 6] = 0;
            /* Zero */
            data[offset + 7] = 0;
            /* PMIN / Last Track Number */
            data[offset + 8] = 1;
            /* PSEC */
            data[offset + 9] = 0;
            /* PFRAME */
            data[offset + 10] = 0;
            plen = 11;
            len += plen;

            /* Start location of the Lead-out area */
            offset = hlen + len;

            /* Session Number */
            data[offset + 0] = 1;
            /* ADR(7-4) CONTROL(3-0) */
            data[offset + 1] = 0x14;
            /* TNO */
            data[offset + 2] = 0;
            /* POINT */
            data[offset + 3] = 0xa2;
            /* Min */
            data[offset + 4] = 0;
            /* Sec */
            data[offset + 5] = 0;
            /* Frame */
            data[offset + 6] = 0;
            /* Zero */
            data[offset + 7] = 0;
            /* PMIN / Start position of Lead-out */
            /* PSEC / Start position of Lead-out */
            /* PFRAME / Start position of Lead-out */
            if (_msf)
            {
                // DSET24(&cp[8], istgt_lba2msf(spec->blockcnt));
                byte[] bytes = BigEndianConverter.GetBytes(_disk.TotalSectors);
                data[offset + 8] = bytes[0];
                data[offset + 9] = bytes[1];
                data[offset + 10] = bytes[2];
            }
            else
            {
                // DSET24(&data[offset + 8], spec->blockcnt);
                byte[] bytes = BigEndianConverter.GetBytes(_disk.TotalSectors);
                data[offset + 8] = bytes[0];
                data[offset + 9] = bytes[1];
                data[offset + 10] = bytes[2];
            }
            plen = 11;
            len += plen;

            /* Track data */
            offset = hlen + len;

            /* Session Number */
            data[offset + 0] = 1;
            /* ADR(7-4) CONTROL(3-0) */
            data[offset + 1] = 0x14;
            /* TNO */
            data[offset + 2] = 0;
            /* POINT */
            data[offset + 3] = 1;
            /* Min */
            data[offset + 4] = 0;
            /* Sec */
            data[offset + 5] = 0;
            /* Frame */
            data[offset + 6] = 0;
            /* Zero */
            data[offset + 7] = 0;
            /* PMIN / Start position of Lead-out */
            /* PSEC / Start position of Lead-out */
            /* PFRAME / Start position of Lead-out */
            if (_msf)
            {
                // TODO: add actual conversion if needed
                // DSET24(&cp[8], istgt_lba2msf(0));
                BigEndianWriter.WriteInt16(data, offset + 8, 0);
            }
            else
            {
                // DSET24(&cp[8], 0);
                BigEndianWriter.WriteInt16(data, offset + 8, 0);
            }
            plen = 11;
            len += plen;

            /* TOC Data Length */
            // DSET16(&data[0], hlen + len - 2);
            BigEndianWriter.WriteInt16(data, 0, (short)(hlen + len - 2));

            return data;
        }
        private byte[] MultiSessionInformation()
        {
            sbyte hlen;
            sbyte len = 0;

            byte[] data = new byte[Length];

            /* First Complete Session Number */
            data[2] = 1;
            /* Last Complete Session Number */
            data[3] = 1;
            hlen = 4;

            /* TOC Track Descriptor */
            int offset = hlen + len;

            /* Reserved */
            data[offset + 0] = 0;
            /* ADR(7-4) CONTROL(3-0) */
            data[offset + 1] = 0x14;
            /* First Track Number In Last Complete Session */
            data[offset + 2] = 1;
            /* Reserved */
            data[offset + 3] = 0;
            /* Start Address of First Track in Last Session */
            if (_msf)
            {
                // TODO: add lba2msf if needed
                // DSET32(&cp[4], istgt_lba2msf(0));
                BigEndianWriter.WriteInt32(data, offset + 4, /*lba2msf*/ 0);
            }
            else
            {
                BigEndianWriter.WriteInt32(data, offset + 4, 0);
            }
            len = 8;

            /* TOC Data Length */
            // DSET16(&data[0], hlen + len - 2);
            BigEndianWriter.WriteInt16(data, 0, (short)(hlen + len - 2));

            return data;
        }
    }
}
