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
    public partial class LogFile
    {
        public LfsRecord FindNextRecord(LfsRecord record, int clientIndex)
        {
            do
            {
                ulong nextLsn = CalculateNextLsn(record.ThisLsn, record.Length);
                if (!IsLsnInFile(nextLsn, clientIndex))
                {
                    return null;
                }

                try
                {
                    record = ReadRecord(nextLsn);
                }
                catch
                {
                    return null;
                }

                ushort clientSeqNumber = m_restartPage.LogRestartArea.LogClientArray[clientIndex].SeqNumber;
                if (record.ClientIndex == clientIndex && record.ClientSeqNumber == clientSeqNumber)
                {
                    return record;
                }
            }
            while (true);
        }

        public List<LfsRecord> FindNextRecords(ulong lsn, int clientIndex)
        {
            LfsRecord record = ReadRecord(lsn);
            List<LfsRecord> result = new List<LfsRecord>();
            do
            {
                record = FindNextRecord(record, clientIndex);
                if (record != null)
                {
                    result.Add(record);
                }
            }
            while (record != null);
            return result;
        }

        public bool IsLsnInFile(ulong lsn, int clientIndex)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            return (lsn >= m_restartPage.LogRestartArea.LogClientArray[clientIndex].OldestLsn &&
                    lsn <= m_restartPage.LogRestartArea.CurrentLsn);
        }
    }
}
