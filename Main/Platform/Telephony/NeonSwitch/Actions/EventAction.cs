//-----------------------------------------------------------------------------
// FILE:        EventAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A dialplan action that submits an event to NeonSwitch.

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

// $todo(jeff.lill):
//
// The current implementation will have potential conflicts if the
// event arguments have embedded commas in their values.

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// <b>Dialplan only:</b> An action that submits an event to NeonSwitch.
    /// </summary>
    public class EventAction : SwitchAction
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="args">The event arguments.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="args"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if the argument collection is empty.</exception>
        /// <exception cref="NotSupportedException">Thrown if any of the argument names or values have embedded commas (,).</exception>
        /// <remarks>
        /// <note>
        /// This action will set the event type to be <see cref="SwitchEventCode.Custom" /> if no
        /// other value is set in the arguments.
        /// </note>
        /// </remarks>
        public EventAction(ArgCollection args)
            : base("event")
        {
            if (args == null)
                throw new ArgumentException("args");

            if (args.Count == 0)
                throw new ArgumentException("[EventAction] expects at least one argument.", "args");

            if (!args.ContainsKey("Event-Name"))
                args["Event-Name"] = "CUSTOM";

            foreach (var key in args)
            {
                if (key.IndexOf(',') != -1)
                    throw new NotSupportedException(string.Format("[EventAction] does not support arguments whose names include a comma, such as in [{0}].", key));

                if (args[key].IndexOf(',') != -1)
                    throw new NotSupportedException(string.Format("[EventAction] does not support arguments whose values include a comma, such as in [{0}={1}].", key, args[key]));
            }

            var sb = new StringBuilder();

            foreach (var key in args)
                sb.AppendFormat("{0}={1},", key, args[key]);

            base.Data = sb.ToString();
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
            if (!context.IsDialplan)
                throw new NotSupportedException("[EventAction] is not supported outside of a dialplan.  Use [Switch.SendEvent()] instead.");

            base.Render(context);
        }
    }
}
