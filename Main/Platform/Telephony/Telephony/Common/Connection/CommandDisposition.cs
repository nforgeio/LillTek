//-----------------------------------------------------------------------------
// FILE:        CommandDisposition.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Describes the ultimate result of a command submitted to the
//              switch for execution on a SwitchConnection.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Describes the ultimate result of a command submitted to the
    /// switch for execution on a <see cref="SwitchConnection" />.
    /// </summary>
    public class CommandDisposition
    {
        /// <summary>
        /// Returns <c>true</c> if the command was submitted or executed successfully.
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// The background job ID to be used for correlating the background job completion
        /// events to a particular command execution (or <see cref="Guid.Empty" />).
        /// </summary>
        public Guid JobID { get; private set; }

        /// <summary>
        /// Returns a read-only dictionary with the response properties.
        /// </summary>
        public ArgCollection Properties { get; private set; }

        /// <summary>
        /// Returns a read-only dictionary holding the response variables.
        /// </summary>
        public ArgCollection Variables { get; private set; }

        /// <summary>
        /// Returns any text returned by the switch.
        /// </summary>
        public string ResponseText { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="packet">The received switch packet.</param>
        internal CommandDisposition(SwitchPacket packet)
        {
            ArgCollection properties;
            ArgCollection variables;

            SwitchHelper.ProcessEventProperties(packet.Headers, out properties, out variables);

            this.Properties = properties;
            this.Variables = variables;

            switch (packet.PacketType)
            {
                case SwitchPacketType.ExecuteAck:

                    this.Success = packet.ExecuteAccepted;
                    this.ResponseText = Properties.Get("Reply-Text", string.Empty);
                    this.JobID = Properties.Get("Job-UUID", Guid.Empty);
                    break;

                case SwitchPacketType.ExecuteResponse:

                    this.Success = true;
                    this.ResponseText = packet.ContentText;
                    break;

                default:

                    Assertion.Fail("Unexpected packet type.");
                    break;
            }
        }
    }
}
