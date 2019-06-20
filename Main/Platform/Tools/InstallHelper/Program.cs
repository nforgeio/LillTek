//-----------------------------------------------------------------------------
// FILE:        Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the InstallHelper tool entrypoint.

using System;
using System.Drawing;
using System.Reflection;
using System.Collections;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows.Forms;

using LillTek.Common;

namespace LillTek.Tools.InstallHelper
{
    /// <summary>
    /// Implements the InstallHelper tool entrypoint.
    /// See the application entry point method <see cref="Program.Main">Main</see>
    /// for a description of the command line parameters.
    /// </summary>
    public class Program
    {
        //---------------------------------------------------------------------
        // Private types

        private sealed class Command
        {
            public string Op;
            public string Param;

            public Command(string op, string param)
            {
                this.Op    = op;
                this.Param = param;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private static string title = Program.Name;

        /// <summary>
        /// Returns the application's name.
        /// </summary>
        internal static string Name
        {
            get { return "Install Helper"; }
        }

        /// <summary>
        /// Returns the title string to be displayed in command
        /// related dialogs.
        /// </summary>
        internal static string Title
        {
            get { return title; }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <remarks>
        /// See <see cref="OverviewDoc"/> for more information on the operation
        /// of the tool and its command line parameters.
        /// </remarks>
        [STAThread]
        public static void Main(string[] args)
        {
            bool        fWait     = false;
            int         processID = 0;
            ArrayList   commands;

            Helper.InitializeApp(Assembly.GetExecutingAssembly());

            args = CommandLine.ExpandFiles(args);

            if (args.Length == 0)
            {
                UsageForm.Show();
                Environment.Exit(1);
                return;
            }

            // Parse the commands

            commands = new ArrayList();
            for (int i = 0; i < args.Length; i++)
            {
                var command = args[i].Trim();

                if (command.StartsWith("-wait:"))
                {
                    fWait = true;
                    try
                    {
                        processID = (int)uint.Parse(command.Substring(6));
                    }
                    catch
                    {
                        MessageBox.Show(string.Format("Invalid process ID in [{0}].", command), Program.Name);
                        Environment.Exit(1);
                        return;
                    }
                }
                else if (command.StartsWith("-title:"))
                {
                    commands.Add(new Command("title", command.Substring(7)));
                }
                else if (command.StartsWith("-start:"))
                {
                    commands.Add(new Command("start", command.Substring(7)));
                }
                else if (command.StartsWith("-configdb:"))
                {
                    commands.Add(new Command("configdb", command.Substring(10)));
                }
                else if (command.StartsWith("-regeventsource:"))
                {
                    commands.Add(new Command("regeventsource", command.Substring(16)));
                }
                else
                {
                    MessageBox.Show(string.Format("Unknown command: {0}", command), Program.Name);
                    Environment.Exit(1);
                    return;
                }
            }

            // Wait for the process to exit if requested.

            if (fWait)
            {
                try
                {
                    var process = Process.GetProcessById(processID);

                    process.EnableRaisingEvents = true;

                    process.WaitForExit();
                }
                catch
                {
                    // The process must have already exited.
                }
            }

            // Process the rest of the commands.

            foreach (Command command in commands)
            {
                try
                {
                    switch (command.Op)
                    {
                        case "title":

                            title = command.Param;
                            break;

                        case "start":

                            StartForm.Show(command.Param);
                            break;

                        case "configdb":

                            string      cmdPath = Helper.EntryAssemblyFolder + Helper.PathSepString + "DBPackage.exe";
                            string      cmdArgs = "\"" + command.Param + "\"";
                            Process     process;

                            process                     = new Process();
                            process.StartInfo           = new ProcessStartInfo(cmdPath, cmdArgs);
                            process.EnableRaisingEvents = true;

                            try
                            {
                                process.Start();
                            }
                            catch
                            {
                                throw new Exception(string.Format("Cannot launch process: {0} {1}", cmdPath, cmdArgs));
                            }

                            process.WaitForExit();
                            break;

                        case "regeventsource":

                            string eventSource = command.Param;

                            NativeSysLogProvider.CreateLogs(eventSource);
                            break;
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show(string.Format("InstallHelper: Operation: [{0}]\r\n\r\nError:\r\n\\r\n{1}", command.Op, e.Message),
                                    "LillTek Install Helper",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
