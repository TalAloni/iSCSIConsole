/* Copyright (C) 2014-2019 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public class NTFSVolumeCreator
    {
        private const int UpcaseFileLength = 65536 * 2;

        public static NTFSVolume Format(Volume volume, int bytesPerCluster, string volumeLabel)
        {
            return Format(volume, 3, 1, bytesPerCluster, volumeLabel);
        }

        public static NTFSVolume Format(Volume volume, byte majorNTFSVersion, byte minorNTFSVersion, int bytesPerCluster, string volumeLabel)
        {
            if (volumeLabel.Length > VolumeNameRecord.MaxVolumeNameLength)
            {
                throw new InvalidNameException();
            }

            if (bytesPerCluster % volume.BytesPerSector > 0)
            {
                throw new ArgumentException("bytesPerCluster must be a multiple of volume.BytesPerSector");
            }

            if (majorNTFSVersion != 3 || (minorNTFSVersion != 0 && minorNTFSVersion != 1))
            {
                throw new NotSupportedException();
            }

            long volumeClusterCount = (volume.Size - NTFSBootRecord.Length) / bytesPerCluster;
            // We wish to make WriteVolumeBitmap() as simple as possible so we use a multiple of ExtendGranularity to avoid having to set bits at the end of the bitmap
            volumeClusterCount = (long)Math.Floor((double)volumeClusterCount / (VolumeBitmap.ExtendGranularity * 8)) * (VolumeBitmap.ExtendGranularity * 8);
            int sectorsPerCluster = bytesPerCluster / volume.BytesPerSector;
            int bytesPerFileRecordSegment = 1024; // Supported values are 1024 or 4096 (when formatted with /L)
            int bytesPerIndexRecord = 4096; // Legal values are 1024, 2048 or 4096. NTFS v5.1 driver will always use 4096.
            int bootSegmentFileSize = 8192;
            int bootSegmentAllocatedLength = (int)Math.Ceiling((double)bootSegmentFileSize / bytesPerCluster) * bytesPerCluster;
            FileNameRecord bootFileNameRecord = new FileNameRecord(MasterFileTable.RootDirSegmentReference, "$Boot", false, DateTime.Now);
            bootFileNameRecord.AllocatedLength = (ulong)bootSegmentAllocatedLength;
            bootFileNameRecord.FileSize = (ulong)bootSegmentFileSize;
            bootFileNameRecord.FileAttributes = FileAttributes.Hidden | FileAttributes.System;
            FileRecordSegment bootSegment = CreateBaseRecordSegment(MasterFileTable.BootSegmentNumber, (ushort)MasterFileTable.BootSegmentNumber, bootFileNameRecord);
            bootSegment.ReferenceCount = 1;
            NonResidentAttributeRecord bootDataRecord = (NonResidentAttributeRecord)bootSegment.CreateAttributeRecord(AttributeType.Data, String.Empty, false);
            bootDataRecord.AllocatedLength = (ulong)bootSegmentAllocatedLength;
            bootDataRecord.FileSize = (ulong)bootSegmentFileSize;
            bootDataRecord.ValidDataLength = (ulong)bootSegmentFileSize;
            int bootDataClusterCount = (int)Math.Ceiling((double)8192 / bytesPerCluster);
            long bootDataStartLCN = 0;
            bootDataRecord.DataRunSequence.Add(new DataRun(bootDataClusterCount, bootDataStartLCN));
            bootDataRecord.HighestVCN = bootDataClusterCount - 1;

            long volumeBitmapFileSize = (long)Math.Ceiling((double)volumeClusterCount / (VolumeBitmap.ExtendGranularity * 8)) * VolumeBitmap.ExtendGranularity;
            long numberOfVolumeBitmapClusters = (long)Math.Ceiling((double)volumeBitmapFileSize / bytesPerCluster);
            long volumeBitmapAllocatedLength = numberOfVolumeBitmapClusters * bytesPerCluster;
            FileNameRecord volumeBitmapFileNameRecord = new FileNameRecord(MasterFileTable.RootDirSegmentReference, "$Bitmap", false, DateTime.Now);
            volumeBitmapFileNameRecord.AllocatedLength = (ulong)volumeBitmapAllocatedLength;
            volumeBitmapFileNameRecord.FileSize = (ulong)volumeBitmapFileSize;
            volumeBitmapFileNameRecord.FileAttributes = FileAttributes.Hidden | FileAttributes.System;
            FileRecordSegment volumeBitmapSegment = CreateBaseRecordSegment(MasterFileTable.BitmapSegmentNumber, (ushort)MasterFileTable.BitmapSegmentNumber, volumeBitmapFileNameRecord);
            volumeBitmapSegment.ReferenceCount = 1;
            NonResidentAttributeRecord volumeBitmapDataRecord = (NonResidentAttributeRecord)volumeBitmapSegment.CreateAttributeRecord(AttributeType.Data, String.Empty, false);
            long volumeBitmapStartLCN = bootDataClusterCount;
            volumeBitmapDataRecord.AllocatedLength = (ulong)volumeBitmapAllocatedLength;
            volumeBitmapDataRecord.FileSize = (ulong)volumeBitmapFileSize;
            volumeBitmapDataRecord.ValidDataLength = (ulong)volumeBitmapFileSize;
            volumeBitmapDataRecord.DataRunSequence.Add(new DataRun(numberOfVolumeBitmapClusters, volumeBitmapStartLCN));
            volumeBitmapDataRecord.HighestVCN = numberOfVolumeBitmapClusters - 1;

            int numberOfMftRecords = 64;
            int mftDataLength = numberOfMftRecords * bytesPerFileRecordSegment;
            FileNameRecord mftFileNameRecord = new FileNameRecord(MasterFileTable.RootDirSegmentReference, "$MFT", false, DateTime.Now);
            mftFileNameRecord.AllocatedLength = (ulong)mftDataLength;
            mftFileNameRecord.FileSize = (ulong)mftDataLength;
            mftFileNameRecord.FileAttributes = FileAttributes.Hidden | FileAttributes.System;
            FileRecordSegment mftSegment = CreateBaseRecordSegment(MasterFileTable.MasterFileTableSegmentNumber, 1, mftFileNameRecord);
            mftSegment.ReferenceCount = 1;
            NonResidentAttributeRecord mftDataRecord = (NonResidentAttributeRecord)mftSegment.CreateAttributeRecord(AttributeType.Data, String.Empty, false);
            mftDataRecord.AllocatedLength = (ulong)mftDataLength;
            mftDataRecord.FileSize = (ulong)mftDataLength;
            mftDataRecord.ValidDataLength = (ulong)mftDataLength;
            int mftDataClusterCount = (int)Math.Ceiling((double)mftDataLength / bytesPerCluster);
            long mftDataStartLCN = volumeBitmapStartLCN + numberOfVolumeBitmapClusters;
            mftDataRecord.DataRunSequence.Add(new DataRun(mftDataClusterCount, mftDataStartLCN));
            mftDataRecord.HighestVCN = mftDataClusterCount - 1;
            NonResidentAttributeRecord mftBitmapRecord = (NonResidentAttributeRecord)mftSegment.CreateAttributeRecord(AttributeType.Bitmap, String.Empty, false);
            int mftBitmapLength = (int)Math.Ceiling((double)numberOfMftRecords / (BitmapData.ExtendGranularity * 8)) * BitmapData.ExtendGranularity;
            int mftBitmapClusterCount =(int)Math.Ceiling((double)mftBitmapLength / bytesPerCluster);
            mftBitmapRecord.AllocatedLength = (ulong)(mftBitmapClusterCount * bytesPerCluster);
            mftBitmapRecord.FileSize = (ulong)mftBitmapLength;
            mftBitmapRecord.ValidDataLength = (ulong)mftBitmapLength;
            long mftBitmapStartLCN = mftDataStartLCN + mftDataClusterCount;
            mftBitmapRecord.DataRunSequence.Add(new DataRun(mftBitmapClusterCount, mftBitmapStartLCN));
            mftBitmapRecord.HighestVCN = 0;

            int bytesPerLogPage = 4096;
            int logFileDataLength = 512 * bytesPerLogPage;
            FileNameRecord logFileNameRecord = new FileNameRecord(MasterFileTable.RootDirSegmentReference, "$LogFile", false, DateTime.Now);
            logFileNameRecord.AllocatedLength = (ulong)logFileDataLength;
            logFileNameRecord.FileSize = (ulong)logFileDataLength;
            logFileNameRecord.FileAttributes = FileAttributes.Hidden | FileAttributes.System;
            FileRecordSegment logFileSegment = CreateBaseRecordSegment(MasterFileTable.LogFileSegmentNumber, (ushort)MasterFileTable.LogFileSegmentNumber, logFileNameRecord);
            logFileSegment.ReferenceCount = 1;
            NonResidentAttributeRecord logFileDataRecord = (NonResidentAttributeRecord)logFileSegment.CreateAttributeRecord(AttributeType.Data, String.Empty, false);
            logFileDataRecord.AllocatedLength = (ulong)logFileDataLength;
            logFileDataRecord.FileSize = (ulong)logFileDataLength;
            logFileDataRecord.ValidDataLength = (ulong)logFileDataLength;
            int logFileClusterCount = (int)Math.Ceiling((double)logFileDataLength / bytesPerCluster);
            long logFileStartLCN = mftBitmapStartLCN + mftBitmapClusterCount;
            logFileDataRecord.DataRunSequence.Add(new DataRun(logFileClusterCount, logFileStartLCN));
            logFileDataRecord.HighestVCN = logFileClusterCount - 1;

            FileNameRecord volumeFileNameRecord = new FileNameRecord(MasterFileTable.RootDirSegmentReference, "$Volume", false, DateTime.Now);
            volumeFileNameRecord.FileAttributes = FileAttributes.Hidden | FileAttributes.System;
            FileRecordSegment volumeSegment = CreateBaseRecordSegment(MasterFileTable.VolumeSegmentNumber, (ushort)MasterFileTable.VolumeSegmentNumber, volumeFileNameRecord);
            volumeSegment.ReferenceCount = 1;
            VolumeNameRecord volumeName = (VolumeNameRecord)volumeSegment.CreateAttributeRecord(AttributeType.VolumeName, String.Empty);
            volumeName.VolumeName = volumeLabel;
            VolumeInformationRecord volumeInformation = (VolumeInformationRecord)volumeSegment.CreateAttributeRecord(AttributeType.VolumeInformation, String.Empty);
            volumeInformation.MajorVersion = majorNTFSVersion;
            volumeInformation.MinorVersion = minorNTFSVersion;
            volumeSegment.CreateAttributeRecord(AttributeType.Data, String.Empty);

            long logFileDataStartSector = logFileStartLCN * sectorsPerCluster;
            WriteLogFile(volume, logFileDataStartSector, logFileDataLength, bytesPerLogPage);

            long attributeDefinitionStartLCN = logFileStartLCN + logFileClusterCount;
            long attributeDefinitionStartSector = attributeDefinitionStartLCN * sectorsPerCluster;
            int attributeDefinitionLength = WriteAttributeDefinition(volume, attributeDefinitionStartSector, bytesPerCluster);
            int attributeDefinitionClusterCount = (int)Math.Ceiling((double)attributeDefinitionLength / bytesPerCluster);
            int attributeDefinitionAllocatedLength = attributeDefinitionClusterCount * bytesPerCluster;
            FileNameRecord attributeDefinitionFileNameRecord = new FileNameRecord(MasterFileTable.RootDirSegmentReference, "$AttrDef", false, DateTime.Now);
            attributeDefinitionFileNameRecord.AllocatedLength = (ulong)attributeDefinitionAllocatedLength;
            attributeDefinitionFileNameRecord.FileSize = (ulong)attributeDefinitionLength;
            attributeDefinitionFileNameRecord.FileAttributes = FileAttributes.Hidden | FileAttributes.System;
            FileRecordSegment attributeDefinitionSegment = CreateBaseRecordSegment(MasterFileTable.AttrDefSegmentNumber, (ushort)MasterFileTable.AttrDefSegmentNumber, attributeDefinitionFileNameRecord);
            attributeDefinitionSegment.ReferenceCount = 1;
            NonResidentAttributeRecord attributeDefinitionDataRecord = (NonResidentAttributeRecord)attributeDefinitionSegment.CreateAttributeRecord(AttributeType.Data, String.Empty, false);
            attributeDefinitionDataRecord.AllocatedLength = (ulong)attributeDefinitionAllocatedLength;
            attributeDefinitionDataRecord.FileSize = (ulong)attributeDefinitionLength;
            attributeDefinitionDataRecord.ValidDataLength = (ulong)attributeDefinitionLength;
            attributeDefinitionDataRecord.DataRunSequence.Add(new DataRun(attributeDefinitionClusterCount, attributeDefinitionStartLCN));
            attributeDefinitionDataRecord.HighestVCN = attributeDefinitionClusterCount - 1;
            
            FileNameRecord badClustersFileNameRecord = new FileNameRecord(MasterFileTable.RootDirSegmentReference, "$BadClus", false, DateTime.Now);
            badClustersFileNameRecord.FileAttributes = FileAttributes.Hidden | FileAttributes.System;
            FileRecordSegment badClustersSegment = CreateBaseRecordSegment(MasterFileTable.BadClusSegmentNumber, (ushort)MasterFileTable.BadClusSegmentNumber, badClustersFileNameRecord);
            badClustersSegment.ReferenceCount = 1;
            badClustersSegment.CreateAttributeRecord(AttributeType.Data, String.Empty);
            NonResidentAttributeRecord badClustersData = (NonResidentAttributeRecord)badClustersSegment.CreateAttributeRecord(AttributeType.Data, "$Bad", false);
            DataRun volumeDataRun = new DataRun();
            volumeDataRun.RunLength = volumeClusterCount;
            volumeDataRun.IsSparse = true;
            badClustersData.DataRunSequence.Add(volumeDataRun);
            badClustersData.HighestVCN = volumeClusterCount - 1;
            badClustersData.AllocatedLength = (ulong)(volumeClusterCount * bytesPerCluster);
            badClustersData.FileSize = 0;
            badClustersData.ValidDataLength = 0;

            FileNameRecord secureFileNameRecord = new FileNameRecord(MasterFileTable.RootDirSegmentReference, "$Secure", false, DateTime.Now);
            secureFileNameRecord.FileAttributes = FileAttributes.Hidden | FileAttributes.System;
            FileRecordSegment secureSegment = CreateBaseRecordSegment(MasterFileTable.SecureSegmentNumber, (ushort)MasterFileTable.SecureSegmentNumber, secureFileNameRecord);
            secureSegment.IsSpecialIndex = true;
            secureSegment.ReferenceCount = 1;
            secureSegment.CreateAttributeRecord(AttributeType.Data, "$SDS");
            IndexRootRecord sdh = (IndexRootRecord)secureSegment.CreateAttributeRecord(AttributeType.IndexRoot, "$SDH");
            IndexHelper.InitializeIndexRoot(sdh, AttributeType.None, CollationRule.SecurityHash, bytesPerIndexRecord, bytesPerCluster);
            IndexRootRecord sii = (IndexRootRecord)secureSegment.CreateAttributeRecord(AttributeType.IndexRoot, "$SII");
            IndexHelper.InitializeIndexRoot(sii, AttributeType.None, CollationRule.UnsignedLong, bytesPerIndexRecord, bytesPerCluster);

            int upcaseDataClusterCount = (int)Math.Ceiling((double)UpcaseFileLength / bytesPerCluster);
            FileNameRecord upcaseFileNameRecord = new FileNameRecord(MasterFileTable.RootDirSegmentReference, "$UpCase", false, DateTime.Now);
            upcaseFileNameRecord.AllocatedLength = UpcaseFileLength;
            upcaseFileNameRecord.FileSize = UpcaseFileLength;
            upcaseFileNameRecord.FileAttributes = FileAttributes.Hidden | FileAttributes.System;
            FileRecordSegment upcaseSegment = CreateBaseRecordSegment(MasterFileTable.UpCaseSegmentNumber, (ushort)MasterFileTable.UpCaseSegmentNumber, upcaseFileNameRecord);
            upcaseSegment.ReferenceCount = 1;
            NonResidentAttributeRecord upcaseFileDataRecord = (NonResidentAttributeRecord)upcaseSegment.CreateAttributeRecord(AttributeType.Data, String.Empty, false);
            upcaseFileDataRecord.AllocatedLength = UpcaseFileLength;
            upcaseFileDataRecord.FileSize = UpcaseFileLength;
            upcaseFileDataRecord.ValidDataLength = UpcaseFileLength;
            long upcaseDataStartLCN = attributeDefinitionStartLCN + attributeDefinitionClusterCount;
            upcaseFileDataRecord.DataRunSequence.Add(new DataRun(upcaseDataClusterCount, upcaseDataStartLCN));
            upcaseFileDataRecord.HighestVCN = upcaseDataClusterCount - 1;

            FileNameRecord extendFileNameRecord = new FileNameRecord(MasterFileTable.RootDirSegmentReference, "$Extend", true, DateTime.Now);
            extendFileNameRecord.FileAttributes = FileAttributes.System | FileAttributes.Hidden | FileAttributes.FileNameIndexPresent;
            FileRecordSegment extendSegment = CreateBaseRecordSegment(MasterFileTable.ExtendSegmentNumber, (ushort)MasterFileTable.ExtendSegmentNumber, extendFileNameRecord);
            extendSegment.IsDirectory = true;
            extendSegment.ReferenceCount = 1;
            IndexRootRecord extendIndexRoot = (IndexRootRecord)extendSegment.CreateAttributeRecord(AttributeType.IndexRoot, IndexHelper.GetIndexName(AttributeType.FileName));
            IndexHelper.InitializeIndexRoot(extendIndexRoot, AttributeType.FileName, CollationRule.Filename, bytesPerIndexRecord, bytesPerCluster);
            
            FileNameRecord mftMirrorFileNameRecord = new FileNameRecord(MasterFileTable.RootDirSegmentReference, "$MFTMirr", false, DateTime.Now);
            int mftMirrorDataLength = 4 * bytesPerFileRecordSegment;
            int mftMirrorDataClusterCount = (int)Math.Ceiling((double)mftMirrorDataLength / bytesPerCluster);
            int mftMirrorAllocatedLength = mftMirrorDataClusterCount * bytesPerCluster;
            mftMirrorFileNameRecord.AllocatedLength = (ulong)mftMirrorAllocatedLength;
            mftMirrorFileNameRecord.FileSize = (ulong)mftMirrorDataLength;
            mftMirrorFileNameRecord.FileAttributes = FileAttributes.Hidden | FileAttributes.System;
            FileRecordSegment mftMirrorSegment = CreateBaseRecordSegment(MasterFileTable.MftMirrorSegmentNumber, (ushort)MasterFileTable.MftMirrorSegmentNumber, mftMirrorFileNameRecord);
            mftMirrorSegment.ReferenceCount = 1;
            NonResidentAttributeRecord mftMirrorDataRecord = (NonResidentAttributeRecord)mftMirrorSegment.CreateAttributeRecord(AttributeType.Data, String.Empty, false);
            mftMirrorDataRecord.AllocatedLength = (ulong)mftMirrorAllocatedLength;
            mftMirrorDataRecord.FileSize = (ulong)mftMirrorDataLength;
            mftMirrorDataRecord.ValidDataLength = (ulong)mftMirrorDataLength;
            long mftMirrorDataStartLCN = upcaseDataStartLCN + upcaseDataClusterCount;
            mftMirrorDataRecord.DataRunSequence.Add(new DataRun(mftMirrorDataClusterCount, mftMirrorDataStartLCN));
            mftMirrorDataRecord.HighestVCN = mftMirrorDataClusterCount - 1;

            FileNameRecord rootDirFileNameRecord = new FileNameRecord(MasterFileTable.RootDirSegmentReference, ".", true, DateTime.Now);
            rootDirFileNameRecord.FileAttributes = FileAttributes.System | FileAttributes.Hidden | FileAttributes.FileNameIndexPresent;
            FileRecordSegment rootDirSegment = CreateBaseRecordSegment(MasterFileTable.RootDirSegmentNumber, (ushort)MasterFileTable.RootDirSegmentNumber, rootDirFileNameRecord);
            rootDirSegment.IsDirectory = true;
            rootDirSegment.ReferenceCount = 1;

            IndexRecord rootDirIndexRecord = new IndexRecord();
            rootDirIndexRecord.RecordVBN = 0;
            // Note that we add the index entries according to collation rules
            rootDirIndexRecord.IndexEntries.Add(new IndexEntry(attributeDefinitionSegment.SegmentReference, attributeDefinitionFileNameRecord.GetBytes()));
            rootDirIndexRecord.IndexEntries.Add(new IndexEntry(badClustersSegment.SegmentReference, badClustersFileNameRecord.GetBytes()));
            rootDirIndexRecord.IndexEntries.Add(new IndexEntry(volumeBitmapSegment.SegmentReference, volumeBitmapFileNameRecord.GetBytes()));
            rootDirIndexRecord.IndexEntries.Add(new IndexEntry(bootSegment.SegmentReference, bootFileNameRecord.GetBytes()));
            rootDirIndexRecord.IndexEntries.Add(new IndexEntry(extendSegment.SegmentReference, extendFileNameRecord.GetBytes()));
            rootDirIndexRecord.IndexEntries.Add(new IndexEntry(logFileSegment.SegmentReference, logFileNameRecord.GetBytes()));
            rootDirIndexRecord.IndexEntries.Add(new IndexEntry(mftSegment.SegmentReference, mftFileNameRecord.GetBytes()));
            rootDirIndexRecord.IndexEntries.Add(new IndexEntry(mftMirrorSegment.SegmentReference, mftMirrorFileNameRecord.GetBytes()));
            rootDirIndexRecord.IndexEntries.Add(new IndexEntry(secureSegment.SegmentReference, secureFileNameRecord.GetBytes()));
            rootDirIndexRecord.IndexEntries.Add(new IndexEntry(upcaseSegment.SegmentReference, upcaseFileNameRecord.GetBytes()));
            rootDirIndexRecord.IndexEntries.Add(new IndexEntry(volumeSegment.SegmentReference, volumeFileNameRecord.GetBytes()));
            rootDirIndexRecord.IndexEntries.Add(new IndexEntry(rootDirSegment.SegmentReference, rootDirFileNameRecord.GetBytes()));

            long rootDirIndexRecordStartLCN = mftMirrorDataStartLCN + mftMirrorDataClusterCount;
            int rootDirIndexRecordClusterCount = (int)Math.Ceiling((double)bytesPerIndexRecord / bytesPerCluster);
            int rootDirIndexRecordAllocatedLength = rootDirIndexRecordClusterCount * bytesPerCluster;
            string rootDirIndexName = IndexHelper.GetIndexName(AttributeType.FileName);
            IndexRootRecord rootDirIndexRoot = (IndexRootRecord)rootDirSegment.CreateAttributeRecord(AttributeType.IndexRoot, rootDirIndexName);
            IndexHelper.InitializeIndexRoot(rootDirIndexRoot, AttributeType.FileName, CollationRule.Filename, bytesPerIndexRecord, bytesPerCluster);
            rootDirIndexRoot.IsParentNode = true;
            IndexEntry rootEntry = new IndexEntry();
            rootEntry.ParentNodeForm = true;
            rootEntry.SubnodeVBN = 0;
            rootDirIndexRoot.IndexEntries.Add(rootEntry);
            IndexAllocationRecord rootDirIndexAllocation = (IndexAllocationRecord)rootDirSegment.CreateAttributeRecord(AttributeType.IndexAllocation, rootDirIndexName, false);
            rootDirIndexAllocation.AllocatedLength = (uint)rootDirIndexRecordAllocatedLength;
            rootDirIndexAllocation.FileSize = (uint)bytesPerIndexRecord;
            rootDirIndexAllocation.ValidDataLength = (uint)bytesPerIndexRecord;
            rootDirIndexAllocation.DataRunSequence.Add(new DataRun(rootDirIndexRecordClusterCount, rootDirIndexRecordStartLCN));
            rootDirIndexAllocation.HighestVCN = rootDirIndexRecordClusterCount - 1;
            ResidentAttributeRecord rootDirBitmap = (ResidentAttributeRecord)rootDirSegment.CreateAttributeRecord(AttributeType.Bitmap, rootDirIndexName);
            rootDirBitmap.Data = new byte[BitmapData.ExtendGranularity];
            BitmapData.SetBit(rootDirBitmap.Data, 0);

            long numberOfClustersInUse = rootDirIndexRecordStartLCN + rootDirIndexRecordClusterCount;
            long volumeBitmapStartSector = volumeBitmapStartLCN * sectorsPerCluster;
            WriteVolumeBitmap(volume, volumeBitmapStartSector, volumeClusterCount, numberOfClustersInUse);

            long mftBitmapStartSector = mftBitmapStartLCN * sectorsPerCluster;
            WriteMftBitmap(volume, mftBitmapStartSector, numberOfMftRecords, mftBitmapLength);

            // Write MFT data
            byte[] mftData = new byte[mftDataLength];
            long mftDataStartSector = mftDataStartLCN * sectorsPerCluster;
            WriteFileRecordSegment(mftData, mftSegment, bytesPerFileRecordSegment, minorNTFSVersion);
            WriteFileRecordSegment(mftData, mftMirrorSegment, bytesPerFileRecordSegment, minorNTFSVersion);
            WriteFileRecordSegment(mftData, logFileSegment, bytesPerFileRecordSegment, minorNTFSVersion);
            WriteFileRecordSegment(mftData, volumeSegment, bytesPerFileRecordSegment, minorNTFSVersion);
            WriteFileRecordSegment(mftData, attributeDefinitionSegment, bytesPerFileRecordSegment, minorNTFSVersion);
            WriteFileRecordSegment(mftData, rootDirSegment, bytesPerFileRecordSegment, minorNTFSVersion);
            WriteFileRecordSegment(mftData, volumeBitmapSegment, bytesPerFileRecordSegment, minorNTFSVersion);
            WriteFileRecordSegment(mftData, bootSegment, bytesPerFileRecordSegment, minorNTFSVersion);
            WriteFileRecordSegment(mftData, badClustersSegment, bytesPerFileRecordSegment, minorNTFSVersion);
            WriteFileRecordSegment(mftData, secureSegment, bytesPerFileRecordSegment, minorNTFSVersion);
            WriteFileRecordSegment(mftData, upcaseSegment, bytesPerFileRecordSegment, minorNTFSVersion);
            WriteFileRecordSegment(mftData, extendSegment, bytesPerFileRecordSegment, minorNTFSVersion);
            for (long segmentNumber = MasterFileTable.ExtendSegmentNumber + 1; segmentNumber < MasterFileTable.FirstReservedSegmentNumber; segmentNumber++)
            {
                FileRecordSegment systemSegment = CreateSystemReservedSegment(segmentNumber);
                WriteFileRecordSegment(mftData, systemSegment, bytesPerFileRecordSegment, minorNTFSVersion);
            }

            volume.WriteSectors(mftDataStartSector, mftData);

            long upcaseDataStartSector = upcaseDataStartLCN * sectorsPerCluster;
            WriteUpCaseFile(volume, upcaseDataStartSector);

            long rootDirIndexRecordStartSector =  rootDirIndexRecordStartLCN * sectorsPerCluster;
            WriteIndexRecord(volume, rootDirIndexRecordStartSector, rootDirIndexRecord, bytesPerIndexRecord);

            // Write MFT mirror data
            byte[] mftMirrorData = new byte[mftMirrorDataLength];
            long mftMirrorDataStartSector = mftMirrorDataStartLCN * sectorsPerCluster;
            WriteFileRecordSegment(mftMirrorData, mftSegment, bytesPerFileRecordSegment, minorNTFSVersion);
            WriteFileRecordSegment(mftMirrorData, mftMirrorSegment, bytesPerFileRecordSegment, minorNTFSVersion);
            WriteFileRecordSegment(mftMirrorData, logFileSegment, bytesPerFileRecordSegment, minorNTFSVersion);
            WriteFileRecordSegment(mftMirrorData, volumeSegment, bytesPerFileRecordSegment, minorNTFSVersion);
            volume.WriteSectors(mftMirrorDataStartSector, mftMirrorData);
            
            NTFSBootRecord bootRecord = CreateNTFSBootRecord(volumeClusterCount, sectorsPerCluster, volume.BytesPerSector, bytesPerFileRecordSegment, bytesPerIndexRecord, mftDataStartLCN, mftMirrorDataStartLCN);
            volume.WriteSectors(0, bootRecord.GetBytes());
            volume.WriteSectors(volume.TotalSectors - 1, bootRecord.GetBytes());

            return new NTFSVolume(volume);
        }

        private static void WriteLogFile(Volume volume, long logFileStartSector, long logFileSize, int bytesPerLogPage)
        {
            const int BytesPerSystemPage = 4096;

            LfsClientRecord ntfsClientRecord = new LfsClientRecord(NTFSLogClient.ClientName);
            LfsRestartPage restartPage = LfsRestartPage.Create(logFileSize, BytesPerSystemPage, bytesPerLogPage, ntfsClientRecord);
            
            long secondRestartPageStartSector = logFileStartSector + BytesPerSystemPage / volume.BytesPerSector;
            long firstRecordPageStartSector = logFileStartSector + 2 * BytesPerSystemPage / volume.BytesPerSector;
            byte[] restartPageBytes = restartPage.GetBytes(BytesPerSystemPage, true);
            volume.WriteSectors(logFileStartSector, restartPageBytes);
            volume.WriteSectors(secondRestartPageStartSector, restartPageBytes);
            byte[] recordPagesBytes = new byte[logFileSize - 2 * BytesPerSystemPage];
            for (int index = 0; index < recordPagesBytes.Length; index += 4)
            {
                LittleEndianWriter.WriteUInt32(recordPagesBytes, index, LfsRecordPage.UninitializedPageSignature);
            }
            volume.WriteSectors(firstRecordPageStartSector, recordPagesBytes);
        }

        /// <returns>Length of AttributeDefinition</returns>
        private static int WriteAttributeDefinition(Volume volume, long attributeListStartSector, int bytesPerCluster)
        {
            List<AttributeDefinitionEntry> entries = new List<AttributeDefinitionEntry>();
            entries.Add(new AttributeDefinitionEntry("$STANDARD_INFORMATION", AttributeType.StandardInformation, AttributeDefinitionFlags.MustBeResident, StandardInformationRecord.RecordDataLengthNTFS12, StandardInformationRecord.RecordDataLengthNTFS30));
            entries.Add(new AttributeDefinitionEntry("$ATTRIBUTE_LIST", AttributeType.AttributeList, AttributeDefinitionFlags.LogNonResident, 0, UInt64.MaxValue));
            entries.Add(new AttributeDefinitionEntry("$FILE_NAME", AttributeType.FileName, AttributeDefinitionFlags.MustBeResident | AttributeDefinitionFlags.Indexable, FileNameRecord.FixedLength + 2, FileNameRecord.FixedLength + 2 + FileNameRecord.MaxFileNameLength * 2));
            entries.Add(new AttributeDefinitionEntry("$OBJECT_ID", AttributeType.ObjectId, AttributeDefinitionFlags.MustBeResident, 0, 256));
            entries.Add(new AttributeDefinitionEntry("$SECURITY_DESCRIPTOR", AttributeType.SecurityDescriptor, AttributeDefinitionFlags.LogNonResident, 0, UInt64.MaxValue));
            entries.Add(new AttributeDefinitionEntry("$VOLUME_NAME", AttributeType.VolumeName, AttributeDefinitionFlags.MustBeResident, 2, 256));
            entries.Add(new AttributeDefinitionEntry("$VOLUME_INFORMATION", AttributeType.VolumeInformation, AttributeDefinitionFlags.MustBeResident, VolumeInformationRecord.RecordDataLength, VolumeInformationRecord.RecordDataLength));
            entries.Add(new AttributeDefinitionEntry("$DATA", AttributeType.Data, 0, 0, UInt64.MaxValue));
            entries.Add(new AttributeDefinitionEntry("$INDEX_ROOT", AttributeType.IndexRoot,AttributeDefinitionFlags.MustBeResident, 0, UInt64.MaxValue));
            entries.Add(new AttributeDefinitionEntry("$INDEX_ALLOCATION", AttributeType.IndexAllocation, AttributeDefinitionFlags.LogNonResident, 0, UInt64.MaxValue));
            entries.Add(new AttributeDefinitionEntry("$BITMAP", AttributeType.Bitmap, AttributeDefinitionFlags.LogNonResident, 0, UInt64.MaxValue));
            entries.Add(new AttributeDefinitionEntry("$REPARSE_POINT", AttributeType.ReparsePoint, AttributeDefinitionFlags.LogNonResident, 0, 16384));
            entries.Add(new AttributeDefinitionEntry("$EA_INFORMATION", AttributeType.ExtendedAttributesInformation, AttributeDefinitionFlags.MustBeResident, 8, 8));
            entries.Add(new AttributeDefinitionEntry("$EA", AttributeType.ExtendedAttributes, 0, 0, 65536));
            entries.Add(new AttributeDefinitionEntry("$LOGGED_UTILITY_STREAM", AttributeType.LoggedUtilityStream, AttributeDefinitionFlags.LogNonResident, 0, 65536));
            entries.Add(new AttributeDefinitionEntry(String.Empty, AttributeType.None, 0, 0, 0));
            byte[] attributeDefitionBytes = AttributeDefinition.GetBytes(entries);
            volume.WriteSectors(attributeListStartSector, attributeDefitionBytes);
            return attributeDefitionBytes.Length;
        }

        /// <returns>Length of UpCase File</returns>
        private static int WriteUpCaseFile(Volume volume, long upcaseFileStartSector)
        {
            byte[] upcaseTableBytes = new byte[UpcaseFileLength];
            for (int index = Char.MinValue; index <= Char.MaxValue; index++)
            {
                char c = (char)index;
                ushort value;
                if (Char.IsHighSurrogate(c))
                {
                    value = (ushort)index;
                }
                else
                {
                    char uppercased = c.ToString().ToUpperInvariant()[0];
                    value = (ushort)uppercased;
                }
                LittleEndianWriter.WriteUInt16(upcaseTableBytes, index * 2, (ushort)value);
            }
            volume.WriteSectors(upcaseFileStartSector, upcaseTableBytes);
            return upcaseTableBytes.Length;
        }

        private static void WriteFileRecordSegment(byte[] mftData, FileRecordSegment segment, int bytesPerFileRecordSegment, byte minorNTFSVersion)
        {
            byte[] segmentBytes = segment.GetBytes(bytesPerFileRecordSegment, minorNTFSVersion, true);
            ByteWriter.WriteBytes(mftData, bytesPerFileRecordSegment * (int)segment.SegmentNumber, segmentBytes);
        }

        private static void WriteIndexRecord(Volume volume, long indexRecordStartSector, IndexRecord indexRecord, int bytesPerIndexRecord)
        {
            byte[] indexRecordBytes = indexRecord.GetBytes(bytesPerIndexRecord, true);
            volume.WriteSectors(indexRecordStartSector, indexRecordBytes);
        }

        private static void WriteVolumeBitmap(Volume volume, long volumeBitmapStartSector, long volumeClusterCount, long numberOfClustersInUse)
        {
            int transferSize = (int)Math.Ceiling((double)numberOfClustersInUse / (volume.BytesPerSector * 8));
            byte[] transferBytes = new byte[transferSize * volume.BytesPerSector];
            for (int clusterIndex = 0; clusterIndex < numberOfClustersInUse; clusterIndex++)
            {
                VolumeBitmap.SetBit(transferBytes, clusterIndex);
            }
            volume.WriteSectors(volumeBitmapStartSector, transferBytes);

            long numberOfVolumeBitmapSectors = (long)Math.Ceiling((double)volumeClusterCount / (volume.BytesPerSector * 8));
            long sectorOffset = transferSize;
            while (sectorOffset <  numberOfVolumeBitmapSectors)
            {
                long sectorsRemaining = numberOfVolumeBitmapSectors - sectorOffset;
                transferSize = (int)Math.Min(Settings.MaximumTransferSizeLBA, sectorsRemaining);
                transferBytes = new byte[transferSize * volume.BytesPerSector];
                volume.WriteSectors(volumeBitmapStartSector + sectorOffset, transferBytes);
                sectorOffset += transferSize;
            }
        }

        private static void WriteMftBitmap(Volume volume, long mftBitmapStartSector, long numberOfMftRecords, int bitmapLength)
        {
            byte[] mftBitmap = new byte[volume.BytesPerSector];
            for (int index = 0; index < bitmapLength * 8; index++)
            {
                if (index < MasterFileTable.FirstReservedSegmentNumber || index >= numberOfMftRecords)
                {
                    BitmapData.SetBit(mftBitmap, index);
                }
            }
            volume.WriteSectors(mftBitmapStartSector, mftBitmap);
        }

        private static FileRecordSegment CreateBaseRecordSegment(long segmentNumber, ushort sequenceNumber, FileNameRecord fileNameRecord)
        {
            FileRecordSegment baseRecordSegment = new FileRecordSegment(segmentNumber, sequenceNumber);
            baseRecordSegment.IsInUse = true;
            baseRecordSegment.UpdateSequenceNumber = 1;
            StandardInformationRecord standardInformation = (StandardInformationRecord)baseRecordSegment.CreateAttributeRecord(AttributeType.StandardInformation, String.Empty);
            standardInformation.CreationTime = fileNameRecord.CreationTime;
            standardInformation.ModificationTime = fileNameRecord.ModificationTime;
            standardInformation.MftModificationTime = fileNameRecord.MftModificationTime;
            standardInformation.LastAccessTime = fileNameRecord.LastAccessTime;
            standardInformation.FileAttributes = fileNameRecord.FileAttributes;
            FileNameAttributeRecord fileNameAttribute = (FileNameAttributeRecord)baseRecordSegment.CreateAttributeRecord(AttributeType.FileName, String.Empty);
            fileNameAttribute.IsIndexed = true;
            fileNameAttribute.Record = fileNameRecord;
            return baseRecordSegment;
        }

        private static FileRecordSegment CreateSystemReservedSegment(long segmentNumber)
        {
            FileRecordSegment baseRecordSegment = new FileRecordSegment(segmentNumber, (ushort)segmentNumber);
            baseRecordSegment.IsInUse = true;
            baseRecordSegment.UpdateSequenceNumber = 1;
            DateTime creationTime = DateTime.Now;
            StandardInformationRecord standardInformation = (StandardInformationRecord)baseRecordSegment.CreateAttributeRecord(AttributeType.StandardInformation, String.Empty);
            standardInformation.CreationTime = creationTime;
            standardInformation.ModificationTime = creationTime;
            standardInformation.MftModificationTime = creationTime;
            standardInformation.LastAccessTime = creationTime;
            standardInformation.FileAttributes = FileAttributes.System | FileAttributes.Hidden;
            baseRecordSegment.CreateAttributeRecord(AttributeType.Data, String.Empty);
            return baseRecordSegment;
        }

        private static NTFSBootRecord CreateNTFSBootRecord(long numberOfClusters, int sectorsPerCluster, int bytesPerSector, int bytesPerFileRecordSegment, int bytesPerIndexRecord, long mftStartLCN, long mftMirrorStartLCN)
        {
            int bytesPerCluster = sectorsPerCluster * bytesPerSector;

            NTFSBootRecord bootRecord = new NTFSBootRecord();
            bootRecord.Jump = new byte[] { 0xEB, 0x52, 0x90 };
            bootRecord.BytesPerSector = (ushort)bytesPerSector;
            bootRecord.SectorsPerCluster = (byte)sectorsPerCluster;
            bootRecord.BytesPerFileRecordSegment = bytesPerFileRecordSegment;
            bootRecord.BytesPerIndexRecord = bytesPerIndexRecord;
            bootRecord.TotalSectors = (ulong)(numberOfClusters * sectorsPerCluster);
            bootRecord.MftStartLCN = (ulong)mftStartLCN;
            bootRecord.MftMirrorStartLCN = (ulong)mftMirrorStartLCN;
            bootRecord.SectorsPerTrack = 63;
            bootRecord.NumberOfHeads = 255;
            bootRecord.NumberOfHiddenSectors = 63;
            Random random = new Random();
            bootRecord.VolumeSerialNumber = (ulong)random.Next() << 32 | (uint)random.Next();
            bootRecord.Code = new byte[] { 0xFA, 0x33, 0xC0, 0x8E, 0xD0, 0xBC, 0x00, 0x7C, 0xFB, 0xB8, 0xC0, 0x07, 0x8E, 0xD8, 0xE8, 0x16, 0x00, 0xB8, 0x00, 0x0D, 0x8E, 0xC0, 0x33, 0xDB, 0xC6, 0x06, 0x0E, 0x00, 0x10, 0xE8, 0x53, 0x00, 0x68, 0x00, 0x0D, 0x68, 0x6A, 0x02, 0xCB, 0x8A, 0x16, 0x24, 0x00, 0xB4, 0x08, 0xCD, 0x13, 0x73, 0x05, 0xB9, 0xFF, 0xFF, 0x8A, 0xF1, 0x66, 0x0F, 0xB6, 0xC6, 0x40, 0x66, 0x0F, 0xB6, 0xD1, 0x80, 0xE2, 0x3F, 0xF7, 0xE2, 0x86, 0xCD, 0xC0, 0xED, 0x06, 0x41, 0x66, 0x0F, 0xB7, 0xC9, 0x66, 0xF7, 0xE1, 0x66, 0xA3, 0x20, 0x00, 0xC3, 0xB4, 0x41, 0xBB, 0xAA, 0x55, 0x8A, 0x16, 0x24, 0x00, 0xCD, 0x13, 0x72, 0x0F, 0x81, 0xFB, 0x55, 0xAA, 0x75, 0x09, 0xF6, 0xC1, 0x01, 0x74, 0x04, 0xFE, 0x06, 0x14, 0x00, 0xC3, 0x66, 0x60, 0x1E, 0x06, 0x66, 0xA1, 0x10, 0x00, 0x66, 0x03, 0x06, 0x1C, 0x00, 0x66, 0x3B, 0x06, 0x20, 0x00, 0x0F, 0x82, 0x3A, 0x00, 0x1E, 0x66, 0x6A, 0x00, 0x66, 0x50, 0x06, 0x53, 0x66, 0x68, 0x10, 0x00, 0x01, 0x00, 0x80, 0x3E, 0x14, 0x00, 0x00, 0x0F, 0x85, 0x0C, 0x00, 0xE8, 0xB3, 0xFF, 0x80, 0x3E, 0x14, 0x00, 0x00, 0x0F, 0x84, 0x61, 0x00, 0xB4, 0x42, 0x8A, 0x16, 0x24, 0x00, 0x16, 0x1F, 0x8B, 0xF4, 0xCD, 0x13, 0x66, 0x58, 0x5B, 0x07, 0x66, 0x58, 0x66, 0x58, 0x1F, 0xEB, 0x2D, 0x66, 0x33, 0xD2, 0x66, 0x0F, 0xB7, 0x0E, 0x18, 0x00, 0x66, 0xF7, 0xF1, 0xFE, 0xC2, 0x8A, 0xCA, 0x66, 0x8B, 0xD0, 0x66, 0xC1, 0xEA, 0x10, 0xF7, 0x36, 0x1A, 0x00, 0x86, 0xD6, 0x8A, 0x16, 0x24, 0x00, 0x8A, 0xE8, 0xC0, 0xE4, 0x06, 0x0A, 0xCC, 0xB8, 0x01, 0x02, 0xCD, 0x13, 0x0F, 0x82, 0x19, 0x00, 0x8C, 0xC0, 0x05, 0x20, 0x00, 0x8E, 0xC0, 0x66, 0xFF, 0x06, 0x10, 0x00, 0xFF, 0x0E, 0x0E, 0x00, 0x0F, 0x85, 0x6F, 0xFF, 0x07, 0x1F, 0x66, 0x61, 0xC3, 0xA0, 0xF8, 0x01, 0xE8, 0x09, 0x00, 0xA0, 0xFB, 0x01, 0xE8, 0x03, 0x00, 0xFB, 0xEB, 0xFE, 0xB4, 0x01, 0x8B, 0xF0, 0xAC, 0x3C, 0x00, 0x74, 0x09, 0xB4, 0x0E, 0xBB, 0x07, 0x00, 0xCD, 0x10, 0xEB, 0xF2, 0xC3, 0x0D, 0x0A, 0x41, 0x20, 0x64, 0x69, 0x73, 0x6B, 0x20, 0x72, 0x65, 0x61, 0x64, 0x20, 0x65, 0x72, 0x72, 0x6F, 0x72, 0x20, 0x6F, 0x63, 0x63, 0x75, 0x72, 0x72, 0x65, 0x64, 0x00, 0x0D, 0x0A, 0x4E, 0x54, 0x4C, 0x44, 0x52, 0x20, 0x69, 0x73, 0x20, 0x6D, 0x69, 0x73, 0x73, 0x69, 0x6E, 0x67, 0x00, 0x0D, 0x0A, 0x4E, 0x54, 0x4C, 0x44, 0x52, 0x20, 0x69, 0x73, 0x20, 0x63, 0x6F, 0x6D, 0x70, 0x72, 0x65, 0x73, 0x73, 0x65, 0x64, 0x00, 0x0D, 0x0A, 0x50, 0x72, 0x65, 0x73, 0x73, 0x20, 0x43, 0x74, 0x72, 0x6C, 0x2B, 0x41, 0x6C, 0x74, 0x2B, 0x44, 0x65, 0x6C, 0x20, 0x74, 0x6F, 0x20, 0x72, 0x65, 0x73, 0x74, 0x61, 0x72, 0x74, 0x0D, 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x83, 0xA0, 0xB3, 0xC9, 0x00, 0x00 };
            return bootRecord;
        }
    }
}
