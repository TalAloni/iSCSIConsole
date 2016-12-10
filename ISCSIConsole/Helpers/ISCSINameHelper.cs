/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace ISCSIConsole
{
    public class ISCSINameHelper
    {
        /// <summary>
        /// Check if a name is a valid initiator or target name (a.k.a. iSCSI name)
        /// </summary>
        public static bool IsValidISCSIName(string name)
        {
            if (name.ToLower().StartsWith("iqn."))
            {
                return IsValidIQN(name);
            }
            else
            {
                return IsValidEUI(name);
            }
        }

        public static bool IsValidIQN(string name)
        {
            if (name.ToLower().StartsWith("iqn."))
            {
                if (name.Length > 12 && name[8] == '-' && name[11] == '.')
                {
                    int year = Conversion.ToInt32(name.Substring(4, 4), -1);
                    int month = Conversion.ToInt32(name.Substring(9, 2), -1);
                    if (year != -1 && (month >= 1 && month <= 12))
                    {
                        string reversedDomain;
                        string subQualifier = String.Empty;
                        int index = name.IndexOf(":");
                        if (index >= 12) // index cannot be non-negative and < 12
                        {
                            reversedDomain = name.Substring(12, index - 12);
                            subQualifier = name.Substring(index + 1);
                            return IsValidReversedDomainName(reversedDomain) && IsValidSubQualifier(subQualifier);
                        }
                        else
                        {
                            reversedDomain = name.Substring(12);
                            return IsValidReversedDomainName(reversedDomain);
                        }
                    }
                }

            }
            return false;
        }

        public static bool IsValidReversedDomainName(string name)
        {
            string[] components = name.Split('.');
            if (components.Length < 1)
            {
                return false;
            }

            foreach (string component in components)
            {
                if (component.Length == 0 || component.StartsWith("-") || component.EndsWith("-"))
                {
                    return false;
                }

                for (int index = 0; index < component.Length; index++)
                {
                    bool isValid = (component[index] >= '0' && component[index] <= '9') ||
                                   (component[index] >= 'a' && component[index] <= 'z') ||
                                   (component[index] >= 'A' && component[index] <= 'Z') ||
                                   (component[index] == '-');

                    if (!isValid)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static bool IsValidSubQualifier(string subQualifier)
        {
            // RFC 3720: The owner of the domain name can assign everything after the reversed domain name as desired.
            // RFC 3720: iSCSI names are composed only of displayable characters.
            // RFC 3720: No whitespace characters are used in iSCSI names.
            // Note: String.Empty is a valid sub-qualifier.
            if (subQualifier.Contains(" "))
            {
                return false;
            }
            return true;
        }

        public static bool IsValidEUI(string name)
        {
            if (name.ToLower().StartsWith("eui.") && name.Length == 20)
            {
                string identifier = name.Substring(5);
                return OnlyHexChars(identifier);
            }
            return false;
        }

        public static bool OnlyHexChars(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                bool isValid = (value[index] >= '0' && value[index] <= '9') ||
                               (value[index] >= 'a' && value[index] <= 'f') ||
                               (value[index] >= 'A' && value[index] <= 'F');

                if (!isValid)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
