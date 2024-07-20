using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using DiskAccessLibrary;

public interface IGetDiskFromConfig
{
    Disk GetDisk(Config.BaseDisk config);
}

public class Config
{
    private static Config instance;

    public static Config Instance
    {
        get
        {
            return instance ?? (instance= new Config());
        }
    }

    public enum DiskType
    {
        DiskImage,
        RAMDisk,
        PhysicalDisk,
        VolumeDisk,
    }

    public class BaseDisk
    {
        public DiskType DiskType { get; set; }
        public string Path { get; set; }
        public int Index { get; set; }

        public override string ToString()
        {
            return $"{DiskType}:{Index}@{Path}";
        }
    }

    public class Target
    {
        public string Name { get; set; }
        public List<BaseDisk> Disks { get; set; }
        public override string ToString()
        {
            return Name;
        }
    }

    public List<Target> Targets { get; set; }

    public void Load(string filePath)
    {
        Targets = new List<Target>();
        Target currTarget = null;
        var lines = File.ReadAllLines(filePath);
        foreach (var line in lines)
        {
            string diskLine;
            int targetIdx = line.IndexOf('=');
            if (targetIdx > 0)
            {
                currTarget = new Target();
                Targets.Add(currTarget);
                currTarget.Disks = new List<BaseDisk>();
                currTarget.Name = line.Substring(0, targetIdx).Trim();
                diskLine = line.Substring(targetIdx + 1, line.Length - targetIdx -1).Trim();
            }
            else
            {
                diskLine = line.Trim();
            }

            foreach (var diskstr in diskLine.Split(','))
            {
                if(string.IsNullOrEmpty(diskstr))
                    continue;
                BaseDisk disk = new BaseDisk();
                var diskCfg = diskstr.Split(':');
                disk.DiskType = (DiskType)Enum.Parse(typeof(DiskType), diskCfg[0]);
                switch (disk.DiskType)
                {
                    case DiskType.PhysicalDisk:
                        disk.Index = int.Parse(diskCfg[1]);
                        break;
                    default:
                        throw new Exception("no support disk type");
                }

                currTarget.Disks.Add(disk);
            }
        }
    }
}