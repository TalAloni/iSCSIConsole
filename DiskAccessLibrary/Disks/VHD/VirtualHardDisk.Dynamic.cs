/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.IO;
using System.Collections.Generic;
using Utilities;
using DiskAccessLibrary.VHD;

namespace DiskAccessLibrary
{
    public partial class VirtualHardDisk
    {
        private byte[] ReadSectorsFromDynamicDisk(long sectorIndex, int sectorCount)
        {
            byte[] buffer = new byte[sectorCount * BytesPerDiskSector];
            int sectorsInBlock = (int)(m_dynamicHeader.BlockSize / BytesPerDiskSector);
            int sectorOffset = 0;
            while (sectorOffset < sectorCount)
            {
                uint blockIndex = (uint)((sectorIndex + sectorOffset) * BytesPerDiskSector / m_dynamicHeader.BlockSize);
                int sectorOffsetInBlock = (int)(((sectorIndex + sectorOffset) * BytesPerDiskSector % m_dynamicHeader.BlockSize) / BytesPerDiskSector);
                int sectorsRemainingInBlock = sectorsInBlock - sectorOffsetInBlock;
                int sectorsToRead = Math.Min(sectorCount - sectorOffset, sectorsRemainingInBlock);

                uint blockStartSector;
                if (m_blockAllocationTable.IsBlockInUse(blockIndex, out blockStartSector))
                {
                    // Each data block has a sector bitmap preceding the data, the bitmap is padded to a 512-byte sector boundary.
                    int blockBitmapSectorCount = (int)Math.Ceiling((double)sectorsInBlock / (BytesPerDiskSector * 8));
                    // "All sectors within a block whose corresponding bits in the bitmap are zero must contain 512 bytes of zero on disk"
                    byte[] temp = m_file.ReadSectors(blockStartSector + blockBitmapSectorCount + sectorOffsetInBlock, sectorsToRead);
                    ByteWriter.WriteBytes(buffer, sectorOffset * BytesPerDiskSector, temp);
                }
                sectorOffset += sectorsToRead;
            }
            return buffer;
        }

