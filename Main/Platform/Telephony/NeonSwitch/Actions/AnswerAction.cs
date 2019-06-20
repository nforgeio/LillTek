//-----------------------------------------------------------------------------
// FILE:        AnswerAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Answers a call.

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
    /// Answers a call.
    /// </summary>
    public class AnswerAction : SwitchAction
    {
        /// <summary>
        /// Constructs an action to answer the current call on an executing dialplan.
        /// </summary>
        public AnswerAction()
        {
        }

        /// <summary>
        /// Constructs an action to answer a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        public AnswerAction(Guid callID)
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
            if (context.IsDialplan)
                context.Actions.Add(new SwitchExecuteAction("answer"));
            else
            {
                CheckCallID();
                context.Actions.Add(new SwitchExecuteAction(CallID, "answer"));
            }
        }
    }
}
