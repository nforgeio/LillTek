//-----------------------------------------------------------------------------
// FILE:        AppLogMessenger.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a reliable persistent messaging solution using the
//              AppLog class.

using System;
using System.Collections.Generic;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.Messaging
{
#if TODO

    /// <summary>
    /// Defines the startup configuration for the <see cref="IReliableMessenger"/> interface.
    /// </summary>
    public class ReliableMessengerConfig
    {
        public Dictionary<string, ITopologyProvider> ClusterMap;

        public TimeSpan PollInterval;

        public TimeSpan RetryWait;
    }

    /// <summary>
    /// Implements a reliable persistent messaging solution using the <see cref="AppLog" />
    /// class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 
    /// </para>
    /// </remarks>
    public class IReliableMessenger
    {
        public IReliableMessenger(AppLogWriter logWriter, AppLogReader logReader, ReliableMessengerConfig config)
        {

        }
    }

#endif
}
