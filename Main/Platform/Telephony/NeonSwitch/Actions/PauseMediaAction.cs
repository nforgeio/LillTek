//-----------------------------------------------------------------------------
// FILE:        PauseMediaAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Pauses or resumes the playing of media.

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
    /// Pauses or resumes the playing of media.
    /// </summary>
    public class PauseMediaAction : SwitchAction
    {
        private bool pause;

        /// <summary>
        /// Pauses or resumes the media playing on the current call in
        /// an executing dialplan.
        /// </summary>
        /// <param name="pause">Pass <c>true</c> to pause the media, <c>false</c> to resume it.</param>
        public PauseMediaAction(bool pause)
        {
            this.pause = pause;
        }

        /// <summary>
        /// Pauses or resumes the media playing on a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="pause">Pass <c>true</c> to pause the media, <c>false</c> to resume it.</param>
        public PauseMediaAction(Guid callID, bool pause)
        {

            this.CallID = callID;
            this.pause  = pause;
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
            var pauseString = pause ? "on" : "off";

            if (context.IsDialplan)
                context.Actions.Add(new SwitchExecuteAction("pause", "{{$Unique-ID}} {0}", pauseString));
            else
                context.Actions.Add(new SwitchExecuteAction(CallID, "pause", pauseString));
        }
    }
}
