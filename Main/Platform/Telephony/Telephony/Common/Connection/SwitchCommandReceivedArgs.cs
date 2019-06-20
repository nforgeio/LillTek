//-----------------------------------------------------------------------------
// FILE:        SwitchCommandReceivedArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Arguments received when the SwitchConnection.CommandReceived
//              event is raised.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Arguments received when the <see cref="SwitchConnection" />.<see cref="SwitchConnection.CommandReceived" />
    /// event is raised.
    /// </summary>
    public class SwitchCommandReceivedArgs : EventArgs
    {
        /// <summary>
        /// Returns the command text.
        /// </summary>
        public string CommandText { get; private set; }

        /// <summary>
        /// Returns a read-only dictionary holding the command properties 
        /// or <c>null</c> if the command has no properties.
        /// </summary>
        public ArgCollection Properties { get; private set; }

        /// <summary>
        /// Returns the command content type or <c>null</c>.
        /// </summary>
        public string ContentType { get; private set; }

        /// <summary>
        /// Returns the command content as bytes or <c>null</c>.
        /// </summary>
        public byte[] Content { get; private set; }

        /// <summary>
        /// Returns the command content as text if it is text, <c>null</c> otherwise.
        /// </summary>
        public string ContentText { get; private set; }

        /// <summary>
        /// Indicates that the command was processed successfully.  Command event handlers should
        /// set this to <c>false</c> if the command failed.  This defaults to <c>true</c>.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The text to be included in the reply to the the remote side of the connection.
        /// Command event handlers may set this to a success or error message or leave this
        /// set to <c>null</c>.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Only the first line of text will actually be sent in the reply.
        /// </note>
        /// </remarks>
        public string ReplyText { get; set; }

        /// <summary>
        /// The text to be included in the response to the remote side of the connection/
        /// Command event handlers may set this as desired or leave this as <c>null</c>.
        /// </summary>
        public string ResponseText { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="packet">The received packet.</param>
        internal SwitchCommandReceivedArgs(SwitchPacket packet)
        {
            this.CommandText  = packet.CommandText;
            this.Properties   = packet.Headers;
            this.ContentType  = packet.ContentType;
            this.Content      = packet.Content;
            this.ContentText  = packet.ContentText;
            this.Success      = true;
            this.ReplyText    = null;
            this.ResponseText = null;

            if (Properties != null)
                Properties.IsReadOnly = true;
        }
    }
}
