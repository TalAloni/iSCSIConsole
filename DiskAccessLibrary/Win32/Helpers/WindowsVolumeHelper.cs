/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace DiskAccessLibrary
{
    public class WindowsVolumeHelper
    {
        /// <summary>
        /// Get Windows Volume Guid of MBR disk
        /// </summary>
        /// <param name="partitionStartOffset">In bytes</param>
        /// <returns></returns>
        public static Guid? GetWindowsVolumeGuid(uint mbrDiskSignature, ulong partitionStartOffset)
        {
            byte[] identifier = new byte[12];
            LittleEndianWriter.WriteUInt32(identifier, 0, mbrDiskSignature);
            LittleEndianWriter.WriteUInt64(identifier, 4, partitionStartOffset);

            RegistryKey mountedDevices = Registry.LocalMachine.OpenSubKey(@"SYSTEM\MountedDevices");
            foreach (string valueName in mountedDevices.GetValueNames())
            {
                object valueObject = mountedDevices.GetValue(valueName);
                byte[] value = valueObject as byte[];
                if (value != null && value.Length == 12)
                {
                    if (ByteUtils.AreByteArraysEqual(value, identifier))
                    {
                        if (valueName.StartsWith(@"\??\Volume"))
                        {
                            string guidString = valueName.Substring(10);
                            return new Guid(guidString);
                        }
                    }
                }
            }
            return null;
        }

        public static Guid? GetWindowsVolumeGuid(Volume volume)
        {
            if (volume is MBRPartition)
            {
                MBRPartition partition = (MBRPartition)volume;
                MasterBootRecord mbr = MasterBootRecord.ReadFromDisk(((MBRPartition)volume).Disk);
                return GetWindowsVolumeGuid(mbr.DiskSignature, (ulong)(partition.FirstSector * partition.BytesPerSector));
            }
            else if (volume is GPTPartition)
            {
                return ((GPTPartition)volume).VolumeGuid;
            }
            else if (volume is DynamicVolume)
            {
                return ((DynamicVolume)volume).VolumeGuid;
            }
            else if (volume is OperatingSystemVolume)
            {
                return ((OperatingSystemVolume)volume).VolumeGuid;
            }
            else
            {
                return null;
            }
        }

        public static Volume GetVolumeByGuid(Guid windowsVolumeGuid)
        {
            List<Volume> volumes = GetVolumes();
            foreach (Volume volume in volumes)
            {
                Guid? guid = GetWindowsVolumeGuid(volume);
                if (guid.HasValue && guid.Value == windowsVolumeGuid)
                {
                    return volume;
                }
            }
            return null;
        }

        public static List<Volume> GetVolumes()
        {
            List<PhysicalDisk> disks = PhysicalDiskHelper.GetPhysicalDisks();
            List<Volume> result = new List<Volume>();

            // Get partitions:
            foreach (Disk disk in disks)
            {
                if (!DynamicDisk.IsDynamicDisk(disk))
                {
                    List<Partition> partitions = BasicDiskHelper.GetPartitions(disk);
                    foreach (Partition partition in partitions)
                    {
                        result.Add(partition);
                    }
                }
            }

            // Get dynamic volumes
            List<DynamicVolume> dynamicVolumes = WindowsDynamicVolumeHelper.GetDynamicVolumes();
            foreach (DynamicVolume volume in dynamicVolumes)
            {
                result.Add(volume);
            }

            return result;
        }
    }
}
