//-----------------------------------------------------------------------------
// FILE:        AzureRoleType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Identifies the possible types of Windows Azure service roles.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;

namespace LillTek.Azure
{
    /// <summary>
    /// Identifies the possible types of Windows Azure service roles.
    /// </summary>
    public enum AzureRoleType
    {
        /// <summary>
        /// The Azure role type could not be determined.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The role hosts a a web site or web service.
        /// </summary>
        Web,

        /// <summary>
        /// The role hosts a worker process.
        /// </summary>
        Worker,

        /// <summary>
        /// The role hosts a virtual machine.
        /// </summary>
        VirtualMachine,

        /// <summary>
        /// The application is a process launched by an Azure role.
        /// </summary>
        Process,
    }
}
