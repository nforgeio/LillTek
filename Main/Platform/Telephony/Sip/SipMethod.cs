//-----------------------------------------------------------------------------
// FILE:        SipMethod.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the known SIP methods.

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Describes the known SIP methods.
    /// </summary>
    public enum SipMethod
    {
        /// <summary></summary>
        Invite,

        /// <summary></summary>
        Reinvite,

        /// <summary></summary>
        Register,

        /// <summary></summary>
        Ack,

        /// <summary></summary>
        Cancel,

        /// <summary></summary>
        Bye,

        /// <summary></summary>
        Options,

        /// <summary></summary>
        Info,

        /// <summary></summary>
        Notify,

        /// <summary></summary>
        Subscribe,

        /// <summary></summary>
        Unsubscribe,

        /// <summary></summary>
        Update,

        /// <summary></summary>
        Message,

        /// <summary></summary>
        Refer,

        /// <summary></summary>
        Prack,

        /// <summary></summary>
        Publish,

        /// <summary>
        /// This value is used if the SIP method is not known by this implementation
        /// of the SIP stack.
        /// </summary>
        Unknown
    }
}
