/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public class CollationHelper
    {
        public static int Compare(byte[] key1, byte[] key2, CollationRule collationRule)
        {
            switch(collationRule)
            {
                case CollationRule.Filename:
                    {
                        string str1 = FileNameRecord.ReadFileName(key1, 0);
                        string str2 = FileNameRecord.ReadFileName(key2, 0);
                        return String.Compare(str1, str2, StringComparison.OrdinalIgnoreCase);
                    }
                case CollationRule.UnicodeString:
                    {
                        string str1 = Encoding.Unicode.GetString(key1);
                        string str2 = Encoding.Unicode.GetString(key2);
                        return String.Compare(str1, str2, StringComparison.OrdinalIgnoreCase);
                    }
                default:
                    throw new NotImplementedException();
            }
        }

        public static int FindIndexInParentNode(List<IndexEntry> entries, byte[] key, CollationRule collationRule)
        {
            if (entries.Count == 0)
            {
                throw new ArgumentException("Parent Index Record must contain at least 1 entry");
            }

            if (entries.Count == 1)
            {
                // The root can contain a single entry pointing to a leaf record
                return 0;
            }

            int lowerIndex = 0;
            int upperIndex = entries.Count - 2;
            int comparisonResult;
            while (lowerIndex < upperIndex)
            {
                int middleIndex = (lowerIndex + upperIndex) / 2;
                IndexEntry middle = entries[middleIndex];
                comparisonResult = Compare(middle.Key, key, collationRule);
                if (comparisonResult == 0)
                {
                    return middleIndex;
                }
                else if (comparisonResult > 0) // middle > key
                {
                    upperIndex = middleIndex - 1;
                }
                else // middle < key
                {
                    lowerIndex = middleIndex + 1;
                }
            }

            // At this point any entry following 'middle' is greater than 'key',
            // and any entry preceding 'middle' is lesser than 'key'.
            // So we either put 'key' before or after 'middle'.
            comparisonResult = Compare(entries[lowerIndex].Key, key, collationRule);
            if (comparisonResult < 0) // middle < key
            {
                return lowerIndex + 1;
            }
            else
            {
                return lowerIndex;
            }
        }

        public static int FindIndexInLeafNode(List<IndexEntry> entries, byte[] key, CollationRule collationRule)
        {
            if (entries.Count == 0)
            {
                return -1;
            }

            int lowerIndex = 0;
            int upperIndex = entries.Count - 1;
            int comparisonResult;
            while (lowerIndex < upperIndex)
            {
                int middleIndex = (lowerIndex + upperIndex) / 2;
                IndexEntry middle = entries[middleIndex];
                comparisonResult = Compare(middle.Key, key, collationRule);
                if (comparisonResult == 0)
                {
                    return middleIndex;
                }
                else if (comparisonResult > 0) // middle > key
                {
                    upperIndex = middleIndex - 1;
                }
                else // middle < key
                {
                    lowerIndex = middleIndex + 1;
                }
            }

            comparisonResult = Compare(entries[lowerIndex].Key, key, collationRule);
            if (comparisonResult == 0)
            {
                return lowerIndex;
            }
            else
            {
                return -1;
            }
        }

        public static int FindIndexForSortedInsert(List<IndexEntry> entries, byte[] key, CollationRule collationRule)
        {
            if (entries.Count == 0)
            {
                return 0;
            }

            int lowerIndex = 0;
            int upperIndex = entries.Count - 1;
            int comparisonResult;
            while (lowerIndex < upperIndex)
            {
                int middleIndex = (lowerIndex + upperIndex) / 2;
                IndexEntry middle = entries[middleIndex];
                comparisonResult = Compare(middle.Key, key, collationRule);
                if (comparisonResult == 0)
                {
                    return middleIndex;
                }
                else if (comparisonResult > 0) // middle > key
                {
                    upperIndex = middleIndex - 1;
                }
                else // middle < key
                {
                    lowerIndex = middleIndex + 1;
                }
            }

            // At this point any entry following 'middle' is greater than 'key',
            // and any entry preceding 'middle' is lesser than 'key'.
            // So we either put 'key' before or after 'middle'.
            comparisonResult = Compare(entries[lowerIndex].Key, key, collationRule);
            if (comparisonResult < 0) // middle < key
            {
                return lowerIndex + 1;
            }
            else
            {
                return lowerIndex;
            }
        }
    }
}
