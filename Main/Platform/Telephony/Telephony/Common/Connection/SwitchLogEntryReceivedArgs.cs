//-----------------------------------------------------------------------------
// FILE:        SwitchLogEntryReceivedArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Arguments received when the SwitchConnection.LogEntryReceived
//              event is raised.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Arguments received when the <see cref="SwitchConnection" />.<see cref="SwitchConnection.LogEntryReceived" />
    /// event is raised.
    /// </summary>
    public class SwitchLogEntryReceivedArgs : EventArgs
    {
        /// <summary>
        /// Returns a read-only dictionary holding the log event properties.
        /// </summary>
        public ArgCollection Properties { get; private set; }

        /// <summary>
        /// Returns a read-only dictionary holding the log event variables.
        /// </summary>
        public ArgCollection Variables { get; private set; }

        /// <summary>
        /// Returns the log level.
        /// </summary>
        public SwitchLogLevel Level { get; private set; }

        /// <summary>
        /// Returns the text of the log message.
        /// </summary>
        public string MessageText { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="packet">The switch packet holding the job completion event.</param>
        internal SwitchLogEntryReceivedArgs(SwitchPacket packet)
        {
            ArgCollection properties;
            ArgCollection variables;

            SwitchHelper.ProcessEventProperties(packet.Headers, out properties, out variables);

            this.Properties  = properties;
            this.Variables   = variables;
            this.MessageText = packet.ContentText;
        }
    }
}
