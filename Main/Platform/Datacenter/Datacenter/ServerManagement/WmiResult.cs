//-----------------------------------------------------------------------------
// FILE:        WmiResult.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The WMI query result associating the set of WmiObject
//              instances returned with the named WMI WQL query submitted to the
//              remote server.

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
    /// The WMI query result associating the set of <see cref="WmiObject" />
    /// instances returned with the named WMI WQL query submitted to the
    /// remote server.
    /// </summary>
    [DataContract(Namespace = ServerManager.ContractNamespace)]
    public sealed class WmiResult
    {
        /// <summary>
        /// Name of the <see cref="WmiQuery" /> submitted to the remote
        /// server that produced this result.
        /// </summary>
        [DataMember]
        public string QueryName { get; set; }

        /// <summary>
        /// The collection of <see cref="WmiObject" /> instances returned
        /// by the query.
        /// </summary>
        [DataMember]
        public List<WmiObject> Objects;

        /// <summary>
        /// Default constructor used by serializers.
        /// </summary>
        public WmiResult()
        {
            this.Objects = new List<WmiObject>();
        }

        /// <summary>
        /// Initializes the instance with the objects returned in the searcher passed.
        /// </summary>
        /// <param name="queryName">Name of the query.</param>
        /// <param name="searcher">Management object searcher holding the WMI query result objects.</param>
        public WmiResult(string queryName, ManagementObjectSearcher searcher)
        {
            this.QueryName = queryName;
            this.Objects   = new List<WmiObject>();

            foreach (ManagementObject obj in searcher.Get())
                this.Objects.Add(new WmiObject(obj));
        }
    }
}
