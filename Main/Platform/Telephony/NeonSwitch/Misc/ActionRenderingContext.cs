//-----------------------------------------------------------------------------
// FILE:        ActionRenderingContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the rendering context that SwitchActions will use to
//              render their actions to the switch. 

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
    /// Holds the rendering context that <see cref="SwitchAction" />s will use to
    /// render their actions to the switch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="SwitchAction" /> implementations that override the <see cref="SwitchAction.Render" />
    /// method will need to add generated <see cref="SwitchExecuteAction" /> instances to the
    /// <see cref="Actions "/> list.
    /// </para>
    /// <para>
    /// The <see cref="Variables" /> collection holds the state of the action variables as
    /// they change over the course of rendering the dialplan.  The <see cref="SetVariableAction" /> 
    /// class updates this collection as its instances are rendered into the context.  Subsequent
    /// actions may examine these variables to modify how they render themselves.
    /// </para>
    /// </remarks>
    public class ActionRenderingContext
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Renders and then executes a set of actions. 
        /// </summary>
        /// <param name="actions">The hig-level actions.</param>
        internal static void Execute(params SwitchAction[] actions)
        {
            var context = new ActionRenderingContext(false);

            foreach (var action in actions)
                action.Render(context);

            context.ExecuteActions();
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Returns <c>true</c> if the actions are being rendered for a dialplan, <c>false</c>
        /// if the commands are being rendered for a session or event driven applications.
        /// </summary>
        public bool IsDialplan { get; private set; }

        /// <summary>
        /// Returns the collection of dialplan variables set during the course of
        /// rendering the actions.
        /// </summary>
        public ArgCollection Variables { get; private set; }

        /// <summary>
        /// Returns the collection of <see cref="SwitchExecuteAction" />s where
        /// <see cref="SwitchAction" />.<see cref="SwitchAction.Render" />
        /// methods will add the generated actions.
        /// </summary>
        public List<SwitchExecuteAction> Actions { get; private set; }

        /// <summary>
        /// Constructs a context for rendering commands.
        /// </summary>
        /// <param name="isDialplan">Pass <c>true</c> if we're rending commands for a dialplan.</param>
        internal ActionRenderingContext(bool isDialplan)
        {
            this.IsDialplan = isDialplan;
            this.Variables  = new ArgCollection(ArgCollectionType.Unconstrained);
            this.Actions    = new List<SwitchExecuteAction>();
        }

        /// <summary>
        /// Executes the actions added the context.
        /// </summary>
        /// <remarks>
        /// This is used by sync and async sessions to make things happen.
        /// </remarks>
        internal void ExecuteActions()
        {
            foreach (var action in Actions)
            {
                Switch.Execute(action.Application, action.Data);
            }
        }
    }
}
