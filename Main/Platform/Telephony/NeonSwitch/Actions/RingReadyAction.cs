//-----------------------------------------------------------------------------
// FILE:        RingReadyAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Transmits a SIP 180 Ringing response to be sent to the call originator
//              which typically causes the caller's phone to simulate a ringing
//              sound without having to transfer media.

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
    /// Transmits a SIP <b>180 Ringing</b> response to be sent to the call originator
    /// which typically causes the caller's phone to simulate a ringing sound without 
    /// having to transfer media.
    /// </summary>
    public class RingReadyAction : SwitchAction
    {
        /// <summary>
        /// Constructs an action to send a <b>180 Ringing</b> response to the
        /// current call on an executing dialplan.
        /// </summary>
        public RingReadyAction()
        {
        }

        /// <summary>
        /// Constructs an action to send a <b>180 Ringing</b> response to a
        /// specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        public RingReadyAction(Guid callID)
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
                context.Actions.Add(new SwitchExecuteAction("ring_ready"));
            else
            {
                CheckCallID();
                context.Actions.Add(new SwitchExecuteAction(CallID, "ring_ready"));
            }
        }
    }
}
