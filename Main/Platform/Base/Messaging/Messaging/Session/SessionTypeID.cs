//-----------------------------------------------------------------------------
// FILE:        SessionTypeID.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumeration that defines the built-in session types.

using System;
using System.Collections.Generic;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Defines the built-in session types within <see cref="MsgSessionAttribute" /> tags.
    /// </summary>
    public enum SessionTypeID
    {
        /// <summary>
        /// To be used internally by the messaging library only.
        /// </summary>
        Unknown,

        /// <summary>
        /// Indicates that a non built-in session is specified.
        /// This is the default value.
        /// </summary>
        Custom,

        /// <summary>
        /// Indicates that a <see cref="QuerySession" /> should be created for the handler.
        /// </summary>
        Query,

        /// <summary>
        /// Indicates that a <see cref="DuplexSession" /> should be created for the handler.
        /// </summary>
        Duplex,

        /// <summary>
        /// Indicates that a <see cref="ReliableTransferSession" /> should be created for the handler.
        /// </summary>
        ReliableTransfer,
    }
}
