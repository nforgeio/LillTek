//-----------------------------------------------------------------------------
// FILE:        RemoteFile.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a file on a remote server.

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
    /// Describes a file on a remote server.
    /// </summary>
    [DataContract(Namespace = ServerManager.ContractNamespace)]
    public sealed class RemoteFile
    {
        /// <summary>
        /// The fully qualified file path.
        /// </summary>
        [DataMember]
        public string Path { get; set; }

        /// <summary>
        /// The file <see cref="FileAttributes" />.
        /// </summary>
        [DataMember]
        public FileAttributes Attributes { get; set; }

        /// <summary>
        /// Time the file was created (UTC).
        /// </summary>
        [DataMember]
        public DateTime CreationTimeUtc { get; set; }

        /// <summary>
        /// Last time the file was modified (UTC).
        /// </summary>
        [DataMember]
        public DateTime LastWriteTimeUtc { get; set; }

        /// <summary>
        /// Last time the file was accessed (UTC).
        /// </summary>
        [DataMember]
        public DateTime LastAccessTimeUtc { get; set; }

        /// <summary>
        /// Size of the file in bytes.
        /// </summary>
        [DataMember]
        public long Length { get; set; }

        /// <summary>
        /// Default constructor to be used by serializers.
        /// </summary>
        public RemoteFile()
        {
        }

        /// <summary>
        /// Constructs an instance from the file information passed.
        /// </summary>
        /// <param name="fileInfo">The <see cref="FileInfo" />.</param>
        public RemoteFile(FileInfo fileInfo)
        {
            this.Path              = fileInfo.FullName;
            this.Attributes        = fileInfo.Attributes;
            this.Length            = fileInfo.Length;
            this.CreationTimeUtc   = fileInfo.CreationTimeUtc;
            this.LastAccessTimeUtc = fileInfo.LastAccessTimeUtc;
            this.LastWriteTimeUtc  = fileInfo.LastWriteTimeUtc;
        }
    }
}
