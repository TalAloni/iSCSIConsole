/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

public abstract class Disk
{
    private bool m_isReadOnly = false;

    /// <summary>
    /// Sector refers to physical disk sector
    /// </summary>
    public abstract byte[] ReadSectors(long sectorIndex, int sectorCount);
    public abstract void WriteSectors(long sectorIndex, byte[] data);

    public byte[] ReadSector(long sectorIndex)
    {
        return ReadSectors(sectorIndex, 1);
    }

    public bool IsReadOnly
    {
        get
        {
            return m_isReadOnly;
        }
        set
        {
            m_isReadOnly = value;
        }
    }

    public abstract int BytesPerSector
    {
        get;
    }

    public abstract long Size
    {
        get;
    }

    public long TotalSectors
    {
        get
        {
            return this.Size / this.BytesPerSector;
        }
    }
}

