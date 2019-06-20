//-----------------------------------------------------------------------------
// FILE:        IPFilterItem.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Specifies an IP address filter item.

using System;
using System.Net;

using LillTek.Common;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Specifies an IP filter item to be used within a <see cref="IPFilter" />
    /// object for indicating which client IP addresses are to be granted access
    /// to a service and which are to be denied access.
    /// </summary>
    /// <remarks>
    /// <note>
    /// At this time, only IPv4 addresses are supported.
    /// </note>
    /// </remarks>
    public struct IPFilterItem
    {
        private bool    grant;          // True if this filter item grants access, false to deny access
        private int     subnetMask;     // Non-zero if this is a subnet filter
        private int     address;        // The IP address 

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="grant"><c>true</c> if this item grants access for clients at an IP address.</param>
        /// <param name="address">The IP address.</param>
        public IPFilterItem(bool grant, IPAddress address)
        {
            this.grant      = grant;
            this.subnetMask = 0;
            this.address    = NetHelper.ToInt32(address);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="grant"><c>true</c> if this item grants access for clients at an IP subnet.</param>
        /// <param name="address">The IP address.</param>
        /// <param name="networkPrefix">The number of leading network bits in the address.</param>
        /// <remarks>
        /// <b>networkBits</b> is the number of bits specified after the slash (/) in the 
        /// classless Inter-Domain Routing (CIDR) notation for a subnet (ie. the <b>16</b> in
        /// <b>206.44.123.10/16</b>).
        /// </remarks>
        public IPFilterItem(bool grant, IPAddress address, int networkPrefix)
        {
            this.grant      = grant;
            this.subnetMask = NetHelper.GetSubnetMask(networkPrefix);
            this.address    = NetHelper.ToInt32(address) & subnetMask;
        }

        /// <summary>
        /// Determines whether the filter item applies to the specified IP address.
        /// </summary>
        /// <param name="address">The IPv4 address to be tested.</param>
        /// <returns>
        /// A <see cref="TriState" /> is returned.  <see cref="TriState.True" /> indicates
        /// that the IP address is explicitly granted access by the filter item, <see cref="TriState.False" />
        /// indicates that the address is explicitly deined access, and <see cref="TriState.Unknown" />
        /// indicates that the filter item does not apply to the address.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown for non-IPv4 addresses.</exception>
        public TriState GrantAccess(IPAddress address)
        {
            return GrantAccess(NetHelper.ToInt32(address));
        }

        /// <summary>
        /// Determines whether the filter item applies to the specified IP address.
        /// </summary>
        /// <param name="address">The 32-bit IPv4 address to be tested.</param>
        /// <returns>
        /// A <see cref="TriState" /> is returned.  <see cref="TriState.True" /> indicates
        /// that the IP address is explicitly granted access by the filter item, <see cref="TriState.False" />
        /// indicates that the address is explicitly deined access, and <see cref="TriState.Unknown" />
        /// indicates that the filter item does not apply to the address.
        /// </returns>
        internal TriState GrantAccess(int address)
        {
            if (subnetMask != 0)
            {
                if ((address & subnetMask) == this.address)
                    return grant;
                else
                    return TriState.Unknown;
            }
            else
            {
                if (this.address != address)
                    return TriState.Unknown;

                return grant;
            }
        }
    }
}
