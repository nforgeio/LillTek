//-----------------------------------------------------------------------------
// FILE:        WmiResultSet.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Encapsulates the set of results returned for each WMI query sent
//              to a remote server.  These results are keyed by the case insensitive
//              query name.

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
    /// Encapsulates the set of results returned for each WMI query sent
    /// to a remote server.  These results are keyed by the case insensitive
    /// query name.
    /// </summary>
    [DataContract(Namespace = ServerManager.ContractNamespace)]
    public sealed class WmiResultSet
    {
        /// <summary>
        /// The named query results.
        /// </summary>
        [DataMember]
        public Dictionary<string, WmiResult> Results { get; set; }

        /// <summary>
        /// Initializes an empty result set.  Query results can be added by calling
        /// the <see cref="Add" /> method.
        /// </summary>
        public WmiResultSet()
        {
            this.Results = new Dictionary<string, WmiResult>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds a query result to the set.
        /// </summary>
        /// <param name="result">The result.</param>
        public void Add(WmiResult result)
        {
            Results.Add(result.QueryName, result);
        }

        /// <summary>
        /// Returns the result generated for the named query (or <c>null</c>).
        /// </summary>
        /// <param name="queryName">The query name (case insensitive).</param>
        /// <returns>The query result or <c>null</c> if none is found.</returns>
        public WmiResult this[string queryName]
        {
            get
            {
                WmiResult result;

                if (Results.TryGetValue(queryName, out result))
                    return result;
                else
                    return null;
            }
        }
    }
}
