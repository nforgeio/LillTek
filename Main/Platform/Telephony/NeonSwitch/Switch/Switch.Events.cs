//-----------------------------------------------------------------------------
// FILE:        Switch.Events.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the NeonSwitch eventing.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;
using LillTek.Telephony.Common;

namespace LillTek.Telephony.NeonSwitch
{
    public static partial class Switch
    {
        private static IDisposable                                      eventBinding;
        private static EventConsumer                                    eventConsumer;
        private static bool                                             stopPending;
        private static event EventHandler<DialPlanEventArgs>            dialPlanEvent;
        private static event EventHandler<UserDirectoryEventArgs>       userDirectoryEvent;
        private static event EventHandler<CallSessionArgs>              callSessionEvent;
        private static event EventHandler<ExecuteEventArgs>             executeEvent;
        private static event EventHandler<ExecuteBackgroundEventArgs>   executeBackgroundEvent;
        private static event EventHandler<SwitchEventArgs>              eventReceived;
        private static event EventHandler<JobCompletedEventArgs>        jobCompletedEvent;
        private static Dictionary<string, Type>                         subcommandHandlers;

        /// <summary>
        /// Initializes switch event handling support.
        /// </summary>
        private static void InitEventHandling()
        {
            subcommandHandlers = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reflects an assembly looking for public classes that implement <see cref="ISwitchSubcommand" />
        /// and registers any found with the switch command dispatch subsystem.
        /// </summary>
        /// <param name="assembly">The source assembly.</param>
        /// <remarks>
        /// <note>
        /// The application may call this method <b>only</b> when executing within its
        /// <see cref="SwitchApp.Main" /> method.
        /// </note>
        /// <para>
        /// This method registers the name of the class without the namespace and
        /// also stripping the "Command" suffix if present as the name of the
        /// subcommand.
        /// </para>
        /// <note>
        /// Registered subcommands are case insensitive.
        /// </note>
        /// </remarks>
        public static void RegisterAssemblySubcommands(Assembly assembly)
        {
            if (!SwitchApp.InMain)
                throw new InvalidOperationException("[RegisterAssemblySubcommands] may be called only from within the SwitchApp.Main() method.");

            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsPublic || !typeof(ISwitchSubcommand).IsAssignableFrom(type))
                    continue;

                string  subcommand = type.Name.ToLowerInvariant();
                Type    existing;

                if (subcommand.EndsWith("command"))
                    subcommand = subcommand.Substring(0, subcommand.Length - "command".Length);

                if (subcommandHandlers.TryGetValue(subcommand, out existing))
                {
                    SysLog.LogWarning("NeonSwitch subcommand conflict. Subcommand class [{0}] conflicts with [{1}].  [{1}] will be used.", type.FullName, existing.FullName);
                    continue;
                }

                subcommandHandlers.Add(subcommand, type);
            }
        }

