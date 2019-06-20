//-----------------------------------------------------------------------------
// FILE:        ServiceInstallProperties.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes native Windows service installation properties.

using System;
using System.Runtime.InteropServices;

using System.ServiceProcess;
using System.Configuration;
using System.Windows.Forms;

using LillTek.Common;
using LillTek.Windows;

using Handle = System.IntPtr;

namespace LillTek.Service
{
    /// <summary>
    /// Describes native Windows service installation properties.
    /// </summary>
    public class ServiceInstallProperties
    {
        /// <summary>
        /// The service name.
        /// </summary>
        public string Name;

        /// <summary>
        /// The service startup mode.
        /// </summary>
        public ServiceStartMode StartMode = ServiceStartMode.Manual;

        /// <summary>
        /// The security account type the service should run under.
        /// </summary>
        public ServiceAccount Account = ServiceAccount.LocalSystem;

        /// <summary>
        /// The security account name.
        /// </summary>
        public string UserName = null;

        /// <summary>
        /// The security account password.
        /// </summary>
        public string Password = null;

        /// <summary>
        /// Constructs a ServiceInstallProperties instance.
        /// </summary>
        /// <param name="name">The service name.</param>
        public ServiceInstallProperties(string name)
        {
            this.Name = name;
        }
    }
}
