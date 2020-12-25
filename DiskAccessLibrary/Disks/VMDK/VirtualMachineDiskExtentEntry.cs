/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using Utilities;

namespace DiskAccessLibrary.VMDK
{
    public class VirtualMachineDiskExtentEntry
    {
        public bool ReadAccess;
        public bool WriteAccess;
        public long SizeInSectors;
        public ExtentType ExtentType;
        public string FileName;
        public Nullable<long> Offset;

        public string GetEntryLine()
        {
            string access;
            if (ReadAccess && WriteAccess)
            {
                access = "RW";
            }
            else if (ReadAccess)
            {
                access = "RDONLY";
            }
            else
            {
                access = "NOACCESS";
            }

            string line = String.Format("{0} {1} {2} \"{3}\"", access, SizeInSectors, ExtentType.ToString().ToUpper(), FileName);
            if (Offset.HasValue)
            {
                line += " " + Offset.Value.ToString();
            }
            return line;
        }

        public static VirtualMachineDiskExtentEntry ParseEntry(string line)
        {
            VirtualMachineDiskExtentEntry entry = new VirtualMachineDiskExtentEntry();
            List<string> parts = QuotedStringUtils.SplitIgnoreQuotedSeparators(line, ' ');
            if (String.Equals(parts[0], "RW", StringComparison.InvariantCultureIgnoreCase))
            {
                entry.WriteAccess = true;
                entry.ReadAccess = true;
            }
            else if (String.Equals(parts[0], "RDONLY", StringComparison.InvariantCultureIgnoreCase))
            {
                entry.ReadAccess = true;
            }
            entry.SizeInSectors = Conversion.ToInt64(parts[1]);
            entry.ExtentType = GetExtentTypeFromString(parts[2]);
            entry.FileName = QuotedStringUtils.Unquote(parts[3]);
            if (parts.Count > 4)
            {
                entry.Offset = Conversion.ToInt64(parts[4]);
            }
            return entry;
        }

        public static ExtentType GetExtentTypeFromString(string extentType)
        {
            switch (extentType.ToUpper())
            {
                case "FLAT":
                    return ExtentType.Flat;
                case "SPARSE":
                    return ExtentType.Sparse;
                case "ZERO":
                    return ExtentType.Zero;
                case "VMFS":
                    return ExtentType.VMFS;
                case "VMFSSPARSE":
                    return ExtentType.VMFSSparse;
                case "VMFSRDM":
                    return ExtentType.VMFSRDM;
                case "VMFSRAW":
                    return ExtentType.VMFSRaw;
                default:
                    return ExtentType.Zero;
            }
        }
    }
}
