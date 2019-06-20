//-----------------------------------------------------------------------------
// FILE:        NamespaceDoc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: NDoc namespace documentation

#if NDOC

using System;
using System.IO;
using System.Net;

using FreeSWITCH;

using LillTek.Common;

namespace LillTek.Telephony.Common {

    /// <summary>
    /// The <b>LillTek.Telephony.dll</b> assembly includes client side NeonSwitch related 
    /// definitions.  These definitions are within the <b>LillTek.Telephony.Common</b>
    /// namespace (the same as the server side definitions).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The major LillTek client side classes include the <see cref="SwitchHelper"/> class which
    /// provides global NeonSwitch related utilities, the <see cref="SwitchEventCode" /> and
    /// <see cref="ChannelState" /> enumerations which will be used to describe what's happening
    /// in the switch, and the <see cref="SwitchConnection" /> class which provides a mechanism
    /// for external applications to establish a connection to a NeonSwitch node to monitor and
    /// control it.
    /// </para>
    /// </remarks>
    public class NeonSwitchClientOverviewDoc {

    }
}

#endif // NUNIT
