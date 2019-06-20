//-----------------------------------------------------------------------------
// FILE:        LogAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Logs text to the console.

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
    /// <b>Dial-plan only:</b> Logs text to the console.
    /// </summary>
    public class LogAction : SwitchAction
    {
        /// <summary>
        /// <b>Dialplan only:</b> Constructs an action that logs text at the <see cref="SwitchLogLevel.Debug "/> level.
        /// </summary>
        /// <param name="text">The text to be logged.</param>
        public LogAction(string text)
            : base("log", text)
        {
        }

        /// <summary>
        /// Constructs an action that logs text at the specified level.
        /// </summary>
        /// <param name="level">The new log level.</param>
        /// <param name="text">The text to be logged.</param>
        public LogAction(SwitchLogLevel level, string text)
            : base("log", string.Format("{0} {1}", SwitchHelper.GetSwitchLogLevelString(level), text))
        {
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
                throw new NotSupportedException("[LogAction] is not supported outside of a dialplan.  Use [Switch.Log()] instead.");

            base.Render(context);
        }
    }
}
