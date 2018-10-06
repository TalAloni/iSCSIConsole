/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.IO;

namespace DiskAccessLibrary
{
    public class DiskFullException : IOException
    {
        public DiskFullException() : this("Disk Full")
        {
        }

        public DiskFullException(string message) : base(message)
        {
#if Win32
            HResult = IOExceptionHelper.GetHResultFromWin32Error(Win32Error.ERROR_DISK_FULL);
#endif
        }
    }
}
