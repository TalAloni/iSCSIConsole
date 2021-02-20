using System.Runtime.InteropServices;

namespace ISCSIConsole
{
    public class RuntimeHelper
    {
        public static bool IsWin32
        {
            get
            {
#if NET472 || NETCOREAPP
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#else
                return true;
#endif
            }
        }
    }
}
