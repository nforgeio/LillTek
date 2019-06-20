//-----------------------------------------------------------------------------
// FILE:        BreakAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Directs the switch  to cancel the current operation or optionally 
//              all pending applications in the dialplan.

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
    /// Directs the switch  to cancel the current operation or optionally 
    /// all pending applications in the dialplan.
    /// </summary>
    public class BreakAction : SwitchAction
    {
        private bool all;

        /// <summary>
        /// Constructs an action to stop the current application executing in a dialplan.
        /// </summary>
        public BreakAction()
        {
            this.all = false;
        }

        /// <summary>
        /// Constructs an action to stop the current application and optionally
        /// all pending actions in the dialplan.
        /// </summary>
        /// <param name="all">Pass <c>true</c> to remove all pending actions.</param>
        public BreakAction(bool all)
        {
            this.all = all;
        }

        /// <summary>
        /// Constructs an action to stop the current application on a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        public BreakAction(Guid callID)
        {
            this.CallID = callID;
            this.all    = false;
        }

        /// <summary>
        /// Constructs an action to stop the current application and optionally
        /// app pending actions on a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="all">Pass <c>true</c> to remove all pending actions.</param>
        public BreakAction(Guid callID, bool all)
        {
            this.CallID = callID;
            this.all    = all;
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
                context.Actions.Add(new SwitchExecuteAction("break", all ? "all" : string.Empty));
            else
            {
                CheckCallID();
                context.Actions.Add(new SwitchExecuteAction("uuid_break", "{0:D}{1}", CallID, all ? " all" : string.Empty));
            }
        }
    }
}
