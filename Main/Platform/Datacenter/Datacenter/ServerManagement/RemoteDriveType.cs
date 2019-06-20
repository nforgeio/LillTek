//-----------------------------------------------------------------------------
// FILE:        RemoteDriveType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible remote disk drive types.

using System;
using System.IO;
using System.Text;
using System.Management;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceModel;
using System.Runtime.Serialization;

using LillTek.Common;
using LillTek.Service;

namespace LillTek.Datacenter.ServerManagement
{
    /// <summary>
    /// Enumerates the possible remote disk drive types.
    /// </summary>
    [DataContract(Namespace = ServerManager.ContractNamespace)]
    public enum RemoteDriveType
    {
        /// <summary>
        /// The drive type is unknown.
        /// </summary>
        Unknown,

        /// <summary>
        /// Local fixed drive.
        /// </summary>
        LocalFixed,

        /// <summary>
        /// Removable drive.
        /// </summary>
        Removable,

        /// <summary>
        /// CD-ROM drive.
        /// </summary>
        CDROM,
    }
}
