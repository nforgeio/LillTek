//-----------------------------------------------------------------------------
// FILE:        RemoteSpecialFolder.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible special remote folder types.

using System;
using System.Runtime.Serialization;

namespace LillTek.Datacenter.ServerManagement
{
    /// <summary>
    /// Enumerates the possible special remote folder types.
    /// </summary>
    [DataContract(Namespace = LillTek.Datacenter.ServerManagement.ServerManager.ContractNamespace)]
    public enum RemoteSpecialFolder
    {
        /// <summary>
        /// The root of the operating system installation directory.
        /// </summary>
        System,

        /// <summary>
        /// The temporary directory.
        /// </summary>
        Temporary,

        /// <summary>
        /// The program files directory.
        /// </summary>
        ProgramFiles,

        /// <summary>
        /// The server manager's application folder.
        /// </summary>
        ServerManager,
    }
}
