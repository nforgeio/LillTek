//-----------------------------------------------------------------------------
// FILE:        NetworkConfig.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Initializes default network settings from values in the Network
//              configuration section.

using System;
using System.Net;
using System.Net.Sockets;

namespace LillTek.Common
{
    /// <summary>
    /// Initializes default network settings from values in the <b>Network</b>
    /// configuration section.
    /// </summary>
    public static class NetworkConfig
    {
        /// <summary>
        /// Initializes default network settings from values in the <b>Network</b>
        /// configuration section.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This class currently supports the following settings and defaults:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>DefaultConnectionLimit</b></term>
        ///     <description>
        ///     The maximum number of concurrent connections allowed by <see cref="ServicePoint"/> object
        ///     (defaults to 100).
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>MaxServicePointIdleTime</b></term>
        ///     <description>
        ///     The maximum time a <see cref="ServicePoint"/> with no connections will wait for
        ///     additional connections before being released (defaults to 1 minute).
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>DnsResolutionTTL</b></term>
        ///     <description>
        ///     The maximum time that a DNS resolution will remain cached (defaults to 2 minutes).
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>EnableDnsRoundRobin</b></term>
        ///     <description>
        ///     Specifies whether DNS resolutions for names with multiple associated records
        ///     will round-robin through the available records or just return the first 
        ///     {defaults to <c>false</c>).
        ///     </description>
        /// </item>
        /// </list>
        /// <note>
        /// This method intializes some of the global .NET <see cref="ServicePointManager"/> properties.
        /// </note>
        /// </remarks>
        public static void Initialize()
        {
            var config = new Config("Network");

            ServicePointManager.DefaultConnectionLimit  = config.Get("DefaultConnectionLimit", 100);
            ServicePointManager.MaxServicePointIdleTime = TimeSpanToMilliseconds(config.Get("MaxServicePointIdleTime", TimeSpan.FromMinutes(1)));
            ServicePointManager.DnsRefreshTimeout       = TimeSpanToMilliseconds(config.Get("DnsResolutionTTL", TimeSpan.FromMinutes(2)));
        }

        /// <summary>
        /// Converts a <see cref="TimeSpan"/> into integer milliseconds.
        /// </summary>
        /// <param name="interval"></param>
        /// <returns></returns>
        private static int TimeSpanToMilliseconds(TimeSpan interval)
        {
            var milliseconds = interval.TotalMilliseconds;

            if (milliseconds < 0)
            {
                return 0;
            }
            else if (milliseconds > int.MaxValue)
            {
                return int.MaxValue;
            }
            else
            {
                return (int)milliseconds;
            }
        }
    }
}
