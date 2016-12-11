/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Security.Principal;

namespace ISCSIConsole
{
    public class SecurityHelper
    {
        public static bool IsAdministrator()
        {
            WindowsIdentity windowsIdentity = null;
            try
            {
                windowsIdentity = WindowsIdentity.GetCurrent();
            }
            catch
            {
                return false;
            }
            WindowsPrincipal windowsPrincipal = new WindowsPrincipal(windowsIdentity);
            return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
