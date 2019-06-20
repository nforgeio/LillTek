//-----------------------------------------------------------------------------
// FILE:        EchoAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Directs the switch to echo the caller's audio back, with an optional delay.

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
    /// Directs the switch to echo the caller's audio back, with an optional delay.
    /// </summary>
    /// <remarks>
    /// This action is used primarily for testing purposes.
    /// </remarks>
    public class EchoAction : SwitchAction
    {
        private int delaySeconds;

        /// <summary>
        /// Constructs an action to echo audio with no delay for the
        /// current call on the executing dialplan.
        /// </summary>
        public EchoAction()
        {
        }

        /// <summary>
        /// Constructs an action to echo audio with a delay for the
        /// current call on an executing dialplan.
        /// </summary>
        /// <param name="delay">The echo delay.</param>
        public EchoAction(TimeSpan delay)
        {
            this.delaySeconds = SwitchHelper.GetScheduleSeconds(delay);
        }

        /// <summary>
        /// Constructs an action to echo audio with no delay for 
        /// a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        public EchoAction(Guid callID)
        {
            this.CallID = callID;
        }

        /// <summary>
        /// Constructs an action to echo audio with a delay for 
        /// a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="delay">The echo delay.</param>
        public EchoAction(Guid callID, TimeSpan delay)
        {
            this.CallID       = callID;
            this.delaySeconds = SwitchHelper.GetScheduleSeconds(delay);
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
            if (context.IsDialplan)
            {
                if (delaySeconds > 0)
                    context.Actions.Add(new SwitchExecuteAction("delay_echo", "{0}", delaySeconds));
                else
                    context.Actions.Add(new SwitchExecuteAction("echo", "{0}"));
            }
            else
            {
                CheckCallID();

                if (delaySeconds > 0)
                    context.Actions.Add(new SwitchExecuteAction(CallID, "delay_echo", "{0}", delaySeconds));
                else
                    context.Actions.Add(new SwitchExecuteAction(CallID, "echo", "{0}"));
            }
        }
    }
}
