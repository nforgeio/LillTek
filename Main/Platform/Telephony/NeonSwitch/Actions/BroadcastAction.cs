//-----------------------------------------------------------------------------
// FILE:        BroadcastAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Broadcasts a sound file to either or both of the call legs, optionally 
//              delaying the operation.

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
    /// Broadcasts a sound file to either or both of the call legs, optionally delaying the operation.
    /// </summary>
    public class BroadcastAction : SwitchAction
    {
        private CallLeg     legs;
        private string      filePath;
        private TimeSpan    delay;

        /// <summary>
        /// Constructs a dialplan action that plays the sound file immediately to
        /// the specified call legs.
        /// </summary>
        /// <param name="legs">Specifies the call legs to hear the sound.</param>
        /// <param name="filePath">Path to the sound file.</param>
        public BroadcastAction(CallLeg legs, string filePath)
            : base("sched_broadcast")
        {
            this.legs     = legs;
            this.filePath = Switch.ExpandFilePath(filePath);
        }

        /// <summary>
        /// Constructs a action that plays the sound file immediately to
        /// the specified legs of the given call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="legs">Specifies the call legs to hear the sound.</param>
        /// <param name="filePath">Path to the sound file.</param>
        public BroadcastAction(Guid callID, CallLeg legs, string filePath)
            : this(legs, filePath)
        {
            this.CallID   = callID;
            this.legs     = legs;
            this.filePath = Switch.ExpandFilePath(filePath);
        }

        /// <summary>
        /// Constructs an action that plays the sound file the specified call legs
        /// with the specified delay.
        /// </summary>
        /// <param name="legs">Specifies the call legs to hear the sound.</param>
        /// <param name="filePath">Path to the sound file.</param>
        /// <param name="delay">The time to wait before playing the file.</param>
        public BroadcastAction(CallLeg legs, string filePath, TimeSpan delay)
        {
            this.legs     = legs;
            this.filePath = Switch.ExpandFilePath(filePath);
            this.delay    = delay;
        }

        /// <summary>
        /// Constructs an action that plays the sound file the specified call legs
        /// with the specified delay to a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="legs">Specifies the call legs to hear the sound.</param>
        /// <param name="filePath">Path to the sound file.</param>
        /// <param name="delay">The time to wait before playing the file.</param>
        public BroadcastAction(Guid callID, CallLeg legs, string filePath, TimeSpan delay)
        {
            this.CallID   = callID;
            this.legs     = legs;
            this.filePath = Switch.ExpandFilePath(filePath);
            this.delay    = delay;
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
            int         delaySeconds = SwitchHelper.GetScheduleSeconds(delay);
            string  legString;

            switch (legs)
            {
                case CallLeg.A:

                    legString = "aleg";
                    break;

                case CallLeg.B:

                    legString = "bleg";
                    break;

                default:
                case CallLeg.Both:

                    legString = "both";
                    break;
            }

            if (context.IsDialplan)
                context.Actions.Add(new SwitchExecuteAction("sched_broadcast", "+{0} '{1}' {2}", delaySeconds, Switch.ExpandFilePath(filePath), legString));
            else
                context.Actions.Add(new SwitchExecuteAction(CallID, "sched_broadcast", "+{0} '{1}' {2}", delaySeconds, Switch.ExpandFilePath(filePath), legString));
        }
    }
}
