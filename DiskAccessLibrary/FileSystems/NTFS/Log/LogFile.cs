/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
        private bool m_isFirstTailPageTurn;
        private ushort m_nextUpdateSequenceNumber = (ushort)new Random().Next(UInt16.MaxValue);

        public LogFile(NTFSVolume volume) : base(volume, MasterFileTable.LogSegmentReference)
        {
            if (!IsLogClean())
            {
                throw new NotSupportedException("The volume was not dismounted cleanly, the Windows NTFS driver must be used to bring the volume back to a consistent state");
            }
        }

        private LfsRestartPage ReadRestartPage()
        {
            byte[] pageBytes = ReadData(0, Volume.BytesPerSector);
            uint systemPageSize = LfsRestartPage.GetSystemPageSize(pageBytes, 0);
            int bytesToRead = (int)systemPageSize - pageBytes.Length;
            if (bytesToRead > 0)
            {
                byte[] temp = ReadData((ulong)pageBytes.Length, bytesToRead);
                pageBytes = ByteUtils.Concatenate(pageBytes, temp);
            }
            MultiSectorHelper.RevertUsaProtection(pageBytes, 0);
            m_restartPage = new LfsRestartPage(pageBytes, 0);
            return m_restartPage;
        }

        public int FindClientIndex(string clientName)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            for (int index = 0; index < m_restartPage.LogRestartArea.LogClientArray.Count; index++)
            {
                if (String.Equals(m_restartPage.LogRestartArea.LogClientArray[index].ClientName, clientName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
            return -1;
        }

        public LfsClientRecord GetClientRecord(int clientIndex)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            return m_restartPage.LogRestartArea.LogClientArray[clientIndex];
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

            m_restartPage.LogRestartArea.IsClean = isClean;
            m_restartPage.LogRestartArea.RevisionNumber++;
            WriteRestartPage(m_restartPage);
        }

        private void WriteRestartPage(LfsRestartPage restartPage)
        {
            byte[] pageBytes = restartPage.GetBytes((int)restartPage.SystemPageSize, true);
            WriteData(0, pageBytes);
            WriteData(restartPage.SystemPageSize, pageBytes);
        }

        public bool IsLogClean()
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            if (!m_restartPage.LogRestartArea.IsInUse)
            {
                // If the log file is not in use than it must be clean.
                return true;
            }
            else if (m_restartPage.LogRestartArea.IsClean)
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
            int dataOffset = m_restartPage.LogRestartArea.LogPageDataOffset;
            LfsRecord record = page.ReadRecord(recordOffsetInPage);
            if (record.ThisLsn != lsn)
            {
                throw new InvalidDataException("LogRecord Lsn does not match expected value");
            }
            if (record.Length < LfsRecord.HeaderLength)
            {
                throw new InvalidDataException("LogRecord length is invalid");
            }
            if (record.IsMultiPageRecord)
            {
                int recordLength = (int)(LfsRecord.HeaderLength + record.ClientDataLength);
                int bytesRemaining = recordLength - (LfsRecord.HeaderLength + record.Data.Length);
                while (bytesRemaining > 0)
                {
                    pageOffsetInFile += m_restartPage.LogPageSize;
                    if (pageOffsetInFile == m_restartPage.LogRestartArea.FileSize)
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

        public LfsRecord WriteRecord(int clientIndex, LfsRecordType recordType, ulong clientPreviousLsn, ulong clientUndoNextLsn, uint transactionId, byte[] clientData)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            if (m_restartPage.LogRestartArea.IsClean)
            {
                // We should clear the CleanDismount flag before making changes to the log file
                WriteRestartPage(false);
            }

            ushort clientSeqNumber = m_restartPage.LogRestartArea.LogClientArray[clientIndex].SeqNumber;

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
            WriteRecord(record);

            // Update CurrentLsn / LastLsnDataLength
            m_restartPage.LogRestartArea.CurrentLsn = record.ThisLsn;
            m_restartPage.LogRestartArea.LastLsnDataLength = (uint)record.Data.Length;
            if (record.IsMultiPageRecord)
            {
                // We can optimize by only writing the restart page if we already had one transfer after the last flushed LSN.
                WriteRestartPage(false);
            }
            return record;
        }

        private void WriteRecord(LfsRecord record)
        {
            ulong pageOffsetInFile = LsnToPageOffsetInFile(record.ThisLsn);
            int recordOffsetInPage = LsnToRecordOffsetInPage(record.ThisLsn);
            bool initializeNewPage = (recordOffsetInPage == m_restartPage.LogRestartArea.LogPageDataOffset);
            LfsRecordPage page;
            if (initializeNewPage) // We write the record at the beginning of a new page, we must initialize the page
            {
                // When the NTFS v5.1 driver restarts a dirty log file, it does not expect to find more than one transfer after the last flushed LSN.
                // If more than one transfer is encountered, it is treated as a fatal error and the driver will report STATUS_DISK_CORRUPT_ERROR.
                // Updating the CurrentLsn / LastLsnDataLength in the restart area will make the NTFS driver see the new page as the only transfer after the flushed LCN.
                WriteRestartPage(false);

                page = new LfsRecordPage((int)m_restartPage.LogPageSize, m_restartPage.LogRestartArea.LogPageDataOffset);
                page.LastLsnOrFileOffset = 0;
                page.LastEndLsn = 0;
                page.NextRecordOffset = m_restartPage.LogRestartArea.LogPageDataOffset;
                page.UpdateSequenceNumber = m_nextUpdateSequenceNumber; // There is no rule governing the value of the USN
                m_nextUpdateSequenceNumber++;
            }
            else
            {
                page = ReadPage(pageOffsetInFile);
            }
            int bytesAvailableInFirstPage = (int)m_restartPage.LogPageSize - recordOffsetInPage;
            int bytesToWrite = Math.Min(record.Length, bytesAvailableInFirstPage);
            int bytesRemaining = record.Length - bytesToWrite;
            record.IsMultiPageRecord = (bytesRemaining > 0);
            byte[] recordBytes = record.GetBytes();
            page.WriteBytes(recordOffsetInPage, recordBytes, bytesToWrite);
            int bytesAvailableInPage = (int)m_restartPage.LogPageSize - (int)m_restartPage.LogRestartArea.LogPageDataOffset;
            int pageCount = 1 + (int)Math.Ceiling((double)bytesRemaining / bytesAvailableInPage);
            page.PageCount = (ushort)pageCount;
            page.PagePosition = 1;
            if (!record.IsMultiPageRecord && (recordOffsetInPage + bytesToWrite > page.NextRecordOffset))
            {
                page.NextRecordOffset = (ushort)(recordOffsetInPage + bytesToWrite);
            }

            ulong firstTailPageOffset = m_restartPage.SystemPageSize * 2;
            ulong secondTailPageOffset = m_restartPage.SystemPageSize * 2 + m_restartPage.LogPageSize;
            ulong tailPageOffset = m_isFirstTailPageTurn ? firstTailPageOffset : secondTailPageOffset;
            m_isFirstTailPageTurn = !m_isFirstTailPageTurn;
            if (record.ThisLsn > page.LastLsnOrFileOffset)
            {
                page.LastLsnOrFileOffset = record.ThisLsn;
            }

            if (!record.IsMultiPageRecord && record.ThisLsn > page.LastEndLsn)
            {
                page.LastEndLsn = record.ThisLsn;
                page.HasRecordEnd = true;
            }
            // Write tail copy
            ulong lastPageLsn = page.LastLsnOrFileOffset;
            page.LastLsnOrFileOffset = pageOffsetInFile;
            WritePage(tailPageOffset, page);
            page.LastLsnOrFileOffset = lastPageLsn;
            // Write page
            WritePage(pageOffsetInFile, page);

            int pagePosition = 2;
            while (bytesRemaining > 0)
            {
                pageOffsetInFile += m_restartPage.LogPageSize;
                if (pageOffsetInFile == m_restartPage.LogRestartArea.FileSize)
                {
                    pageOffsetInFile = m_restartPage.SystemPageSize * 2 + m_restartPage.LogPageSize * 2;
                }

                bytesToWrite = Math.Min(bytesRemaining, bytesAvailableInPage);
                LfsRecordPage nextPage = new LfsRecordPage((int)m_restartPage.LogPageSize, m_restartPage.LogRestartArea.LogPageDataOffset);
                Array.Copy(recordBytes, recordBytes.Length - bytesRemaining, nextPage.Data, 0, bytesToWrite);
                bytesRemaining -= bytesToWrite;

                nextPage.LastLsnOrFileOffset = record.ThisLsn;
                if (bytesRemaining == 0)
                {
                    nextPage.LastEndLsn = record.ThisLsn;
                    nextPage.Flags |= LfsRecordPageFlags.RecordEnd;
                }
                nextPage.PageCount = (ushort)pageCount;
                nextPage.PagePosition = (ushort)pagePosition;
                nextPage.UpdateSequenceNumber = m_nextUpdateSequenceNumber; // There is no rule governing the value of the USN
                m_nextUpdateSequenceNumber++;
                WritePage(pageOffsetInFile, nextPage);
                pagePosition++;
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

                    if (tailPage.LastEndLsn > m_restartPage.LogRestartArea.CurrentLsn)
                    {
                        m_restartPage.LogRestartArea.CurrentLsn = tailPage.LastEndLsn;
                        int recordOffsetInPage = LsnToRecordOffsetInPage(tailPage.LastEndLsn);
                        LfsRecord record = tailPage.ReadRecord(recordOffsetInPage);
                        m_restartPage.LogRestartArea.LastLsnDataLength = (uint)record.Data.Length;
                        WriteRestartPage(m_restartPage);
                    }
                }
            }
        }

        // Placeholder method that will implement caching in the future
        private LfsRecordPage ReadPage(ulong pageOffset)
        {
            return ReadPageFromFile(pageOffset);
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
            return new LfsRecordPage(pageBytes, m_restartPage.LogRestartArea.LogPageDataOffset);
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

        private ulong LsnToPageOffsetInFile(ulong lsn)
        {
            int seqNumberBits = (int)m_restartPage.LogRestartArea.SeqNumberBits;
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

            ulong currentLsn = m_restartPage.LogRestartArea.CurrentLsn;
            int currentLsnRecordLength = (int)(m_restartPage.LogRestartArea.RecordHeaderLength + m_restartPage.LogRestartArea.LastLsnDataLength);
            return CalculateNextLsn(currentLsn, currentLsnRecordLength);
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
                int bytesAvailableInPage = (int)m_restartPage.LogPageSize - (int)m_restartPage.LogRestartArea.LogPageDataOffset;
                int middlePageCount = bytesRemaining / bytesAvailableInPage;
                int recordBytesInLastPage = bytesRemaining % bytesAvailableInPage;
                bytesToSkip = recordBytesInFirstPage + middlePageCount * (int)m_restartPage.LogPageSize + m_restartPage.LogRestartArea.LogPageDataOffset + recordBytesInLastPage;
                nextRecordOffsetInPage = (recordOffsetInPage + bytesToSkip) % (int)m_restartPage.LogPageSize;
            }

            int bytesRemainingInPage = (int)m_restartPage.LogPageSize - nextRecordOffsetInPage;
            if (bytesRemainingInPage < m_restartPage.LogRestartArea.RecordHeaderLength)
            {
                bytesToSkip += bytesRemainingInPage + m_restartPage.LogRestartArea.LogPageDataOffset;
            }

            ulong pageOffsetInFile = LsnToPageOffsetInFile(lsn);
            if (pageOffsetInFile + (uint)recordOffsetInPage + (uint)bytesToSkip >= m_restartPage.LogRestartArea.FileSize)
            {
                // We skip the gap of LSNs that do not map to a valid file offset
                int fileSizeBits = m_restartPage.LogRestartArea.FileSizeBits;
                bytesToSkip += (int)Math.Pow(2, fileSizeBits) - (int)m_restartPage.LogRestartArea.FileSize;
                // We skip the two restart pages and the two tail pages
                bytesToSkip += (int)m_restartPage.SystemPageSize * 2 + (int)m_restartPage.LogPageSize * 2;
            }

            return lsn + ((uint)bytesToSkip >> 3);
        }
    }
}
