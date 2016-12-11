/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;

namespace ISCSIConsole
{
    public partial class Program
    {
        private static FileStream m_logFile;

        public static bool OpenLogFile(string logFilePath)
        {
            try
            {
                // We must avoid using buffered writes, using it will negatively affect the performance and reliability.
                // Note: once the file system write buffer is filled, Windows may delay any (buffer-dependent) pending write operations, which will create a deadlock.
                m_logFile = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read, 0x1000, FileOptions.WriteThrough);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void CloseLogFile()
        {
            if (m_logFile != null)
            {
                lock (m_logFile)
                {
                    m_logFile.Close();
                    m_logFile = null;
                }
            }
        }

        public static void OnLogEntry(object sender, LogEntry entry)
        {
            if (m_logFile != null && entry.Severity != Severity.Trace)
            {
                lock (m_logFile)
                {
                    StreamWriter writer = new StreamWriter(m_logFile);
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ");
                    writer.WriteLine("{0} {1} [{2}] {3}", entry.Severity.ToString().PadRight(12), timestamp, entry.Source, entry.Message);
                    writer.Flush();
                }
            }
        }
    }
}
