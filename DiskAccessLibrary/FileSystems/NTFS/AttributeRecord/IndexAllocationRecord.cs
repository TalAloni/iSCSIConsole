/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    /// <remarks>
    /// IndexAllocation attribute is always non-resident.
    /// </remarks>
    public class IndexAllocationRecord : NonResidentAttributeRecord
    {
        public IndexAllocationRecord(string name, ushort instance) : base(AttributeType.IndexAllocation, name, instance)
        {
        }

        public IndexAllocationRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
        }
    }
}
