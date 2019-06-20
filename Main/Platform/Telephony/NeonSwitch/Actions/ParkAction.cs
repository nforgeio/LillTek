//-----------------------------------------------------------------------------
// FILE:        ParkAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Parks the call on the switch.

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
    /// Parks the call on the switch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This action essentially disconnects the B-leg of the call and puts the
    /// call into limbo on the switch.  No audio will be transmitted in either
    /// direction while a call is parked (e.g. hold music).  The application
    /// will need to bridge or transfer the call connect it to another endpoint.
    /// </para>
    /// <para>
    /// Applications will typically park a call for a brief period of time
    /// just before they bridge the call.
    /// </para>
    /// </remarks>
    public class ParkAction : SwitchAction
    {
        /// <summary>
        /// Constructs an action to park the current call in an executing dialplan.
        /// </summary>
        public ParkAction()
        {
        }

        /// <summary>
        /// Constructs and action to park a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        public ParkAction(Guid callID)
        {
            this.CallID = callID;
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
                context.Actions.Add(new SwitchExecuteAction("park"));
            else
            {
                CheckCallID();
                context.Actions.Add(new SwitchExecuteAction("uuid_park", "{1:D}", CallID));
            }
        }
    }
}
