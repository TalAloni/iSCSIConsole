using Microsoft.Win32.SafeHandles;
using System.Collections.Generic;
using System.IO;

namespace DiskAccessLibrary
{
    public partial class SPTIHelper
    {
        public static List<DeviceInfo> GetSPTIDevices()
        {
            List<DeviceInfo> result = new List<DeviceInfo>();
            List<DeviceInfo> tapeDeviceList = DeviceInterfaceUtils.GetDeviceList(DeviceInterfaceUtils.TapeClassGuid);
            List<DeviceInfo> mediumChangerDeviceList = DeviceInterfaceUtils.GetDeviceList(DeviceInterfaceUtils.MediumChangerClassGuid);
            result.AddRange(tapeDeviceList);
            result.AddRange(mediumChangerDeviceList);
            return result;
        }
    }
}
