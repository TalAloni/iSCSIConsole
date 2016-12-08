/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace DiskAccessLibrary
{
    public class Settings
    {
        // Note: The benefits beyond 16MB buffer are not very significant,
        // e.g. 512MB will only give ~5.6% speed boost over 64MB, ~7.9% over 32MB, and ~10.1% over 16MB.
        public static int MaximumTransferSizeLBA = 65536; // 32 MB (assuming 512-byte sectors)
    }
}
