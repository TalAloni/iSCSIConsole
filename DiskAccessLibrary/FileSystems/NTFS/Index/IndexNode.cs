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

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public class IndexNode // intermediate node
    {
        public List<IndexNodeEntry> Entries = new List<IndexNodeEntry>();

        public IndexNode(byte[] buffer, int offset)
        {
            int position = offset;

            while (true)
            {
                IndexNodeEntry node = new IndexNodeEntry(buffer, ref position);
                if (node.IsLastEntry && !node.PointsToSubnode)
                {
                    break;
                }
                Entries.Add(node);
                if (node.IsLastEntry)
                {
                    break;
                }
            }
        }
    }
}
