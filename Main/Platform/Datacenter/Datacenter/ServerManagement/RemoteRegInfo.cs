//-----------------------------------------------------------------------------
// FILE:        RemoteRegInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Information about a remote registry key or value.

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
    /// Information about a remote registry key or value.
    /// </summary>
    [DataContract(Namespace = ServerManager.ContractNamespace)]
    public sealed class RemoteRegInfo
    {
        /// <summary>
        /// The fully qualified name of the key or value.
        /// </summary>
        [DataMember]
        public string Name { get; set; }

        /// <summary>
        /// <c>true</c> if the item is a registry key, false for a value.
        /// </summary>
        [DataMember]
        public bool IsKey { get; set; }

        /// <summary>
        /// A registry value encoded as a string.
        /// </summary>
        [DataMember]
        public string Value { get; set; }

        /// <summary>
        /// One of the WinApi.REG_SZ,... constants describing the
        /// type of a value as stored in the registry.
        /// </summary>
        [DataMember]
        public int ValueType { get; set; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public RemoteRegInfo()
        {
        }
    }
}
