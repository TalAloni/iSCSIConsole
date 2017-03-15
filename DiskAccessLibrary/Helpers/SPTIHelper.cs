using Microsoft.Win32.SafeHandles;
using System.Collections.Generic;
using System.IO;

namespace DiskAccessLibrary
{
    public partial class SPTIHelper
    {

        public static List<SPTIDevice> GetSPTIDevices()
        {
            List<SPTIDevice> result = new List<SPTIDevice>();
            List<string> devicePathList = new List<string>();
            List<string> tapeDeviceList = DeviceInterfaceUtils.GetDevicePathList(DeviceInterfaceUtils.TapeClassGuid);
            List<string> mediumChangerDeviceList = DeviceInterfaceUtils.GetDevicePathList(DeviceInterfaceUtils.MediumChangerClassGuid);
            devicePathList.AddRange(tapeDeviceList);
            devicePathList.AddRange(mediumChangerDeviceList);
            foreach (string devicePath in devicePathList)
            {
                SPTIDevice sptiDevice = new SPTIDevice(devicePath);
                result.Add(sptiDevice);
            }
            return result;
        }

    }
}
