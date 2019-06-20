//-----------------------------------------------------------------------------
// FILE:        Program.cs
// OWNER:       Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Application entry point.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;

using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.Test.Messaging
{
    /// <summary>
    /// Application entry point.
    /// </summary>
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Helper.InitializeApp(Assembly.GetExecutingAssembly());
            Config.SetConfigPath(Helper.GetEntryAssembly());
            NetTrace.Start();

            NativeSysLogProvider.CreateLogs("MessagingTest");
            SysLog.LogProvider = new NativeSysLogProvider("MessagingTest");

            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}