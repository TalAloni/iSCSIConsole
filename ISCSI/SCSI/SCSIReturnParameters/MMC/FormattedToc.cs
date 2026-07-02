using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace SCSI
{
    internal class FormattedToc
    {
        const int Length = 64;
        byte[] data = new byte[Length];
        bool _msf = false;

        /// <summary>
        /// The TOC data length indicates the length in bytes of the following TOC data. The TOC data length value
        /// does not include the TOC data length field itself.This value is not modified when the allocation length is
        /// insufficient to return all of the TOC data available.
        /// </summary>
        private ushort TocDataLength
        {
            get { return BigEndianConverter.ToUInt16(data, 0); }
            set { BigEndianWriter.WriteUInt16(data, 0, value); }
        }

        /// <summary>
        /// The First Track Number field indicates the first track number in the first complete session Table of Contents.
        /// </summary>
        public byte FirstTrackNumber
        {
            get { return data[2]; }
            set { data[2] = value; }
        }

        /// <summary>
        /// The Last Track Number field indicates the last track number in the last complete session Table of Contents 
        /// before the lead-out.
        /// </summary>
        public byte LastTrackNumber
        {
            get { return data[3]; }
            set { data[3] = value; }
        }

        /// <summary>
        /// An MSF bit of zero indicates that the Logical Block Address field
        /// contains a logical block address.An MSF bit of one indicates the Logical Block Address field contains an
        /// MSF address.
        /// </summary>
        /// <param name="msf"></param>
        public FormattedToc(bool msf)
        {
            _msf = msf;
        }

        /// <summary>
        /// Sets TOC Track Descriptor in the Formatted TOC data array.
        /// </summary>
        /// <param name="position">Descriptor position in the data array</param>
        /// <param name="adr">The ADR field gives the type of information encoded in the Q sub-channel of the block where this TOC
        /// entry was found.</param>
        /// <param name="control">The Control Field indicates the attributes, of the track.</param>
        /// <param name="trackNumber">The Track Number field indicates the track number for which the data in the TOC track descriptor is valid.
        /// A track number of AAh indicates that the track descriptor is for the start of the lead-out area.</param>
        /// <param name="address">The Logical Block Address contains the address of the first block with user information for that track 
        /// number as read from the Table of Contents.</param>
        public void SetTocTrackDescriptor(byte position, byte adr, byte control, byte trackNumber, uint address)
        {
            int offset = 4 + position * 8; // 4 descriptor start offset, 8 - size of descriptor (TOC/PMA/ATIP Response Data Format 0000)
            // Reserved
            data[offset + 0] = 0;
            // ADR(7-4) CONTROL(3-0)
            data[offset + 1] = (byte)(adr << 4 | (control & 0xF));
            data[offset + 2] = trackNumber;
            // Reserved
            data[offset + 3] = 0;

            /* Track Start Address */
            if (_msf)
            {
                BigEndianWriter.WriteUInt32(data, offset + 4, MmcHelper.Lba2Msf(address));
            }
            else
            {
                BigEndianWriter.WriteUInt32(data, offset + 4, address);
            }
        }
        
        /// <summary>
        /// Adjusts data length for requested track count.
        /// </summary>
        /// <param name="trackCount"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void UpdateDataLength(int trackCount)
        {
            // 4 bytes of data + trackCount descriptors - TOC data length field itself 
            TocDataLength = (byte)(4 + trackCount * 8 - 2);
        }

        public byte[] GetBytes()
        {
            return data;
        }
    }
}
