//-----------------------------------------------------------------------------
// FILE:        RemoteDrive.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a remote disk drive.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

using LillTek.Common;
using LillTek.Service;

namespace LillTek.Datacenter.ServerManagement
{
    /// <summary>
    /// Describes a remote disk drive.
    /// </summary>
    [DataContract(Namespace = ServerManager.ContractNamespace)]
    public sealed class RemoteDrive
    {
        /// <summary>
        /// The drive name (with terminating colon).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Indicates the drive type.
        /// </summary>
        public RemoteDriveType Type { get; set; }

        /// <summary>
        /// The total size of the drive in bytes.
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// The total free space on the drive in bytes.
        /// </summary>
        public long FreeSize { get; set; }

        /// <summary>
        /// Default constructor to be used only by serializers.
        /// </summary>
        public RemoteDrive()
        {
        }

        /// <summary>
        /// Constructs an instance from the parameters passed.
        /// </summary>
        /// <param name="name">The drive name (including the terminating colon).</param>
        /// <param name="type">The druve type.</param>
        /// <param name="totalSize">The total size of the drive in bytes.</param>
        /// <param name="freeSize">The total free space on the drive in bytes.</param>
        public RemoteDrive(string name, RemoteDriveType type, long totalSize, long freeSize)
        {
            this.Name      = name;
            this.Type      = type;
            this.TotalSize = totalSize;
            this.FreeSize  = freeSize;
        }
    }
}
