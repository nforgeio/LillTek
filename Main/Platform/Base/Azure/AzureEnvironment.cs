//-----------------------------------------------------------------------------
// FILE:        AzureEnvironmentType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible Azure service deployment environments.

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
    /// Enumerates the possible Azure service deployment environments.
    /// </summary>
    public enum AzureEnvironment
    {
        /// <summary>
        /// The deployment environment could not be determined.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The service is deployed to a <b>development</b> environment.
        /// </summary>
        Dev,

        /// <summary>
        /// The service is deployed to a <b>test</b> environment.
        /// </summary>
        Test,

        /// <summary>
        /// The service is deployed to a <b>internal beta</b> environment.
        /// </summary>
        Int,

        /// <summary>
        /// The service is deployed to a <b>public beta</b> environment.
        /// </summary>
        Beta,

        /// <summary>
        /// The service is deployed to a <b>production</b> environment.
        /// </summary>
        Prod
    }
}
