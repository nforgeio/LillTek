//-----------------------------------------------------------------------------
// FILE:        SipCreateDialogDelegate.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines delegate called when a SipCore raises its CreateServerDialogEvent and
//              CreateClientDialogEvent events.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Defines delegate called when a <see cref="SipCore" /> raises its <see cref="SipCore.CreateServerDialogEvent" /> 
    /// and <see cref="SipCore.CreateClientDialogEvent" /> events.
    /// </summary>
    /// <param name="sender">The event source (the <see cref="SipCore" />).</param>
    /// <param name="args">The <see cref="SipCreateDialogArgs" /> event arguments.</param>
    /// <remarks>
    /// <para>
    /// This event is used to give the application an opportunity to
    /// create a custom dialog derived from <see cref="SipDialog" />.
    /// The event handler should set the <see cref="SipCreateDialogArgs" />.<see cref="SipCreateDialogArgs.Dialog" />
    /// property to the new dialog.
    /// </para>
    /// <para>
    /// If no handler is enlisted in this event or if no dialog is
    /// created, then the base class will create a <see cref="SipDialog" />
    /// instance instead.
    /// </para>
    /// </remarks>
    public delegate void SipCreateDialogDelegate(object sender, SipCreateDialogArgs args);
}
