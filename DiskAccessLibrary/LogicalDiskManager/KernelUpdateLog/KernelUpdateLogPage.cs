/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public class KernelUpdateLogEntry
    {
        public KernelUpdateLogEntryStatus Status;
        public ulong CommittedTransactionID;
        public ulong PendingTransactionID;
        public uint RecoverySequenceNumber; // DMDiag calls this: recover_seqno
    }

    /// <summary>
    /// Each KLOG page is 512 bytes.
    /// Note: a sector can contain more than one KLOG page.
    /// </summary>
    public class KernelUpdateLogPage
    {
        public const int Length = 512;

        public string Signature = "KLOG"; // KLOG
        public ulong UnknownTransactionID; // Updates occasionally, gets the value of the latest PendingTransactionID,  all the KLOG blocks have the same value here
        // SequenceNumber: Learned from observation, not incremented on every write, but always incremented by 1.
        // the sequence is log-wide (not per page), however, when updating multiple pages at once, they can share the same sequence number.
        public uint SequenceNumber;
        public uint NumberOfPages;
        public uint PageIndex; // KLOG page index

        private List<KernelUpdateLogEntry> m_logEntries = new List<KernelUpdateLogEntry>(); // PendingTransactionID, CommitTransactionID

        public KernelUpdateLogPage(byte[] buffer)
        {
            Signature = ByteReader.ReadAnsiString(buffer, 0x00, 4);
            UnknownTransactionID = BigEndianConverter.ToUInt64(buffer, 0x04);
            SequenceNumber = BigEndianConverter.ToUInt32(buffer, 0x0C);
            NumberOfPages = BigEndianConverter.ToUInt32(buffer, 0x10);
            PageIndex = BigEndianConverter.ToUInt32(buffer, 0x14);
            
            // Note: for the first KLog, the most recent entry is at the top
            m_logEntries.Clear();

            int offset = 0x18;
            while (offset < buffer.Length - 24) // room of one more entry
            {
                KernelUpdateLogEntryStatus status = (KernelUpdateLogEntryStatus)buffer[offset];
                offset += 1;
                if (status != KernelUpdateLogEntryStatus.NotExist)
                {
                    KernelUpdateLogEntry entry = new KernelUpdateLogEntry();
                    entry.Status = status;
                    entry.CommittedTransactionID = BigEndianConverter.ToUInt64(buffer, offset);
                    offset += 8;
                    entry.PendingTransactionID = BigEndianConverter.ToUInt64(buffer, offset);
                    offset += 8;
                    entry.RecoverySequenceNumber = BigEndianConverter.ToUInt32(buffer, offset);
                    offset += 4;
                    m_logEntries.Add(entry);
                    offset += 3; // padding to align to 4-byte boundary
                }
                else
                {
                    break;
                }
            }
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Length];
            ByteWriter.WriteAnsiString(buffer, 0, Signature, 4);
            BigEndianWriter.WriteUInt64(buffer, 0x04, UnknownTransactionID);
            BigEndianWriter.WriteUInt32(buffer, 0x0C, SequenceNumber);
            BigEndianWriter.WriteUInt32(buffer, 0x10, NumberOfPages);
            BigEndianWriter.WriteUInt32(buffer, 0x14, PageIndex);

            int offset = 0x18;
            foreach (KernelUpdateLogEntry entry in m_logEntries)
            {
                buffer[offset] = (byte)entry.Status;
                offset += 1;
                BigEndianWriter.WriteUInt64(buffer, offset, entry.CommittedTransactionID);
                offset += 8;
                BigEndianWriter.WriteUInt64(buffer, offset, entry.PendingTransactionID);
                offset += 8;
                BigEndianWriter.WriteUInt32(buffer, offset, entry.RecoverySequenceNumber);
                offset += 4;
                offset += 3; // padding to align to 4-byte boundary
            }

            return buffer;
        }

        public List<KernelUpdateLogEntry> LogEntries
        {
            get
            {
                return m_logEntries;
            }
        }

        public void SetLastEntry(ulong committedTransactionID, ulong pendingTransactionID)
        { 
            if (m_logEntries.Count > 0)
            {
                m_logEntries.RemoveAt(m_logEntries.Count - 1);
            }
            KernelUpdateLogEntry entry = new KernelUpdateLogEntry();
            entry.Status = KernelUpdateLogEntryStatus.Commit;
            entry.CommittedTransactionID = committedTransactionID;
            entry.PendingTransactionID = pendingTransactionID;
            m_logEntries.Add(entry);
        }

        public static KernelUpdateLogPage ReadFromDisk(Disk disk, PrivateHeader privateHeader, TOCBlock tocBlock, int pageIndex)
        {
            ulong sectorIndex = privateHeader.PrivateRegionStartLBA + tocBlock.LogStart + (uint)(pageIndex * Length / disk.BytesPerSector);
            int pageOffset = (pageIndex * Length) % disk.BytesPerSector;
            byte[] sector = disk.ReadSector((long)sectorIndex);
            if (pageOffset > 0)
            {
                sector = ByteReader.ReadBytes(sector, pageOffset, Length);
            }
            KernelUpdateLogPage result = new KernelUpdateLogPage(sector);
            return result;
        }

        public static void WriteToDisk(Disk disk, PrivateHeader privateHeader, TOCBlock tocBlock, KernelUpdateLogPage page)
        {
            ulong sectorIndex = privateHeader.PrivateRegionStartLBA + tocBlock.LogStart + (uint)(page.PageIndex * Length / disk.BytesPerSector);
            int pageOffset = ((int)page.PageIndex * Length) % disk.BytesPerSector;
            byte[] pageBytes = page.GetBytes();
            if (disk.BytesPerSector > Length)
            {
                byte[] sectorBytes = disk.ReadSector((long)sectorIndex);
                ByteWriter.WriteBytes(sectorBytes, pageOffset, pageBytes);
                disk.WriteSectors((long)sectorIndex, sectorBytes);
            }
            else
            {
                disk.WriteSectors((long)sectorIndex, pageBytes);
            }
        }
    }
}
