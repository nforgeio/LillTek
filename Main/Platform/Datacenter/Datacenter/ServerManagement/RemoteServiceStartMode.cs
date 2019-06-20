//-----------------------------------------------------------------------------
// FILE:        RemoteServiceStartMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible service start modes.

using System;
using System.Runtime.Serialization;

namespace LillTek.Datacenter.ServerManagement
{
    /// <summary>
    /// Enumerates the possible service start modes.
    /// </summary>
    [DataContract(Namespace = ServerManager.ContractNamespace)]
    public enum RemoteServiceStartMode
    {
        /// <summary>
        /// The service start mode is not known.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The service is disabled.
        /// </summary>
        Disabled = 1,

        /// <summary>
        /// The service is started manually.
        /// </summary>
        Manual = 2,

        /// <summary>
        /// The service is started automatically when the operating system starts.
        /// </summary>
        Automatic = 3
    }
}
