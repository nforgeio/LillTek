//-----------------------------------------------------------------------------
// FILE:        MsgRouterMetrics.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Router runtime performance metrics.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Router runtime performance metrics.
    /// </summary>
    /// <threadsafety instance="true" />
    public sealed class MsgRouterMetrics
    {
        /// <summary>
        /// Counts the message session retries.
        /// </summary>
        public readonly InterlockedCounter SessionRetries = new InterlockedCounter();

        /// <summary>
        /// Counts the message session timeouts.
        /// </summary>
        public readonly InterlockedCounter SessionTimeouts = new InterlockedCounter();
    }
}
