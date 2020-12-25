/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System.Runtime.InteropServices;

namespace DiskAccessLibrary
{
    public enum SecurityPrivilegeName
    {
        SeRestorePrivilege,
        SeBackupPrivilege,
        SeManageVolumePrivilege,
    }

    public class SecurityUtils
    {
        public const int TOKEN_ADJUST_PRIVILEGES = 0x00000020;
        public const int TOKEN_QUERY = 0x00000008;
        
        public const int SE_PRIVILEGE_ENABLED = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public int LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TOKEN_PRIVILEGES
        {
            public int PrivilegeCount;
            public LUID Luid;
            public int Attributes;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        public static extern int OpenProcessToken(int ProcessHandle, int DesiredAccess,
        ref int tokenhandle);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern int GetCurrentProcess();

        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        public static extern int LookupPrivilegeValue(string lpsystemname, string lpname,
        [MarshalAs(UnmanagedType.Struct)] ref LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int AdjustTokenPrivileges(int tokenhandle, int disableprivs, [MarshalAs(UnmanagedType.Struct)]ref TOKEN_PRIVILEGES Newstate,
            int bufferlength, int PreivousState, int Returnlength);

        public static bool ObtainManageVolumePrivilege()
        {
            return ObtainPrivilege(SecurityPrivilegeName.SeManageVolumePrivilege);
        }

        public static bool ObtainPrivilege(SecurityPrivilegeName privilegeName)
        {
            int tokenHandle = 0;
            int retval = 0;

            TOKEN_PRIVILEGES tokenPrivileges = new TOKEN_PRIVILEGES();
            LUID privilegeLuid = new LUID();

            retval = OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ref tokenHandle);
            if (retval == 0) //If the function succeeds, the return value is nonzero
            {
                return false;
            }

            retval = LookupPrivilegeValue(null, privilegeName.ToString(), ref privilegeLuid);
            if (retval == 0) //If the function succeeds, the return value is nonzero
            {
                return false;
            }

            tokenPrivileges.PrivilegeCount = 1;
            tokenPrivileges.Attributes = SE_PRIVILEGE_ENABLED;
            tokenPrivileges.Luid = privilegeLuid;

            retval = AdjustTokenPrivileges(tokenHandle, 0, ref tokenPrivileges, 0, 0, 0);
            if (retval == 0) // If the function succeeds, the return value is nonzero 
            {
                return false;
            }
            else
            {
                // http://msdn.microsoft.com/en-us/library/windows/desktop/aa375202%28v=vs.85%29.aspx
                // GetLastError returns one of the following values when the function succeeds:
                // ERROR_SUCCESS, ERROR_NOT_ALL_ASSIGNED

                int errorCode = Marshal.GetLastWin32Error();
                return (errorCode == (int)Win32Error.ERROR_SUCCESS);
            }
        }
    }
}
