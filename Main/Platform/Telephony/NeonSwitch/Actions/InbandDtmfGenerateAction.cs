//-----------------------------------------------------------------------------
// FILE:        InbandDtmfGenerateAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enables or disables the generation of in-band DTMF tones.

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
    /// Enables or disables the generation of in-band DTMF tones.
    /// </summary>
    /// <remarks>
    /// <note>
    /// In-band generation of DTMF tones is disabled by default for new calls.
    /// </note>
    /// </remarks>
    public class InbandDtmfGenerateAction : SwitchAction
    {
        private bool enable;

        /// <summary>
        /// Constructs an action that enables or disables the generation of
        /// in-band DTMF tones for the current call on the executing dialplan.
        /// </summary>
        /// <param name="enable">Pass <c>true</c> to enable in-band DTMF tones, <c>false</c> for out-of-band tones.</param>
        public InbandDtmfGenerateAction(bool enable)
        {
            this.enable = enable;
        }

        /// <summary>
        /// Constructs an action that enables or disables the generation of
        /// in-band DTMF tones for a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="enable">Pass <c>true</c> to enable in-band DTMF tones, <c>false</c> for out-of-band tones.</param>
        public InbandDtmfGenerateAction(Guid callID, bool enable)
        {
            this.CallID = callID;
            this.enable = enable;
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
            var command = enable ? "start_dtmf_generate" : "stop_dtmf_generate";

            if (context.IsDialplan)
                context.Actions.Add(new SwitchExecuteAction(command));
            else
            {
                CheckCallID();
                context.Actions.Add(new SwitchExecuteAction(CallID, command));
            }
        }
    }
}
