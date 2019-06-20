//-----------------------------------------------------------------------------
// FILE:        MsgHeader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a message extension header.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Messaging
{
    /// <summary>
    /// Message extension headers provide a mechanism for extending the
    /// message format.
    /// </summary>
    /// <remarks>
    /// Up to 255 extended headers can be added to a <see cref="Msg" />'s
    /// <see cref="Msg._ExtensionHeaders" /> collection.  Each header
    /// includes a <see cref="MsgHeaderID" /> and up to 65535 bytes of
    /// binary data.
    /// </remarks>
    public class MsgHeader
    {
        /// <summary>
        /// The header ID.
        /// </summary>
        public readonly MsgHeaderID HeaderID;

        /// <summary>
        /// The content data.
        /// </summary>
        public readonly byte[] Contents;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="headerID">The header ID.</param>
        /// <param name="contents">The header contents.</param>
        public MsgHeader(MsgHeaderID headerID, byte[] contents)
        {
            if (contents == null)
                throw new ArgumentNullException("contents");

            if (contents.Length > ushort.MaxValue)
                throw new ArgumentException("[contents] cannot exceed 65535 bytes.");

            this.HeaderID = headerID;
            this.Contents = contents;
        }
    }
}
