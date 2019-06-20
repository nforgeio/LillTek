//-----------------------------------------------------------------------------
// FILE:        RedirectAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Redirects an unanswered call to one or more SIP endpoints.

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
    /// Redirects an unanswered call to one or more SIP endpoints.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This action can only be used on unanswered calls.  NeonSwitch will
    /// respond to the call source with a <b>302 Moved Temporarily</b> response
    /// if a single SIP endpoint is passed and a <b>300 Multiple Choices</b>
    /// response for multiple SIP endpoints.
    /// </para>
    /// <note>
    /// Applications that need to reroute answered calls should use
    /// <see cref="DeflectAction" /> instead.
    /// </note>
    /// </remarks>
    public class RedirectAction : SwitchAction
    {
        private string[] sipUris;

        /// <summary>
        /// Constructs an action to redirect the current call of an executing dialplan
        /// to one or more SIP endpoints.
        /// </summary>
        /// <param name="sipUris">The SIP URIs.</param>
        public RedirectAction(params string[] sipUris)
        {
            if (sipUris == null || sipUris.Length == 0)
                throw new ArgumentException("[RedirectAction] requires at least one SIP URI.", "sipUris");

            this.sipUris = sipUris;
        }

        /// <summary>
        /// Constructs an action to redirect the a specific call to one or more SIP endpoints.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="sipUris">The SIP URIs.</param>
        public RedirectAction(Guid callID, params string[] sipUris)
            : this(sipUris)
        {
            this.CallID = callID;
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
            string formattedUris;

            if (sipUris.Length == 1)
                formattedUris = sipUris[0];
            else
            {
                var sb = new StringBuilder();

                foreach (var uri in sipUris)
                    sb.AppendFormat("{0},", uri);

                if (sb.Length > 0)
                    sb.Length--;

                formattedUris = sb.ToString();
            }

            if (context.IsDialplan)
                context.Actions.Add(new SwitchExecuteAction("redirect", formattedUris));
            else
            {
                CheckCallID();
                context.Actions.Add(new SwitchExecuteAction(CallID, "redirect", formattedUris));
            }
        }
    }
}
