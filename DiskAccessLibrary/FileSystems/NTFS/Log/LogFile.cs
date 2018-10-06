/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public partial class LogFile : NTFSFile
    {
        private LogRestartPage m_restartPage;
        private LogRecordPage m_firstTailPage;
        private LogRecordPage m_secondTailPage;

        public LogFile(NTFSVolume volume) : base(volume, MasterFileTable.LogSegmentReference)
        {
        }

        public LogRestartPage ReadRestartPage()
        {
            byte[] pageBytes = ReadData(0, Volume.BytesPerSector);
            uint systemPageSize = LogRestartPage.GetSystemPageSize(pageBytes, 0);
            int bytesToRead = (int)systemPageSize - pageBytes.Length;
            if (bytesToRead > 0)
            {
                byte[] temp = ReadData((ulong)pageBytes.Length, bytesToRead);
                pageBytes = ByteUtils.Concatenate(pageBytes, temp);
            }
            m_restartPage = new LogRestartPage(pageBytes, 0);
            return m_restartPage;
        }

        private int FindClientIndex(string clientName)
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

        public LogRecord ReadCurrentRestartRecord()
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }
            return ReadRecord(m_restartPage.LogRestartArea.CurrentLsn);
        }

        public bool IsLogClean()
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            bool isLogFileClosed = (m_restartPage.LogRestartArea.ClientInUseList == LogRestartArea.NoClients);
            if  (isLogFileClosed)
            {
                // Windows 2000 and earlier will close the log file by setting the ClientInUseList to NoClients when the volume is shutdown cleanly.
                // If the log file is closed than it must be clean.
                return true;
            }
            else if ((m_restartPage.LogRestartArea.Flags & LogRestartFlags.LogFileIsClean) != 0)
            {
                // Windows XP and later will set the clean bit when the volume is shutdown cleanly.
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

        public LogRecord ReadRecord(ulong lsn)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            ulong pageOffsetInFile = LsnToPageOffsetInFile(lsn);
            int recordOffsetInPage = LsnToRecordOffsetInPage(lsn);
            LogRecordPage page = ReadPage(pageOffsetInFile);
            return page.ReadRecord(recordOffsetInPage, m_restartPage.LogRestartArea.LogPageDataOffset);
        }

        private LogRecordPage ReadPage(ulong pageOffset)
        {
            if (m_firstTailPage == null || m_secondTailPage == null)
            {
                m_firstTailPage = ReadPageFromFile(m_restartPage.SystemPageSize * 2);
                m_secondTailPage = ReadPageFromFile(m_restartPage.SystemPageSize * 2 + m_restartPage.LogPageSize);
            }

            LogRecordPage page = null;
            if (pageOffset == m_firstTailPage.LastLsnOrFileOffset)
            {
                page = m_firstTailPage;
            }

            if (pageOffset == m_secondTailPage.LastLsnOrFileOffset)
            {
                if (page == null || m_secondTailPage.LastEndLsn >= m_firstTailPage.LastEndLsn)
                {
                    page = m_secondTailPage;
                }
            }

            if (page == null)
            {
                page = ReadPageFromFile(pageOffset);
            }
            return page;
        }

        private LogRecordPage ReadPageFromFile(ulong pageOffset)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            byte[] pageBytes = ReadData(pageOffset, (int)m_restartPage.LogPageSize);
            return new LogRecordPage(pageBytes, 0, m_restartPage.LogRestartArea.LogPageDataOffset);
        }

        private void WritePage(ulong pageOffset, LogRecordPage page)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            byte[] pageBytes = page.GetBytes((int)m_restartPage.LogPageSize, m_restartPage.LogRestartArea.LogPageDataOffset);
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
    }
}
