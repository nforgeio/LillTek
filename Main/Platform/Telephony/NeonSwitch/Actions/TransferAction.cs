//-----------------------------------------------------------------------------
// FILE:        TransferAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Transfers a call to a dialplan extension.

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
    /// Transfers a call to a dialplan extension.
    /// </summary>
    public class TransferAction : SwitchAction
    {
        private string extension;

        /// <summary>
        /// Constructs an action that transfers both legs of the current dialplan call to an
        /// extension in a dialplan context.
        /// </summary>
        /// <param name="extension">The extension number.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="extension"/> is <c>null</c> or empty.</exception>
        public TransferAction(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                throw new ArgumentException("Extension cannot be NULL or empty.", "extension");

            this.extension        = extension;
            this.DialplanContext  = "default";
            this.Delay            = TimeSpan.Zero;
            this.TransferBothLegs = true;
        }

        /// <summary>
        /// Constructs an action that transfers both legs of a specific call to an
        /// extension in a dialplan context.
        /// </summary>
        /// <param name="callID">Teh target call ID.</param>
        /// <param name="extension">The extension number.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="extension"/> is <c>null</c> or empty.</exception>
        public TransferAction(Guid callID, string extension)
            : this(extension)
        {
            this.CallID = callID;
        }

        /// <summary>
        /// Identifies the extension's dialplan context (defaults to <b>default</b>).
        /// </summary>
        public string DialplanContext { get; set; }

        /// <summary>
        /// Indicates whether the transfer is to be delayed (defaults to <b>no delay</b>).
        /// </summary>
        public TimeSpan Delay { get; set; }

        /// <summary>
        /// Indicates whether both legs of the call are to be transfered (defaults to <c>true</c>).  Set
        /// this to <c>false</c> to transfer only the B-leg.
        /// </summary>
        public bool TransferBothLegs { get; set; }

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
            var delaySeconds = SwitchHelper.GetScheduleSeconds(Delay);
            var legString    = TransferBothLegs ? "-both" : "-bleg";

            if (delaySeconds <= 0)
            {
                if (context.IsDialplan)
                    context.Actions.Add(new SwitchExecuteAction("transfer", "{0} {1} XML {2}", legString, extension, context));
                else
                    context.Actions.Add(new SwitchExecuteAction("uuid_transfer", "{0:D} {1} {2} XML {3}", CallID, legString, extension, DialplanContext));
            }
            else
            {
                if (context.IsDialplan)
                    context.Actions.Add(new SwitchExecuteAction("sched_transfer", "+{0} {1} {2} XML {3}", delaySeconds, legString, extension, DialplanContext));
                else
                    context.Actions.Add(new SwitchExecuteAction("sched_transfer", "+{0} {1:D} {2} {3} XML {4}", delaySeconds, CallID, legString, extension, DialplanContext));
            }
        }
    }
}
