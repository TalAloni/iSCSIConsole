/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace Utilities
{
    public class KeyValuePairUtils
    {
        public static KeyValuePairList<string, string> GetKeyValuePairList(string nullDelimitedString)
        {
            KeyValuePairList<string, string> result = new KeyValuePairList<string, string>();

            string[] entries = nullDelimitedString.Split('\0');
            foreach (string entry in entries)
            {
                string[] pair = entry.Split('=');
                if (pair.Length >= 2)
                {
                    string key = pair[0];
                    string value = pair[1];
                    result.Add(key, value);
                }
            }
            return result;
        }

        public static string ToNullDelimitedString(KeyValuePairList<string, string> list)
        {
            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in list)
            {
                builder.AppendFormat("{0}={1}\0", pair.Key, pair.Value);
            }
            return builder.ToString();
        }

        public static string ToString(KeyValuePairList<string, string> list)
        {
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < list.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(", ");
                }
                builder.AppendFormat("{0}={1}", list[index].Key, list[index].Value);
            }
            return builder.ToString();
        }
    }
}
