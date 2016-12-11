/* Copyright (C) 2012-2015 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Utilities
{
    public class SocketUtils
    {
        /// <summary>
        /// Socket will be forcefully closed, all pending data will be ignored, and socket will be deallocated.
        /// </summary>
        public static void ReleaseSocket(Socket socket)
        {
            if (socket != null)
            {
                if (socket.Connected)
                {
                    try
                    {
                        socket.Shutdown(SocketShutdown.Both);
                        socket.Disconnect(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                    catch (SocketException)
                    { }
                }
                socket.Close();
                socket = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }
}
