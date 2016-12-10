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
    public class VolumeHandlePool
    {
        // We will use the handle pool to share handles to volumes across the application (useful when handle need to lock access to a volume)
        private static Dictionary<Guid, SafeFileHandle> m_handlePool = new Dictionary<Guid, SafeFileHandle>();

        /// <param name="newAllocation">True if a new handle has been allocated for the caller, the caller must release the handle eventually</param>
        public static SafeFileHandle ObtainHandle(Guid volumeGuid, FileAccess access, ShareMode shareMode, out bool newAllocation)
        {
            if (m_handlePool.ContainsKey(volumeGuid))
            {
                newAllocation = false;
                return m_handlePool[volumeGuid];
            }
            else
            {
                newAllocation = true;
                SafeFileHandle handle = HandleUtils.GetVolumeHandle(volumeGuid, access, shareMode);
                m_handlePool.Add(volumeGuid, handle);
                return handle;
            }
        }

        public static bool ReleaseHandle(Guid volumeGuid)
        {
            if (m_handlePool.ContainsKey(volumeGuid))
            {
                SafeFileHandle handle = m_handlePool[volumeGuid];
                if (!handle.IsClosed)
                {
                    handle.Close();
                }
                m_handlePool.Remove(volumeGuid);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
