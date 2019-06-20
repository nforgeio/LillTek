//-----------------------------------------------------------------------------
// FILE:        ClientAccessPolicyProtocol.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used by Silverlight applications to specify the protocol
//              (HTTP or TCP) that the platform will use for verifying
//              security access before allowing LiteSocket connections.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Used by Silverlight applications to specify the protocol
    /// (HTTP or TCP) that the platform will use for verifying
    /// security access before allowing <see cref="LiteSocket" />
    /// connections.
    /// </summary>
    /// <remarks>
    /// <note>
    /// Non-Silverlight platforms (including Windows Phone) do not perform
    /// this security check.
    /// </note>
    /// </remarks>
    public enum ClientAccessPolicyProtocol
    {
        /// <summary>
        /// Silverlight will use HTTP on port 80 to retrieve the <b>clientaccesspolicy.xml</b> file.
        /// </summary>
        Http,

        /// <summary>
        /// Silverlight will use a custom TCP protocol on port <b>943</b> to retreive the policy information.
        /// </summary>
        Tcp
    }
}
