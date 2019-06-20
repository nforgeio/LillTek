//-----------------------------------------------------------------------------
// FILE:        HoldAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Sets or clears a call's hold status.

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
    /// Sets or clears a call's hold status.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Placing a call on hold essentially mutes the audio in both directions
    /// but the call still remains established between the endpoints.  This
    /// differs from <b>parking</b> a call, where the call is disconnected
    /// and reconnected to a holding area on the switch.
    /// </para>
    /// </remarks>
    public class HoldAction : SwitchAction
    {
        private bool hold;

        /// <summary>
        /// Constructs an action that places the current call of an executing
        /// dialplan on hold.
        /// </summary>
        public HoldAction()
        {
            this.hold = true;
        }

        /// <summary>
        /// Constructs an action that places the current call of an executing
        /// dialplan into the specified hold state.
        /// </summary>
        /// <param name="hold">
        /// Pass <c>true</c> to place the call on hold, <c>false</c> to
        /// re-enable the call.
        /// </param>
        public HoldAction(bool hold)
        {
            this.hold = hold;
        }

        /// <summary>
        /// Constructs an action that places a specific call on hold.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        public HoldAction(Guid callID)
        {
            this.CallID = callID;
            this.hold   = true;
        }

        /// <summary>
        /// Constructs an action that places a specific call into the
        /// desired hold state.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="hold">
        /// Pass <c>true</c> to place the call on hold, <c>false</c> to
        /// re-enable the call.
        /// </param>
        public HoldAction(Guid callID, bool hold)
        {
            this.CallID = callID;
            this.hold   = hold;
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
            if (!context.IsDialplan)
                context.Actions.Add(new SwitchExecuteAction(hold ? "hold" : "unhold"));
            else
            {
                CheckCallID();
                context.Actions.Add(new SwitchExecuteAction("uuid_hold", "{0}{1:D}", hold ? string.Empty : "off ", CallID));
            }
        }
    }
}
