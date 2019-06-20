//-----------------------------------------------------------------------------
// FILE:        HeartbeatAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enables or disables the periodic generation of call heartbeat events.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;
using LillTek.Telephony.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// Enables or disables the periodic generation of call heartbeat events.
    /// </summary>
    /// <remarks>
    /// <note>
    /// The switch will round the heartbeat interval specified to seconds such
    /// that the minimum positive interval used will be one second.
    /// </note>
    /// </remarks>
    public class HeartbeatAction : SwitchAction
    {
        private TimeSpan interval;

        /// <summary>
        /// Constructs an action that enables or disables the detection of
        /// in-band DTMF tones for the current call on the executing dialplan.
        /// </summary>
        /// <param name="interval">Pass a positive interval to enable heartbeat events, otherwise heartbeats will be disabled.</param>
        /// <remarks>
        /// <note>
        /// The switch will round the heartbeat interval specified to seconds such
        /// that the minimum positive interval used will be one second.
        /// </note>
        /// </remarks>
        public HeartbeatAction(TimeSpan interval)
        {
            this.interval = interval;
        }

        /// <summary>
        /// Constructs an action that enables or disables the detection of
        /// in-band DTMF tones for a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="interval">Pass a positive interval to enable heartbeat events, otherwise heartbeats will be disabled.</param>
        /// <remarks>
        /// <note>
        /// The switch will round the heartbeat interval specified to seconds such
        /// that the minimum positive interval used will be one second.
        /// </note>
        /// </remarks>
        public HeartbeatAction(Guid callID, TimeSpan interval)
        {
            this.CallID   = callID;
            this.interval = interval;
        }

        /// <summary>
        /// Renders the high-level switch action instance into zero or more <see cref="SwitchExecuteAction" />
        /// instances and then adds these to the <see cref="ActionRenderingContext" />.<see cref="ActionRenderingContext.Actions" />
        /// collection.
        /// </summary>
        /// <param name="context">The action rendering context.</param>
        /// <remarks>
        /// <note>
        /// It is perfectly reasonable for an action to render no actions to the
        /// context or to render multiple actions based on its properties.
        /// </note>
        /// </remarks>
        public override void Render(ActionRenderingContext context)
        {
            var intervalSeconds = SwitchHelper.GetScheduleSeconds(interval);

            if (context.IsDialplan)
                context.Actions.Add(new SwitchExecuteAction("uuid_session_heartbeat", "${{Unique-ID}} {0}", intervalSeconds));
            else
            {
                CheckCallID();
                context.Actions.Add(new SwitchExecuteAction(CallID, "uuid_session_heartbeat", "{0}", intervalSeconds));
            }
        }
    }
}
