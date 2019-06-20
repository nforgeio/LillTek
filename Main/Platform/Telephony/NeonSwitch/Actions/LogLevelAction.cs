//-----------------------------------------------------------------------------
// FILE:        LogLevelAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Overrides the switch log level for the call.

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

// $todo(jeff.lill):
//
// This doesn't appear to be working for some reason.  Will need to come
// back to this later.

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// <b>Dialplan only:</b> Overrides the switch log level for the call.
    /// </summary>
    public class LogLevelAction : SwitchAction
    {
        private SwitchLogLevel level;

        /// <summary>
        /// Constructs an action to change the log level of the current
        /// call in an executing dialplan.
        /// </summary>
        /// <param name="level">The new log level.</param>
        public LogLevelAction(SwitchLogLevel level)
        {
            this.level = level;
        }

        /// <summary>
        /// Constructs an action to change the log level of the specified call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="level">The new log level.</param>
        public LogLevelAction(Guid callID, SwitchLogLevel level)
        {
            this.CallID = callID;
            this.level  = level;
        }

        /// <summary>
        /// Renders the high-level switch action instance into zero or more <see cref="SwitchExecuteAction" />
        /// instances and then adds these to the <see cref="ActionRenderingContext" />.<see cref="ActionRenderingContext.Actions" />
        /// collection.
        /// </summary>
        /// <param name="context">The action rendering context.</param>
        /// <exception cref="NotSupportedException">Thrown if the action is being rendered outside of a dialplan.</exception>
        /// <remarks>
        /// <note>
        /// It is perfectly reasonable for an action to render no actions to the
        /// context or to render multiple actions based on its properties.
        /// </note>
        /// </remarks>
        public override void Render(ActionRenderingContext context)
        {
            string logLevelString = SwitchHelper.GetSwitchLogLevelString(level);

            if (context.IsDialplan)
                context.Actions.Add(new SwitchExecuteAction("session_loglevel", logLevelString));
            else
            {
                CheckCallID();
                context.Actions.Add(new SwitchExecuteAction(CallID, "session_loglevel", logLevelString));
            }
        }
    }
}
