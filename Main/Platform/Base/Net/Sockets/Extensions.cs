//-----------------------------------------------------------------------------
// FILE:        Extensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Network related extensions.

// Note that the values defined in these enums map directly to the values
// defined by the DNS protocol.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Network related extensions.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Filters the set of IP addresses and returns an array of the IPv4 addresses found.
        /// </summary>
        /// <param name="addresses">The input addresses.</param>
        /// <returns>The filtered array of output IPv4 addresses.</returns>
        /// <remarks>
        /// <para>
        /// This method is useful in later operating systems such as Windows 7 which is
        /// starting to return IPv6 addresses in some situations.
        /// </para>
        /// </remarks>
        public static IPAddress[] IPv4Only(this IEnumerable<IPAddress> addresses)
        {
            var list = new List<IPAddress>();

            foreach (var address in addresses)
                if (address.AddressFamily == AddressFamily.InterNetwork)
                    list.Add(address);

            return list.ToArray();
        }
    }
}
