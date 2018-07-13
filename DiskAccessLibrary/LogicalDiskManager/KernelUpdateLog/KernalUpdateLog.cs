/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    public class KernelUpdateLog
    {
        List<KernelUpdateLogPage> m_pages = new List<KernelUpdateLogPage>();

        public KernelUpdateLog(List<KernelUpdateLogPage> pages)
        {
            m_pages = pages;
        }

        public void SetLastEntry(DynamicDisk databaseDisk, ulong committedTransactionID, ulong pendingTransactionID)
        {
            SetLastEntry(databaseDisk.Disk, databaseDisk.PrivateHeader, databaseDisk.TOCBlock, committedTransactionID, pendingTransactionID);
        }

        public void SetLastEntry(Disk disk, PrivateHeader privateHeader, TOCBlock tocBlock, ulong committedTransactionID, ulong pendingTransactionID)
        {
            if (m_pages.Count > 0)
            {
                m_pages[0].SetLastEntry(committedTransactionID, pendingTransactionID);
                // Windows kernel stores the last committedTransactionID / pendingTransactionID in memory,
                // and it will overwrite the values we write as soon as dmadmin is started,
                // However, it doesn't seem to cause any issues
                KernelUpdateLogPage.WriteToDisk(disk, privateHeader, tocBlock, m_pages[0]);
            }
            else
            {
                throw new InvalidOperationException("KLog records have not been previously read from disk");
            }
        }

        public static KernelUpdateLog ReadFromDisk(DynamicDisk databaseDisk)
        {
            return ReadFromDisk(databaseDisk.Disk, databaseDisk.PrivateHeader, databaseDisk.TOCBlock);
        }

        public static KernelUpdateLog ReadFromDisk(Disk disk, PrivateHeader privateHeader, TOCBlock tocBlock)
        {
            List<KernelUpdateLogPage> pages = new List<KernelUpdateLogPage>();
            KernelUpdateLogPage firstPage = KernelUpdateLogPage.ReadFromDisk(disk, privateHeader, tocBlock, 0);
            pages.Add(firstPage);
            for (int index = 1; index < firstPage.NumberOfPages; index++)
            {
                KernelUpdateLogPage page = KernelUpdateLogPage.ReadFromDisk(disk, privateHeader, tocBlock, index);
                pages.Add(page);
            }
            return new KernelUpdateLog(pages);
        }

        public List<KernelUpdateLogPage> Pages
        {
            get
            {
                return m_pages;
            }
        }
    }
}
