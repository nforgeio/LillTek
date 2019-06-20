//-----------------------------------------------------------------------------
// FILE:        SipB2BUAEventDelegate.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines delegate called when a SipB2BUserAgent raises one of its events.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Defines delegate called when a <see cref="SipB2BUserAgent{TState}" /> raises one of its events.
    /// </summary>
    /// <typeparam name="TState">The application session state type.</typeparam>
    /// <param name="sender">The event source (the <see cref="SipB2BUserAgent{TState}" />).</param>
    /// <param name="args">The <see cref="SipB2BUAEventArgs{TState}" /> event arguments.</param>
    public delegate void SipB2BUAEventDelegate<TState>(object sender, SipB2BUAEventArgs<TState> args);
}
