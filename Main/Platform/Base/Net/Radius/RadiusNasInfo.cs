//-----------------------------------------------------------------------------
// FILE:        RadiusNasInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the credentials for a Network Access Service (NAS)
//              device instance.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Radius
{
    /// <summary>
    /// Holds information about a Network Access Service (NAS) device instance
    /// including its credentials.
    /// </summary>
    /// <remarks>
    /// Each device wishing to authenticate against a RADIUS server must
    /// share a secret with the server.  The RADIUS server will use the
    /// device's IP address to map to the secret.  This class describes
    /// the mapping between a device's IP address, the secret, and optionally,
    /// the DNS host name that maps to the address.
    /// </remarks>
    public sealed class RadiusNasInfo
    {
        /// <summary>
        /// The optional DNS host name for the NAS device (or <c>null</c>).
        /// </summary>
        public readonly string Host;

        /// <summary>
        /// The IP address of the NAS device.
        /// </summary>
        public IPAddress Address;

        /// <summary>
        /// The shared secret.
        /// </summary>
        public readonly string Secret;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="address">The NAS device's IP address.</param>
        /// <param name="secret">The shared secret.</param>
        public RadiusNasInfo(IPAddress address, string secret)
        {
            this.Host    = null;
            this.Address = address;
            this.Secret  = secret;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="host">The host name (or <c>null</c>).</param>
        /// <param name="secret">The shared secret.</param>
        public RadiusNasInfo(string host, string secret)
        {
            this.Host    = host;
            this.Address = IPAddress.Any;
            this.Secret  = secret;
        }
    }
}
