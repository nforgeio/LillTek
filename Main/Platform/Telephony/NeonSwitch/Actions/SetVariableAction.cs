//-----------------------------------------------------------------------------
// FILE:        SetVariableAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Sets or optionally unsets (removes) a call variable.

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
    /// <b>Sets</b> or optionally <b>unsets</b> (removes) a call variable.
    /// </summary>
    public class SetVariableAction : SwitchAction
    {
        private string      name;
        private string      value;

        /// <summary>
        /// Constructs an action that removes a named channel variable for
        /// the current call on an executing dialplan.
        /// </summary>
        /// <param name="name">The variable name.</param>
        public SetVariableAction(string name)
        {
            this.name  = name;
            this.value = null;
        }

        /// <summary>
        /// Constructs an action that assigns a value to a named call variable
        /// for the current call on an executing dialplan.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <param name="value">The new value (or <c>null</c> to unset the variable).</param>
        public SetVariableAction(string name, string value)
        {
            this.name  = name;
            this.value = value;
        }

        /// <summary>
        /// Constructs an action that removes a named channel variable for
        /// a specific call.
        /// </summary>
        /// <param name="callID">The target callID.</param>
        /// <param name="name">The variable name.</param>
        public SetVariableAction(Guid callID, string name)
        {
            this.CallID = callID;
            this.name   = name;
            this.value  = null;
        }

        /// <summary>
        /// Constructs an action that assigns a value to a named call variable
        /// a specific call.
        /// </summary>
        /// <param name="callID">The target callID.</param>
        /// <param name="name">The variable name.</param>
        /// <param name="value">TThe new value (or <c>null</c> to unset the variable).</param>
        public SetVariableAction(Guid callID, string name, string value)
        {
            this.CallID = callID;
            this.name   = name;
            this.value  = value;
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
                CheckCallID();

            // Always generate [unset] actions since we don't know what the
            // the initial state of the action variables are and the application
            // may wish to remove a pre-existing variable.

            if (value == null)
            {
                if (context.IsDialplan)
                    context.Actions.Add(new SwitchExecuteAction("unset", name));
                else
                    context.Actions.Add(new SwitchExecuteAction("uuid_setvar", "{0:D} {1}", CallID, name));

                return;
            }

            // When setting variable, check to see if the variable has already been
            // set to the same value.  Render an action only if this is not the case.

            string currentValue;

            if (!context.Variables.TryGetValue(name, out currentValue) || currentValue != value)
            {
                context.Variables[name] = value;

                if (context.IsDialplan)
                    context.Actions.Add(new SwitchExecuteAction("set", "{0}={1}", name, value));
                else
                    context.Actions.Add(new SwitchExecuteAction("uuid_setvar", "{0:D} {1} {2}", CallID, name, value));
            }
        }
    }
}
