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
    public class DirectoryNotEmptyException : IOException
    {
        public DirectoryNotEmptyException() : this("The directory is not empty")
        {
        }

        public DirectoryNotEmptyException(string message) : base(message)
        {
#if Win32
            HResult = IOExceptionHelper.GetHResultFromWin32Error(Win32Error.ERROR_DIR_NOT_EMPTY);
#endif
        }
    }
}
