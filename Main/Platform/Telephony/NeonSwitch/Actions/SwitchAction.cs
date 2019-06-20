//-----------------------------------------------------------------------------
// FILE:        SwitchAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Base class for switch actions to performed on an call after the call 
//              enters the executing state.

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
    /// Base class for switch actions to performed on an call after the call enters the executing state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Switch actions are assigned to calls during the call <b>routing</b> phase.  During this
    /// time, the basic decisions about what to do with a call are made.  This can occur within
    /// the underlying FreeSWITCH XML Dialplan processor or within a <see cref="Switch" /><see cref="Switch.DialPlanEvent" />
    /// handler.  Switch actions may also be generated during the execution of session based or
    /// event driven NeonSwitch applications.
    /// </para>
    /// <para>
    /// The essential purpose for a dialplan processor is to assign a list of routing actions 
    /// to be performed on the call once it enters the <b>executing</b> state.  Actions
    /// are specified as <b>&lt;action application="app-name" data="params" /&gt;</b>
    /// elements nested within <b>&lt;condition&gt;...&lt;condition /&gt;</b> tags.
    /// For NeonSwitch applications handling the <see cref="Switch.DialPlanEvent" />,
    /// the call actions are specified creating <see cref="SwitchAction" /> instances
    /// and adding them to the event's <see cref="DialPlanEventArgs"/>.<see cref="DialPlanEventArgs.Actions"/>
    /// list.
    /// </para>
    /// <para>
    /// Non-dialplan applications will invoke actions via the <see cref="CallSession" />.<see cref="CallSession.Execute(SwitchAction)" /> 
    /// and <see cref="CallState" />.<see cref="CallState.Execute(SwitchAction)" /> methods.
    /// </para>
    /// <para>
    /// The base <see cref="SwitchAction" /> class includes the protected <see cref="SwitchAction.Application" />
    /// and <see cref="SwitchAction.Data" /> properties that map exactly to what can
    /// be specified in an XML dialplan.  There are also several built-in derived classes such as
    /// <see cref="AnswerAction" />, <see cref="BridgeAction" />,
    /// <see cref="HangupAction" />, and others that can be easier to use in many situations
    /// by hiding the syntax of the underlying FreeSWITCH modules.
    /// </para>
    /// <para>
    /// NeonSwitch applications may also implement custom actions by deriving from
    /// <see cref="SwitchAction" /> and setting the <see cref="Application" /> and <see cref="Data" />
    /// properties as required or overriding the <see cref="Render" /> method.
    /// </para>
    /// <para>
    /// The virtual <see cref="Render" /> method is called by NeonSwitch when the
    /// action is to be serialized to dialplan or invoked by an application.  The default 
    /// implementation simply adds a single dialplan action based on the <see cref="Application" />
    /// and <see cref="Data" /> properties.  Override implementation may generate one or
    /// more actions based on other class properties.
    /// </para>
    /// <para>
    /// Action implementations should be prepared to render for a dialplan where the
    /// call ID is implied or form an application where commands need to be generated
    /// for a specific call.  Actions can use the <see cref="ActionRenderingContext" />.<see cref="ActionRenderingContext.IsDialplan" />
    /// property to determine whether the call ID is implied or not.
    /// </para>
    /// <note>
    /// Actions that cannot generate a command for a dialplan or an applications should
    /// throw a <see cref="NotSupportedException" />.
    /// </note>
    /// </remarks>
    public class SwitchAction
    {
        /// <summary>
        /// The target call ID or <see cref="Guid.Empty" /> for actions to be executed
        /// on a dialplan.
        /// </summary>
        public Guid CallID { get; set; }

        /// <summary>
        /// Returns the name of the NeonSwitch application to be invoked on the call.
        /// </summary>
        protected string Application { get; set; }

        /// <summary>
        /// Returns the parameter/data to be passed to the application (or <b>empty</b>).
        /// </summary>
        protected string Data { get; set; }

        /// <summary>
        /// Protected constructor that assumes the derived class will set the <see cref="Application" />
        /// and <see cref="Data" /> properties within its constructor.
        /// </summary>
        protected SwitchAction()
        {
            this.Application =
            this.Data        = string.Empty;
        }

        /// <summary>
        /// Constructs an action that calls a NeonSwitch application.
        /// </summary>
        /// <param name="application">The application name.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="application" /> is <c>null</c>.</exception>
        public SwitchAction(string application)
        {
            if (application == null)
                throw new ArgumentNullException("application");

            this.Application = application;
            this.Data        = string.Empty;
        }

        /// <summary>
        /// Constructs an action that calls a NeonSwitch application passing application
        /// parameters/data.
        /// </summary>
        /// <param name="application">The application name.</param>
        /// <param name="data">The parameter/data.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="application" /> is <c>null</c>.</exception>
        public SwitchAction(string application, string data)
        {
            if (application == null)
                throw new ArgumentNullException("application");

            if (data == null)
                data = string.Empty;

            this.Application = application;
            this.Data        = data;
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
        public virtual void Render(ActionRenderingContext context)
        {
            if (context.IsDialplan)
                context.Actions.Add(new SwitchExecuteAction(this.Application, this.Data));
            else
            {
                CheckCallID();
                context.Actions.Add(new SwitchExecuteAction(CallID, this.Application, this.Data));
            }
        }

        /// <summary>
        /// Verifies that the <see cref="CallID" /> property is not <c>null</c>.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown if <see cref="CallID" /> is <b>null.</b></exception>
        protected void CheckCallID()
        {
            if (CallID == Guid.Empty)
                throw new NotSupportedException(string.Format("[{0}] requires a valid [CallID] when not executing in a dialplan.", this.GetType().Name));
        }
    }
}
