//-----------------------------------------------------------------------------
// FILE:        SwitchEvent.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The base class for NeonSwitch events. 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;
using LillTek.Telephony.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// The base class for NeonSwitch events. 
    /// </summary>
    /// <remarks>
    /// Events that are trigged from the underlying FreeSWITCH layer are typically
    /// derived from this class to gain some common functionality.
    /// </remarks>
    public class SwitchEvent
    {
        private switch_event        switchArgs;     // Low-level event arguments
        private ArgCollection       headers;        // Dictionary of event headers

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="switchArgs">The low-level event arguments.</param>
        internal SwitchEvent(switch_event switchArgs)
        {
            this.switchArgs = switchArgs;
            this.EventType  = (SwitchEventCode)switchArgs.event_id;
            this.Priority   = (SwitchPriority)switchArgs.priority;

            // Load the headers into a read-only ArgCollection.

            headers = new ArgCollection(ArgCollectionType.Unconstrained);

            for (var header = switchArgs.headers; header != null; header = header.next)
                headers[header.name] = Helper.UrlDecode(header.value);

            headers.IsReadOnly = true;

            // Parse the common headers.

            try
            {
                this.SwitchID = new Guid(headers["Core-UUID"]);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }

            try
            {
                this.EventDateUtc = Helper.ParseInternetDate(headers["Event-Date-GMT"]);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Returns the <see cref="SwitchEventCode" /> enumneration value identifying the 
        /// type of the event.
        /// </summary>
        public SwitchEventCode EventType { get; private set; }

        /// <summary>
        /// The globally unique ID for the NeonSwitch instance.
        /// </summary>
        public Guid SwitchID { get; private set; }

        /// <summary>
        /// Returns the event time (UTC).
        /// </summary>
        public DateTime EventDateUtc { get; private set; }

        /// <summary>
        /// Returns the relative priority of the event.
        /// </summary>
        public SwitchPriority Priority { get; private set; }

        /// <summary>
        /// Returns a read-only dictionary holding the event headers.
        /// </summary>
        public ArgCollection Headers
        {
            get { return headers; }
        }

        /// <summary>
        /// Returns the the event body text.
        /// </summary>
        public string Body
        {
            get { return switchArgs.body; }
        }

        /// <summary>
        /// Dumps the event to the debug trace.
        /// </summary>
        /// <param name="caption">The caption for the event.</param>
        [Conditional("DEBUG")]
        public void Dump(string caption)
        {
            var sb = new StringBuilder();

            sb.AppendLine("******************************************");
            sb.AppendFormatLine("{0}:", caption);
            sb.AppendLine();

            foreach (var key in headers)
                sb.AppendFormatLine("{0}: {1}", key, headers[key]);

            sb.AppendLine("******************************************");

            Debug.Write(sb.ToString());
        }

        /// <summary>
        /// Dumps a formatted event to the debug trace.
        /// </summary>
        /// <param name="format">The caption format string.</param>
        /// <param name="args">The format arguments.</param>
        [Conditional("DEBUG")]
        public void Dump(string format, params object[] args)
        {
            Dump(string.Format(format, args));
        }
    }
}
