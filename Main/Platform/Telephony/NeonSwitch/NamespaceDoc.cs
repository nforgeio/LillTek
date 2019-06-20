//-----------------------------------------------------------------------------
// FILE:        NamespaceDoc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: NDoc namespace documentation

using System;
using System.IO;
using System.Net;

using FreeSWITCH;

using LillTek.Common;
using LillTek.Telephony.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// Overview of how NeonSwitch applications are loaded into a FreeSWITCH process for 
    /// handling calls at the application level.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <b>LillTek.Telephony.NeonSwitch</b> namespace includes types from two different
    /// assemblies: <b>LillTek.Telephony.dll</b> and <b>LillTek.Telephony.NeonSwitch.dll</b>.
    /// The <b>LillTek.Telephony.dll</b> assembly contains common code suitable for use by
    /// client applications.  The overview for this assembly can be found at <see cref="LillTek.Telephony.Common.OverviewDoc" />
    /// and the NeonSwitch client side overview can be found at <see cref="LillTek.Telephony.Common.OverviewDoc"/>
    /// The <b>LillTek.Telephony.NeonSwitch.dll</b> assembly includes classes intended for use
    /// by applications integrated directly into a NeonSwitch node.  These classes are described 
    /// below.
    /// </para>
    /// <para>
    /// The <b>LillTek.Telephony.NeonSwitch.dll</b> class library includes the classes necessary
    /// for building NeonSwitch applications that integrate closely with FreeSWITCH based by hooking
    /// directly into the low-level events and functions provided by the FreeSWITCH environment
    /// using the <b>mod_managed</b> module included with FreeSWITCH.
    /// </para>
    /// <para>
    /// This library supports the loading of one or more LillTek applications per FreeSWITCH
    /// instance.  The application and all of the assemblies it depends on (which are
    /// not registered in the GAC) must be copied to the FreeSWITCH <b>mod\managed</b> folder
    /// so <b>mod_managed</b> can discover and load the application and <b>mod_managed</b> must be
    /// present in the <b>mod</b> folder and enabled in the FreeSWITCH <b>\conf\autoload_configs\modules.conf.xml</b>
    /// configuration file.
    /// </para>
    /// <para>
    /// The loader works reading a simple INI file in the same folder as the loader DLL that
    /// has the same file name but uses <b>.ini</b> as the file extension.  So a loader assembly
    /// named <b>LillTek.Telephony.NeonAppLoader.dll</b> will read its configuration from
    /// <b>LillTek.Telephony.NeonAppLoader.ini</b>. 
    /// </para>
    /// <para>
    /// This is file contains name/value settings specified as:
    /// </para>
    /// <code language="none">
    /// <b>&lt;name&gt; = &lt;value&gt;</b>
    /// </code>
    /// <para>
    /// Name/Value pairs are specified one per line, with setting names being case insenstivive. 
    /// Blank lines and lines beginning with <b>//</b> or <b>--</b> are ignored.  This is essentially
    /// a very simplified form of the format implemented by the <see cref="LillTek.Common.Config" /> class.
    /// </para>
    /// <para>
    /// At this point, the loader assembly looks only for settings named <b>AppName</b>, <b>AppPath</b> and <b>AppClass</b>.  
    /// <b>AppName</b> is required and identifies the name FreeSWITCH should use for the module.
    /// </para>
    /// <note>
    /// <b>AppName</b> may not include whitespace characters.
    /// </note>
    /// <para>
    /// <b>AppPath</b> is the path to the folder containing the application DLLs to be loaded.  This can be 
    /// relative to the folder where the loader DLL resides or be an absolute path.  <b>AppClass</b> is optional
    /// and specifies the fully qualified name of the application entry point class. The loader will then load
    /// and start the NeonSwitch application assemblies located in this folder, as descibed below
    /// in <b>NeonSwitch Application Lifecycle</b>.
    /// </para>
    /// <note>
    /// The load will fail if the INI file is not present, does not contain the <b>AppName</b> and <b>AppPath</b>,
    /// settings if the application folder does not exist, or if the folder doesn't contain a valid NeonSwitch
    /// application.
    /// </note>
    /// <para>
    /// Multiple NeonSwitch applications can be loaded into a single FreeSWITCH process by making
    /// copies of the <b>LillTek.Telephony.NeonAppLoader.dll</b> file in the <b>mod\managed</b> folder,
    /// each with their own INI file referencing the application folder.  Example: For an application 
    /// named <b>Acme.Foo</b> you could make a copy of the loader assembly named <b>Acme.Foo.dll</b>
    /// in <b>mod\managed</b>, create the <b>mod\managed\Acme.Foo</b> subfolder and copy the application
    /// files there, and then create the <b>Acme.Foo.ini</b> file in <b>mod\managed</b> and set its
    /// contents to:
    /// </para>
    /// <code language="none">
    /// AppName  = MySwitchApp
    /// AppPath  = Acme.Foo
    /// AppClass = Acme.Foo.MySwitchApp
    /// </code>
    /// <para>
    /// You could perform similar steps for another application called <b>Acme.Bar</b>.  FreeSWITCH
    /// and the loader will load each application into its process, each within its own application'
    /// domain.
    /// </para>
    /// <para><b><u>NeonSwitch vs. FreeSWITCH</u></b></para>
    /// <para>
    /// NeonSwitch is based on FreeSWITCH and of course, shares a lot in common with this excellent 
    /// telephony engine.  The main goal for NeonSwitch was to add enhancements and abstractions to
    /// make it as easy to develop managed .NET applications that integrate into FreeSWITCH at a
    /// low-level while also maintaining complex application state within the FreeSWITCH process
    /// including integration with LillTek Messaging.
    /// </para>
    /// <para>
    /// My first idea was to try to keep the application separate from FreeSWITCH by running the
    /// application in its own process and using the FreeSWITCH <b>mod_event_socket</b> module to
    /// monitor and respond to FreeSWITCH events.  This approach had the advantage of a very clean
    /// separation of the application and the switch, but after more research, I grew concerned 
    /// about the potential for occasionally losing connectivity with the switch and also that
    /// under load, FreeSWITCH may generate events faster than an application could consume them
    /// via a socket.  FreeSWITCH simply starts discarding events in this situation--not something
    /// I want to deal with.
    /// </para>
    /// <para>
    /// So, the next step was to use <b>mod_managed</b> to integrate a .NET application directly
    /// into FreeSWITCH and after a few days of work, I was able to get this working.  Along the
    /// way I realized that the FreeSWITCH application programming model doesn't address exactly
    /// what I want to accomplish.
    /// </para>
    /// <para>
    /// FreeSWITCH looks like it was designed mainly to host simple and mostly stateless application 
    /// and command plugins, especially for .NET managed code.  The stock <b>mod_managed</b> implementaion
    /// requires that custom plugins be deployed in a single assembly DLL with references only to the
    /// <b>FreeSWITCH.Managed</b> assembly and assemblies loaded into the GAC.  Multi-assembly applications
    /// are not directly supported.  This is a big problem for the typical LillTek application that
    /// needs to reference several assemblies.
    /// </para>
    /// <para>
    /// The stock <b>mod_managed</b> module isn't really set up to cleanly support applications that need
    /// to start when FreeSWITCH starts, then maintain state over extended period of time and then 
    /// perform a clean shut down when FreeSWITCH stops.  The <b>mod_managed</b> design goal seems
    /// to have been really centered around using .NET as more of a scripting engine to add a bit of
    /// custom code to a dialplan, etc. rather as a way to develop complex integrated applications.
    /// In addition, <b>mod_managed</b> seems to be oriented more towards code that reacts to events
    /// generated by the switch, rather than originating events (such as a dialer application would do).
    /// </para>
    /// <para>
    /// Of course, my statements above are a little extreme, especially since I was able to build
    /// the NeonSwitch layer on top of FreeSWITCH (with a couple of custom tweaks to <b>mod_managed</b>. 
    /// What I have really done is to remap <b>mod_managed</b> applet orientation into an API that 
    /// cleanly supports more complex .NET applications.
    /// </para>
    /// <note>
    /// NeonSwitch does not fundementally change FreeSWITCH behaviors.  All existing modules and plugins
    /// will run exactly the same as they already do.  Even the changes made to <b>mod_managed</b> were
    /// done in a way to maintain backwards compatibility.
    /// </note>
    /// <para>
    /// Here are the essential differences between NeonSwitch and FreeSWITCH:
    /// </para>
    /// <list type="bullet">
    ///     <item>
    ///     NeonSwitch has added the <b>LillTek.Telephony.NeonAppLoader.dll</b> assembly to
    ///     act as a bootstrap application loader.  This works with <b>mod_managed</b>
    ///     to load a multi-assembly application from a folder beneath or outside of the
    ///     <b>mod\managed</b> FreeSWITCH folder.  <b>NeonAppLoader</b> then continues to
    ///     act as a bridge between <b>mod_managed</b> and the application.
    ///     </item>
    ///     <item>
    ///     Whereas FreeSWITCH <b>mod_managed</b> doesn't really have a well-defined concept 
    ///     of global application state, NeonSwitch provides the <see cref="SwitchApp "/>
    ///     class.  All NeonSwitch applications derive from this class and implement
    ///     the <see cref="SwitchApp.Main" /> method that is called when the application
    ///     is loaded into FreeSWITCH and the <see cref="SwitchApp.Close "/>
    ///     that is called when FreeSWITCH terminates the application.
    ///     </item>
    ///     <item>
    ///     NeonSwitch applications will use the static <see cref="Switch" /> class to interact
    ///     with the underlying telephony engine.  This class provides access to the switch state,
    ///     the commands that instruct the switch to perform actions and as well as events
    ///     to which the application can subscribe to monitor and participate in switch operations
    ///     in real time.
    ///     </item>
    ///     <item>
    ///     <see cref="Switch" /> exposes the <see cref="Switch.CallSessionEvent" />,
    ///     <see cref="Switch.ExecuteEvent" /> and <see cref="Switch.ExecuteBackgroundEvent" /> events.
    ///     These correspond to the <see cref="IAppPlugin" />.<see cref="IAppPlugin.Run" />, 
    ///     <see cref="IApiPlugin" />.<see cref="IApiPlugin.Execute" /> and <see cref="IApiPlugin" />.<see cref="IApiPlugin.ExecuteBackground" />
    ///     interface methods exposed by standard <b>mod_managed</b> based modules.  Note that
    ///     I reframed <see cref="IAppPlugin" /> as <see cref="Switch.CallSessionEvent" />
    ///     since this really is invoking a single threaded synchronous application on a call
    ///     rather than a whole application which <see cref="IAppPlugin" /> implies (to me at least).
    ///     </item>
    ///     <item>
    ///     Whereas <b>mod_managed</b> instantiates a new instance of classes implementing
    ///     <see cref="IAppPlugin" /> and <see cref="IApiPlugin" /> for each command execution,
    ///     NeonSwitch creates only a single <see cref="SwitchApp" /> instance and then
    ///     raises events on the static <see cref="Switch" /> class as commands are received 
    ///     from FreeSWITCH.
    ///     </item>
    ///     <item>
    ///     I'm not super happy with the managed FreeSWITCH types generated by SWIG for 
    ///     <b>mod_managed</b>.  These definions include a lot of extra junk, use old
    ///     style C/C++ naming conventions, and are generally not consistent with the
    ///     modern C# style.  NeonSwitch wraps or replaces all FreeSWITCH types with
    ///     nice C# alternatives.  Most NeonSwitch applications will never need to 
    ///     use a FreeSWITCH type directly.
    ///     </item>
    ///     <item>
    ///     NeonSwitch also provides other support classes to make telephony coding 
    ///     as similar as possible to normal LillTek application development.  Examples
    ///     include <see cref="SwitchServiceHost" /> which hosts a standard 
    ///     LillTek service within NeonSwitch, <see cref="SwitchLogProvider" />
    ///     which marries the standard LillTek <see cref="SysLog" /> logging with
    ///     FreeSWITCH's logging infrastructure.  
    ///     </item>
    /// </list>
    /// <para><b><u>NeonSwitch Application Lifecycle</u></b></para>
    /// <para>
    /// The LillTek/FreeSWITCH application lifecycle is a bit different from a normal
    /// console or Windows service based application since the application lifespan is really
    /// tied to the FreeSWITCH process.  The LillTek-FreeSWITCH application lifecycle revolves
    /// around the <see cref="SwitchApp" /> class from the application's perspective.  Every
    /// LillTek/FreeSWITCH application must define a single class that derives from <see cref="SwitchApp" />
    /// and includes a <c>public</c> parameterless constructor.  An instance of this class will
    /// be constructed and initialized when FreeSWITCH starts.
    /// </para>
    /// <para>
    /// Details of the LillTek/FreeSWITCH application lifecycle:
    /// </para>
    /// <list type="number">
    ///     <item>
    ///     FreeSWITCH starts and loads the <b>mod_managed</b> module.
    ///     </item>
    ///     <item>
    ///     <b>mod_managed</b> scans the DLLs in the <b>mod\managed</b> folder for a class
    ///     that implements the <see cref="ILoadNotificationPlugin" /> interface (a <b>mod_managed</b>
    ///     thing).  The <b>BaseLoader</b> class in the <b>LillTek.Telephony.NeonAppLoader</b> assembly
    ///     library performs this function for NeonSwitch applications.
    ///     </item>
    ///     <item>
    ///     <para>
    ///     <b>mod_managed</b> instantiates a <b>BaseLoader</b> instance and calls
    ///     its <see cref="ILoadNotificationPlugin.Load" /> method.  This method reads
    ///     the loader INI file to determine the module name, the folder location for the NeonSwitch
    ///     application and optionally, the name of the application's entry point class. The loader
    ///     then loads DLLs from this folder into the application domain and then instantiates an 
    ///     instance of the <see cref="AppLoader" /> type defined within this assembly and calls
    ///     its <see cref="AppLoader.Load" /> method passing the name of the entry point class 
    ///     (or <c>null</c>).
    ///     </para>
    ///     <note>
    ///     The <b>BaseLoader.Load</b> method saves the module name it retrieves from the INI file in its
    ///     static <b>ModuleName</b> property and the customized <b>mod_managed</b> module probes to
    ///     see if this property exists and uses the value returned as the managed module name, rather
    ///     than using the class name (as it would be by default for stock FreeSWITCH/mod_managed).
    ///     </note>
    ///     </item>
    ///     <item>
    ///     <see cref="AppLoader" /> takes over initializing the static <see cref="Switch" />
    ///     proxy class and then scanning all of the loaded assemblies for a class definition that
    ///     derives from <see cref="SwitchApp" /> and matches the entry point class name (if one
    ///     was passed).  Once a entry point class is found, an instance is constructed and the
    ///     base <see cref="SwitchApp.Main" /> method is called to complete the application
    ///     load process.
    ///     </item>
    ///     <item>
    ///     The application will perform its initialization activites within its <see cref="SwitchApp.Main" />
    ///     method including loading configuration settings, establishing database connections,
    ///     starting a LillTek Messgaging router, etc.  The application will also register event handlers 
    ///     for the <see cref="Switch" /> related events it is interested in monitoring.  The
    ///     application must exit <see cref="SwitchApp.Main" /> once it has completed its startup
    ///     activities so NeonSwitch can begin processing events for the application.
    ///     </item>
    ///     <item>
    ///     The application will continue running until NeonSwitch terminates, the NeonSwitch configuration
    ///     is reloaded, the application's loader DLL is modified on the server, or the application
    ///     itself calls <see cref="SwitchApp.Exit" />.  The application's 
    ///     <see cref="SwitchApp" />.<see cref="SwitchApp.Close" /> method will be called
    ///     when this happens, giving the application a chance to perform an orderly shutdown.
    ///     </item>
    /// </list>
    /// <note>
    /// The FreeSWITCH <b>mod_managed</b> module monitors new, deleted, and changes to files in
    /// the <b>mod_managed</b> folder and starts, stops, or restarts managed applications as necessary.
    /// This functionality does not currently extend to the application loaded by the <b>LillTek.Telephony.NeonAppLoader</b>.
    /// The NeonSwitch application loader does not currently monitor changes to the referenced application
    /// folder.  This feature might be added for future releases.
    /// </note>
    /// <p><b><u>Life-cycle of a NeonSwitch Call</u></b></p>
    /// <para>
    /// Call processing is actually pretty simple at the basic NeonSwitch level.  Calls are started by 
    /// receiving a call from an endpoint (e.g. a phone or a SIP gateway) or by originating a call on the
    /// switch itself (e.g. for a dialer application).  After some initialization, the call enters the
    /// <b>routing</b> phase.  During this phase, the User Directory will be searched for information
    /// about the user making the call as well as the name of the dialplan context to use for routing
    /// the call.  User information may be stored within the underlying FreeSWITCH XML user directory 
    /// and/or the application may enlist in the <see cref="Switch" />.<see cref="Switch.UserDirectoryEvent" />
    /// to perform a custom lookup.
    /// </para>
    /// <para>
    /// Once the user information and dialplan context have been obtained, NeonSwitch will begin processing
    /// the dialplan.  This can be performed by the underlyine FreeSWITCH XML dialplan module or by
    /// the a NeonSwitch application that enlists in the <see cref="Switch" />.<see cref="Switch.DialPlanEvent" />.
    /// The basic purpose behind dialplan processing is to decide on exactly which routing actions are to be
    /// performed on the call.  These actions can include answering the call, bridging the call to an
    /// extension, playing an audio file, hanging up, sending the call to an application (such as voicemail), 
    /// and a multitude of other actions.
    /// </para>
    /// <para>
    /// Once dialplan processing completes, the call will enter the <b>executing</b> phase and the 
    /// actions generated during dialplan process will be executed in order, one-by-one, typically
    /// resulting in the call being bridged to an extension, assigned to an application, or hungup.
    /// </para>
    /// <note>
    /// Note that advanced call event processing such as for an IVR system is handled in application
    /// modules, not directly by dialplan actions.  For example, the last action in the dialplan
    /// might be to assign the call to an IVR application that presents audio menus and accepts
    /// DTMF or speech responses from the caller.
    /// </note>
    /// <para>
    /// Applications or dialplans may also transfer a call.  At this point, the call re-enters the
    /// <b>routing</b> phase (if it was executing in an application) and the call will repeat the
    /// user directory and dialplan processing steps outlined above.
    /// </para>
    /// <para>
    /// Once a call has finished, the switch will release all related resources and terminate the call.
    /// </para>
    /// <b><u>User Directory and Dialplan Event Handlers</u></b>
    /// <para>
    /// By default, NeonSwitch is configured to use the underlying FreeSWITCH XML based user directory and
    /// dialplan modules.  This can be overridden by registering <see cref="Switch" />.<see cref="Switch.UserDirectoryEvent" />
    /// and/or <see cref="Switch" />.<see cref="Switch.DialPlanEvent" /> event handlers within the
    /// application's <see cref="SwitchApp" />.<see cref="SwitchApp.Main" /> method.
    /// </para>
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
    /// properties will be set to the query parameters.
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
    /// <see cref="AnswerAction" />, <see cref="BridgeAction" />,
    /// <see cref="HangupAction" />, and others that can be easier to use in many situations
    /// by hiding the syntax of the underlying FreeSWITCH modules.
    /// </para>
    /// <note>
    /// NeonSwitch assumes that  <see cref="Switch.UserDirectoryEvent" /> and <see cref="Switch.DialPlanEvent" /> 
    /// handlers will execute relatively quickly and return immediately. In fact, in the current implementation,
    /// both of these events are processed on the same background thread, so blocking for any period of time will
    /// block the processing of any subsequent calls.  High performance applications will go through some effort
    /// to increase speed via caching and other techniques.
    /// </note>
    /// <para><b><u>Application Programming Models</u></b></para>
    /// <para>
    /// The <see cref="Switch" /> class is the focal point for applications integrating
    /// directly with NeonSwitch.  There are five basic ways an application can integrate
    /// into the NeonSwitch environment:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term>Directory Events</term>
    ///         <description>
    ///         The <see cref="Switch.UserDirectoryEvent" /> is raised when NeonSwitch needs to
    ///         know information for a user (such as whether the user exists and what the
    ///         authentication credentials are).  By default, this information is obtained
    ///         via the Directory XML configuration files.  This event provides a way for
    ///         applications to override this behavior and obtain this information from
    ///         elsewhere, such as a database.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Dialplan Events</term>
    ///         <description>
    ///         <para>
    ///         The <see cref="Switch.DialPlanEvent" /> is raised when an inbound call is received or
    ///         an existing call is transferred.  Dialplan events provide a way for an application
    ///         to control how a call is routed and also implement very simple call features.
    ///         Applications simply specify a set of <see cref="SwitchAction"/>s to be performed
    ///         for the call such as <see cref="AnswerAction" />, <see cref="BridgeAction" />,
    ///         and <see cref="HangupAction" /> and then NeonSwitch will execute these in order.
    ///         </para>
    ///         <para>
    ///         Dialplan event handlers can override the behavior of dialplan actions specified
    ///         in the dialplan XML configuration files.
    ///         </para>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Call Session Events</term>
    ///         <description>
    ///         <para>
    ///         Call sessions are a relatively simple way for applications to customize call
    ///         handling.  Calls can be routed to an application session using the <see cref="StartSessionAction"/>
    ///         within a dialplan handler, passing the name of the application and any arguments.
    ///         </para>
    ///         <note>
    ///         The application name used is the <b>AppName</b> specified in the application's
    ///         INI file located in the <b>mod\managed</b> folder
    ///         </note>
    ///         <para>
    ///         Applications must enlist in <see cref="Switch.CallSessionEvent" /> to implement call sessions.
    ///         Session handlers are essentially single threaded event loops that call various
    ///         methods on the <see cref="CallSession" /> instance passed in the event arguments.
    ///         This event loop will continue until the call is terminated or transferred.
    ///         </para>
    ///         <para>
    ///         Call sessions provide applications with a fairly rich and easy-to-use programming
    ///         model for developing applications.  This is suited for application running on lightly
    ///         loaded switches or applications that make some routing decisions up front and then
    ///         transfer the call elsewhere relatively quickly.  Due to their single-threaded nature,
    ///         call sessions are not well suited to managing long running calls, especially on
    ///         highly loaded switches.
    ///         </para>
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>General Events</term>
    ///         <description>
    ///         Applications can enlist in the <see cref="Switch.EventReceived" /> event to see and handle
    ///         all low-level switch events on a fully asynchronous basis. Applications will need to
    ///         implement call state machines using the <see cref="CallState" /> class which can be
    ///         somewhat complex, but with complexity comes the power to implement high performance
    ///         advanced applications.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Command Handlers</term>
    ///         <description>
    ///         <para>
    ///         Applications may expose API commands that may be called from other NeonSwitch and FreeSWITCH
    ///         applications. These commands use the underlying FreeSWITCH commanding infrastructure that supports
    ///         both synchronous and asynchronous commands which can be invoked using the <see cref="Switch.Execute(string)" />
    ///         and <see cref="Switch.ExecuteBackground(string)" /> overrides.
    ///         </para>
    ///         <para>
    ///         NeonSwitch applications expose a single command to the switch ecosystem.  The command name
    ///         registered with the switch is the <b>AppName</b> specified in the application's INI file
    ///         located in the <b>mod\managed</b> folder.  Applications are free to implement subcommands
    ///         by parsing the command argument string.
    ///         </para>
    ///         <para>
    ///         NeonSwitch provides two ways for applications to expose command implementations.  First,
    ///         applications can simply subscribe to <see cref="Switch.ExecuteEvent" /> and/or
    ///         <see cref="Switch.ExecuteBackgroundEvent" /> events.  NeonSwitch will raise these events to execute
    ///         commands targeted at the application.  The event arguments will hold the command
    ///         arguments and for <see cref="Switch.ExecuteEvent" />, methods to stream text back
    ///         to the caller.  Applications that actually implement a command should set the <b>Handled</b>
    ///         property of the event arguments to <c>true</c>.
    ///         </para>
    ///         <para>
    ///         The second dispatch method requires that the application implement classes that
    ///         derive from <see cref="ISwitchSubcommand" /> where each class implements a specific 
    ///         application subcommand.  Applications will need to register these commands by calling 
    ///         <see cref="Switch.RegisterAssemblySubcommands"/> for each assembly that has command 
    ///         implementation classes.  This method reflects the assembly and uses the class name 
    ///         without the namespace and also stripping any "Command" suffix as the registered
    ///         subcommand.
    ///         </para>
    ///         <para>
    ///         When NeonSwitch receives a command, it first raises the proper execute event giving
    ///         the handler a first crack at handling the command.  If there is no event handler or
    ///         if the handler didn't set the <b>Handled</b> property in the event arguments to
    ///         <c>true</c>, then NeonSwitch will try to map the command to a subcommand implementation.
    ///         </para>
    ///         <para>
    ///         First, NeonSwitch will extract the first word from the raw command arguments and
    ///         use this as the subcommand name.  The remain text will be extracted as the subcommand
    ///         arguments.  Next, NeonSwitch will see if the subcommand name matches any of the
    ///         command classes registered via <see cref="Switch.RegisterAssemblySubcommands"/>.
    ///         If a match is found that then an instance of the subcommand class will be constructed and 
    ///         and <see cref="ISwitchSubcommand.Execute" /> or <see cref="ISwitchSubcommand.ExecuteBackground"/>
    ///         will be called.
    ///         </para>
    ///         <note>
    ///         Subcommand names are case insensitive.
    ///         </note>
    ///         </description>
    ///     </item>
    /// </list>
    /// <para>
    /// Note that applications can mix-and-match programming styles for example:
    /// </para>
    /// <list type="bullet">
    ///     <item>
    ///     Hook <see cref="Switch.DialPlanEvent" /> to route a call to an application call session.
    ///     </item>
    ///     <item>
    ///     The call session prompts the user for some information and to make a routing
    ///     decision and then bridges the call and exits.
    ///     </item>
    ///     <item>
    ///     The application then monitors <see cref="Switch.EventReceived" /> for the event indicating
    ///     that the call has completed, and a CDR is generated.
    ///     </item>
    /// </list>
    /// </remarks>
    public static class OverviewDoc
    {
    }
}

