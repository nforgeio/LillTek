//-----------------------------------------------------------------------------
// FILE:        Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: It slices, it dices, it makes jullienne fries!

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// It slices, it dices, it makes jullienne fries!
    /// </summary>
    public static class Program
    {
        private static StreamWriter         logWriter;          // The log stream
        private static StreamWriter         outWriter;          // The output stream specified by -out:file
                                                                // (null if writing to stdout)
        private static DateTime             nextStatusUpdate;   // Next time to update status output 
        private static TimeSpan             statusInterval;     // Interval between status updates
        private static string               orgCmdLine;         // The reassembled command line
        private static NetworkBinding       cloudEP = null;     // The messaging cloud EP (or null)
        private static LeafRouter           router  = null;     // The message router (or null)

        /// <summary>
        /// Returns the application's command line string.
        /// </summary>
        public static string CommandLine
        {
            get { return orgCmdLine; }
        }

        /// <summary>
        /// The name and version of this tool.
        /// </summary>
        public static string ToolName { get; private set; }

        /// <summary>
        /// Appends a formatted line of text to the application log file.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        public static void Log(string format, params object[] args)
        {
            if (args.Length == 0)
                logWriter.WriteLine(format);
            else
                logWriter.WriteLine(string.Format(format, args));
        }

        /// <summary>
        /// Writes a formatted line of text to stdout.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        public static void Output(string format, params object[] args)
        {
            if (outWriter == null)
            {
                if (args.Length == 0)
                    Console.Out.WriteLine(format);
                else
                    Console.Out.WriteLine(string.Format(format, args));
            }
            else
            {
                if (args.Length == 0)
                    outWriter.WriteLine(format);
                else
                    outWriter.WriteLine(string.Format(format, args));
            }
        }

        /// <summary>
        /// Writes a formatted line of text to stderr.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        public static void Error(string format, params object[] args)
        {
            if (args.Length == 0)
                Console.Error.WriteLine(format);
            else
                Console.Error.WriteLine(string.Format(format, args));
        }

        /// <summary>
        /// Writes formatted text to the console window.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        /// <remarks>
        /// This is designed to be used for commands that wish to present
        /// status information to the user in the console window.  The command
        /// works clearing the console window writing the requested string.
        /// </remarks>
        public static void WriteStatus(string format, params object[] args)
        {
            Console.Clear();
            Console.CursorLeft = 0;
            Console.CursorTop   = 0;

            if (args.Length == 0)
                Console.Write(format);
            else
                Console.Write(string.Format(format, args));
        }

        /// <summary>
        /// Returns <c>true</c> if it's time to update the status of the
        /// operation in the console window.  This will return true
        /// approximately once a second.
        /// </summary>
        public static bool UpdateStatusNow
        {
            get
            {
                var now = SysTime.Now;

                if (outWriter != null && now >= nextStatusUpdate)
                {
                    nextStatusUpdate = now + statusInterval;
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Returns the message router to be used for subsequent commands,
        /// starting one if necessary.
        /// </summary>
        public static MsgRouter Router
        {
            get
            {
                RouterSettings  settings;
                Config          config;
                MsgEP           routerEP;

                if (router != null)
                    return router;

                config = new Config("MsgRouter");

                // $todo(jeff.lill): 
                //
                // At some point this should be loaded from
                // a configuration file or on the command line.

                routerEP = new MsgEP(string.Format("physical://DETACHED/{0}/{1}", Const.DCDefHubName, Helper.NewGuid()));
                settings = new RouterSettings(routerEP);
                settings.AdvertiseTime = TimeSpan.FromSeconds(1);   // Set this to a really small value to give
                                                                    // the router a good chance to discover all
                                                                    // other routers quickly.
                settings.AppName = "LillTek.Vegomatic";
                settings.CloudEP = cloudEP != null ? cloudEP
                                                   : config.Get("CloudEP", settings.CloudEP);

                router = new LeafRouter();
                router.Start(settings);
                Thread.Sleep(5000);     // Give the router chance to discover the routes

                return router;
            }
        }

        /// <summary>
        /// It slices, it dices, it make jullienne fries!
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Main(string[] args)
        {
            string usage =
@"
Usage: vegomatic [-out:<file>] [-ep:<cloud-ep>] [-log:<path>] 
                 <command> <cmdarg0> <cmdarg1>...

Commands
--------

vegomatic aws ...        - Amazon Web Service (AWS) commands
vegomatic text ...       - TEXT related commands
vegomatic file ...       - Misc file utilities
vegomatic crypto ...     - Cyptographic utilities
vegomatic zip ...        - ZIP Archive creation
vegomatic unzip ...      - ZIP Archive extraction
vegomatic auth ...       - Authentication Service commands
vegomatic pack ...       - Application Package commands
vegomatic appstore ...   - Application Store commands
vegomatic ini ...        - INI file manipulation commands
vegomatic eventlog ...   - Windows Event Log commands
vegomatic helpmunge...   - Munges code documentation help files
vegomatic silverlight... - Extended web service proxy generator
                           for Silverlight applications
vegomatic linq2sql ...   - Munges LINQ to SQL source files
                           generated by SQLMetal
vegomatic svn ...        - Subversion related utilities
vegomatic tf ...         - TFS related utilities
vegomatic source ...     - Source code related commands
vegomatic timezone ...   - Timezone related commands
vegomatic http ...       - HTTP.SYS related commands
vegomatic net ...        - Network utilities
vegomatic web ...        - Website utilities
vegomatic iis ...        - IIS related management
vegomatic tsql ...       - T-SQL script generation
vegomatic memory ...     - Memory related commands

Options
-------

-out:<file>         Redirects standard output to this file. Using this
                    option allows progress status to be written to the
                    console window.

-ep:<cloud-ep>      Specifies the LillTek Messaging UDP multicast 
                    endpoint to be used when transmitting the messages.
                    This is expressed as <dotted-quad>:<port>
                    (see the LillTek Messaging note below).

-log:<path>         Generate a log file.

The application will also create a file called ""vegomatic.log"" 
in the current directory where certain commands will write summarizes 
of what happened.

LillTek Messaging
-----------------
Some vegomatic commands need route messages via the LillTek Messaging
layer.  For this to work properly, vegomatic needs to know the 
UDP multicast endpoint to use for discovering the other messaging
nodes in the cloud.

This multicast endpoint can be specified explicitly via the
-ep:<cloud-ep> option.  If this option is not present, then
the command will look for the MsgRouter.CloudEP setting in
the local configuration file or in the file referenced by the 
LillTek.ConfigOverride environment variable if present.  Otherwise, 
the default endpoint specified by LillTek.Const.DCCloudEP will 
be used.
";
            Dictionary<string, string>  options;
            StringBuilder               sb;
            string                      cmdLine;
            string[]                    cmdArgs;
            int                         pos;
            int                         p, pEnd;
            string                      name, value;
            string                      command;
            string                      logPath;

            try
            {

                Program.ToolName = "Vegomatic v" + Helper.GetVersionString(Assembly.GetExecutingAssembly());

                usage = Program.ToolName + "\r\n" +
                        Helper.GetCopyright(Assembly.GetExecutingAssembly()) + "\r\n\r\n" +
                        usage;

                // Reassemble the command line

                sb = new StringBuilder();
                sb.Append("vegomatic");
                for (int i = 0; i < args.Length; i++)
                {
                    sb.Append(' ');
                    if (args[i].IndexOf(' ') == -1)
                        sb.Append(args[i]);
                    else
                        sb.AppendFormat("\"{0}\"", args[i]);
                }

                // Expand any command line response files.

                args = new CommandLine(args, true).Arguments;
            }
            catch (Exception e)
            {
                Error("{0}: {1}", e.GetType().Name, e.Message);
                return 1;
            }

            orgCmdLine = sb.ToString();

            // Execute the command

            if (args.Length == 0)
            {
                Program.Error(usage);
                return 1;
            }

            try
            {
                // Initialize the status update timer

                statusInterval   = TimeSpan.FromSeconds(1.0);
                nextStatusUpdate = SysTime.Now;

                // Initialize the log stream

                logPath = null;
                foreach (string arg in args)
                {
                    if (!arg.StartsWith("-"))
                        break;

                    if (arg.StartsWith("-log:"))
                    {
                        logPath = arg.Substring(5);
                        if (logPath == string.Empty)
                            logPath = null;
                    }
                }

                logWriter = new StreamWriter(Stream.Null);

                if (logPath != null)
                {
                    try
                    {
                        logWriter = new StreamWriter(logPath);
                    }
                    catch
                    {
                        logWriter = new StreamWriter(Stream.Null);
                    }
                }

                sb = new StringBuilder();
                sb.Append("vegomatic");
                foreach (string arg in args)
                    sb.Append(" " + arg);

                cmdLine = sb.ToString();
                logWriter.WriteLine(cmdLine);
                logWriter.WriteLine(new string('-', cmdLine.Length));
                logWriter.WriteLine();

                // Parse the global options and extract the command arguments.

                options = new Dictionary<string, string>();
                for (pos = 0; pos < args.Length; pos++)
                {
                    if (!args[pos].StartsWith("-"))
                        break;

                    p = 1;
                    pEnd = args[pos].IndexOf(':');
                    if (pEnd == -1)
                    {
                        name = args[pos].Substring(p).ToLowerInvariant();
                        value = "yes";
                    }
                    else
                    {
                        name = args[pos].Substring(p, pEnd - p).ToLowerInvariant();
                        value = args[pos].Substring(pEnd + 1);
                    }

                    options.Add(name, value);
                }

                if (pos == args.Length)
                {
                    Program.Error(usage);
                    return 1;
                }

                command = args[pos++];

                if (pos == args.Length)
                    cmdArgs = new string[0];
                else
                {
                    cmdArgs = new string[args.Length - pos];
                    Array.Copy(args, pos, cmdArgs, 0, cmdArgs.Length);
                }

                if (options.TryGetValue("ep", out value))
                    cloudEP = NetworkBinding.Parse(value);

                // Initialize the output stream.

                if (options.TryGetValue("out", out value))
                    outWriter = new StreamWriter(value);

                // Execute the command

                switch (command.ToLowerInvariant())
                {
                    case "aws":

                        return AwsCommand.Execute(cmdArgs);

                    case "text":

                        return TextCommand.Execute(cmdArgs);

                    case "source":

                        return SourceCommand.Execute(cmdArgs);

                    case "ini":

                        return IniCommand.Execute(cmdArgs);

                    case "eventlog":

                        return EventLogCommand.Execute(cmdArgs);

                    case "file":

                        return FileCommand.Execute(cmdArgs);

                    case "crypto":

                        return CryptoCommand.Execute(cmdArgs);

                    case "zip":

                        return ZipCommand.Execute(cmdArgs);

                    case "unzip":

                        return UnzipCommand.Execute(cmdArgs);

                    case "auth":

                        return AuthCommand.Execute(cmdArgs);

                    case "pack":

                        return PackCommand.Execute(cmdArgs);

                    case "helpmunge":

                        return HelpMungeCommand.Execute(cmdArgs);

                    case "silverlight":

                        return SilverlightCommand.Execute(cmdArgs);

                    case "linq2sql":

                        return Linq2SqlCommand.Execute(cmdArgs);

                    case "svn":

                        return SvnCommand.Execute(cmdArgs);

                    case "tf":

                        return TFCommand.Execute(cmdArgs);

                    case "timezone":

                        return TimeZoneCommand.Execute(cmdArgs);

                    case "http":

                        return HttpCommand.Execute(cmdArgs);

                    case "net":

                        return NetCommand.Execute(cmdArgs);

                    case "web":

                        return WebCommand.Execute(cmdArgs);

                    case "iis":

                        return IisCommand.Execute(cmdArgs);

                    case "tsql":

                        return TSQLCommand.Execute(cmdArgs);

                    case "memory":

                        return MemoryCommand.Execute(cmdArgs);

                    default:

                        Program.Error(usage);
                        return 1;
                }
            }
            catch (Exception e)
            {
                Program.Error("Error [{0}]: {1}", e.GetType().Name, e.Message);
                return 1;
            }
            finally
            {
                if (logWriter != null)
                    logWriter.Close();

                if (outWriter != null)
                    outWriter.Close();
            }
        }
    }
}
