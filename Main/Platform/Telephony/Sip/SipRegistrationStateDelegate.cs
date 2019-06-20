//-----------------------------------------------------------------------------
// FILE:        SipRegistrationStateDelegate.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines delegates called when a SipCore raises its RegistrationChanged event.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Defines delegates called when a <see cref="SipCore" /> raises its <see cref="SipCore.RegistrationChanged" /> event.
    /// </summary>
    /// <param name="sender">The event source (the <see cref="SipCore" />).</param>
    /// <param name="args">The <see cref="SipRegistrationStateArgs" /> event arguments.</param>
    public delegate void SipRegistrationStateDelegate(object sender, SipRegistrationStateArgs args);
}
