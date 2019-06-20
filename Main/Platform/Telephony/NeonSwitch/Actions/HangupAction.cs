//-----------------------------------------------------------------------------
// FILE:        HangupAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Directs the switch to terminate a call.

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
    /// Directs the switch to terminate a call.
    /// </summary>
    public class HangupAction : SwitchAction
    {
        private SwitchHangupReason  reason;
        private TimeSpan            delay;

        /// <summary>
        /// Constructs a hangup action with the <see cref="SwitchHangupReason.NormalClearing" /> 
        /// reason code for the current call in an executing dial plan.
        /// </summary>
        public HangupAction()
        {
            this.reason = SwitchHangupReason.NormalClearing;
            this.delay  = TimeSpan.Zero;
        }

        /// <summary>
        /// Constructs a delayed hangup action for the current call in an executing dial plan.
        /// </summary>
        /// <param name="delay">The amount of time to delay the hangup.</param>
        /// <remarks>
        /// <para>
        /// This action essentially delays the hangup until a time in the future
        /// with the <see cref="SwitchHangupReason.AllottedTimeout" /> reason code.
        /// </para>
        /// <note>
        /// <paramref name="delay" /> is ignored when executing outside of a dialplan.
        /// </note>
        /// </remarks>
        public HangupAction(TimeSpan delay)
        {
            this.reason = SwitchHangupReason.AllottedTimeout;
            this.delay  = delay;
        }

        /// <summary>
        /// Constructs a hangup action with a specified reason code for the current call
        /// in an executing dial plan.
        /// </summary>
        /// <param name="reason">The hangup reason code.</param>
        public HangupAction(SwitchHangupReason reason)
        {
            this.reason = reason;
            this.delay  = TimeSpan.Zero;
        }

        /// <summary>
        /// Constructs a delayed hangup action with a specified reason code for the current
        /// call in an executing dial plan.
        /// </summary>
        /// <param name="reason">The hangup reason code.</param>
        /// <param name="delay">The amount of time to delay the hangup.</param>
        /// <remarks>
        /// <note>
        /// <paramref name="delay" /> is ignored when executing outside of a dialplan.
        /// </note>
        /// </remarks>
        public HangupAction(SwitchHangupReason reason, TimeSpan delay)
        {
            this.reason = reason;
            this.delay  = delay;
        }

        /// <summary>
        /// Constructs a hangup action with the <see cref="SwitchHangupReason.NormalClearing" /> 
        /// reason code for a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        public HangupAction(Guid callID)
        {
            this.CallID = callID; ;
            this.reason = SwitchHangupReason.NormalClearing;
            this.delay  = TimeSpan.Zero;
        }

        /// <summary>
        /// Constructs a delayed hangup action for a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="delay">The amount of time to delay the hangup.</param>
        /// <remarks>
        /// <para>
        /// This action essentially delays the hangup until a time in the future
        /// with the <see cref="SwitchHangupReason.AllottedTimeout" /> reason code.
        /// </para>
        /// <note>
        /// <paramref name="delay" /> is ignored when executing outside of a dialplan.
        /// </note>
        /// </remarks>
        public HangupAction(Guid callID, TimeSpan delay)
        {
            this.CallID = callID; ;
            this.reason = SwitchHangupReason.AllottedTimeout;
            this.delay  = delay;
        }

        /// <summary>
        /// Constructs a hangup action with a specified reason code for a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="reason">The hangup reason code.</param>
        public HangupAction(Guid callID, SwitchHangupReason reason)
        {
            this.CallID = callID; ;
            this.reason = reason;
            this.delay  = TimeSpan.Zero;
        }

        /// <summary>
        /// Constructs a delayed hangup action with a specified reason code for a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="reason">The hangup reason code.</param>
        /// <param name="delay">The amount of time to delay the hangup.</param>
        /// <remarks>
        /// <note>
        /// <paramref name="delay" /> is ignored when executing outside of a dialplan.
        /// </note>
        /// </remarks>
        public HangupAction(Guid callID, SwitchHangupReason reason, TimeSpan delay)
        {
            this.CallID = callID; ;
            this.reason = reason;
            this.delay  = delay;
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
            var reasonString = SwitchHelper.GetSwitchHangupReasonString(reason);
            int delaySeconds = SwitchHelper.GetScheduleSeconds(delay);

            if (context.IsDialplan)
            {
                if (delaySeconds <= 0)
                    context.Actions.Add(new SwitchExecuteAction("hangup", reasonString));
                else
                    context.Actions.Add(new SwitchExecuteAction("sched_hangup", "+{0} {1}", delaySeconds, reasonString));
            }
            else
            {
                CheckCallID();

                if (delaySeconds <= 0)
                    context.Actions.Add(new SwitchExecuteAction(CallID, "hangup", reasonString));
                else
                    context.Actions.Add(new SwitchExecuteAction(CallID, "sched_hangup", "+{0} {1}", delaySeconds, reasonString));
            }
        }
    }
}