        public bool AreSectorsInUse(long sectorIndex, int sectorCount)
        {
            if (m_vhdFooter.DiskType != VirtualHardDiskType.Fixed)
            {
                int sectorsInBlock = (int)(m_dynamicHeader.BlockSize / BytesPerDiskSector);
                int sectorOffset = 0;
                while (sectorOffset < sectorCount)
                {
                    uint blockIndex = (uint)((sectorIndex + sectorOffset) * BytesPerDiskSector / m_dynamicHeader.BlockSize);
                    int sectorOffsetInBlock = (int)(((sectorIndex + sectorOffset) * BytesPerDiskSector % m_dynamicHeader.BlockSize) / BytesPerDiskSector);
                    int sectorsRemainingInBlock = sectorsInBlock - sectorOffsetInBlock;
                    int sectorsToRead = Math.Min(sectorCount - sectorOffset, sectorsRemainingInBlock);

                    uint blockStartSector;
                    if (m_blockAllocationTable.IsBlockInUse(blockIndex, out blockStartSector))
                    {
                        byte[] bitmap = ReadBlockUsageBitmap(blockIndex);
                        if (!AreSectorsInUse(bitmap, sectorOffsetInBlock, sectorsToRead))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                    sectorOffset += sectorsToRead;
                }
            }
            return true;
        }

        private byte[] ReadBlockUsageBitmap(uint blockIndex)
        {
            if (m_vhdFooter.DiskType != VirtualHardDiskType.Fixed)
            {
                uint blockStartSector;
                int sectorsInBlock = (int)(m_dynamicHeader.BlockSize / BytesPerDiskSector);
                // Each data block has a sector bitmap preceding the data, the bitmap is padded to a 512-byte sector boundary.
                int blockBitmapSectorCount = (int)Math.Ceiling((double)sectorsInBlock / (BytesPerDiskSector * 8));
                if (m_blockAllocationTable.IsBlockInUse(blockIndex, out blockStartSector))
                {
                    byte[] bitmap = m_file.ReadSectors(blockStartSector, blockBitmapSectorCount);
                    return bitmap;
                }
                else
                {
                    return new byte[blockBitmapSectorCount * BytesPerDiskSector];
                }
            }
            else
            {
                throw new InvalidOperationException("Fixed VHDs do not have a Block Usage Bitmap");
            }
        }

        private void WriteSectorsToDynamicDisk(long sectorIndex, byte[] data)
        {
            int sectorCount = data.Length / BytesPerDiskSector;
            int sectorsInBlock = (int)(m_dynamicHeader.BlockSize / BytesPerDiskSector);
            int sectorOffset = 0;
            while (sectorOffset < sectorCount)
            {
                uint blockIndex = (uint)((sectorIndex + sectorOffset) * BytesPerDiskSector / m_dynamicHeader.BlockSize);
                int sectorOffsetInBlock = (int)(((sectorIndex + sectorOffset) * BytesPerDiskSector % m_dynamicHeader.BlockSize) / BytesPerDiskSector);
                int sectorsRemainingInBlock = sectorsInBlock - sectorOffsetInBlock;
                int sectorsToWrite = Math.Min(sectorCount - sectorOffset, sectorsRemainingInBlock);

                uint blockStartSector;
                if (!m_blockAllocationTable.IsBlockInUse(blockIndex, out blockStartSector))
                {
                    blockStartSector = AllocateDynamicDiskBlock(blockIndex);
                }

                // Each data block has a sector bitmap preceding the data, the bitmap is padded to a 512-byte sector boundary.
                int blockBitmapSectorCount = (int)Math.Ceiling((double)sectorsInBlock / (BytesPerDiskSector * 8));
                // "All sectors within a block whose corresponding bits in the bitmap are zero must contain 512 bytes of zero on disk"
                int blockBitmapSectorOffset = sectorOffsetInBlock / (BytesPerDiskSector * 8);
                int sectorOffsetInBitmap = sectorOffsetInBlock % (BytesPerDiskSector * 8);
                int remainingInBitmapSector = (BytesPerDiskSector * 8) - sectorOffsetInBitmap;
                int blockBitmapSectorsToUpdate = 1;
                if (sectorsToWrite > remainingInBitmapSector)
                {
                    blockBitmapSectorsToUpdate = (int)Math.Ceiling((double)(sectorsToWrite - remainingInBitmapSector) / (BytesPerDiskSector * 8));
                }

                byte[] bitmap = m_file.ReadSectors(blockStartSector + blockBitmapSectorOffset, blockBitmapSectorsToUpdate);
                UpdateBlockUsageBitmap(bitmap, sectorOffsetInBitmap, sectorsToWrite, true);
                m_file.WriteSectors(blockStartSector + blockBitmapSectorOffset, bitmap);
                byte[] temp = ByteReader.ReadBytes(data, sectorOffset * BytesPerDiskSector, sectorsToWrite * BytesPerDiskSector);
                m_file.WriteSectors(blockStartSector + blockBitmapSectorCount + sectorOffsetInBlock, temp);

                sectorOffset += sectorsToWrite;
            }
        }

        /// <returns>Block start sector</returns>
        private uint AllocateDynamicDiskBlock(uint blockIndex)
        {
            long footerOffset = m_file.Size - VHDFooter.Length;
            uint blockStartSector = (uint)(footerOffset / BytesPerDiskSector);
            int sectorsInBlock = (int)(m_dynamicHeader.BlockSize / BytesPerDiskSector);
            // Each data block has a sector bitmap preceding the data, the bitmap is padded to a 512-byte sector boundary.
            int blockBitmapSectorCount = (int)Math.Ceiling((double)sectorsInBlock / (BytesPerDiskSector * 8));
            // Block Size does not include the size of the block bitmap.
            ExtendFile(blockBitmapSectorCount * BytesPerDiskSector + m_dynamicHeader.BlockSize);
            byte[] bitmap = new byte[blockBitmapSectorCount * BytesPerDiskSector];
            m_file.WriteSectors(blockStartSector, bitmap);
            // All sectors within a block whose corresponding bits in the bitmap are zero must contain 512 bytes of zero on disk.
            byte[] blockSectors = new byte[sectorsInBlock * BytesPerDiskSector];
            m_file.WriteSectors(blockStartSector + blockBitmapSectorCount, blockSectors);

            // Update the Block Allocation Table
            m_blockAllocationTable.SetBlockStartSector(blockIndex, blockStartSector);
            byte[] blockAllocationTableBytes = m_blockAllocationTable.GetBytes();
            long blockAllocationTableSectorIndex = (long)(m_dynamicHeader.TableOffset / VirtualHardDisk.BytesPerDiskSector);
            m_file.WriteSectors(blockAllocationTableSectorIndex, blockAllocationTableBytes);

            return blockStartSector;
        }

        public void AllocateAllDynamicDiskBlocks()
        {
            if (m_vhdFooter.DiskType != VirtualHardDiskType.Fixed)
            {
                for (uint blockIndex = 0; blockIndex < m_dynamicHeader.MaxTableEntries; blockIndex++)
                {
                    if (!m_blockAllocationTable.IsBlockInUse(blockIndex))
                    {
                        AllocateDynamicDiskBlock(blockIndex);
                    }
                }
            }
        }

        private static bool AreSectorsInUse(byte[] bitmap, int sectorOffsetInBitmap, int sectorCount)
        {
            int leadingBits = (8 - (sectorOffsetInBitmap % 8)) % 8;
            for (int sectorOffset = 0; sectorOffset < leadingBits; sectorOffset++)
            {
                if (!IsSectorInUse(bitmap, sectorOffsetInBitmap + sectorOffset))
                {
                    return false;
                }
            }

            int byteCount = Math.Max(sectorCount - leadingBits, 0) / 8;
            int byteOffsetInBitmap = (sectorOffsetInBitmap + leadingBits) / 8;
            for (int byteOffset = 0; byteOffset < byteCount; byteOffset++)
            {
                if (bitmap[byteOffsetInBitmap + byteOffset] != 0xFF)
                {
                    return false;
                }
            }

            for (int sectorOffset = leadingBits + byteCount * 8; sectorOffset < sectorCount; sectorOffset++)
            {
                if (!IsSectorInUse(bitmap, sectorOffsetInBitmap + sectorOffset))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSectorInUse(byte[] bitmap, int sectorOffsetInBitmap)
        {
            int byteOffset = sectorOffsetInBitmap / 8;
            int bitOffset = 7 - sectorOffsetInBitmap % 8;
            bool isUsed = (bitmap[byteOffset] & (byte)(0x01 << bitOffset)) > 0;
            return isUsed;
        }

        private static void UpdateBlockUsageBitmap(byte[] bitmap, int sectorOffsetInBitmap, int sectorCount, bool isUsed)
        {
            for (int offsetInBitmap = 0; offsetInBitmap < sectorCount; offsetInBitmap++)
            {
                UpdateBlockUsageBitmap(bitmap, sectorOffsetInBitmap + offsetInBitmap, isUsed);
            }
        }

        private static void UpdateBlockUsageBitmap(byte[] bitmap, int sectorOffsetInBitmap, bool isUsed)
        {
            int byteOffset = sectorOffsetInBitmap / 8;
            int bitOffset = 7 - sectorOffsetInBitmap % 8;
            if (isUsed)
            {
                bitmap[byteOffset] |= (byte)(0x01 << bitOffset);
            }
            else
            {
                bitmap[byteOffset] &= (byte)(~(0x01 << bitOffset));
            }
        }

        /// <param name="diskSize">In bytes</param>
        /// <exception cref="System.IO.IOException"></exception>
        /// <exception cref="System.UnauthorizedAccessException"></exception>
        public static VirtualHardDisk CreateDynamicDisk(string path, long diskSize)
        {
            if (diskSize % BytesPerDiskSector > 0)
            {
                throw new ArgumentException("diskSize must be a multiple of sector size");
            }

            const int BlockSizeInBytes = 4096 * BytesPerDiskSector;

            VHDFooter footer = new VHDFooter();
            footer.OriginalSize = (ulong)diskSize;
            footer.CurrentSize = (ulong)diskSize;
            footer.SetCurrentTimeStamp();
            footer.SetDiskGeometry((ulong)diskSize / BytesPerDiskSector);
            footer.DiskType = VirtualHardDiskType.Dynamic;

            DynamicDiskHeader header = new DynamicDiskHeader();
            header.TableOffset = VHDFooter.Length + DynamicDiskHeader.Length;
            header.BlockSize = BlockSizeInBytes;
            header.MaxTableEntries = (uint)Math.Ceiling((double)diskSize / BlockSizeInBytes);

            BlockAllocationTable blockAllocationTable = new BlockAllocationTable(header.MaxTableEntries);
            byte[] footerBytes = footer.GetBytes();
            byte[] headerBytes = header.GetBytes();
            byte[] blockAllocationTableBytes = blockAllocationTable.GetBytes();

            int fileSize = VHDFooter.Length + DynamicDiskHeader.Length + blockAllocationTableBytes.Length + VHDFooter.Length;
            RawDiskImage diskImage = RawDiskImage.Create(path, fileSize, BytesPerDiskSector);
            diskImage.WriteSectors(0, footerBytes);
            diskImage.WriteSectors(1, headerBytes);
            diskImage.WriteSectors(3, blockAllocationTableBytes);
            diskImage.WriteSectors(fileSize / BytesPerDiskSector - 1, footerBytes);

            return new VirtualHardDisk(path);
        }
    }
}
