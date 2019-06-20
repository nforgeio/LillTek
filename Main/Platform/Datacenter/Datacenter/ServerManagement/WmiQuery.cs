//-----------------------------------------------------------------------------
// FILE:        WmiQuery.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The named WMI WQL query to be transmitted to a remote machine.

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
    /// The named WMI WQL query to be transmitted to a remote machine.
    /// </summary>
    [DataContract(Namespace = ServerManager.ContractNamespace)]
    public sealed class WmiQuery
    {
        /// <summary>
        /// The query's name.
        /// </summary>
        /// <remarks>
        /// Multiple named WMI queries can be transmitted to a remote server
        /// in a single sevice manager request.  This name is used in <see cref="WmiResultSet" />
        /// to correlate responses to queries.
        /// </remarks>
        [DataMember]
        public string Name { get; set; }

        /// <summary>
        /// The WMI query string.
        /// </summary>
        /// <remarks>
        /// A WMI query string is a subset of the SQL language used to select values
        /// from the WMI database on the server.  This language is called <b>WMI Query Langage (WQL)</b>
        /// by Microsoft.  The specification for this is located <a href="http://msdn.microsoft.com/en-us/library/aa394606(VS.85).aspx">here</a>.
        /// </remarks>
        [DataMember]
        public string Query { get; set; }

        /// <summary>
        /// Default constructor used by serializers.
        /// </summary>
        public WmiQuery()
        {
        }

        /// <summary>
        /// Constructs a query from the parameters passed.
        /// </summary>
        /// <param name="name">The query name.</param>
        /// <param name="query">The query string.</param>
        /// <remarks>
        /// A WMI query string is a subset of the SQL language used to select values
        /// from the WMI database on the server.  This language is called <b>WMI Query Langage (WQL)</b>
        /// by Microsoft.  The specification for this is located <a href="http://msdn.microsoft.com/en-us/library/aa394606(VS.85).aspx">here</a>.
        /// </remarks>
        public WmiQuery(string name, string query)
        {
            this.Name  = name;
            this.Query = query;
        }
    }
}
