//-----------------------------------------------------------------------------
// FILE:        MsgFlags.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Message flag bits

using System;

#if WINFULL
using LillTek.Messaging.Internal;
#endif

namespace LillTek.Messaging
{
    /// <summary>
    /// Defines the message flag bits.
    /// </summary>
    [Flags]
    public enum MsgFlag : int
    {
        /// <summary>
        /// Set if the message has a non-empty <see cref="Msg.MsgID" />.
        /// </summary>
        MsgID = 0x00000001,

        /// <summary>
        /// Set if the message has a non-empty <see cref="Msg.SessionID" />.
        /// </summary>
        SessionID = 0x00000002,

        /// <summary>
        /// Set if the message is to be broadcast to all matching endpoints.
        /// </summary>
        Broadcast = 0x00000004,

        /// <summary>
        /// Set if the message is the initiating message of a session.
        /// </summary>
        OpenSession = 0x00000008,

        /// <summary>
        /// Set if the message is to be targeted at the server side of a session
        /// if the message has a non-empty SessionID.  If this isn't set then the
        /// message to be directed to the client side.
        /// </summary>
        ServerSession = 0x00000010,

        /// <summary>
        /// Set if the router implementing the application handler for the
        /// message's target endpoint should send a <see cref="ReceiptMsg" /> 
        /// back to the endpoint specified by <see cref="Msg._ReceiptEP" /> upon
        /// reception of the message.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The message's <see cref="Msg._MsgID" /> property must be
        /// set to a non-empty value for receipt tracking to work.
        /// </note>
        /// </remarks>
        ReceiptRequest = 0x00000020,

        /// <summary>
        /// Set if the message should be scheduled for high priority
        /// processing.
        /// </summary>
        /// <remarks>
        /// This flag should be used with care and only for messages that
        /// are guaranteed to take very little time to process and will
        /// not flood the message router.  This will typically be set only
        /// for internal Messaging layer messages.
        /// </remarks>
        Priority = 0x00000040,

        /// <summary>
        /// Set if the message includes extended headers.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Extended headers are the mechanism for extending the messaging
        /// protocol at a low level.  Up to 255 headers can be added to
        /// a message with each header consisting of a byte header type ID
        /// and then up to 65535 bytes of binary data.
        /// </para>
        /// <para>
        /// It is not generally necessary to explicitly set this flag.  The
        /// <see cref="Msg" /> class will set this during message serialization
        /// if extension headers are present.
        /// </para>
        /// </remarks>
        ExtensionHeaders = 0x00000080,

        /// <summary>
        /// Set to favor local routes when delivering the message.
        /// </summary>
        /// <remarks>
        /// This is used in an attempt to try to constrain routing of
        /// the message first to the local process, the local machine,
        /// and then the subnet.  This flag has meaning only when routing
        /// messages to logical endpoints.
        /// </remarks>
        ClosestRoute = 0x00000100,

        /// <summary>
        /// Set when the message includes an optional security token.
        /// </summary>
        SecurityToken = 0x00000200,

        /// <summary>
        /// Used by Unit tests to indicate to <see cref="MsgRouter.Query" /> that
        /// an existing <see cref="Msg._Session" /> value should be reused.
        /// </summary>
        KeepSessionID = 0x08000000,

        /// <summary>
        /// Masks the 3-bits used for encoding a <see cref="RoutingScope" /> value.
        /// </summary>
        RoutingScopeMask = 0x70000000,
    }
}
