//-----------------------------------------------------------------------------
// FILE:        SdpMediaType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible SDP media types.

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Enumerates the possible SDP media types.
    /// </summary>
    public enum SdpMediaType
    {
        /// <summary>
        /// The media type cannot be identified by the current implementation
        /// of the SIP stack.
        /// </summary>
        Unknown,

        /// <summary>
        /// Audio media.
        /// </summary>
        Audio,

        /// <summary>
        /// Video media.
        /// </summary>
        Video,

        /// <summary>
        /// Textual media.
        /// </summary>
        Text,

        /// <summary>
        /// Application media.
        /// </summary>
        Application,

        /// <summary>
        /// Message media.
        /// </summary>
        Message
    }
}
