//-----------------------------------------------------------------------------
// FILE:        FsConfigDialPlan.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Generates the XML document to be returned to FreeSWITCH when a 
//              dial plan configuration event handler returns successfully.

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
    /// Generates the XML document to be returned to FreeSWITCH when a 
    /// dial plan configuration event handler returns successfully.
    /// </summary>
    internal class FsConfigDialPlan : FsConfigBase
    {
        /// <summary>
        /// Constructs an empty configuration document for the named configuation section.
        /// </summary>
        /// <param name="context">The dial plan context.</param>
        /// <param name="actions">The actions associated with the dial plan.</param>
        public FsConfigDialPlan(string context, IEnumerable<SwitchExecuteAction> actions)
            : base("dialplan")
        {
            if (context == null)
                throw new ArgumentNullException("context");

            if (actions == null)
                throw new ArgumentNullException("actions");

            AddSectionChild(
                new XElement("context",
                    new XAttribute("name", context),
                    new XElement("extension",
                        new XAttribute("name", "extension"),
                        new XElement("condition",
                            from a in actions
                            select CreateActionElement(a)))));
        }

        /// <summary>
        /// Creates a propely formatted <b>action</b> <see cref="XElement" /> from
        /// an action string.
        /// </summary>
        /// <param name="action">The dial plan action.</param>
        /// <returns>The created <see cref="XElement" />.</returns>
        private static XElement CreateActionElement(SwitchExecuteAction action)
        {
            var element =
                new XElement("action",
                    new XAttribute("application", action.Application));

            if (!string.IsNullOrWhiteSpace(action.Data))
                element.Add(new XAttribute("data", action.Data));

            return element;
        }
    }
}
