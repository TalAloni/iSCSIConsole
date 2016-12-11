/* Copyright (C) 2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ISCSIConsole
{
    public partial class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            if (args.Length > 0)
            {
                if (args[0] == "/help")
                {
                    
                }
                if (args[0] == "/log")
                {
                    string path = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
                    if (!path.EndsWith(@"\"))
                    {
                        path += @"\";
                    }
                    path += String.Format("Log {0}.txt", DateTime.Now.ToString("yyyy-MM-dd HH-mm"));
                    bool success = OpenLogFile(path);
                    if (!success)
                    {
                        MessageBox.Show("Cannot open log file", "Error");
                    }
                }
                else
                {
                    StringBuilder builder = new StringBuilder();
                    builder.AppendLine("Command line arguments:");
                    builder.AppendLine("/log - will write log file to executable directory");
                    MessageBox.Show(builder.ToString(), "Error");
                    return;
                }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        public static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            HandleUnhandledException(e.Exception);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject != null)
            {
                Exception ex = (Exception)e.ExceptionObject;
                HandleUnhandledException(ex);
            }
        }

        private static void HandleUnhandledException(Exception ex)
        {
            string message = String.Format("Exception: {0}: {1} Source: {2} {3}", ex.GetType(), ex.Message, ex.Source, ex.StackTrace);
            MessageBox.Show(message, "Error");
            Application.Exit();
        }
    }
}
