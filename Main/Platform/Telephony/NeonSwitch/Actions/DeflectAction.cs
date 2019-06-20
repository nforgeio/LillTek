//-----------------------------------------------------------------------------
// FILE:        DeflectAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Deflects an already answered call to a SIP endpoint.

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
    /// Deflects an already answered call to a SIP endpoint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This action can only be used on established calls (calls that have been
    /// answered).  NeonSwitch will hangup the call and then send the call source
    /// SIP <b>REFER</b> and <b>INVITE</b> messages requesting that it reroute the
    /// call.
    /// </para>
    /// <note>
    /// Applications that need to reroute unanswered calls should use
    /// <see cref="RedirectAction" /> instead.
    /// </note>
    /// </remarks>
    public class DeflectAction : SwitchAction
    {
        private string sipUri;

        /// <summary>
        /// Constructs an action that deflects the current call on an
        /// executing dial plan.
        /// </summary>
        /// <param name="sipUri">The SIP URI for the new destination.</param>
        public DeflectAction(string sipUri)
        {
            this.sipUri = sipUri;
        }

        /// <summary>
        /// Constructs an action that deflects a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="sipUri">The SIP URI for the new destination.</param>
        public DeflectAction(Guid callID, string sipUri)
        {
            this.CallID = callID;
            this.sipUri = sipUri;
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
                context.Actions.Add(new SwitchExecuteAction("deflect", sipUri));
            else
            {
                CheckCallID();
                context.Actions.Add(new SwitchExecuteAction("uuid_deflect", "{0:D} {1}", CallID, sipUri));
            }
        }
    }
}
