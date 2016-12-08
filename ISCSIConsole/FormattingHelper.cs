/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace ISCSIConsole
{
    public class FormattingHelper
    {
        public static string GetStandardSizeString(long value)
        {
            string[] suffixes = { " B", "KB", "MB", "GB", "TB", "PB", "EB" };
            int suffixIndex = 0;
            while (value > 9999)
            {
                value = value / 1024;
                suffixIndex++;
            }

            if (suffixIndex < suffixes.Length)
            {
                string FourCharacterValue = value.ToString();
                while (FourCharacterValue.Length < 4)
                {
                    FourCharacterValue = " " + FourCharacterValue;
                }
                return String.Format("{0} {1}", FourCharacterValue, suffixes[suffixIndex]);
            }
            else
            {
                return "Too Big";
            }
        }
    }
}
