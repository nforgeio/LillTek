//-----------------------------------------------------------------------------
// FILE:        FlushDtmfAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Flushes any queued DTMF digits received on a call.

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
    /// Flushes any queued DTMF digits received on a call.
    /// </summary>
    public class FlushDtmfAction : SwitchAction
    {
        /// <summary>
        /// Constructs an action to flush DTMF digits for current call in an executing dialplan.
        /// </summary>
        public FlushDtmfAction()
        {
        }

        /// <summary>
        /// Constructs an action to flush DTMF digits for a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        public FlushDtmfAction(Guid callID)
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
                context.Actions.Add(new SwitchExecuteAction("flush_dtmf"));
            else
            {
                CheckCallID();
                context.Actions.Add(new SwitchExecuteAction("uuid_flush_dtmf", "{0:D}", CallID));
            }
        }
    }
}
