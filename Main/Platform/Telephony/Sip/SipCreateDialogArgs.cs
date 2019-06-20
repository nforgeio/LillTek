//-----------------------------------------------------------------------------
// FILE:        SipCreateDialogArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the arguments passed when a SipCore raises its CreateServerDialogEvent 
//              and CreateClientDialog events.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Defines arguments passed when a <see cref="SipCore" /> raises its <see cref="SipCore.CreateServerDialogEvent" /> 
    /// and <see cref="SipCore.CreateClientDialogEvent" /> events.
    /// </summary>
    public sealed class SipCreateDialogArgs
    {
        /// <summary>
        /// Set this to the custom application dialog class derived from <see cref="SipDialog" />.
        /// </summary>
        public SipDialog Dialog = null;

        /// <summary>
        /// Constructor.
        /// </summary>
        internal SipCreateDialogArgs()
        {
        }
    }
}
