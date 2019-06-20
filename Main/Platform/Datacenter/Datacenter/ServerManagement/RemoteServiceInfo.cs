//-----------------------------------------------------------------------------
// FILE:        RemoteServiceInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a remote service.

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
    /// Describes a remote service.
    /// </summary>
    [DataContract(Namespace = ServerManager.ContractNamespace)]
    public sealed class RemoteServiceInfo
    {
        /// <summary>
        /// The service name.
        /// </summary>
        [DataMember]
        public string ServiceName { get; set; }

        /// <summary>
        /// The current service state.
        /// </summary>
        [DataMember]
        public ServiceState State { get; set; }

        /// <summary>
        /// Indicates how the service is configured to be started.
        /// </summary>
        [DataMember]
        public RemoteServiceStartMode StartMode { get; set; }

        /// <summary>
        /// Default constructor used by serializers.
        /// </summary>
        public RemoteServiceInfo()
        {
        }

        /// <summary>
        /// Constructs an instance from the parameters passed.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <param name="state">The service's current state.</param>
        /// <param name="startMode">The service start mode.</param>
        public RemoteServiceInfo(string serviceName, ServiceState state, RemoteServiceStartMode startMode)
        {
            this.ServiceName = serviceName;
            this.State       = state;
            this.StartMode   = startMode;
        }
    }
}
