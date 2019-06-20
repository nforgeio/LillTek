//-----------------------------------------------------------------------------
// FILE:        SwitchJobCompletedArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Arguments received when the the switch completes the execution
//              of a background job.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Arguments received when the the switch completes the execution of a background job.
    /// </summary>
    public class SwitchJobCompletedArgs : EventArgs
    {
        /// <summary>
        /// Returns a read-only dictionary holding the completed job properties.
        /// </summary>
        public ArgCollection Properties { get; private set; }

        /// <summary>
        /// Returns a read-only dictionary of the completed job variables.
        /// </summary>
        public ArgCollection Variables { get; private set; }

        /// <summary>
        /// The reply text or <c>null</c>.
        /// </summary>
        public string ReplyText { get; private set; }

        /// <summary>
        /// The background job ID to be used for correlating the background job completion
        /// events to a particular command execution.
        /// </summary>
        public Guid JobID { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="packet">The switch packet holding the job completion event.</param>
        internal SwitchJobCompletedArgs(SwitchPacket packet)
        {
            ArgCollection   properties;
            ArgCollection   variables;
            string          value;
            Guid            jobID;

            Assertion.Test(packet.EventCode == SwitchEventCode.BackgroundJob);

            if (packet.Headers.TryGetValue("Job-UUID", out value) && Guid.TryParse(value, out jobID))
                this.JobID = jobID;

            SwitchHelper.ProcessEventProperties(packet.Headers, out properties, out variables);

            this.Properties = properties;
            this.Variables  = variables;
            this.ReplyText  = packet.ContentText;
        }
    }
}
