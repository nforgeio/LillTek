//-----------------------------------------------------------------------------
// FILE:        StartSessionAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Starts a call session for a specific application.

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
    /// Starts a call session for a specific application.
    /// </summary>
    /// <remarks>
    /// <note>
    /// By default, this action will start a managed NeonSwitch application call
    /// session.  Set the <see cref="IsLowLevel" /> property to <c>true</c> to
    /// initiate a low-level unmanaged FreeSWITCH call session.
    /// </note>
    /// </remarks>
    public class StartSessionAction : SwitchAction
    {
        /// <summary>
        /// Constructs an action to assign the call on the currently 
        /// executing dialplan to a new application call session.
        /// </summary>
        /// <param name="application">The target application.</param>
        /// <param name="arguments">The session arguments.</param>
        public StartSessionAction(string application, string arguments)
        {
            this.Application = application;
            this.Data        = arguments;
        }

        /// <summary>
        /// Constructs an action to assign a specific call to a new 
        /// application call session.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="application">The target application.</param>
        /// <param name="arguments">The session arguments.</param>
        public StartSessionAction(Guid callID, string application, string arguments)
            : this(application, arguments)
        {
            this.CallID = callID;
        }

        /// <summary>
        /// Indicates whether the session is to be assigned to a low-level unmanaged
        /// application or to a managed NeonSwitch application.  This defaults to
        /// <c>false</c>.
        /// </summary>
        public bool IsLowLevel { get; set; }

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
            if (IsLowLevel)
            {
                if (context.IsDialplan)
                    context.Actions.Add(new SwitchExecuteAction(Application, Data));
                else
                {
                    CheckCallID();
                    context.Actions.Add(new SwitchExecuteAction(CallID, Application, Data));
                }
            }
            else
            {
                if (context.IsDialplan)
                    context.Actions.Add(new SwitchExecuteAction("managed", Application + " " + Data));
                else
                {
                    CheckCallID();
                    context.Actions.Add(new SwitchExecuteAction(CallID, "managed", Application + " " + Data));
                }
            }
        }
    }
}
