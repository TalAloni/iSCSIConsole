/* Copyright (C) 2012-2024 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;

namespace ISCSI.Logging
{
    public class LogEntry : EventArgs
    {
        public DateTime Time;
        public Severity Severity;
        public string Source;
        public string Message;

        public LogEntry(DateTime time, Severity severity, string source, string message)
        {
            Time = time;
            Severity = severity;
            Source = source;
            Message = message;
        }
    }
}
