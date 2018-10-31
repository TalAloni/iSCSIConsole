/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;

public class DiskExtent
{
    private Disk m_disk;
    private long m_firstSector;
    private long m_size; // In bytes

    public DiskExtent(Disk disk, long firstSector, long size)
    {
        m_disk = disk;
        m_firstSector = firstSector;
        m_size = size;
    }

    public byte[] ReadSector(long sectorIndex)
    {
        return ReadSectors(sectorIndex, 1);
    }

    public byte[] ReadSectors(long sectorIndex, int sectorCount)
    {
        CheckBoundaries(sectorIndex, sectorCount);
        return m_disk.ReadSectors(m_firstSector + sectorIndex, sectorCount);
    }

    public void WriteSectors(long sectorIndex, byte[] data)
    {
        CheckBoundaries(sectorIndex, data.Length / this.BytesPerSector);
        m_disk.WriteSectors(m_firstSector + sectorIndex, data);
    }

    public void CheckBoundaries(long sectorIndex, int sectorCount)
    {
        if (sectorIndex < 0 || sectorIndex + (sectorCount - 1) >= this.TotalSectors)
        {
            throw new ArgumentOutOfRangeException("Attempted to access data outside of volume");
        }
    }

    public int BytesPerSector
    {
        get
        {
            return m_disk.BytesPerSector;
        }
    }

    public long Size
    {
        get
        {
            return m_size;
        }
    }

    public bool IsReadOnly
    {
        get
        {
            return m_disk.IsReadOnly;
        }
    }

    public long FirstSector
    {
        get
        {
            return m_firstSector;
        }
    }

    public long TotalSectors
    {
        get
        {
            return this.Size / this.BytesPerSector;
        }
    }

    public long LastSector
    {
        get
        {
            return FirstSector + TotalSectors - 1;
        }
    }

    public Disk Disk
    {
        get
        {
            return m_disk;
        }
    }
}