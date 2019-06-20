//-----------------------------------------------------------------------------
// FILE:        DialedEndpointList.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: The list of dialed DialedEndpoints to use when orginating or
//              bridging a call.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// The list of <see cref="DialedEndpoint"/>s to use when orginating or
    /// bridging a call.
    /// </summary>
    public class DialedEndpointList : List<DialedEndpoint>
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public DialedEndpointList()
        {
        }

        /// <summary>
        /// Constructs a list with an initial capacity.
        /// </summary>
        /// <param name="capacity">The capacity.</param>
        public DialedEndpointList(int capacity)
            : base(capacity)
        {
        }
    }
}
