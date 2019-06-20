//-----------------------------------------------------------------------------
// FILE:        LocalEPMap.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds a set of logical endpoints and then provides a method
//              to determine if any of these endpoints match a given endpoint.

using System;
using System.Collections.Generic;

using LillTek.Common;

// $todo(jeff.lill): 
//
// This algorithm won't scale well for large numbers of 
// routes.  This isn't a big of a problem as the similar
// LogicalRouteTable issue since most applications will
// deploy with very few locality map entries.

namespace LillTek.Messaging
{

    /// <summary>
    /// Holds a set of logical endpoints and then provides a method to determine 
    /// if any of these endpoints match a given endpoint.  Used for implementing
    /// routing locality.
    /// </summary>
    /// <threadsafety instance="false" />
    public sealed class LocalEPMap
    {
        private List<MsgEP> endpoints;

        /// <summary>
        /// Constructor.
        /// </summary>
        public LocalEPMap()
        {
            endpoints = new List<MsgEP>();
        }

        /// <summary>
        /// Adds a logical endpoint to the map.
        /// </summary>
        /// <param name="logicalEP">The logical <see cref="MsgEP" />.</param>
        /// <remarks>
        /// <note>Logical endpoints with wildcards can be added to the set.</note>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the endpoint passed is not logical.</exception>
        public void Add(MsgEP logicalEP)
        {
            if (!logicalEP.IsLogical)
                throw new ArgumentException("Logical endpoint expected.", "logicalEP");

            endpoints.Add(logicalEP);
        }

        /// <summary>
        /// Determines whether the endpoint passed matches one or more of the
        /// endpoints in the set.
        /// </summary>
        /// <param name="logicalEP">The logical <see cref="MsgEP" /> to be tested.</param>
        /// <returns><c>true</c> if there's a match.</returns>
        /// <remarks>
        /// <note>Logical endpoints with wildcards can be passed.</note>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the endpoint passed is not logical.</exception>
        public bool Match(MsgEP logicalEP)
        {
            if (!logicalEP.IsLogical)
                throw new ArgumentException("Logical endpoint expected.", "logicalEP");

            for (int i = 0; i < endpoints.Count; i++)
                if (logicalEP.LogicalMatch(logicalEP))
                    return true;

            return false;
        }
    }
}
