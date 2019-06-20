//-----------------------------------------------------------------------------
// FILE:        SwitchEventReceivedArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Arguments received when the SwitchConnection.EventReceived
//              event is raised.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Arguments received when the <see cref="SwitchConnection" />.<see cref="SwitchConnection.EventReceived" />
    /// event is raised.
    /// </summary>
    public class SwitchEventReceivedArgs : EventArgs
    {
        /// <summary>
        /// Returns the time the event was raised (UTC).
        /// </summary>
        public DateTime TimeUtc { get; private set; }

        /// <summary>
        /// Returns the event name (or the empty string).
        /// </summary>
        public string EventName { get; private set; }

        /// <summary>
        /// Returns the event code.
        /// </summary>
        public SwitchEventCode EventCode { get; private set; }

        /// <summary>
        /// Returns a read-only dictionary of the event properties.
        /// </summary>
        public ArgCollection Properties { get; private set; }

        /// <summary>
        /// Returns a read-only dictionary of the event variables.
        /// </summary>
        public ArgCollection Variables { get; private set; }

        /// <summary>
        /// Returns the event content type (or the empty string).
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Returns the event content (or <c>null</c>).
        /// </summary>
        public byte[] Content { get; private set; }

        /// <summary>
        /// Returns the event content as text (or <c>null</c>).
        /// </summary>
        public string ContentText { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="packet">The switch packet.</param>
        internal SwitchEventReceivedArgs(SwitchPacket packet)
        {
            ArgCollection   properties;
            ArgCollection   variables;
            string          eventName;

            this.TimeUtc = packet.Headers.Get("Event-Date-GMT", DateTime.MinValue);

            if (packet.Headers.TryGetValue("Event-Name", out eventName))
            {
                this.EventName = eventName;
                this.EventCode = SwitchHelper.ParseEventCode(eventName);
            }
            else
            {
                // I don't think this should ever happen.

                SysLog.LogWarning("SwitchConnection received an event without an [Event-Name] property.");

                this.EventName = string.Empty;
                this.EventCode = default(SwitchEventCode);
            }

            SwitchHelper.ProcessEventProperties(packet.Headers, out properties, out variables);

            this.Properties  = properties;
            this.Variables   = variables;
            this.ContentType = packet.ContentType;
            this.Content     = packet.Content;
            this.ContentText = packet.ContentText;
        }
    }
}
