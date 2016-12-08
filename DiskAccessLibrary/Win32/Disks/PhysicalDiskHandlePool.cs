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

namespace DiskAccessLibrary
{
    public class PhysicalDiskHandlePool
    {
        // We will use the handle pool to share handles to disks across the application (useful when handle need to lock access to a device)
        private static Dictionary<int, SafeFileHandle> m_handlePool = new Dictionary<int, SafeFileHandle>();

        /// <param name="newAllocation">True if a new handle has been allocated for the caller, the caller must release the handle eventually</param>
        public static SafeFileHandle ObtainHandle(int physicalDiskIndex, FileAccess access, ShareMode shareMode, out bool newAllocation)
        {
            if (m_handlePool.ContainsKey(physicalDiskIndex))
            {
                newAllocation = false;
                return m_handlePool[physicalDiskIndex];
            }
            else
            {
                newAllocation = true;
                SafeFileHandle handle = HandleUtils.GetDiskHandle(physicalDiskIndex, access, shareMode);
                m_handlePool.Add(physicalDiskIndex, handle);
                return handle;
            }
        }

        public static bool ReleaseHandle(int physicalDiskIndex)
        {
            if (m_handlePool.ContainsKey(physicalDiskIndex))
            {
                SafeFileHandle handle = m_handlePool[physicalDiskIndex];
                if (!handle.IsClosed)
                {
                    handle.Close();
                }
                m_handlePool.Remove(physicalDiskIndex);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
