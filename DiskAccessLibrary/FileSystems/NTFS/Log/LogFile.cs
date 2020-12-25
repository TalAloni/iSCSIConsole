/* Copyright (C) 2018-2019 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    public partial class LogFile : NTFSFile
    {
        private LfsRestartPage m_restartPage;
        private bool m_isFirstRestartPageTurn;
        private LfsRecordPage m_tailPage;
        private ulong m_tailPageOffsetInFile;
        private bool m_isTailCopyDirty;
        private bool m_isFirstTailPageTurn;
        private List<LfsRecord> m_recordsPendingWrite = new List<LfsRecord>();
        private ushort m_nextUpdateSequenceNumber = (ushort)new Random().Next(UInt16.MaxValue);

        public LogFile(NTFSVolume volume) : base(volume, MasterFileTable.LogSegmentReference)
        {
            // We only throw exception if the volume is not readonly 
            if (!IsLogClean() && !volume.IsReadOnly)
            {
                throw new NotSupportedException("The volume was not dismounted cleanly, the Windows NTFS driver must be used to bring the volume back to a consistent state");
            }
        }

        private LfsRestartPage ReadRestartPage()
        {
            byte[] firstPageBytes = ReadData(0, Volume.BytesPerSector);
            uint systemPageSize = LfsRestartPage.GetSystemPageSize(firstPageBytes, 0);
            int bytesToRead = (int)systemPageSize - firstPageBytes.Length;
            if (bytesToRead > 0)
            {
                byte[] temp = ReadData((ulong)firstPageBytes.Length, bytesToRead);
                firstPageBytes = ByteUtils.Concatenate(firstPageBytes, temp);
            }
            MultiSectorHelper.RevertUsaProtection(firstPageBytes, 0);
            LfsRestartPage firstRestartPage = new LfsRestartPage(firstPageBytes, 0);
            byte[] secondPageBytes = ReadData(systemPageSize, (int)systemPageSize);
            MultiSectorHelper.RevertUsaProtection(secondPageBytes, 0);
            LfsRestartPage secondRestartPage = new LfsRestartPage(secondPageBytes, 0);
            if (secondRestartPage.RestartArea.CurrentLsn > firstRestartPage.RestartArea.CurrentLsn)
            {
                m_restartPage = secondRestartPage;
                m_isFirstRestartPageTurn = true;
            }
            else
            {
                m_restartPage = firstRestartPage;
            }
            return m_restartPage;
        }

        public int FindClientIndex(string clientName)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            for (int index = 0; index < m_restartPage.RestartArea.LogClientArray.Count; index++)
            {
                if (String.Equals(m_restartPage.RestartArea.LogClientArray[index].ClientName, clientName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
            return -1;
        }

        public bool IsClientInUse(int clientIndex)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            int nextClientInUseIndex = m_restartPage.RestartArea.ClientInUseList;
            while (nextClientInUseIndex != LfsRestartArea.NoClient)
            {
                if (nextClientInUseIndex == clientIndex)
                {
                    return true;
                }
                nextClientInUseIndex = m_restartPage.RestartArea.LogClientArray[nextClientInUseIndex].NextClient;
            }

            return false;
        }

        public LfsClientRecord GetClientRecord(int clientIndex)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            return m_restartPage.RestartArea.LogClientArray[clientIndex];
        }

        /// <remarks>
        /// This method should only be called after properly setting the values in a client restart record
        /// </remarks>
        public void WriteRestartPage(bool isClean)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            if (m_recordsPendingWrite.Count > 0)
            {
                bool endOfTransferRecorded;
                FlushRecords(m_recordsPendingWrite, out endOfTransferRecorded);
                m_recordsPendingWrite.Clear();
            }

            if (isClean && m_isTailCopyDirty)
            {
                // Copy the tail page to the correct location in the log file
                WritePage(m_tailPageOffsetInFile, m_tailPage);
            }

            m_restartPage.RestartArea.IsClean = isClean;
            m_restartPage.RestartArea.RevisionNumber++;
            WriteRestartPage(m_restartPage);
        }

        private void WriteRestartPage(LfsRestartPage restartPage)
        {
            byte[] pageBytes = restartPage.GetBytes((int)restartPage.SystemPageSize, true);
            // The NTFS v5.1 driver will always read both restart pages and compare the CurrentLsn to determine which is more recent (even if the CleanDismount flag is set)
            ulong offset = m_isFirstRestartPageTurn ? 0 : restartPage.SystemPageSize;
            WriteData(offset, pageBytes);
            m_isFirstRestartPageTurn = !m_isFirstRestartPageTurn;
        }

        public bool IsLogClean()
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            if (!m_restartPage.RestartArea.IsInUse)
            {
                // If the log file is not in use than it must be clean.
                return true;
            }
            else if (m_restartPage.RestartArea.IsClean)
            {
                // If the clean bit is set than the log file must be clean.
                return true;
            }
            else
            {
                // The volume has not been shutdown cleanly.
                // It's possible that the log is clean if the volume was completely idle for at least five seconds preceding the unclean shutdown.
                // Currently, we skip the analysis and assume that's not the case.
                return false;
            }
        }

        public LfsRecord ReadRecord(ulong lsn)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            ulong pageOffsetInFile = LsnToPageOffsetInFile(lsn);
            int recordOffsetInPage = LsnToRecordOffsetInPage(lsn);
            LfsRecordPage page = ReadPage(pageOffsetInFile);
            if (page == null)
            {
                throw new InvalidDataException("LfsRecord LSN points to an uninitialized page");
            }
            int dataOffset = m_restartPage.RestartArea.LogPageDataOffset;
            LfsRecord record = page.ReadRecord(recordOffsetInPage);
            if (record.ThisLsn != lsn)
            {
                throw new InvalidDataException("LfsRecord LSN does not match expected value");
            }
            if (record.Length < LfsRecord.HeaderLength)
            {
                throw new InvalidDataException("LfsRecord length is invalid");
            }
            if (record.IsMultiPageRecord)
            {
                int recordLength = (int)(LfsRecord.HeaderLength + record.ClientDataLength);
                int bytesRemaining = recordLength - (LfsRecord.HeaderLength + record.Data.Length);
                while (bytesRemaining > 0)
                {
                    pageOffsetInFile += m_restartPage.LogPageSize;
                    if (pageOffsetInFile == m_restartPage.RestartArea.FileSize)
                    {
                        pageOffsetInFile = m_restartPage.SystemPageSize * 2 + m_restartPage.LogPageSize * 2;
                    }
                    page = ReadPage(pageOffsetInFile);
                    int bytesToRead = Math.Min((int)m_restartPage.LogPageSize - dataOffset, bytesRemaining);
                    record.Data = ByteUtils.Concatenate(record.Data, page.ReadBytes(dataOffset, bytesToRead));
                    bytesRemaining -= bytesToRead;
                }
            }
            return record;
        }

        public LfsRecord WriteRecord(int clientIndex, LfsRecordType recordType, ulong clientPreviousLsn, ulong clientUndoNextLsn, uint transactionId, byte[] clientData, bool flushToDisk)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            // It's perfectly valid to clear the CleanDismount flag after writing the transfer.
            // Note that CurrentLsn is used to determine which is restart page recent so we should not update the restart page without incrementing CurrentLsn first.

            ushort clientSeqNumber = m_restartPage.RestartArea.LogClientArray[clientIndex].SeqNumber;

            LfsRecord record = new LfsRecord();
            record.ClientSeqNumber = clientSeqNumber;
            record.ClientIndex = (ushort)clientIndex;
            record.RecordType = recordType;
            record.ClientPreviousLsn = clientPreviousLsn;
            record.ClientUndoNextLsn = clientUndoNextLsn;
            record.TransactionId = transactionId;
            record.ClientDataLength = (uint)clientData.Length;
            record.Data = clientData;
            record.ThisLsn = GetNextLsn();
            m_recordsPendingWrite.Add(record);
            bool endOfTransferRecorded = false;
            if (flushToDisk)
            {
                FlushRecords(m_recordsPendingWrite, out endOfTransferRecorded);
                m_recordsPendingWrite.Clear();
            }

            // Update CurrentLsn / LastLsnDataLength
            m_restartPage.RestartArea.CurrentLsn = record.ThisLsn;
            m_restartPage.RestartArea.LastLsnDataLength = (uint)record.Data.Length;

            // If the client is writing a ClientRestart, the call to write a restart page (when updating the client record) is imminent, so no need to write a restart page now.
            // Note that CurrentLsn is used to determine which restart page is more recent so we should not update the restart page without incrementing CurrentLsn.
            if (recordType != LfsRecordType.ClientRestart && endOfTransferRecorded)
            {
                // When the NTFS v5.1 driver restarts a dirty log file, it does not expect to find more than one transfer after the last flushed LSN.
                // If more than one transfer is encountered, it is treated as a fatal error and the driver will report STATUS_DISK_CORRUPT_ERROR.
                // Updating the CurrentLsn / LastLsnDataLength in the restart area will make the NTFS driver see the next page we write as the only transfer after the flushed LSN.
                WriteRestartPage(false);
            }
            return record;
        }

        /// <param name="endOfTransferRecorded">True if one or more pages has been filled and the next transfer will be seen as a separate IO transfer</param>
        private void FlushRecords(List<LfsRecord> records, out bool endOfTransferRecorded)
        {
            KeyValuePairList<ulong, LfsRecordPage> pagesToWrite = new KeyValuePairList<ulong, LfsRecordPage>();
            int bytesAvailableInPage = (int)m_restartPage.LogPageSize - (int)m_restartPage.RestartArea.LogPageDataOffset;
            LfsRecordPage currentPage = null;
            bool overwriteTailPage = false;

            for (int recordIndex = 0; recordIndex < records.Count; recordIndex++)
            {
                LfsRecord record = records[recordIndex];
                ulong pageOffsetInFile = LsnToPageOffsetInFile(record.ThisLsn);
                int recordOffsetInPage = LsnToRecordOffsetInPage(record.ThisLsn);
                bool initializeNewPage = (recordOffsetInPage == m_restartPage.RestartArea.LogPageDataOffset);
                if (initializeNewPage) // We write the record at the beginning of a new page, we must initialize the page
                {
                    currentPage = new LfsRecordPage((int)m_restartPage.LogPageSize, m_restartPage.RestartArea.LogPageDataOffset);
                    currentPage.LastLsnOrFileOffset = 0;
                    currentPage.LastEndLsn = 0;
                    currentPage.NextRecordOffset = m_restartPage.RestartArea.LogPageDataOffset;
                    currentPage.UpdateSequenceNumber = m_nextUpdateSequenceNumber; // There is no rule governing the value of the USN
                    m_nextUpdateSequenceNumber++;

                    pagesToWrite.Add(pageOffsetInFile, currentPage);
                }
                else
                {
                    if (recordIndex == 0) // currentPage == null
                    {
                        if (m_tailPage == null)
                        {
                            m_tailPage = ReadPage(pageOffsetInFile); ;
                            m_tailPageOffsetInFile = pageOffsetInFile;
                        }
                        else if (m_tailPageOffsetInFile != pageOffsetInFile)
                        {
                            throw new InvalidOperationException("LFS log records must be written sequentially");
                        }
                        currentPage = m_tailPage;
                        overwriteTailPage = true;
                        pagesToWrite.Add(pageOffsetInFile, currentPage);
                    }
                    // currentPage cannot be null when !initializeNewPage && recordIndex > 0
                    System.Diagnostics.Debug.Assert(currentPage != null);
                }
                int bytesAvailableInFirstPage = (int)m_restartPage.LogPageSize - recordOffsetInPage;
                int bytesToWrite = Math.Min(record.Length, bytesAvailableInFirstPage);
                int bytesRemaining = record.Length - bytesToWrite;
                record.IsMultiPageRecord = (bytesRemaining > 0);
                byte[] recordBytes = record.GetBytes();
                currentPage.WriteBytes(recordOffsetInPage, recordBytes, bytesToWrite);

                currentPage.LastLsnOrFileOffset = record.ThisLsn;
                if (!record.IsMultiPageRecord)
                {
                    currentPage.NextRecordOffset = (ushort)(recordOffsetInPage + bytesToWrite);
                    currentPage.LastEndLsn = record.ThisLsn;
                    currentPage.HasRecordEnd = true;
                }

                bool reusePage = !record.IsMultiPageRecord && (bytesAvailableInFirstPage >= m_restartPage.RestartArea.RecordHeaderLength);
                if (!reusePage)
                {
                    currentPage = null;
                }

                while (bytesRemaining > 0)
                {
                    pageOffsetInFile += m_restartPage.LogPageSize;
                    if (pageOffsetInFile == m_restartPage.RestartArea.FileSize)
                    {
                        pageOffsetInFile = m_restartPage.SystemPageSize * 2 + m_restartPage.LogPageSize * 2;
                    }

                    bytesToWrite = Math.Min(bytesRemaining, bytesAvailableInPage);
                    LfsRecordPage nextPage = new LfsRecordPage((int)m_restartPage.LogPageSize, m_restartPage.RestartArea.LogPageDataOffset);
                    Array.Copy(recordBytes, recordBytes.Length - bytesRemaining, nextPage.Data, 0, bytesToWrite);
                    bytesRemaining -= bytesToWrite;

                    nextPage.LastLsnOrFileOffset = record.ThisLsn;
                    if (bytesRemaining == 0)
                    {
                        nextPage.LastEndLsn = record.ThisLsn;
                        nextPage.NextRecordOffset = (ushort)(m_restartPage.RestartArea.LogPageDataOffset + bytesToWrite);
                        nextPage.Flags |= LfsRecordPageFlags.RecordEnd;
                        int bytesAvailableInLastPage = (int)m_restartPage.LogPageSize - ((int)m_restartPage.RestartArea.LogPageDataOffset + bytesToWrite);
                        bool reuseLastPage = (bytesAvailableInLastPage >= m_restartPage.RestartArea.RecordHeaderLength);
                        if (reuseLastPage)
                        {
                            currentPage = nextPage;
                        }
                    }
                    nextPage.UpdateSequenceNumber = m_nextUpdateSequenceNumber; // There is no rule governing the value of the USN
                    m_nextUpdateSequenceNumber++;
                    pagesToWrite.Add(pageOffsetInFile, nextPage);
                }
            }

            // The tail copy is used to avoid losing both the records from the previous IO transfer and the current one if the page becomes corrupted due to a power failue.
            endOfTransferRecorded = (pagesToWrite.Count > 1 || currentPage == null);
            if (!endOfTransferRecorded)
            {
                ulong pageOffsetInFile = pagesToWrite[0].Key;
                if (m_isTailCopyDirty || !overwriteTailPage)
                {
                    WritePage(pageOffsetInFile, currentPage);
                    m_isTailCopyDirty = false;
                }
                else
                {
                    WriteTailCopy(pageOffsetInFile, currentPage); ;
                    m_isTailCopyDirty = true;
                }
            }
            else
            {
                for (int pageIndex = 0; pageIndex < pagesToWrite.Count; pageIndex++)
                {
                    ulong pageOffsetInFile = pagesToWrite[pageIndex].Key;
                    LfsRecordPage page = pagesToWrite[pageIndex].Value;
                    page.PagePosition = (ushort)(pageIndex + 1);
                    page.PageCount = (ushort)pagesToWrite.Count;

                    if (pageIndex == 0 && !m_isTailCopyDirty && overwriteTailPage)
                    {
                        // We are about to overwrite a page from an older transfer that does not have a tail copy, create a tail copy
                        WriteTailCopy(pageOffsetInFile, page);
                    }

                    WritePage(pageOffsetInFile, page);
                }
            }

            if (currentPage == null)
            {
                m_tailPage = null;
                m_isTailCopyDirty = false;
            }
            else
            {
                m_tailPage = currentPage;
                m_tailPageOffsetInFile = pagesToWrite[pagesToWrite.Count - 1].Key;
            }
        }

        /// <summary>
        /// This method will repair the log file by copying the tail copies back to their correct location.
        /// If necessary, the restart area will be updated to reflect CurrentLsn and LastLsnDataLength.
        /// </summary>
        /// <remarks>This method will only repair the log file, further actions are needed to bring the volume back to a consistent state.</remarks>
        private void RepairLogFile()
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            // Note: this implementation is not as exhaustive as it should be.
            LfsRecordPage firstTailPage = null;
            LfsRecordPage secondTailPage = null;
            try
            {
                firstTailPage = ReadPageFromFile(m_restartPage.SystemPageSize * 2);
            }
            catch (InvalidDataException)
            {
            }

            try
            {
                secondTailPage = ReadPageFromFile(m_restartPage.SystemPageSize * 2 + m_restartPage.LogPageSize);
            }
            catch (InvalidDataException)
            {
            }

            // Find the most recent tail copy
            LfsRecordPage tailPage = null;
            if (firstTailPage != null)
            {
                tailPage = firstTailPage;
            }

            if (tailPage == null || (secondTailPage != null && secondTailPage.LastEndLsn > firstTailPage.LastEndLsn))
            {
                tailPage = secondTailPage;
            }

            if (tailPage != null)
            {
                LfsRecordPage page = null;
                try
                {
                    page = ReadPageFromFile(tailPage.LastLsnOrFileOffset);
                }
                catch (InvalidDataException)
                {
                }

                if (page == null || tailPage.LastEndLsn > page.LastLsnOrFileOffset)
                {
                    ulong pageOffsetInFile = tailPage.LastLsnOrFileOffset;
                    tailPage.LastLsnOrFileOffset = tailPage.LastEndLsn;
                    WritePage(pageOffsetInFile, tailPage);

                    if (tailPage.LastEndLsn > m_restartPage.RestartArea.CurrentLsn)
                    {
                        m_restartPage.RestartArea.CurrentLsn = tailPage.LastEndLsn;
                        int recordOffsetInPage = LsnToRecordOffsetInPage(tailPage.LastEndLsn);
                        LfsRecord record = tailPage.ReadRecord(recordOffsetInPage);
                        m_restartPage.RestartArea.LastLsnDataLength = (uint)record.Data.Length;
                        WriteRestartPage(m_restartPage);
                    }
                }
            }
        }

        private LfsRecordPage ReadPage(ulong pageOffset)
        {
            if (m_tailPage != null && pageOffset == m_tailPageOffsetInFile)
            {
                return m_tailPage;
            }
            else
            {
                return ReadPageFromFile(pageOffset);
            }
        }

        private LfsRecordPage ReadPageFromFile(ulong pageOffset)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            byte[] pageBytes = ReadData(pageOffset, (int)m_restartPage.LogPageSize);
            uint pageSignature = LittleEndianConverter.ToUInt32(pageBytes, 0);
            if (pageSignature == LfsRecordPage.UninitializedPageSignature)
            {
                return null;
            }
            MultiSectorHelper.RevertUsaProtection(pageBytes, 0);
            return new LfsRecordPage(pageBytes, m_restartPage.RestartArea.LogPageDataOffset);
        }

        private void WritePage(ulong pageOffset, LfsRecordPage page)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            byte[] pageBytes = page.GetBytes((int)m_restartPage.LogPageSize, true);
            WriteData(pageOffset, pageBytes);
        }

        private void WriteTailCopy(ulong pageOffset, LfsRecordPage page)
        {
            ulong firstTailPageOffset = m_restartPage.SystemPageSize * 2;
            ulong secondTailPageOffset = m_restartPage.SystemPageSize * 2 + m_restartPage.LogPageSize;
            ulong tailPageOffset = m_isFirstTailPageTurn ? firstTailPageOffset : secondTailPageOffset;
            m_isFirstTailPageTurn = !m_isFirstTailPageTurn;

            ulong lastLsn = page.LastLsnOrFileOffset;
            page.LastLsnOrFileOffset = pageOffset;
            WritePage(tailPageOffset, page);
            // Restore the page to its original state
            page.LastLsnOrFileOffset = lastLsn;
        }

        private ulong LsnToPageOffsetInFile(ulong lsn)
        {
            int seqNumberBits = (int)m_restartPage.RestartArea.SeqNumberBits;
            ulong fileOffset = (lsn << seqNumberBits) >> (seqNumberBits - 3);
            return fileOffset & ~(m_restartPage.LogPageSize - 1);
        }

        private int LsnToRecordOffsetInPage(ulong lsn)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            return (int)((lsn << 3) & (m_restartPage.LogPageSize - 1));
        }

        private ulong GetNextLsn()
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            ulong currentLsn = m_restartPage.RestartArea.CurrentLsn;
            if (currentLsn == 0) // Freshly formatted disk
            {
                int bytesToSkip = (int)m_restartPage.SystemPageSize * 2 + (int)m_restartPage.LogPageSize * 2 + m_restartPage.RestartArea.LogPageDataOffset;
                return (uint)bytesToSkip >> 3;
            }
            else
            {
                int currentLsnRecordLength = (int)(m_restartPage.RestartArea.RecordHeaderLength + m_restartPage.RestartArea.LastLsnDataLength);
                return CalculateNextLsn(currentLsn, currentLsnRecordLength);
            }
        }

        private ulong CalculateNextLsn(ulong lsn, int recordLength)
        {
            int recordOffsetInPage = LsnToRecordOffsetInPage(lsn);
            int bytesToSkip = recordLength;
            int nextRecordOffsetInPage = recordOffsetInPage + recordLength;
            if (nextRecordOffsetInPage >= m_restartPage.LogPageSize)
            {
                int recordBytesInFirstPage = (int)m_restartPage.LogPageSize - recordOffsetInPage;
                int bytesRemaining = recordLength - recordBytesInFirstPage;
                int bytesAvailableInPage = (int)m_restartPage.LogPageSize - (int)m_restartPage.RestartArea.LogPageDataOffset;
                int middlePageCount = bytesRemaining / bytesAvailableInPage;
                int recordBytesInLastPage = bytesRemaining % bytesAvailableInPage;
                bytesToSkip = recordBytesInFirstPage + middlePageCount * (int)m_restartPage.LogPageSize + m_restartPage.RestartArea.LogPageDataOffset + recordBytesInLastPage;
                nextRecordOffsetInPage = (recordOffsetInPage + bytesToSkip) % (int)m_restartPage.LogPageSize;
            }

            int bytesRemainingInPage = (int)m_restartPage.LogPageSize - nextRecordOffsetInPage;
            if (bytesRemainingInPage < m_restartPage.RestartArea.RecordHeaderLength)
            {
                bytesToSkip += bytesRemainingInPage + m_restartPage.RestartArea.LogPageDataOffset;
            }

            ulong pageOffsetInFile = LsnToPageOffsetInFile(lsn);
            if (pageOffsetInFile + (uint)recordOffsetInPage + (uint)bytesToSkip >= m_restartPage.RestartArea.FileSize)
            {
                // We skip the gap of LSNs that do not map to a valid file offset
                int fileSizeBits = m_restartPage.RestartArea.FileSizeBits;
                bytesToSkip += (int)Math.Pow(2, fileSizeBits) - (int)m_restartPage.RestartArea.FileSize;
                // We skip the two restart pages and the two tail pages
                bytesToSkip += (int)m_restartPage.SystemPageSize * 2 + (int)m_restartPage.LogPageSize * 2;
            }

            return lsn + ((uint)bytesToSkip >> 3);
        }
    }
}
