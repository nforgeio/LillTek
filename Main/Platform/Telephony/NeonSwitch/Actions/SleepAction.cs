//-----------------------------------------------------------------------------
// FILE:        SleepAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Pauses dialplan processing for a period of time, optionally 
//              consuming any received DTMF digits.

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
    /// Pauses dialplan processing for a period of time, optionally consuming any received DTMF digits.
    /// </summary>
    /// <remarks>
    /// <note>
    /// This action will throw a <see cref="NotSupportedException" /> if executed outside of the
    /// context of a dialplan.
    /// </note>
    /// </remarks>
    public class SleepAction : SwitchAction
    {
        private TimeSpan    duration;
        private bool        consumeDTMF;

        /// <summary>
        /// Constucts an action that pauses for the dialplan for
        /// the specified time.
        /// </summary>
        /// <param name="duration">The time to pause.</param>
        public SleepAction(TimeSpan duration)
            : base()
        {
            this.duration    = duration;
            this.consumeDTMF = false;
        }

        /// <summary>
        /// Constucts an action that pauses for the dialplan for
        /// the specified time and optionally consumes received
        /// DTMF digits.
        /// </summary>
        /// <param name="duration">The time to pause.</param>
        /// <param name="consumeDTMF">Pass <c>true</c> to consume DTMF digits.</param>
        public SleepAction(TimeSpan duration, bool consumeDTMF)
            : base()
        {
            this.duration    = duration;
            this.consumeDTMF = false;
        }

        /// <summary>
        /// Renders the high-level switch action instance into zero or more <see cref="SwitchExecuteAction" />
        /// instances and then adds these to the <see cref="ActionRenderingContext" />.<see cref="ActionRenderingContext.Actions" />
        /// collection.
        /// </summary>
        /// <param name="context">The action rendering context.</param>
        /// <exception cref="NotSupportedException">Thrown if this action is executed outside of the context of a dialplan.</exception>
        /// <remarks>
        /// <note>
        /// It is perfectly reasonable for an action to render no actions to the
        /// context or to render multiple actions based on its properties.
        /// </note>
        /// </remarks>
        public override void Render(ActionRenderingContext context)
        {
            if (!context.IsDialplan)
                throw new NotSupportedException("[SleepAction] cannot be executed outside the context of a dialplan.");

            if (duration <= TimeSpan.Zero)
                return;     // Nothing to do.

            string  sleepEatDigits;
            bool    orgConsumeDTMF;

            if (!context.Variables.TryGetValue("sleep_eat_digits", out sleepEatDigits) || !bool.TryParse(sleepEatDigits, out orgConsumeDTMF))
                orgConsumeDTMF = false;

            if (orgConsumeDTMF != this.consumeDTMF)
                new SetVariableAction("sleep_eat_digits", this.consumeDTMF ? "true" : "false").Render(context);

            context.Actions.Add(new SwitchExecuteAction("sleep", ((int)duration.TotalMilliseconds).ToString()));

            if (orgConsumeDTMF != this.consumeDTMF)
                new SetVariableAction("sleep_eat_digits", orgConsumeDTMF ? "true" : "false").Render(context);
        }
    }
}
