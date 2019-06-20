//-----------------------------------------------------------------------------
// FILE:        SipDialogDelegate.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines delegates called when a SipCore raises its DialogCreated,
//              DialogConfirmed or DialogClosed events.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Defines delegates call when a <see cref="SipCore" /> raises its <see cref="SipCore.DialogCreated" />,
    /// <see cref="SipCore.DialogConfirmed" />, or <see cref="SipCore.DialogClosed" /> events.
    /// </summary>
    /// <param name="sender">The event source (the <see cref="SipCore" />).</param>
    /// <param name="args">The <see cref="SipDialogEventArgs" /> event arguments.</param>
    public delegate void SipDialogDelegate(object sender, SipDialogEventArgs args);
}
