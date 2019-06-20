//-----------------------------------------------------------------------------
// FILE:        PlayDtmfAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Plays in-band DTMF tones.

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
    /// Plays DTMF tones.  Note that by default DTMF tones will be played
    /// out-of-band unless a previous <see cref="InbandDtmfGenerateAction" /> 
    /// enabled in-band tone generation for the call.
    /// </summary>
    public class PlayDtmfAction : SwitchAction
    {
        private string      dtmfDigits;
        private TimeSpan    toneDuration;

        /// <summary>
        /// Constructs an action that plays the DTMF tones passed with
        /// the default individual tone duration of <see cref="Switch.MinDtmfDuration" />
        /// to the current call of an executing dial plan.
        /// </summary>
        /// <param name="dtmfDigits">The DTMF digits.</param>
        public PlayDtmfAction(string dtmfDigits)
        {
            this.dtmfDigits   = Dtmf.Validate(dtmfDigits);
            this.toneDuration = Switch.MinDtmfDuration;
        }

        /// <summary>
        /// Constructs an action that plays the DTMF tones passed with
        /// the specified individual tone duration to the current call of an 
        /// executing dial plan.
        /// </summary>
        /// <param name="dtmfDigits">The DTMF digits.</param>
        /// <param name="toneDuration">The tone duration.</param>
        /// <remarks>
        /// <note>
        /// The actual tone duration used will be adjusted so that it falls
        /// within the switch limits of <see cref="Switch.MinDtmfDuration "/>..<see cref="Switch.MaxDtmfDuration" />.
        /// </note>
        /// </remarks>
        public PlayDtmfAction(string dtmfDigits, TimeSpan toneDuration)
        {
            if (toneDuration < Switch.MinDtmfDuration)
                toneDuration = Switch.MinDtmfDuration;
            else if (toneDuration > Switch.MaxDtmfDuration)
                toneDuration = Switch.MaxDtmfDuration;

            this.dtmfDigits   = Dtmf.Validate(dtmfDigits);
            this.toneDuration = toneDuration;
        }

        /// <summary>
        /// Constructs an action that plays the DTMF tones passed with
        /// the default individual tone duration of <see cref="Switch.MinDtmfDuration" />
        /// to a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="dtmfDigits">The DTMF digits.</param>
        public PlayDtmfAction(Guid callID, string dtmfDigits)
        {
            this.CallID       = callID;
            this.dtmfDigits   = Dtmf.Validate(dtmfDigits);
            this.toneDuration = Switch.MinDtmfDuration;
        }

        /// <summary>
        /// Constructs an action that plays the DTMF tones passed with
        /// the specified individual tone duration to a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="dtmfDigits">The DTMF digits.</param>
        /// <param name="toneDuration">The tone duration.</param>
        /// <remarks>
        /// <note>
        /// The actual tone duration used will be adjusted so that it falls
        /// within the switch limits of <see cref="Switch.MinDtmfDuration "/>..<see cref="Switch.MaxDtmfDuration" />.
        /// </note>
        /// </remarks>
        public PlayDtmfAction(Guid callID, string dtmfDigits, TimeSpan toneDuration)
        {
            if (toneDuration < Switch.MinDtmfDuration)
                toneDuration = Switch.MinDtmfDuration;
            else if (toneDuration > Switch.MaxDtmfDuration)
                toneDuration = Switch.MaxDtmfDuration;

            this.CallID       = callID;
            this.dtmfDigits   = Dtmf.Validate(dtmfDigits);
            this.toneDuration = toneDuration;
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
            if (dtmfDigits.Length == 0 || toneDuration <= TimeSpan.Zero)
                return;     // Nothing to do

            if (context.IsDialplan)
            {
                context.Actions.Add(new SwitchExecuteAction("send_dtmf", "{0}@{1}", dtmfDigits, ((int)toneDuration.TotalMilliseconds)));

                // Note that FreeSWITCH does not wait until the digits are actually played before
                // moving on to the next action.  So, I'm going to estimate how long it will
                // take to play the digits and issue a sleep action for this period.  Note that
                // I'm going to add 1000ms to the computed duration as a buffer.

                new SleepAction(Helper.Multiply(toneDuration, dtmfDigits.Length) + TimeSpan.FromMilliseconds(1000)).Render(context);
            }
            else
            {
                CheckCallID();
                context.Actions.Add(new SwitchExecuteAction("uuid_send_dtmf", "{0:D} {1}@{2}", CallID, dtmfDigits, ((int)toneDuration.TotalMilliseconds)));
            }
        }
    }
}
