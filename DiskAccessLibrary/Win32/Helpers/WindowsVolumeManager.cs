/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Utilities;
using DiskAccessLibrary.LogicalDiskManager;

namespace DiskAccessLibrary
{
    public class WindowsVolumeManager
    {
        public static bool ExclusiveLockIfMounted(Guid windowsVolumeGuid)
        {
            return ExclusiveLockIfMounted(windowsVolumeGuid, FileAccess.ReadWrite);
        }

        public static bool ExclusiveLockIfMounted(Guid windowsVolumeGuid, FileAccess fileAccess)
        {
            if (IsMounted(windowsVolumeGuid))
            {
                return ExclusiveLock(windowsVolumeGuid, fileAccess);
            }
            else
            {
                return true;
            }
        }

        public static bool ExclusiveLock(Guid windowsVolumeGuid)
        {
            return ExclusiveLock(windowsVolumeGuid, FileAccess.ReadWrite);
        }

        /// <summary>
        /// Windows will flush all cached data to the volume before locking it.
        /// Note: we can only lock a dynamic volume if the disk is online.
        /// </summary>
        /// <returns>True if a new lock has been obtained</returns>
        public static bool ExclusiveLock(Guid windowsVolumeGuid, FileAccess fileAccess)
        {
            bool newAllocation;
            // Windows Vista / 7: Valid handle cannot be obtained for a dynamic volume using ShareMode.ReadWrite.
            // The FSCTL_LOCK_VOLUME documentation demands ShareMode.ReadWrite, but ShareMode.None or ShareMode.Write will work too.
            SafeFileHandle handle = VolumeHandlePool.ObtainHandle(windowsVolumeGuid, fileAccess, ShareMode.None, out newAllocation);
            if (newAllocation)
            {
                if (!handle.IsInvalid)
                {
                    bool success = VolumeUtils.LockVolume(handle);
                    if (!success)
                    {
                        VolumeHandlePool.ReleaseHandle(windowsVolumeGuid);
                    }
                    return success;
                }
                else
                {
                    VolumeHandlePool.ReleaseHandle(windowsVolumeGuid);
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Note that the volume will be remounted again on first access.
        /// </summary>
        public static bool DismountVolume(Guid windowsVolumeGuid)
        {
            bool releaseHandle;
            SafeFileHandle handle = VolumeHandlePool.ObtainHandle(windowsVolumeGuid, FileAccess.ReadWrite, ShareMode.ReadWrite, out releaseHandle);
            bool success = false;
            if (!handle.IsInvalid)
            {
                success = VolumeUtils.DismountVolume(handle);
            }

            if (releaseHandle) // new allocation
            {
                VolumeHandlePool.ReleaseHandle(windowsVolumeGuid);
            }
            return success;
        }


        public static bool ReleaseLock(Guid windowsVolumeGuid)
        {
            return VolumeHandlePool.ReleaseHandle(windowsVolumeGuid);
        }

        public static bool IsMounted(Guid windowsVolumeGuid)
        {
            return VolumeUtils.IsVolumeMounted(windowsVolumeGuid);
        }

        public static List<string> GetMountPoints(Guid windowsVolumeGuid)
        {
            return VolumeUtils.GetVolumeMountPoints(windowsVolumeGuid);
        }

        /// <summary>
        /// A volume can be mounted by the OS even if it has no mount points
        /// </summary>
        public static bool HasMountPoints(Guid windowsVolumeGuid)
        {
            return (GetMountPoints(windowsVolumeGuid).Count > 0);
        }
    }
}
