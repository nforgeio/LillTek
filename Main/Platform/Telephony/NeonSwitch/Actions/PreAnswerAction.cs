//-----------------------------------------------------------------------------
// FILE:        PreAnswerAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Establishes early media transfer with the caller but does not 
//              actually answer the call.

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
    /// Establishes early media transfer with the caller but does not actually answer the call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is equalivalent to SIP status code 183 with SDP information.  This
    /// will typically be used to enable the transmission of a ringing tone or other
    /// audio back to the caller's device\ before the call is actually answered.
    /// </para>
    /// </remarks>
    public class PreAnswerAction : SwitchAction
    {
        /// <summary>
        /// Constructs an action that pre-answers the current call on an executing dialplan.
        /// </summary>
        public PreAnswerAction()
        {
        }

        /// <summary>
        /// Constructs an action that pre-answers a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        public PreAnswerAction(Guid callID)
        {
            this.CallID = callID;
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
            if (context.IsDialplan)
                context.Actions.Add(new SwitchExecuteAction("pre_answer"));
            else
            {
                CheckCallID();
                context.Actions.Add(new SwitchExecuteAction(CallID, "pre_answer"));
            }
        }
    }
}