        /// <summary>
        /// Processes events received from FreeSWITCH and dispatches them to the
        /// application event handlers.  This is called from <see cref="SwitchApp" />
        /// immediately after <see cref="SwitchApp.Main" /> returns on the
        /// application thread.
        /// </summary>
        internal static void EventLoop()
        {
            // Enlist in low-level global events if the application requested.

            var options = (switch_xml_section_enum_t)0;

            if (dialPlanEvent != null)
                options |= switch_xml_section_enum_t.SWITCH_XML_SECTION_DIALPLAN;

            if (userDirectoryEvent != null)
                options |= switch_xml_section_enum_t.SWITCH_XML_SECTION_DIRECTORY;

            if (options != (switch_xml_section_enum_t)0)
                eventBinding = SwitchXmlSearchBinding.Bind(OnSwitchXmlSearchEvent, options);

            // Decided whether we're going to actually consume NeonSwitch events based
            // on whether the application enlisted in the [EventReceived] event within
            // its [Main()] method.  Note that we we'll exit the event loop (and the
            // application's background thread) immediately if this is the case.

            if (eventReceived == null)
                return;

            eventConsumer = new EventConsumer("all", string.Empty, 0);

            // Loop, waiting for the application to terminate.

            while (!stopPending)
            {
                try
                {
                    var fsEvent     = eventConsumer.pop(1, 0);
                    var switchEvent = new SwitchEvent(fsEvent.InternalEvent);

                    RaiseEventReceived(new SwitchEventArgs(switchEvent));

                    if (switchEvent.EventType == SwitchEventCode.BackgroundJob)
                        RaiseJobCompletedEvent(new JobCompletedEventArgs(switchEvent.Headers.Get("Job-ID", Guid.Empty)));
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }
        }

        /// <summary>
        /// Signals the <see cref="EventLoop" /> to stop processing events and
        /// exit.  This is called by <see cref="SwitchApp" /> during the application
        /// shut down process.
        /// </summary>
        internal static void StopEventLoop()
        {
            stopPending = true;
        }

        /// <summary>
        /// Called by <see cref="SwitchApp" /> at th very end of the application
        /// shut down process to give this class the chance to release any resources
        /// it holds.
        /// </summary>
        internal static void Shutdown()
        {
            if (eventBinding != null)
            {
                eventBinding.Dispose();
                eventBinding = null;
            }

            if (eventConsumer != null)
            {
                eventConsumer.Dispose();
                eventConsumer = null;
            }
        }

        /// <summary>
        /// Called by FreeSWITCH when one of the bound events is raised.
        /// </summary>
        /// <param name="args">The event arguments.</param>
        /// <returns>The event results as an XML document.</returns>
        private static string OnSwitchXmlSearchEvent(SwitchXmlSearchBinding.XmlBindingArgs args)
        {
            string result = null;

            try
            {
                switch (args.Section.ToLower())
                {
                    case "directory":
                        {
                            if (userDirectoryEvent == null)
                                break;

                            var switchEvent = new SwitchEvent(args.Parameters);
                            var action = switchEvent.Headers.Get("action", string.Empty).ToLower();

                            if (action != "sip_auth")
                                return FsConfigNotFound.Xml;    // Ignore non-user directory operations

                            var directoryArgs = new UserDirectoryEventArgs(new SwitchEvent(args.Parameters));

                            userDirectoryEvent(null, directoryArgs);
                            if (directoryArgs.Handled)
                            {
                                if (directoryArgs.Password == null)
                                    directoryArgs.Password = string.Empty;

                                if (directoryArgs.AccessDenied)
                                {
                                    // $hack(jeff.lill): 
                                    //
                                    // There's doesn't appear to be a clean way to tell the switch that the user
                                    // is valid but that access should be denied and no additional lookup should
                                    // be performed by other modules.  I'm going to handle this by setting the
                                    // password to guid.

                                    directoryArgs.Password = Guid.NewGuid().ToString("N");
                                }

                                // Validate and set the common parameters/variables.

                                try
                                {
                                    Dtmf.ValidateNumeric(directoryArgs.VoiceMailPassword, 0, int.MaxValue, true);
                                }
                                catch (Exception e)
                                {
                                    SysLog.LogWarning("UserDirectoryEvent handler ignored [VoiceMailPassword]: {0}", e.Message);
                                }

                                try
                                {
                                    Dtmf.ValidateNumeric(directoryArgs.EffectiveCallerIDNumber, 0, 11, true);
                                }
                                catch (Exception e)
                                {
                                    SysLog.LogWarning("UserDirectoryEvent handler ignored [EffectiveCallerIDNumber]: {0}", e.Message);
                                }

                                try
                                {
                                    Dtmf.ValidateNumeric(directoryArgs.OutboundCallerIDNumber, 0, 11, true);
                                }
                                catch (Exception e)
                                {
                                    SysLog.LogWarning("UserDirectoryEvent handler ignored [OutboundCallerIDNumber]: {0}", e.Message);
                                }

                                if (directoryArgs.VoiceMailPassword != null)
                                    directoryArgs.Parameters["vm-password"] = directoryArgs.VoiceMailPassword;

                                if (directoryArgs.CallingRights != CallingRight.None)
                                {
                                    var sb = new StringBuilder();

                                    if ((directoryArgs.CallingRights & CallingRight.Local) != 0)
                                        sb.Append("local");

                                    if ((directoryArgs.CallingRights & CallingRight.Domestic) != 0)
                                    {
                                        if (sb.Length > 0)
                                            sb.Append(',');

                                        sb.Append("domestic");
                                    }

                                    if ((directoryArgs.CallingRights & CallingRight.International) != 0)
                                    {
                                        if (sb.Length > 0)
                                            sb.Append(',');

                                        sb.Append("international");
                                    }

                                    directoryArgs.Variables["toll_allow"] = sb.ToString();
                                }

                                if (directoryArgs.AccountCode != null)
                                    directoryArgs.Variables["accountcode"] = directoryArgs.AccountCode;

                                if (directoryArgs.CallerContext != null)
                                    directoryArgs.Variables["user_context"] = directoryArgs.CallerContext;

                                if (directoryArgs.EffectiveCallerIDName != null)
                                    directoryArgs.Variables["effective_caller_id_name"] = directoryArgs.EffectiveCallerIDName;

                                if (directoryArgs.EffectiveCallerIDNumber != null)
                                    directoryArgs.Variables["effective_caller_id_number"] = directoryArgs.EffectiveCallerIDNumber;

                                if (directoryArgs.OutboundCallerIDName != null)
                                    directoryArgs.Variables["outbound_caller_id_name"] = directoryArgs.OutboundCallerIDName;

                                if (directoryArgs.OutboundCallerIDNumber != null)
                                    directoryArgs.Variables["outbound_caller_id_number"] = directoryArgs.OutboundCallerIDNumber;

                                if (directoryArgs.CallGroup != null)
                                    directoryArgs.Variables["callgroup"] = directoryArgs.CallGroup;

                                result = new FsConfigDirectory(directoryArgs.Domain, directoryArgs.UserID, directoryArgs.Password,
                                                               directoryArgs.Parameters, directoryArgs.Variables).ToXml();

                                Debug.WriteLine(result);    // $todo(jeff.lill): Delete this
                            }
                        }
                        break;

                    case "dialplan":
                        {
                            if (dialPlanEvent == null)
                                break;

                            var dialPlanArgs = new DialPlanEventArgs(new SwitchEvent(args.Parameters));

                            dialPlanEvent(null, dialPlanArgs);
                            if (dialPlanArgs.Handled)
                            {
                                if (dialPlanArgs.Context == null)
                                    throw new ArgumentException("DialPlanEvent handler returned [Context=null].");

                                var renderContext = new ActionRenderingContext(true);

                                foreach (var action in dialPlanArgs.Actions)
                                    action.Render(renderContext);

                                result = new FsConfigDialPlan(dialPlanArgs.Context, renderContext.Actions).ToXml();
                            }
                        }
                        break;

                    default:

                        result = null;
                        break;
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }

            return result ?? FsConfigNotFound.Xml;
        }

        /// <summary>
        /// Used to verify that an event subscription is being performed within the
        /// context of the <see cref="SwitchApp.Main" /> method.
        /// </summary>
        /// <param name="eventName">Name of the event being set.</param>
        /// <exception cref="InvalidOperationException">Thrown if we're not in Main().</exception>
        private static void VerifyEventInMain(string eventName)
        {
            if (!SwitchApp.InMain)
                throw new InvalidOperationException(string.Format("[{0}] handlers may only be set within the SwitchApp.Main() method.", eventName));
        }

        /// <summary>
        /// Throws a <see cref="InvalidOperationException" /> describing how an event subscription cannot be removed.
        /// </summary>
        /// <param name="eventName">Name of the event.</param>
        /// <exception cref="InvalidOperationException">Thrown always.</exception>
        private static void ThrowEventRemoveException(string eventName)
        {
            throw new InvalidOperationException(string.Format("[{0}] does not support the removal of event handlers.", eventName));
        }

        /// <summary>
        /// Overrides the default dial-plan handling by allowing the application to
        /// make all switch routing decisions at a low-level.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if a handler is added outside of the <see cref="SwitchApp.Main" /> method 
        /// or an attempt is made to remove a handler.
        /// </exception>
        /// <remarks>
        /// <note>
        /// The application may enlist in this event <b>only</b> when executing within its
        /// <see cref="SwitchApp.Main" /> method.  Also, event handlers cannot be removed.
        /// </note>
        /// <para>
        /// The <b>dial plan event handler</b> provides the application with the opportunity to
        /// decide exactly what will happen to the call by specifying dial plan actions.  During this
        /// time, the basic decisions about what to do with a call are made.
        /// handler.
        /// </para>
        /// <para>
        /// The essential purpose for a dialplan event handler is to assign a list of actions to
        /// be performed on the call once it enters the <b>executing</b> state.  Actions
        /// are specified as <b>&lt;action application="app-name" data="params" /&gt;</b>
        /// elements nested within <b>&lt;condition&gt;...&lt;condition /&gt;</b> tags.
        /// For NeonSwitch applications handling the <see cref="Switch.DialPlanEvent" />,
        /// the call actions are specified creating <see cref="SwitchAction" /> instances
        /// and adding them to the event's <see cref="DialPlanEventArgs"/>.<see cref="DialPlanEventArgs.Actions"/>
        /// list.
        /// </para>
        /// <para>
        /// The base <see cref="SwitchAction" /> class includes the <see cref="SwitchAction.Application" />
        /// and <see cref="SwitchAction.Data" /> properties that map exactly to what can
        /// be specified in an XML dialplan.  There are also several derived classes such as
        /// <see cref="AnswerAction" />, <see cref="BridgeAction" />, <see cref="HangupAction" />, 
        /// and others that can be easier to use in many situations
        /// by hiding the syntax of the underlying FreeSWITCH modules.
        /// </para>
        /// </remarks>
        public static event EventHandler<DialPlanEventArgs> DialPlanEvent
        {
            add
            {
                VerifyEventInMain("DialPlanEvent");
                dialPlanEvent += value;
            }

            remove { ThrowEventRemoveException("DialPlanEvent"); }
        }

        /// <summary>
        /// Overrides the default user directory lookup handling by allowing the application
        /// to provide a custom implementation.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if a handler is added outside of the <see cref="SwitchApp.Main" /> method 
        /// or an attempt is made to remove a handler.
        /// </exception>
        /// <remarks>
        /// <note>
        /// The application may enlist in this event <b>only</b> when executing within its
        /// <see cref="SwitchApp.Main" /> method.  Also, event handlers cannot be removed.
        /// </note>
        /// <para>
        /// The <b>user directory event handler</b> is responsible for looking up information about a user including
        /// the dialplan context to use to process calls from this user potentially their login credentials.  This
        /// event provides the application with the opportunity to customize how this generated.
        /// </para>
        /// <para>
        /// The <see cref="GenericSwitchEventArgs{TEvent}.SwitchEvent" /> property will be set to the low-level 
        /// <see cref="SwitchEvent" /> that triggered the directory lookup and
        /// the <see cref="UserDirectoryEventArgs.Action" />, <see cref="UserDirectoryEventArgs.Domain" />, 
        /// <see cref="UserDirectoryEventArgs.UserID" />,  and <see cref="UserDirectoryEventArgs.IPAddress" /> 
        /// properties will be set to the query
        /// parameters.
        /// </para>
        /// <para>
        /// The event handler should use these input properties to identify the user.  If a valid user
        /// is identified, the handler should set <see cref="UserDirectoryEventArgs.Handled" /> to 
        /// <c>true</c>, set <see cref="UserDirectoryEventArgs.Password" /> to the user's password and 
        /// optionally specify custom parameters and variables for the user by adding them to the 
        /// <see cref="UserDirectoryEventArgs.Parameters" /> and <see cref="UserDirectoryEventArgs.Variables" /> collections.
        /// </para>
        /// <para>
        /// If the handler wishes to deny access to the user it should set <see cref="UserDirectoryEventArgs.Handled" />
        /// and <see cref="UserDirectoryEventArgs.AccessDenied" /> to <c>true</c>.
        /// </para>
        /// <para>
        /// Handlers may also decline to look up a user by leaving <see cref="UserDirectoryEventArgs.Handled" /> 
        /// set to  <c>false</c>.  This will allow other handlers, including the the default XML directory
        /// implementation to perform the lookup.
        /// </para>
        /// <para>
        /// Some common parameters and variables can be set using the <see cref="UserDirectoryEventArgs.VoiceMailPassword" />,
        /// <see cref="UserDirectoryEventArgs.CallingRights" />, <see cref="UserDirectoryEventArgs.AccountCode" />,
        /// <see cref="UserDirectoryEventArgs.CallerContext" />, <see cref="UserDirectoryEventArgs.EffectiveCallerIDName" />,
        /// <see cref="UserDirectoryEventArgs.EffectiveCallerIDNumber" />, <see cref="UserDirectoryEventArgs.OutboundCallerIDName" />,
        /// <see cref="UserDirectoryEventArgs.OutboundCallerIDNumber" />, and <see cref="UserDirectoryEventArgs.CallGroup" />
        /// properties.  The switch will automatically add these values to the appropriate collection before
        /// processing the result.
        /// </para>
        /// </remarks>
        public static event EventHandler<UserDirectoryEventArgs> UserDirectoryEvent
        {
            add
            {
                VerifyEventInMain("UserDirectoryEvent");
                userDirectoryEvent += value;
            }

            remove { ThrowEventRemoveException("UserDirectoryEvent"); }
        }

        /// <summary>
        /// Raised when NeonSwitch is assigned a call session for execution.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if a handler is added outside of the <see cref="SwitchApp.Main" /> method 
        /// or an attempt is made to remove a handler.
        /// </exception>
        /// <remarks>
        /// <note>
        /// The application may enlist in this event <b>only</b> when executing within its
        /// <see cref="SwitchApp.Main" /> method.  Also, event handlers cannot be removed.
        /// </note>
        /// </remarks>
        public static event EventHandler<CallSessionArgs> CallSessionEvent
        {
            add
            {
                VerifyEventInMain("CallSessionEvent");
                callSessionEvent += value;
            }

            remove { ThrowEventRemoveException("CallSessionEvent"); }
        }

        /// <summary>
        /// Raises the <see cref="CallSessionEvent"/> event.
        /// </summary>
        /// <param name="args">The event arguments.</param>
        internal static void RaiseCallSessionEvent(CallSessionArgs args)
        {
            try
            {
                if (callSessionEvent != null)
                    callSessionEvent(null, args);
            }
            catch (Exception e)
            {

                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Raised when NeonSwitch invokes a synchronous application command targeted at
        /// the current application.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if a handler is added outside of the <see cref="SwitchApp.Main" /> method 
        /// or an attempt is made to remove a handler.
        /// </exception>
        /// <remarks>
        /// <note>
        /// The application may enlist in this event <b>only</b> when executing within its
        /// <see cref="SwitchApp.Main" /> method.  Also, event handlers cannot be removed.
        /// </note>
        /// <para>
        /// This event is raised when a NeonSwitch or underlying FreeSWITCH application
        /// invokes a synchronous command of the form:
        /// </para>
        /// <code language="none">
        /// api &lt;AppName&gt; &lt;args&gt;
        /// </code>
        /// <para>
        /// where <b>AppName</b> is the name of the NeonSwitch application as specified in
        /// the application's INI file in the <b>mod\managed</b> folder and <b>args</b> are
        /// the arbitrary textual arguments passed to the command.  NeonSwitch will raise
        /// this event when a synchronous command is received.  The event arguments
        /// include the <see cref="ExecuteEventArgs.SubcommandArgs"/> property which
        /// will be set to arguments passed to the command.  The application can stream
        /// arbitrary text to the caller by calling the <see cref="ExecuteEventArgs.Write(string)" /> 
        /// and <see cref="ExecuteEventArgs.WriteLine(string)" /> overloaded methods.
        /// </para>
        /// </remarks>
        public static event EventHandler<ExecuteEventArgs> ExecuteEvent
        {
            add
            {
                VerifyEventInMain("ExecuteEvent");
                executeEvent += value;
            }

            remove { ThrowEventRemoveException("ExecuteEvent"); }
        }

        /// <summary>
        /// Handles the dispatching of synchronous switch commands to the application
        /// code that implements the command.
        /// </summary>
        /// <param name="args">The event arguments.</param>
        internal static void OnExecute(ExecuteEventArgs args)
        {
            try
            {
                if (executeEvent != null)
                    executeEvent(null, args);

                if (args.Handled)
                    return;

                // Mardshal registered commands.

                Type subcommandType;
                ISwitchSubcommand subcommand;

                if (subcommandHandlers.TryGetValue(args.Subcommand, out subcommandType))
                {
                    subcommand = (ISwitchSubcommand)subcommandType.Assembly.CreateInstance(subcommandType.FullName);
                    subcommand.Execute(args);
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Raised when NeonSwitch invokes an asynchronous application command targeted
        /// at the current application.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if a handler is added outside of the <see cref="SwitchApp.Main" /> method 
        /// or an attempt is made to remove a handler.
        /// </exception>
        /// <remarks>
        /// <note>
        /// The application may enlist in this event <b>only</b> when executing within its
        /// <see cref="SwitchApp.Main" /> method.  Also, event handlers cannot be removed.
        /// </note>
        /// <para>
        /// This event is raised when a NeonSwitch or underlying FreeSWITCH application
        /// invokes a background command of the form:
        /// </para>
        /// <code language="none">
        /// bgapi &lt;AppName&gt; &lt;args&gt;
        /// </code>
        /// <para>
        /// where <b>AppName</b> is the name of the NeonSwitch application as specified in
        /// the application's INI file in the <b>mod\managed</b> folder and <b>args</b> are
        /// the arbitrary textual arguments passed to the command.  NeonSwitch will raise
        /// this event when an asynchronous command is received.  The event arguments
        /// include the <see cref="ExecuteBackgroundEventArgs.SubcommandArgs"/> property which
        /// will be set to arguments passed to the command.
        /// </para>
        /// </remarks>
        public static event EventHandler<ExecuteBackgroundEventArgs> ExecuteBackgroundEvent
        {
            add
            {
                VerifyEventInMain("ExecuteBackgroundEvent");
                executeBackgroundEvent += value;
            }

            remove { ThrowEventRemoveException("ExecuteBackgroundEvent"); }
        }

        /// <summary>
        /// Handles the dispatching of asynchronous switch commands to the application
        /// code that implements the command.
        /// </summary>
        /// <param name="args">The event arguments.</param>
        internal static void OnExecuteBackground(ExecuteBackgroundEventArgs args)
        {
            try
            {
                if (executeBackgroundEvent != null)
                    executeBackgroundEvent(null, args);

                // Mardshal registered commands.

                Type subcommandType;
                ISwitchSubcommand subcommand;

                if (subcommandHandlers.TryGetValue(args.Subcommand, out subcommandType))
                {
                    subcommand = (ISwitchSubcommand)subcommandType.Assembly.CreateInstance(subcommandType.FullName);
                    subcommand.ExecuteBackground(args);
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Raised when NeonSwitch triggers any event.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if a handler is added outside of the <see cref="SwitchApp.Main" /> method 
        /// or an attempt is made to remove a handler.
        /// </exception>
        /// <remarks>
        /// <note>
        /// The application may enlist in this event <b>only</b> when executing within its
        /// <see cref="SwitchApp.Main" /> method.  Also, event handlers cannot be removed.
        /// </note>
        /// <para>
        /// LillTek applications can use this event to monitor all events raised by the
        /// NeonSwitch platform and to implement high-performance applications that
        /// that are not tied to a single thread per call model.
        /// </para>
        /// </remarks>
        public static event EventHandler<SwitchEventArgs> EventReceived
        {
            add
            {
                VerifyEventInMain("EventReceived");
                eventReceived += value;
            }

            remove { ThrowEventRemoveException("EventReceived"); }
        }

        /// <summary>
        /// Raises the <see cref="EventReceived"/> event.
        /// </summary>
        /// <param name="args">The event arguments.</param>
        internal static void RaiseEventReceived(SwitchEventArgs args)
        {
            try
            {
                if (eventReceived != null)
                    eventReceived(null, args);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Raised when NeonSwitch detects that an asynchronous job has completed.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if a handler is added outside of the <see cref="SwitchApp.Main" /> method 
        /// or an attempt is made to remove a handler.
        /// </exception>
        /// <remarks>
        /// <note>
        /// The application may enlist in this event <b>only</b> when executing within its
        /// <see cref="SwitchApp.Main" /> method.  Also, event handlers cannot be removed.
        /// </note>
        /// <para>
        /// This event is raised when a background command or job has been completed by
        /// <b>any application running on the switch</b>.  Application can use this event
        /// to monitor when specific jobs submitted via one of the <see cref="ExecuteBackground(string) "/> 
        /// overrides has completed by comparing the unique job ID returned by the method with
        /// the <see cref="JobCompletedEventArgs.JobID" /> passed to the event handler.
        /// </para>
        /// </remarks>
        public static event EventHandler<JobCompletedEventArgs> JobCompletedEvent
        {
            add
            {
                VerifyEventInMain("JobCompletedEvent");
                jobCompletedEvent += value;
            }

            remove { ThrowEventRemoveException("JobCompletedEvent"); }
        }

        /// <summary>
        /// Raises the <see cref="JobCompletedEvent"/> event.
        /// </summary>
        /// <param name="args">The event arguments.</param>
        internal static void RaiseJobCompletedEvent(JobCompletedEventArgs args)
        {
            try
            {
                if (jobCompletedEvent != null)
                    jobCompletedEvent(null, args);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }
    }
}
