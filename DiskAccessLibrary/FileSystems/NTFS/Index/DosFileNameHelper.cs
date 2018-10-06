/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public class DosFileNameHelper
    {
        public static string GenerateDosName(IndexData parentDirectoryIndex, string fileName)
        {
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            nameWithoutExtension = ConvertToDosCharacters(nameWithoutExtension).ToUpper();
            if (extension.Length > 0)
            {
                extension = "." + ConvertToDosCharacters(extension.Substring(1)).ToUpper();
            }
            if (nameWithoutExtension.Length > 8)
            {
                nameWithoutExtension = nameWithoutExtension.Substring(0, 6) + "~1";
            }
            if (extension.Length > 4)
            {
                extension = extension.Substring(0, 4);
            }

            int index = 1;
            string shortNameWithoutExtension;
            while (index <= 0xFFFFFF)
            {
                string suffix = "~" + index.ToString("X");
                int nameCharsToKeep = Math.Min(nameWithoutExtension.Length, 8 - suffix.Length);
                shortNameWithoutExtension = nameWithoutExtension.Substring(0, nameCharsToKeep) + suffix;
                string dosFileName = shortNameWithoutExtension + extension;
                if (!parentDirectoryIndex.ContainsFileName(dosFileName))
                {
                    return dosFileName;
                }
                index++;
            }

            // Extremely unlikely that we will ever get here
            throw new NotSupportedException(String.Format("Could not find an available DOS name for '{0}'", fileName));
        }

        /// <remarks>
        /// https://en.wikipedia.org/wiki/Design_of_the_FAT_file_system#Directory_table
        /// https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/content/ntifs/nf-ntifs-rtlgenerate8dot3name
        /// </remarks>
        public static bool IsValidDosFileName(string fileName)
        {
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            return (nameWithoutExtension.Length <= 8 && extension.Length <= 4 &&
                    IsValidDosNameString(nameWithoutExtension) &&
                    IsValidDosNameString(extension));
        }

        private static bool IsValidDosNameString(string str)
        {
            for (int index = 0; index < str.Length; index++)
            {
                if (!IsValidDosCharacter(str[index]))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsValidDosCharacter(char c)
        {
            string allowed = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!#$%&'()-@^_`{}~";
            for (int index = 0; index < allowed.Length; index++)
            {
                if (allowed[index] == c)
                {
                    return true;
                }
            }
            return false;
        }

        private static string ConvertToDosCharacters(string str)
        {
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < str.Length; index++)
            {
                if (IsValidDosCharacter(str[index]))
                {
                    builder.Append(str[index]);
                }
                else
                {
                    builder.Append(((ushort)str[index]).ToString("X4"));
                }
            }
            return builder.ToString();
        }
    }
}
