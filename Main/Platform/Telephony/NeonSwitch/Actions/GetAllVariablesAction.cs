//-----------------------------------------------------------------------------
// FILE:        GetAllVariablesAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Writes call related information to the console for diagnostic 
//              or debugging purposes.

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
    /// Writes call related information to the console when executing on a dialplan
    /// and returns all session variables as XML when submitted to a specific call.
    /// </summary>
    public class GetAllVariablesAction : SwitchAction
    {
        /// <summary>
        /// Constructs an action to dump information about the current call in an
        /// executing dialplan.
        /// </summary>
        public GetAllVariablesAction()
        {
        }

        /// <summary>
        /// Constructs an action to dump information about a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        public GetAllVariablesAction(Guid callID)
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
            string      app;
            string      args;

            if (context.IsDialplan)
            {
                app  = "dump";
                args = string.Empty;
            }
            else
            {
                CheckCallID();

                app  = "uuid_dump";
                args = string.Format("{0:D}", CallID); ;
            }

            context.Actions.Add(new SwitchExecuteAction(app, args));
        }
    }
}
