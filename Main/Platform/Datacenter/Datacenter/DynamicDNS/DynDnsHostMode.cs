//-----------------------------------------------------------------------------
// FILE:        DynDnsHostMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes how the Dynamic DNS Server should respond to DNS requests
//              for a particular host name.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Messaging;
using LillTek.Net.Sockets;

namespace LillTek.Datacenter
{
    /// <summary>
    /// Describes how the Dynamic DNS Server should respond to DNS requests for a particular host name.
    /// </summary>
    public enum DynDnsHostMode
    {
        /// <summary>
        /// Used internally. Do not use.
        /// </summary>
        Unknown,

        /// <summary>
        /// The server will return a single A record for the request.  If multiple endpoints
        /// are registered, the server will randomly choose one of the endpoints to load-balance
        /// traffic across the endpoints.
        /// </summary>
        Address,

        /// <summary>
        /// The server will return A records for all registered endpoints.  This is useful for 
        /// discovery scenarios.
        /// </summary>
        AddressList,

        /// <summary>
        /// The server will return a single CNAME record if the host entry is registered by name,
        /// otherwise a single A record will be returned.
        /// </summary>
        CName,

        /// <summary>
        /// The server will return a response that includes MX reecords for all registered hosts.
        /// The MX records will be hardcoded with preference values of zero.
        /// </summary>
        MX,
    }
}
