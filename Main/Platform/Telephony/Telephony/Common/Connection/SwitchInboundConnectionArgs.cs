//-----------------------------------------------------------------------------
// FILE:        SwitchLogEntryReceivedArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Arguments received when the SwitchConnection.InboundConnection
//              event is raised.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Arguments received when the <see cref="SwitchConnection" />.<see cref="SwitchConnection.InboundConnection" />
    /// event is raised.
    /// </summary>
    public class SwitchInboundConnectionArgs : EventArgs
    {
        /// <summary>
        /// Returns the inbound connection.
        /// </summary>
        public SwitchConnection Connection { get; private set; }

        /// <summary>
        /// Set this to <c>false</c> if the connection's internal receive thread is not
        /// to be started.
        /// </summary>
        /// <remarks>
        /// This is useful for unit testing situations where it's easier for the test to
        /// call <see cref="SwitchConnection.ReceivePacket" /> to handle the server side
        /// of the protocol in-line, rather than responding to events.
        /// </remarks>
        internal bool StartConnectionThread { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="connection">The inbound connection.</param>
        internal SwitchInboundConnectionArgs(SwitchConnection connection)
        {
            this.Connection            = connection;
            this.StartConnectionThread = true;
        }
    }
}
