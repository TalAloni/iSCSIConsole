using System;
using System.Collections.Generic;
using System.Text;

namespace SCSI
{
    public class ReadDiscInformationParameter
    {
        internal byte[] GetBytes()
        {
            return new byte[]
            {
                0x0,
                0x20, // size
                0xE, // 1110 (complete session | complete disc)
                0x1, // first track
                (1) & 0xff, /* Number of Sessions (Least Significant Byte) */
                (1) & 0xff, /* First Track Number in Last Session (Least Significant Byte) */
                (1) & 0xff, /* Last Track Number in Last Session (Least Significant Byte) */
                0xFC, // 1111 1100
                0x00, // CD-DA or CD-ROM
                (1 >> 8) & 0xff, /* Number of Sessions (Most Significant Byte) */
                (1 >> 8) & 0xff, /* First Track Number in Last Session (Most Significant Byte) */
                (1 >> 8) & 0xff, /* Last Track Number in Last Session (Most Significant Byte) */
                0, 0, 0, 0, /* Disc Identification */
                0, 0, 0, 0, /* Last Session Lead-in Start Address */
                0, 0, 0, 0, /* Last Possible Lead-out Start Address */
                0, 0, 0, 0, 0, 0, 0, 0, /* Disc Bar Code */
                0, /* Disc Application Code */
                0, /* Number of OPC Tables */
            };
        }
    }
}
