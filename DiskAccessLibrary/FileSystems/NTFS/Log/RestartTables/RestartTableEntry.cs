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
    public abstract class RestartTableEntry
    {
        public const uint RestartEntryAllocated = 0xFFFFFFFF;

        public uint AllocatedOrNextFree;

        public abstract void WriteBytes(byte[] buffer, int offset);

        public abstract int Length
        {
            get;
        }

        public static T ReadEntry<T>(byte[] buffer, int offset, uint majorVersion) where T : RestartTableEntry
        {
            if (typeof(T) == typeof(DirtyPageEntry))
            {
                return (T)(object)new DirtyPageEntry(buffer, offset, majorVersion);
            }
            else if (typeof(T) == typeof(OpenAttributeEntry))
            {
                return (T)(object)new OpenAttributeEntry(buffer, offset, majorVersion);
            }
            else if (typeof(T) == typeof(TransactionEntry))
            {
                return (T)(object)new TransactionEntry(buffer, offset);
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
