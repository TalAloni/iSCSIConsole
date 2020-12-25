/* Copyright (C) 2018-2020 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System.IO;

namespace DiskAccessLibrary
{
    public class InvalidPathException : IOException
    {
        public InvalidPathException() : this("Invalid path")
        {
        }

        public InvalidPathException(string message) : base(message)
        {
            HResult = IOExceptionHelper.GetHResultFromWin32Error(Win32Error.ERROR_BAD_PATHNAME);
        }
    }
}
