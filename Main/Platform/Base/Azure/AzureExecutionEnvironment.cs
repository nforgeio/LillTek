//-----------------------------------------------------------------------------
// FILE:        AzureExecutionEnvironment.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the current Azure execution environment.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;

using LillTek.Common;

namespace LillTek.Azure
{
    /// <summary>
    /// Describes the current Azure execution environment.
    /// </summary>
    public enum AzureExecutionEnvironment
    {
        /// <summary>
        /// The execution environment could not be determined.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The application or has been launched outside the context of Windows Azure
        /// (e.g. is a normal Windows process).
        /// </summary>
        Process,

        /// <summary>
        /// The application is running in the Azure emulator.
        /// </summary>
        Emulator,

        /// <summary>
        /// The application is running on a Windows Azure virtual machine.
        /// </summary>
        AzureVM
    }
}
