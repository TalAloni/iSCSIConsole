/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using DiskAccessLibrary;
using DiskAccessLibrary.LogicalDiskManager;
using SCSI;
using ISCSI.Server;
using Utilities;

namespace ISCSIConsole
{
    partial class Program
    {
        public const string DefaultTargetIQN = "iqn.1991-05.com.microsoft";

        public static void AttachCommand(string[] args)
        {
            if (m_server != null)
            {
                Console.WriteLine("Server is already running");
                return;
            }

            if (args.Length >= 2)
            {
                KeyValuePairList<string, string> parameters = ParseParameters(args, 2);
                if (!VerifyParameters(parameters, "vdisk", "disk", "volume", "readonly", "target"))
                {
                    Console.WriteLine();
                    Console.WriteLine("Invalid parameter");
                    HelpAttach();
                    return;
                }

                switch (args[1].ToLower())
                {
                    case "vdisk":
                        {
                            if (m_selectedDisk == null)
                            {
                                Console.WriteLine("No disk has been selected");
                                break;
                            }

                            if (!(m_selectedDisk is DiskImage))
                            {
                                Console.WriteLine("Selected disk is not a disk image");
                                break;
                            }

                            DiskImage disk = (DiskImage)m_selectedDisk;
                            string defaultStorageTargetName = Path.GetFileNameWithoutExtension(disk.Path);
                            string defaultTargetName = DefaultTargetIQN + ":" + defaultStorageTargetName.Replace(" ", ""); // spaces are not allowed
                            AttachISCSIDisk(disk, defaultTargetName, parameters);
                            break;
                        }
                    case "disk":
                        {
                            if (m_selectedDisk == null)
                            {
                                Console.WriteLine("Error: No disk has been selected.");
                                break;
                            }

                            if (!(m_selectedDisk is PhysicalDisk))
                            {
                                Console.WriteLine("Error: The selected disk is not a physical disk.");
                                break;
                            }

                            bool isAttachmentReadOnly = parameters.ContainsKey("readonly");
                            PhysicalDisk disk = (PhysicalDisk)m_selectedDisk;
                            if (!isAttachmentReadOnly)
                            {
                                if (Environment.OSVersion.Version.Major >= 6)
                                {
                                    bool isDiskReadOnly;
                                    bool isOnline = disk.GetOnlineStatus(out isDiskReadOnly);
                                    if (isOnline)
                                    {
                                        Console.WriteLine();
                                        Console.WriteLine("Error: The selected disk must be taken offline.");
                                        break;
                                    }

                                    if (!isAttachmentReadOnly && isDiskReadOnly)
                                    {
                                        Console.WriteLine();
                                        Console.WriteLine("Error: The selected disk is set to readonly!");
                                        break;
                                    }
                                }
                                else
                                {
                                    Console.WriteLine();
                                    // Locking mechanism is not implemented
                                    Console.Write("Warning: if a volume on this disk is mounted locally, data corruption may occur!");
                                }
                            }
                            string defaultStorageTargetName = string.Format("disk{0}", disk.PhysicalDiskIndex);
                            string defaultTargetName = DefaultTargetIQN + ":" + defaultStorageTargetName;
                            AttachISCSIDisk(disk, defaultTargetName, parameters);
                            break;
                        }
                    case "volume":
                        {
                            if (m_selectedVolume == null)
                            {
                                Console.WriteLine("No volume has been selected.");
                                break;
                            }

                            VolumeDisk virtualDisk = new VolumeDisk(m_selectedVolume);
                            string defaultTargetName = DefaultTargetIQN + ":Volume";
                            bool isAttachmentReadOnly = parameters.ContainsKey("readonly");
                            if (!isAttachmentReadOnly)
                            {
                                if (Environment.OSVersion.Version.Major >= 6)
                                {
                                    if (m_selectedVolume is DynamicVolume)
                                    {
                                        foreach(DiskExtent extent in ((DynamicVolume)m_selectedVolume).Extents)
                                        {
                                            if (extent.Disk is PhysicalDisk)
                                            {
                                                bool isDiskReadOnly;
                                                bool isOnline = ((PhysicalDisk)extent.Disk).GetOnlineStatus(out isDiskReadOnly);
                                                if (isOnline)
                                                {
                                                    Console.WriteLine("Error: All disks containing the volume must be taken offline.");
                                                    return;
                                                }

                                                if (isDiskReadOnly)
                                                {
                                                    Console.WriteLine("Error: A disk containing the volume is set to readonly.");
                                                    return;
                                                }
                                            }
                                        }
                                    }
                                    else if (m_selectedVolume is Partition)
                                    {
                                        Disk disk = ((Partition)m_selectedVolume).Disk;
                                        if (disk is PhysicalDisk)
                                        {
                                            bool isDiskReadOnly;
                                            bool isOnline = ((PhysicalDisk)disk).GetOnlineStatus(out isDiskReadOnly);

                                            if (isOnline)
                                            {
                                                Console.WriteLine("Error: The disk containing the volume must be taken offline.");
                                                return;
                                            }

                                            if (isDiskReadOnly)
                                            {
                                                Console.WriteLine("Error: The disk containing the volume is set to readonly.");
                                                return;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Console.WriteLine();
                                    // Locking mechanism is not implemented
                                    Console.WriteLine("Warning: if this volume is mounted locally, data corruption may occur!");
                                }
                            }
                            AttachISCSIDisk(virtualDisk, defaultTargetName, parameters);
                            break;
                        }
                    default:
                        {
                            Console.WriteLine();
                            Console.WriteLine("Invalid argument.");
                            HelpAttach();
                            break;
                        }
                }
            }
            else
            {
                HelpAttach();
            }
        }

        public static void HelpAttach()
        {
            Console.WriteLine();
            Console.WriteLine("ATTACH VDISK [READONLY] [TARGET=<NAME>]  - Attach virtual hard disk file");
            Console.WriteLine("ATTACH DISK [READONLY] [TARGET=<NAME>]   - Attach selected physical disk");
            Console.WriteLine("ATTACH VOLUME [READONLY] [TARGET=<NAME>] - Attach selected volume");
        }

        public static void AttachISCSIDisk(Disk disk, string defaultTargetName, KeyValuePairList<string, string> parameters)
        {
            if (VerifyParameters(parameters, "readonly", "target"))
            {
                bool isReadOnly = parameters.ContainsKey("readonly");
                disk.IsReadOnly |= isReadOnly;
                if (disk is DiskImage)
                {
                    bool isLocked = ((DiskImage)disk).ExclusiveLock();
                    if (!isLocked)
                    {
                        Console.WriteLine("Error: Cannot lock the disk image for exclusive access");
                        return;
                    }
                }

                ISCSITarget target = null;
                string targetName = defaultTargetName;
                if (parameters.ContainsKey("target"))
                {
                    string name = parameters.ValueOf("target");
                    if (IsValidISCSIName(name))
                    {
                        targetName = name;
                    }
                    else if (IsValidStorageTargetName(name))
                    {
                        targetName = DefaultTargetIQN + ":" + name;
                    }
                    else
                    {
                        Console.WriteLine("Invalid parameter.");
                        HelpAttach();
                    }
                }

                target = FindTarget(targetName);

                if (target == null)
                {
                    target = AddTarget(targetName);
                }

                ((VirtualSCSITarget)target.SCSITarget).Disks.Add(disk);
                Console.WriteLine("Disk added to target: {0}", target.TargetName);
            }
            else
            {
                HelpAttach();
            }
        }

        public static ISCSITarget AddTarget(string targetName)
        {
            List<Disk> disks = new List<Disk>();
            ISCSITarget target = new ISCSITarget(targetName, disks);
            m_targets.Add(target);
            return target;
        }

        public static ISCSITarget FindTarget(string targetName)
        {
            foreach (ISCSITarget target in m_targets)
            {
                // iSCSI names are not case sensitive
                if (target.TargetName.ToLower() == targetName.ToLower())
                {
                    return target;
                }
            }
            return null;
        }

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
                        string storageTargetName = String.Empty;
                        int index = name.IndexOf(":");
                        if (index >= 12) // index cannot be < 12
                        {
                            reversedDomain = name.Substring(12, index - 12);
                            storageTargetName = name.Substring(index + 1);
                            return IsValidReversedDomainName(reversedDomain) && IsValidStorageTargetName(storageTargetName);
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

        public static bool IsValidStorageTargetName(string name)
        {
            // With the exception of the colon prefix, the owner of the domain name can assign everything after the reversed domain name as desired
            // No whitespace characters are used in iSCSI names
            // Note: String.Empty is a valid storage target name
            if (name.StartsWith(":") || name.Contains(" "))
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
