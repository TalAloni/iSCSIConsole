/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <summary>
    /// ATTRIBUTE_RECORD_HEADER: https://docs.microsoft.com/en-us/windows/desktop/DevNotes/attribute-record-header
    /// </summary>
    /// <remarks>
    /// The maximum NTFS file size is 2^64 bytes, so the number of file clusters can be represented using long.
    /// </remarks>
    public class NonResidentAttributeRecord : AttributeRecord
    {
        public const int HeaderLength = 0x40;

        public long LowestVCN;  // The lowest VCN covered by this attribute record, stored as unsigned, but is within the range of long, see note above.
        public long HighestVCN; // The highest VCN covered by this attribute record, stored as unsigned, but is within the range of long, see note above.
        // ushort mappingPairsOffset;
        public byte CompressionUnit;  // Log of the number of clusters in each unit (not valid if the LowestVCN member is nonzero)
        // 5 reserved bytes
        public ulong AllocatedLength; // An even multiple of the cluster size (not valid if the LowestVCN member is nonzero).
        public ulong FileSize;        // The real size of a file with all of its runs combined (not valid if the LowestVCN member is nonzero).
        public ulong ValidDataLength; // Actual data written so far, (always less than or equal to the file size).
                                      // Data beyond ValidDataLength should be treated as 0 (not valid if the LowestVCN member is nonzero).
        // ulong TotalAllocated;      // Presented for the first file record of a compressed stream.
        private DataRunSequence m_dataRunSequence;

        public NonResidentAttributeRecord(AttributeType attributeType, string name, ushort instance) : base(attributeType, name, false, instance)
        {
            HighestVCN = -1; // This is the value that should be set when the attribute contains no data.
            m_dataRunSequence = new DataRunSequence();
        }

        public NonResidentAttributeRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
            LowestVCN = (long)LittleEndianConverter.ToUInt64(buffer, offset + 0x10);
            HighestVCN = (long)LittleEndianConverter.ToUInt64(buffer, offset + 0x18);
            ushort mappingPairsOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x20);
            CompressionUnit = ByteReader.ReadByte(buffer, offset + 0x22);
            AllocatedLength = LittleEndianConverter.ToUInt64(buffer, offset + 0x28);
            FileSize = LittleEndianConverter.ToUInt64(buffer, offset + 0x30);
            ValidDataLength = LittleEndianConverter.ToUInt64(buffer, offset + 0x38);
            m_dataRunSequence = new DataRunSequence(buffer, offset + mappingPairsOffset, (int)this.RecordLengthOnDisk - mappingPairsOffset);

            if ((HighestVCN - LowestVCN + 1) != m_dataRunSequence.DataClusterCount)
            {
                throw new InvalidDataException("Invalid non-resident attribute record");
            }
        }

        public override byte[] GetBytes()
        {
            int dataRunSequenceLength = m_dataRunSequence.RecordLength;
            ushort mappingPairsOffset = (ushort)(HeaderLength + Name.Length * 2);
            int length = this.RecordLength;
            byte[] buffer = new byte[length];
            WriteHeader(buffer, HeaderLength);

            ushort dataRunsOffset = (ushort)(HeaderLength + Name.Length * 2);

            LittleEndianWriter.WriteInt64(buffer, 0x10, LowestVCN);
            LittleEndianWriter.WriteInt64(buffer, 0x18, HighestVCN);
            LittleEndianWriter.WriteUInt16(buffer, 0x20, mappingPairsOffset);
            ByteWriter.WriteByte(buffer, 0x22, CompressionUnit);
            LittleEndianWriter.WriteUInt64(buffer, 0x28, AllocatedLength);
            LittleEndianWriter.WriteUInt64(buffer, 0x30, FileSize);
            LittleEndianWriter.WriteUInt64(buffer, 0x38, ValidDataLength);
            m_dataRunSequence.WriteBytes(buffer, dataRunsOffset);
            return buffer;
        }

        /// <summary>
        /// This method should only be used for informational purposes.
        /// </summary>
        public KeyValuePairList<long, long> GetClustersInUse()
        {
            long clusterCount = HighestVCN - LowestVCN + 1;
            KeyValuePairList<long, long> sequence = m_dataRunSequence.TranslateToLCN(0, clusterCount);
            return sequence;
        }

        /// <summary>
        /// Each attribute record must be aligned to 8-byte boundary, so RecordLength must be a multiple of 8.
        /// When reading attributes, they may contain additional padding,
        /// so we should use RecordLengthOnDisk to advance the buffer position instead.
        /// </summary>
        public override int RecordLength
        {
            get 
            {
                int dataRunSequenceLength = m_dataRunSequence.RecordLength;
                ushort mappingPairsOffset = (ushort)(HeaderLength + Name.Length * 2);
                int length = (int)(mappingPairsOffset + dataRunSequenceLength);
                // Each record must be aligned to 8-byte boundary
                length = (int)Math.Ceiling((double)length / 8) * 8;
                return length;
            }
        }

        public override ulong DataLength
        {
            get
            {
                return FileSize;
            }
        }

        public DataRunSequence DataRunSequence
        {
            get
            {
                return m_dataRunSequence;
            }
        }
        
        public long DataClusterCount
        {
            get
            {
                return HighestVCN - LowestVCN + 1;
            }
        }

        public static new NonResidentAttributeRecord Create(AttributeType type, string name, ushort instance)
        {
            switch (type)
            {
                case AttributeType.IndexAllocation:
                    return new IndexAllocationRecord(name, instance);
                default:
                    return new NonResidentAttributeRecord(type, name, instance);
            }
        }
    }
}
