//-----------------------------------------------------------------------------
// FILE:        Switch.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Provides access to global NeonSwitch state and utilities.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;
using LillTek.Telephony.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// Provides access to global NeonSwitch state, events, and utilities.
    /// </summary>
    /// <remarks>
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
    ///         <see cref="Switch.ExecuteBackgroundEvent" />.  NeonSwitch will raise these events to execute
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
    ///     Hook <see cref="DialPlanEvent" /> to route a call to an application call session.
    ///     </item>
    ///     <item>
    ///     The call session prompts the user for some information and to make a routing
    ///     decision and then bridges the call and exits.
    ///     </item>
    ///     <item>
    ///     The application then monitors <see cref="EventReceived" /> for the event indicating
    ///     that the call has completed, and a CDR is generated.
    ///     </item>
    /// </list>
    /// </remarks>
    public static partial class Switch
    {
        /// <summary>
        /// The module name for the core NeonSwitch application.  This is also the command that
        /// will be used to call on core functions via the FreeSWITCH command infrastructure.
        /// </summary>
        public const string CoreAppName = "NeonSwitch";

        /// <summary>
        /// Returns the application running on the switch.
        /// </summary>
        public static SwitchApp Application { get; internal set; }

        /// <summary>
        /// Static constructor.
        /// </summary>
        static Switch()
        {
            // Load the invariant switch globals.

            Switch.ID              = Guid.Parse(freeswitch.switch_core_get_uuid());
            Switch.Host            = freeswitch.switch_core_get_hostname();
            Switch.Name            = freeswitch.switch_core_get_switchname();
            Switch.MinDtmfDuration = TimeSpan.FromMilliseconds(freeswitch.switch_core_min_dtmf_duration(0));
            Switch.MaxDtmfDuration = TimeSpan.FromMilliseconds(freeswitch.switch_core_max_dtmf_duration(0));

            // Other initialization.

            InitEventHandling();
        }

        /// <summary>
        /// Continues switch initialization after the application has detected that the 
        /// NeonSwitch core service has been started.
        /// </summary>
        internal static void Initialize()
        {
            // Get the installed voices from the core service.

            try
            {
                var voices = new List<string>();

                using (var reader = new StringReader(Switch.ExecuteManaged(Switch.CoreAppName, "getvoices")))
                {
                    for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        voices.Add(line);
                    }
                }

                Switch.InstalledVoices = voices.AsReadOnly();
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        //---------------------------------------------------------------------
        // Global folder paths.

        /// <summary>
        /// Returns the fully qualified path to the base NeonSwitch/FreeSWITCH installation folder.
        /// This maps to the <b>${base_dir}</b> global variable.
        /// </summary>
        public static string BaseFolder
        {
            get { return freeswitch.SWITCH_GLOBAL_dirs.base_dir; }
        }

        /// <summary>
        /// Returns the fully qualified path to the modules folder.
        /// This maps to the <b>${mod_dir}</b> global variable.
        /// </summary>
        public static string ModulesFolder
        {
            get { return freeswitch.SWITCH_GLOBAL_dirs.mod_dir; }
        }

        /// <summary>
        /// Returns the fully qualified path to the configuration folder.
        /// This maps to the <b>${conf_dir}</b> global variable.
        /// </summary>
        public static string ConfigFolder
        {
            get { return freeswitch.SWITCH_GLOBAL_dirs.conf_dir; }
        }

        /// <summary>
        /// Returns the fully qualified path to the log files folder.
        /// This maps to the <b>${log_dir}</b> global variable.
        /// </summary>
        public static string LogFolder
        {
            get { return freeswitch.SWITCH_GLOBAL_dirs.log_dir; }
        }

        /// <summary>
        /// Returns the fully qualified path to the temporary folder.
        /// This maps to the <b>${temp_dir}</b> global variable.
        /// </summary>
        public static string TempFolder
        {
            get { return freeswitch.SWITCH_GLOBAL_dirs.temp_dir; }
        }

        /// <summary>
        /// Returns the fully qualified path to the folder holding the TTS gramars.
        /// This maps to the <b>${grammar_dir}</b> global variable.
        /// </summary>
        public static string GrammarFolder
        {
            get { return freeswitch.SWITCH_GLOBAL_dirs.grammar_dir; }
        }

        /// <summary>
        /// Returns the fully qualified path to the folder where recordings will be written.
        /// This maps to the <b>${recordings_dir}</b> global variable.
        /// </summary>
        public static string RecordingFolder
        {
            get { return freeswitch.SWITCH_GLOBAL_dirs.recordings_dir; }
        }

        /// <summary>
        /// Returns the fully qualified path to the folder holding the system sound files.
        /// This maps to the <b>${sounds_dir}</b> global variable.
        /// </summary>
        public static string SoundFolder
        {
            get { return freeswitch.SWITCH_GLOBAL_dirs.sounds_dir; }
        }

        //---------------------------------------------------------------------
        // Misc switch properties.

        /// <summary>
        /// Returns a read-only collection of the text-to-speech voices installed
        /// on the switch.
        /// </summary>
        public static IList<string> InstalledVoices { get; private set; }

        //---------------------------------------------------------------------
        // Global switch variables.

        /// <summary>
        /// Obtains the value of a global switch variable.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <returns>The value or <c>null</c> if the variable does not exist.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="name" /> is <c>null</c>.</exception>
        public static string GetGlobal(string name)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            return freeswitch.switch_core_get_variable_dup(name);
        }

        /// <summary>
        /// Sets the value of a global switch variable (optionally removing the variable).
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <param name="value">The new value or <c>null</c> to remove the variable.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="name" /> is <c>null</c>.</exception>
        /// <remarks>
        /// Global variables can be referenced in switch actions and commands
        /// using the <b>${name}</b> syntax.
        /// </remarks>
        public static void SetGlobal(string name, string value)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            freeswitch.switch_core_set_variable(name, value);
        }

        /// <summary>
        /// Returns the globally unique ID for the switch instance.
        /// </summary>
        public static Guid ID { get; private set; }

        /// <summary>
        /// Returns the host name of the computer running the switch.
        /// </summary>
        public static string Host { get; private set; }

        /// <summary>
        /// Returns the name of the switch instance.
        /// </summary>
        public static string Name { get; private set; }

        /// <summary>
        /// Returns the minimum allowed duration for a DTMF digit.
        /// </summary>
        public static TimeSpan MinDtmfDuration { get; private set; }

        /// <summary>
        /// Returns the maximum allowed duration for a DTMF digit.
        /// </summary>
        public static TimeSpan MaxDtmfDuration { get; private set; }

        //---------------------------------------------------------------------
        // Global switch operations.

        private static Regex varRegRegex = new Regex(@"\$\{(?<variable>[\w\d\-_]+)}", RegexOptions.Compiled);

        /// <summary>
        /// Expands any embedded global variable references of the form <b>${name}</b>
        /// into the current value.
        /// </summary>
        /// <param name="unexpanded">The string to be expanded.</param>
        /// <returns>The expanded string.</returns>
        public static string ExpandVariables(string unexpanded)
        {
            if (unexpanded == null || unexpanded.Length == 0)
                return unexpanded;

            return varRegRegex.Replace(unexpanded,
                match =>
                {
                    string value;

                    value = GetGlobal(match.Groups["variable"].Value);
                    if (value == null)
                        value = string.Empty;

                    return value;
                });
        }

        /// <summary>
        /// Expands any global variable references in the path passed and then
        /// converts any Windows-style backslash characters into forward slashes.
        /// </summary>
        /// <param name="path">The file path to be expanded.</param>
        /// <returns>The expanded path.</returns>
        /// <remarks>
        /// This method is required for some FreeSWITCH commands that don't handle
        /// backslashes embedded in file paths properly.
        /// </remarks>
        public static string ExpandFilePath(string path)
        {
            return ExpandVariables(path).Replace('\\', '/');
        }

        /// <summary>
        /// Submits an event.
        /// </summary>
        public static void SendEvent()
        {
            // $todo(jeff.lill): Implement this

            throw new NotImplementedException();
        }

        /// <summary>
        /// Submits a log entry to the NeonSwitch logging subsystem. 
        /// </summary>
        /// <param name="level">The log level.</param>
        /// <param name="message">The log message.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message" /> is <c>null</c>.</exception>
        public static void Log(SwitchLogLevel level, string message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            Execute("log", "{0} {1}", SwitchHelper.GetSwitchLogLevelString(level), message);
        }

        /// <summary>
        /// Submits a formatted log entry to the NeonSwitch logging subsystem. 
        /// </summary>
        /// <param name="level">The log level.</param>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="format" /> or <paramref name="args"/> is <c>null</c>.</exception>
        public static void Log(SwitchLogLevel level, string format, params object[] args)
        {
            if (format == null)
                throw new ArgumentNullException("format");

            if (args == null)
                throw new ArgumentNullException("args");

            Execute("log", "{0} {1}", SwitchHelper.GetSwitchLogLevelString(level), string.Format(format, args));
        }

        private static Version cachedFreeSWITCHVersion = null;

        /// <summary>
        /// Returns the build version for the underlying FreeSWITCH server.
        /// </summary>
        public static Version FreeSWITCHVersion
        {
            get
            {
                if (cachedFreeSWITCHVersion != null)
                    return cachedFreeSWITCHVersion;

                var versionString = GetGlobal("freeswitch_version");

                if (versionString == null)
                    cachedFreeSWITCHVersion = new Version();
                else
                {
                    try
                    {
                        cachedFreeSWITCHVersion = new Version(versionString);
                    }
                    catch
                    {
                        SysLog.LogWarning("Unable to parse FreeSWITCH version [{0}].", versionString);
                        cachedFreeSWITCHVersion = new Version();
                    }
                }

                return cachedFreeSWITCHVersion;
            }
        }

        /// <summary>
        /// Returns the build version for the NeonSwitch server.
        /// </summary>
        public static Version Version
        {
            get { return new Version(Build.Version); }
        }

        /// <summary>
        /// Determines whether the named loadable module is loaded.
        /// </summary>
        /// <param name="name">The module name.</param>
        /// <returns><c>true</c> if the module is loaded.</returns>
        public static bool IsModuleLoaded(string name)
        {
            return freeswitch.switch_loadable_module_exists(name) == switch_status_t.SWITCH_STATUS_SUCCESS;
        }

        /// <summary>
        /// Synchronously executes an arbitrary global FreeSWITCH command.
        /// </summary>
        /// <param name="command">The command </param>
        /// <returns>The command response text.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="command"/> is <c>null</c>.</exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        public static string Execute(string command)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            using (var api = new Api(null))
            {
                return api.Execute(command, null);
            }
        }

        /// <summary>
        /// Synchronously executes an arbitrary global FreeSWITCH command with arguments.
        /// </summary>
        /// <param name="command">The command </param>
        /// <param name="args">The command arguments or <c>null</c>.</param>
        /// <returns>The command response text.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="command"/> or <paramref name="args"/> is <c>null</c>.</exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        public static string Execute(string command, string args)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            if (args == null)
                throw new ArgumentNullException("args");

            using (var api = new Api(null))
            {
                return api.Execute(command, args);
            }
        }

        /// <summary>
        /// Synchronously executes an arbitrary global FreeSWITCH command with formatted arguments.
        /// </summary>
        /// <param name="command">The command </param>
        /// <param name="format">The argument format string.</param>
        /// <param name="args">The command arguments.</param>
        /// <returns>The command response text.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="command"/>, <paramref name="format" />, or 
        /// <paramref name="args" /> is <c>null</c>.
        /// </exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        public static string Execute(string command, string format, params object[] args)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            if (format == null)
                throw new ArgumentNullException("format");

            if (args == null)
                throw new ArgumentNullException("args");

            using (var api = new Api(null))
            {
                return api.Execute(command, string.Format(format, args));
            }
        }

        /// <summary>
        /// Synchronously executes an arbitrary managed global FreeSWITCH command.
        /// </summary>
        /// <param name="command">The command </param>
        /// <returns>The command response text.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="command"/> is <c>null</c>.</exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        public static string ExecuteManaged(string command)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            using (var api = new Api(null))
            {
                return api.Execute("managed", command);
            }
        }

        /// <summary>
        /// Synchronously executes an arbitrary managed global FreeSWITCH command with arguments.
        /// </summary>
        /// <param name="command">The command </param>
        /// <param name="args">The command arguments or <c>null</c>.</param>
        /// <returns>The command response text.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="command"/> or <paramref name="args"/> is <c>null</c>.</exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        public static string ExecuteManaged(string command, string args)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            if (args == null)
                throw new ArgumentNullException("args");

            return Execute("managed", "{0} {1}", command, args);
        }

        /// <summary>
        /// Synchronously executes an arbitrary managed global FreeSWITCH command with formatted arguments.
        /// </summary>
        /// <param name="command">The command </param>
        /// <param name="format">The argument format string.</param>
        /// <param name="args">The command arguments.</param>
        /// <returns>The command response text.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="command"/>, <paramref name="format" />, or 
        /// <paramref name="args" /> is <c>null</c>.
        /// </exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        public static string ExecuteManaged(string command, string format, params object[] args)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            if (format == null)
                throw new ArgumentNullException("format");

            if (args == null)
                throw new ArgumentNullException("args");

            return Execute("managed", "{0} {1}", command, string.Format(format, args));
        }

        /// <summary>
        /// Synchronously executes an arbitrary core NeonSwitch command.
        /// </summary>
        /// <param name="command">The command </param>
        /// <returns>The command response text.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="command"/> is <c>null</c>.</exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        public static string ExecuteCore(string command)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            return Execute("managed", "{0} {1}", Switch.CoreAppName, command);
        }

        /// <summary>
        /// Synchronously executes an arbitrary core NeonSwitch command with arguments.
        /// </summary>
        /// <param name="command">The command </param>
        /// <param name="args">The command arguments or <c>null</c>.</param>
        /// <returns>The command response text.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="command"/> or <paramref name="args"/> is <c>null</c>.</exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        public static string ExecuteCore(string command, string args)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            if (args == null)
                throw new ArgumentNullException("args");

            return Execute("managed", "{0} {1} {2}", Switch.CoreAppName, command, args);
        }

        /// <summary>
        /// Synchronously executes an arbitrary core NeonSwitch command with formatted arguments.
        /// </summary>
        /// <param name="command">The command </param>
        /// <param name="format">The argument format string.</param>
        /// <param name="args">The command arguments.</param>
        /// <returns>The command response text.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="command"/>, <paramref name="format" />, or 
        /// <paramref name="args" /> is <c>null</c>.
        /// </exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        public static string ExecuteCore(string command, string format, params object[] args)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            if (format == null)
                throw new ArgumentNullException("format");

            if (args == null)
                throw new ArgumentNullException("args");

            return Execute("managed", "{0} {1} {2}", Switch.CoreAppName, command, string.Format(format, args));
        }

        /// <summary>
        /// Aynchronously executes an arbitrary global FreeSWITCH command.
        /// </summary>
        /// <param name="command">The command </param>
        /// <returns>The unique ID for the job.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="command"/> is <c>null</c>.</exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        /// <remarks>
        /// <note>
        /// <para>
        /// Applications that need to monitor the completion of background jobs
        /// should subscribe to the <see cref="JobCompletedEvent"/> which will
        /// be raised when the job has completed.  The unique job ID will be
        /// present in the event arguments.
        /// </para>
        /// <para>
        /// Note that applications need to be prepared to never see completion
        /// events for specific background jobs.
        /// </para>
        /// </note>
        /// </remarks>
        public static Guid ExecuteBackground(string command)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            return Guid.Parse(Execute("bgapi", command));
        }

        /// <summary>
        /// Aynchronously executes an arbitrary global FreeSWITCH command with arguments.
        /// </summary>
        /// <param name="command">The command </param>
        /// <param name="args">The command arguments or <c>null</c>.</param>
        /// <returns>The unique ID for the job.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="command"/> or <paramref name="args" /> is <c>null</c>.</exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        /// <remarks>
        /// <note>
        /// <para>
        /// Applications that need to monitor the completion of background jobs
        /// should subscribe to the <see cref="JobCompletedEvent"/> which will
        /// be raised when the job has completed.  The unique job ID will be
        /// present in the event arguments.
        /// </para>
        /// <para>
        /// Note that applications need to be prepared to never see completion
        /// events for specific background jobs.
        /// </para>
        /// </note>
        /// </remarks>
        public static Guid ExecuteBackground(string command, string args)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            if (args == null)
                throw new ArgumentNullException("args");

            return Guid.Parse(Execute("bgapi", "{0} {1}", command, args));
        }

        /// <summary>
        /// Aynchronously executes an arbitrary global FreeSWITCH command with formatted arguments.
        /// </summary>
        /// <param name="command">The command </param>
        /// <param name="format">The argument format string.</param>
        /// <param name="args">The command arguments.</param>
        /// <returns>The unique ID for the job.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="command"/>, <paramref name="format" />, or 
        /// <paramref name="args" /> is <c>null</c>.
        /// </exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        /// <remarks>
        /// <note>
        /// <para>
        /// Applications that need to monitor the completion of background jobs
        /// should subscribe to the <see cref="JobCompletedEvent"/> which will
        /// be raised when the job has completed.  The unique job ID will be
        /// present in the event arguments.
        /// </para>
        /// <para>
        /// Note that applications need to be prepared to never see completion
        /// events for specific background jobs.
        /// </para>
        /// </note>
        /// </remarks>
        public static Guid ExecuteBackground(string command, string format, params object[] args)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            if (format == null)
                throw new ArgumentNullException("format");

            if (args == null)
                throw new ArgumentNullException("args");

            return Guid.Parse(Execute("bgapi", "{0} {1}", command, string.Format(format, args)));
        }

        /// <summary>
        /// Aynchronously executes an arbitrary managed global FreeSWITCH command.
        /// </summary>
        /// <param name="command">The command </param>
        /// <returns>The unique ID for the job.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="command"/> is <c>null</c>.</exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        /// <remarks>
        /// <note>
        /// <para>
        /// Applications that need to monitor the completion of background jobs
        /// should subscribe to the <see cref="JobCompletedEvent"/> which will
        /// be raised when the job has completed.  The unique job ID will be
        /// present in the event arguments.
        /// </para>
        /// <para>
        /// Note that applications need to be prepared to never see completion
        /// events for specific background jobs.
        /// </para>
        /// </note>
        /// </remarks>
        public static Guid ExecuteManagedBackground(string command)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            return Guid.Parse(Execute("managedrun", command));
        }

        /// <summary>
        /// Aynchronously executes an arbitrary managed global FreeSWITCH command with arguments.
        /// </summary>
        /// <param name="command">The command </param>
        /// <param name="args">The command arguments or <c>null</c>.</param>
        /// <returns>The unique ID for the job.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="command"/> or <paramref name="args" /> is <c>null</c>.</exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        /// <remarks>
        /// <note>
        /// <para>
        /// Applications that need to monitor the completion of background jobs
        /// should subscribe to the <see cref="JobCompletedEvent"/> which will
        /// be raised when the job has completed.  The unique job ID will be
        /// present in the event arguments.
        /// </para>
        /// <para>
        /// Note that applications need to be prepared to never see completion
        /// events for specific background jobs.
        /// </para>
        /// </note>
        /// </remarks>
        public static Guid ExecuteManagedBackground(string command, string args)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            if (args == null)
                throw new ArgumentNullException("args");

            return Guid.Parse(Execute("managedrun", "{0} {1}", command, args));
        }

        /// <summary>
        /// Aynchronously executes an arbitrary managed global FreeSWITCH command with formatted arguments.
        /// </summary>
        /// <param name="command">The command </param>
        /// <param name="format">The argument format string.</param>
        /// <param name="args">The command arguments.</param>
        /// <returns>The unique ID for the job.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="command"/>, <paramref name="format" />, or 
        /// <paramref name="args" /> is <c>null</c>.
        /// </exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        /// <remarks>
        /// <note>
        /// <para>
        /// Applications that need to monitor the completion of background jobs
        /// should subscribe to the <see cref="JobCompletedEvent"/> which will
        /// be raised when the job has completed.  The unique job ID will be
        /// present in the event arguments.
        /// </para>
        /// <para>
        /// Note that applications need to be prepared to never see completion
        /// events for specific background jobs.
        /// </para>
        /// </note>
        /// </remarks>
        public static Guid ExecuteManagedBackground(string command, string format, params object[] args)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            if (format == null)
                throw new ArgumentNullException("format");

            if (args == null)
                throw new ArgumentNullException("args");

            return Guid.Parse(Execute("managedrun", "{0} {1}", command, string.Format(format, args)));
        }

        /// <summary>
        /// Aynchronously executes an arbitrary core NeonSwitch command.
        /// </summary>
        /// <param name="command">The command </param>
        /// <returns>The unique ID for the job.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="command"/> is <c>null</c>.</exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        /// <remarks>
        /// <note>
        /// <para>
        /// Applications that need to monitor the completion of background jobs
        /// should subscribe to the <see cref="JobCompletedEvent"/> which will
        /// be raised when the job has completed.  The unique job ID will be
        /// present in the event arguments.
        /// </para>
        /// <para>
        /// Note that applications need to be prepared to never see completion
        /// events for specific background jobs.
        /// </para>
        /// </note>
        /// </remarks>
        public static Guid ExecuteCoreBackground(string command)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            return Guid.Parse(Execute("managedrun", "{0} {1}", Switch.CoreAppName, command));
        }

        /// <summary>
        /// Aynchronously executes an arbitrary core NeonSwitch command with arguments.
        /// </summary>
        /// <param name="command">The command </param>
        /// <param name="args">The command arguments or <c>null</c>.</param>
        /// <returns>The unique ID for the job.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="command"/> or <paramref name="args" /> is <c>null</c>.</exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        /// <remarks>
        /// <note>
        /// <para>
        /// Applications that need to monitor the completion of background jobs
        /// should subscribe to the <see cref="JobCompletedEvent"/> which will
        /// be raised when the job has completed.  The unique job ID will be
        /// present in the event arguments.
        /// </para>
        /// <para>
        /// Note that applications need to be prepared to never see completion
        /// events for specific background jobs.
        /// </para>
        /// </note>
        /// </remarks>
        public static Guid ExecuteCoreBackground(string command, string args)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            if (args == null)
                throw new ArgumentNullException("args");

            return Guid.Parse(Execute("managedrun", "{0} {1} {2}", Switch.CoreAppName, command, args));
        }

        /// <summary>
        /// Aynchronously executes an arbitrary core NeonSwitch command with formatted arguments.
        /// </summary>
        /// <param name="command">The command </param>
        /// <param name="format">The argument format string.</param>
        /// <param name="args">The command arguments.</param>
        /// <returns>The unique ID for the job.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="command"/>, <paramref name="format" />, or 
        /// <paramref name="args" /> is <c>null</c>.
        /// </exception>
        /// <exception cref="SwitchException">Thrown if the command was not completed successfully.</exception>
        /// <remarks>
        /// <note>
        /// <para>
        /// Applications that need to monitor the completion of background jobs
        /// should subscribe to the <see cref="JobCompletedEvent"/> which will
        /// be raised when the job has completed.  The unique job ID will be
        /// present in the event arguments.
        /// </para>
        /// <para>
        /// Note that applications need to be prepared to never see completion
        /// events for specific background jobs.
        /// </para>
        /// </note>
        /// </remarks>
        public static Guid ExecuteCoreBackground(string command, string format, params object[] args)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            if (format == null)
                throw new ArgumentNullException("format");

            if (args == null)
                throw new ArgumentNullException("args");

            return Guid.Parse(Execute("managedrun", "{0} {1}", Switch.CoreAppName, command, string.Format(format, args)));
        }
    }
}
