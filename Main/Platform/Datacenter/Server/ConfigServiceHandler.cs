//-----------------------------------------------------------------------------
// FILE:        ConfigServiceHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the configuration service handler.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Datacenter.Msgs;
using LillTek.Messaging;

// $todo(jeff.lill): 
//
// I need to add some mechanism that makes use of the 
// ExeVersion parameter (perhaps some kind of ##range clause
// to the ##switch statement).

// $todo(jeff.lill): 
//
// Implement a subnet switch mechanism that selects settings
// based on the subnet the server's active network adapter 
// is on.

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Implements the LillTek Configuration Service Handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The configuration service's purpose is to provide centralized
    /// configuration services across the network.  The client side of
    /// this capability is implemented by the <see cref="ConfigServiceProvider" />
    /// class and the server side by this class.
    /// </para>
    /// <para>
    /// This service handler currently advertises the following message
    /// endpoints:
    /// </para>
    /// <code language="none">
    /// abstract://LillTek/DataCenter/ConfigService
    /// </code>
    /// <para>
    /// This may be remapped to another logical endpoint via the message
    /// routers <b>MsgRouter.AbstractMap</b> configuation setting.
    /// </para>
    /// <para><b><u>Implementation</u></b></para>
    /// <para>
    /// Client applications request their configuration information via a
    /// <see cref="ConfigServiceProvider" /> instance.  This class works
    /// initiating a query to the handler's endpoint.  The client sends
    /// a <see cref="GetConfigMsg" /> message and the handler responds with a
    /// <see cref="GetConfigAck" />.
    /// </para>
    /// <para>
    /// The goal of all of this is to basically return a text *.ini file
    /// to the client which will be processed by the <see cref="Config" />
    /// class.  The <see cref="GetConfigMsg" /> provides for a parameterized
    /// query so that the handler can customize the response as necessary.
    /// These parameters include:
    /// </para>
    /// <code language="none">
    /// MachineName     -- the client machine's host name
    /// ExeName         -- the unqualified client application's executable file
    /// ExeVersion      -- the client application version
    /// Usage           -- categorizes the application's use
    /// </code>
    /// <para>
    /// The config service handler gets the configuration information it serves
    /// from text files located in the directory specified by the 
    /// <b>LillTek.DatacenterConfigService.SettingsFolder</b> configuration setting. 
    /// These settings files will using naming conventions that 
    /// relates to the <b>ExeName</b> and <b>MachineName</b> request parameters with
    /// configuration information relating to a specific application being saved in
    /// a file named <b>app-&lt;ExeName&gt;.ini</b> and settings relating to a specific
    /// machine saved in a file named <b>svr-&lt;MachineName&gt;.ini</b>.
    /// </para>
    /// <para>
    /// The config service handler performs the following steps when processing
    /// a client request: 
    /// </para>
    /// <list type="number">
    ///     <item>
    ///     If a non-empty <b>ExeName</b> is specified and the corresponding
    ///     <b>app-&lt;ExeName&gt;.ini</b> file is present, then it will be loaded
    ///     into a buffer.
    ///     </item>
    ///     <item>
    ///     If a non-empty <b>MachineName</b> is specified and the corresponding
    ///     <b>svr-&lt;MachineName&gt;.ini</b> file is present, then it will be appended
    ///     the buffer.
    ///     </item>
    ///     <item>
    ///     Any <b>##include</b> and <b>##switch</b> statements in the buffered configuration 
    ///     information will be processed as described below providing a mechanism to further
    ///     customize the configuration.
    ///     </item>
    ///     <item>
    ///     The resulting configuration text will be returned to the client in
    ///     a <see cref="GetConfigAck" /> message.
    ///     </item>
    /// </list>
    /// <para><b><u>The ##switch Statement</u></b></para>
    /// <para>
    /// The <b>##switch..##endswitch</b> statement can be used within configuration *.ini files
    /// to customize the response based on the configuration parameters.  Note the use of two 
    /// pound signs "##" to distingush between <b>##switch</b> statements that execute on the
    /// Configuration Service from <b>#switch</b> statements that are processed by the
    /// <see cref="Config" /> client.  Here's an example:
    /// </para>
    /// <code language="none">
    /// FooService.Bar = 10
    /// 
    /// ##switch $(Usage)
    /// 
    ///     ##case Primary
    /// 
    ///         FooService.Performance = High
    ///         FooService.Primary     = yes
    /// 
    ///     ##case Backup
    /// 
    ///         FooService.Performance = Low
    ///         FooService.Primary     = no
    /// 
    ///     ##default
    /// 
    ///         FooService.Performance = Medium
    /// 
    /// ##endswitch
    /// 
    /// FooService.FooBar = 20
    /// </code>
    /// <para>
    /// This example defines the FooService.Bar and FooService.FooBar configuration
    /// settings before and after a ##switch...##endswitch statement.  The ##switch statement
    /// defines the value of FooService.Performance based on the value of the <b>Usage</b>
    /// parameter passed by the client.  So if the client passed <b>Usage=Primary</b>
    /// the config service handler would process this statement and return:
    /// </para>
    /// <code language="none">
    /// FooService.Bar         = 10
    /// FooService.Performance = High
    /// FooService.Primary     = yes
    /// FooService.FooBar      = 20
    /// </code>
    /// <para>
    /// The ##switch statement currently accepts only the four configuration
    /// parameter names: <b>MachineName</b>, <b>ExeName</b>, <b>ExeVersion</b>,
    /// and <b>Usage</b>.  The statement performs case insenstive matching of
    /// both the parameter names as well as the parameter values.
    /// </para>
    /// <para>
    /// The <b>##default</b> clause can be used to include settings when the
    /// variable doesn't match any of the <b>##case</b> values.  Note that to
    /// work properly, the <b>##default</b> clause should appear after all of
    /// the <b>##case</b> clauses in the <b>##switch</b> statement.
    /// </para>
    /// <para><b><u>The ##include Statement</u></b></para>
    /// The <b>##include</b> provides a mechanism for sharing common settings across
    /// multiple configuration files.  This statement specfies the name of a
    /// configuration text file to be inserted into configuration output.  Here's
    /// an example:
    /// <code language="none">
    /// ##include Default.ini
    /// 
    /// FooService.Foo = 10
    /// FooService.Bar = 20
    /// </code>
    /// <para>
    /// This example specifies that the contents of the file <b>Default.ini</b>
    /// be inserted at the beginning of the configuration output.  The ##include
    /// statement looks for the file passed in folder specified by the 
    /// <b>LillTek.Datacenter.ConfigService.SettingsFolder</b> setting.  Note 
    /// that relative and absolute file paths are not supported for security reasons.
    /// </para>
    /// <para>
    /// <b>##include</b> statements may be nested up to 16 levels deep and may also
    /// be nested within <b>##switch</b> statements.
    /// </para>
    /// <para><b><u>Typical Usage</u></b></para>
    /// <para>
    /// This implementation envisions that the operations staff will typically
    /// deploy a single default or global file with data center wide
    /// settings and include this at the top of the application and server
    /// configuration files.  Then generic application settings will be 
    /// specified in the application files and any service specific overrides
    /// being specified in the server files.
    /// </para>
    /// <para>
    /// By using this pattern, the global settings will appear first in the
    /// configuration values returned to the client, followed by the application
    /// settings and then finally, the server specific settings (essentially 
    /// ordering the setting definitions from general to more specific).  Since the
    /// <see cref="Config" /> class processes configuration settings from top
    /// to bottom, settings the more specific settings will replace the more
    /// general definitions they follow.
    /// </para>
    /// <para><b><u>Configuration Settings</u></b></para>
    /// <para>
    /// By default, the config service handler's configuration setting keys are 
    /// prefixed by <b>LillTek.Datacenter.ConfigService</b> (a custom prefix can
    /// be passed to <see cref="Start" />.
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Setting</th>        
    /// <th width="1">Default</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>SettingsFolder</td>
    ///     <td>(see comment)</td>
    ///     <td>
    ///         The path to the file system folder holding the configuration *.ini files.
    ///         This path can be absolute or relative to the configuration service's
    ///         application executable file.  This will default to a folder named <b>Settings</b>
    ///         beneath the folder holding the application executable.
    ///     </td>
    /// </tr>
    /// </table>
    /// </div>
    /// </remarks>
    public class ConfigServiceHandler : IServiceHandler
    {
        /// <summary>
        /// The service's default configuration key prefix.
        /// </summary>
        public const string ConfigPrefix = "LillTek.Datacenter.ConfigService";

        private const int MaxIncludeDepth = 16;

        private MsgRouter   router;             // The associated router (or null if
                                                // the handler is stopped).
        private string      settingsFolder;     // Fully qualified path to the settings folder
        private char[]      badChars = new char[] { '/', '\\', ':' };

        /// <summary>
        /// Constructs a config service handler instance.
        /// </summary>
        public ConfigServiceHandler()
        {
            this.router = null;
        }

        /// <summary>
        /// Associates the service handler with a message router by registering
        /// the necessary application message handlers.
        /// </summary>
        /// <param name="router">The message router.</param>
        /// <param name="keyPrefix">The configuration key prefix or (null to use <b>LillTek.Datacenter.ConfigService</b>).</param>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// Applications that expose performance counters will pass a non-<c>null</c> <b>perfCounters</b>
        /// instance.  The service handler should add any counters it implements to this set.
        /// If <paramref name="perfPrefix" /> is not <c>null</c> then any counters added should prefix their
        /// names with this parameter.
        /// </para>
        /// </remarks>
        public void Start(MsgRouter router, string keyPrefix, PerfCounterSet perfCounters, string perfPrefix)
        {
            Config config;

            // Make sure that the LillTek.Datacenter message types have been
            // registered with the LillTek.Messaging subsystem.

            LillTek.Datacenter.Global.RegisterMsgTypes();

            // Verify the router parameter

            if (router == null)
                throw new ArgumentNullException("router", "Router cannot be null.");

            if (this.router != null)
                throw new InvalidOperationException("This handler has already been started.");

            // Determine the location of the settings folder

            if (keyPrefix == null)
                keyPrefix = ConfigPrefix;

            config = new Config(keyPrefix);

            settingsFolder = config.Get("SettingsFolder");
            if (settingsFolder == null)
                settingsFolder = Helper.EntryAssemblyFolder + Helper.PathSepString + "Settings";

            if (!settingsFolder.EndsWith(Helper.PathSepString))
                settingsFolder += Helper.PathSepString;

            if (!Directory.Exists(settingsFolder.Substring(0, settingsFolder.Length - 1)))
                throw new FileNotFoundException(string.Format("Settings folder [{0}] does not exist.", settingsFolder));

            // Register the handler and get ready to accept client requests

            this.router = router;
            router.Dispatcher.AddTarget(this);
        }

        /// <summary>
        /// Initiates a graceful shut down of the service handler by ignoring
        /// new client requests.
        /// </summary>
        public void Shutdown()
        {
            Stop();
        }

        /// <summary>
        /// Immediately terminates the processing of all client messages.
        /// </summary>
        public void Stop()
        {
            using (TimedLock.Lock(router.SyncRoot))
            {
                if (router != null)
                {
                    router.Dispatcher.RemoveTarget(this);
                    router = null;
                }
            }
        }

        /// <summary>
        /// Returns the current number of client requests currently being processed.
        /// </summary>
        public int PendingCount
        {
            get { return 0; }
        }

        private sealed class SwitchState
        {
            public string   Value;
            public bool     IsVersion;
            public bool     PrevState;
            public bool     CaseProcessed;
            public bool     DefaultProcessed;

            public SwitchState(string value, bool prevState)
            {
                this.Value            = value;
                this.IsVersion        = false;
                this.PrevState        = prevState;
                this.CaseProcessed    = false;
                this.DefaultProcessed = false;
            }

            public SwitchState(Version value, bool prevState)
            {
                this.Value            = value.ToString();
                this.IsVersion        = true;
                this.PrevState        = prevState;
                this.CaseProcessed    = false;
                this.DefaultProcessed = false;
            }
        }

        private sealed class ParseState
        {
            /// <summary>
            /// The original query.
            /// </summary>
            public GetConfigMsg Query;

            /// <summary>
            /// The stack of file names already being processed (used for circular reference detection).
            /// </summary>
            public Stack<string> FileStack;

            /// <summary>
            /// The stack of ##switch enable states.
            /// </summary>
            public Stack<SwitchState> SwitchStack;

            /// <summary>
            /// <c>true</c> if config lines are to be included in the output.
            /// </summary>
            public bool IncludeLines;

            /// <summary>
            /// Constructor
            /// </summary>
            public ParseState(GetConfigMsg query)
            {
                this.Query        = query;
                this.FileStack    = new Stack<string>();
                this.SwitchStack  = new Stack<SwitchState>();
                this.IncludeLines = true;
            }
        }

        /// <summary>
        /// Processes the settings file passed and appends the result to the
        /// string builder.
        /// </summary>
        /// <param name="sb">The string builder.</param>
        /// <param name="state">Parsing state info.</param>
        /// <param name="fname">The settings file name.</param>
        private void AppendFile(StringBuilder sb, ParseState state, string fname)
        {
            int             switchDepth = state.SwitchStack.Count;
            StreamReader    reader;
            string          line,    lwr;
            int             lineNum;

            reader = new StreamReader(settingsFolder + fname);
            try
            {
                state.FileStack.Push(fname);

                line = reader.ReadLine();
                lineNum = 1;
                while (line != null)
                {
                    lwr = line.Trim().ToLowerInvariant();
                    if (lwr.StartsWith("##include"))
                    {

                        string includeFile;

                        includeFile = lwr.Substring(9).Trim();

                        if (state.FileStack.Count >= MaxIncludeDepth)
                            throw new FormatException(string.Format("File[{0}:{1}]: ##include file nesting exceeds [{2}].", fname, lineNum, MaxIncludeDepth));

                        foreach (string f in state.FileStack)
                            if (String.Compare(f, includeFile, true) == 0)
                                throw new FormatException(string.Format("File[{0}:{1}]: Circular ##include reference to [{2}].", fname, lineNum, fname));

                        AppendFile(sb, state, includeFile);
                    }
                    else if (lwr.StartsWith("##switch"))
                    {
                        string var;

                        var = lwr.Substring(8).Trim().ToLowerInvariant();
                        switch (var)
                        {
                            case "$(machinename)":

                                state.SwitchStack.Push(new SwitchState(state.Query.MachineName, state.IncludeLines));
                                break;

                            case "$(exefile)":

                                state.SwitchStack.Push(new SwitchState(state.Query.ExeFile, state.IncludeLines));
                                break;

                            case "$(exeversion)":

                                state.SwitchStack.Push(new SwitchState(state.Query.ExeVersion, state.IncludeLines));
                                break;

                            case "$(usage)":

                                state.SwitchStack.Push(new SwitchState(state.Query.Usage, state.IncludeLines));
                                break;

                            default:

                                throw new FormatException(string.Format("Unknown ##switch variable [{0}].", var));
                        }

                        state.IncludeLines = false;
                    }
                    else if (lwr.StartsWith("##endswitch"))
                    {
                        if (state.SwitchStack.Count <= switchDepth)
                            throw new FormatException(string.Format("File[{0}:{1}]: No matching ##switch statement.", fname, lineNum));

                        state.IncludeLines = state.SwitchStack.Pop().PrevState;
                    }
                    else if (lwr.StartsWith("##case"))
                    {
                        string      value;
                        SwitchState switchState = state.SwitchStack.Peek();

                        if (state.SwitchStack.Count == switchDepth)
                            throw new FormatException(string.Format("File[{0}:{1}]: No matching ##switch statement.", fname, lineNum));

                        if (switchState.DefaultProcessed)
                            throw new FormatException(string.Format("File[{0}:{1}]: No matching ##case clauses must appear before ##default clauses.", fname, lineNum));

                        value = lwr.Substring(6).Trim();
                        if (!switchState.IsVersion && String.Compare(value, switchState.Value, true) == 0)
                        {
                            if (switchState.CaseProcessed)
                                throw new FormatException(string.Format("File[{0}:{1}]: Duplicate ##case clause.", fname, lineNum));

                            switchState.CaseProcessed = true;
                            state.IncludeLines = true;
                        }
                        else if (switchState.IsVersion)
                        {
                            Version ver1, ver2;

                            try
                            {
                                ver1 = new Version(value);
                                ver2 = new Version(switchState.Value);
                            }
                            catch
                            {
                                throw new FormatException(string.Format("File[{0}:{1}]: Illegal version syntax.", fname, lineNum));
                            }

                            if (ver1 != ver2)
                                state.IncludeLines = false;
                            else
                            {
                                if (switchState.CaseProcessed)
                                    throw new FormatException(string.Format("File[{0}:{1}]: Duplicate ##case clause.", fname, lineNum));

                                switchState.CaseProcessed = true;
                                state.IncludeLines = true;
                            }
                        }
                        else
                            state.IncludeLines = false;
                    }
                    else if (lwr.StartsWith("##default"))
                    {
                        SwitchState switchState = state.SwitchStack.Peek();

                        if (state.SwitchStack.Count == switchDepth)
                            throw new FormatException(string.Format("File[{0}:{1}]: No matching ##switch statement.", fname, lineNum));

                        if (switchState.DefaultProcessed)
                            throw new FormatException(string.Format("File[{0}:{1}]: Multiple ##default clauses.", fname, lineNum));

                        state.IncludeLines = !switchState.CaseProcessed;
                        switchState.DefaultProcessed = true;
                    }
                    else
                    {
                        if (state.IncludeLines)
                            sb.AppendLine(line);
                    }

                    line = reader.ReadLine();
                    lineNum++;
                }
            }
            finally
            {
                state.FileStack.Pop();
                reader.Close();
            }

            if (state.SwitchStack.Count > switchDepth)
                throw new FormatException(string.Format("File[{0}:{1}]: Missing ##endswitch.", fname, lineNum));
        }

        /// <summary>
        /// Performs the configuration lookup on behalf of client applications.
        /// </summary>
        /// <param name="query">The query message.</param>
        [MsgSession(Type = SessionTypeID.Query)]
        [MsgHandler(LogicalEP = ConfigServiceProvider.GetConfigEP)]
        public void OnMsg(GetConfigMsg query)
        {
            using (TimedLock.Lock(router.SyncRoot))
            {
                if (router == null)
                    return;     // The handler has been shut down or stopped

                try
                {
                    var     sb = new StringBuilder();
                    bool    load = false;
                    string  fname;

                    // Append the exeFile based config (if any)

                    if (query.ExeFile != null && query.ExeFile.Trim() != string.Empty)
                    {
                        fname = "app-" + query.ExeFile + ".ini";
                        if (fname.IndexOfAny(badChars) != -1)
                            throw new ArgumentException("[ExeFile] has invalid characters.");

                        if (File.Exists(settingsFolder + fname))
                        {
                            load = true;
                            AppendFile(sb, new ParseState(query), fname);
                        }
                    }

                    // Append the machineName based config (if any)

                    if (query.MachineName != null && query.MachineName.Trim() != string.Empty)
                    {
                        fname = "svr-" + query.MachineName + ".ini";
                        if (fname.IndexOfAny(badChars) != -1)
                            throw new ArgumentException("[MachineName] has invalid characters.");

                        if (File.Exists(settingsFolder + fname))
                        {
                            load = true;
                            AppendFile(sb, new ParseState(query), fname);
                        }
                    }

                    if (load)
                        router.ReplyTo(query, new GetConfigAck(sb.ToString()));
                    else
                        router.ReplyTo(query, new GetConfigAck(new Exception("No configuration settings.")));
                }
                catch (Exception e)
                {
                    router.ReplyTo(query, new GetConfigAck(e));
                }
            }
        }
    }
}
