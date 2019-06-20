//-----------------------------------------------------------------------------
// FILE:        IisCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements Team Foundation related commands.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Data;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements the Team Foundation Server related commands.
    /// </summary>
    public static class TFCommand
    {
        /// <summary>
        /// Executes the specified TF command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {
            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic tf changeset <workspace-path> <source-path> <namespace> [ internal ]

    workspace-path  - Path of the folder enlisted into TFS source control.
    source-path     - Path to the output C# source file.
    namespace       - C# namespace to wrap around the generated class.
    internal        - Generate an internal rather than a public class

This command queries the TFS repository to determine the current changeset 
number for a specified local workspace and then it generates a C# source file
that defines a class with the changeset number as a constant.  This is intended
to be used during builds to automate the generation of build version
numbers.

Note that this command will generate a changeset number of 0 if TFS
could not determine the current changeset.
";

            if (args.Length < 1)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "changeset":

                    if (args.Length == 4)
                    {
                        return Changeset(args[1], args[2], args[3], false);
                    }
                    else if (args.Length == 5)
                    {
                        return Changeset(args[1], args[2], args[3], args[4] == "internal");
                    }
                    else
                    {
                        Program.Error(usage);
                        return 1;
                    }

                default:

                    Program.Error(usage);
                    return 1;
            }
        }

        /// <summary>
        /// Generates the build changeset file with the current TFS changeset number.
        /// </summary>
        /// <param name="workspacePath">Path of the folder enlisted into TFS source control.</param>
        /// <param name="sourcePath">Path to the output C# source file.</param>
        /// <param name="nameSpace">C# namespace to wrap around the generated class.</param>
        /// <param name="generateInternal">Pass <c>true</c> to generate an <c>internal</c> rather than a <c>public</c> class.</param>
        /// <returns>Zero on success.</returns>
        private static int Changeset(string workspacePath, string sourcePath, string nameSpace, bool generateInternal)
        {
            try
            {
                workspacePath = Environment.ExpandEnvironmentVariables(workspacePath);
                sourcePath    = Environment.ExpandEnvironmentVariables(sourcePath);

                // Get the current SVN revision number.

                var         args    = string.Format("history \"{0};1~W\" /r /stopafter:1 /i", workspacePath);
                var         command = string.Format("tf.exe {0}", args);
                var         result  = Helper.ExecuteCaptureStreams("tf.exe", args, TimeSpan.FromSeconds(30));
                string      changeSet;

                // tf history c:\workspace1;1~W /r /stopafter:1 /i

                if (result.ExitCode != 0)
                {
                    Program.Error(command);
                    Program.Error(string.Empty);
                    Program.Error(result.StandardError);
                    Program.Error("[{0}] command did not return the changeset number.  Setting 0000 instead.", command);

                    changeSet = "0000";
                    goto skipRead;
                }

                // I'm expecting the output from this command to look like:
                //
                //      Changeset User              Date       Comment
                //      --------- ----------------- ---------- -------------------------------------------------------------------------
                //      50892     Jeff Lill         1/18/2014  Vegomatic: Ported to VS2013.
                //
                // with the first column of the third line being the revision number.

                var lineNumber = 0;

                using (var reader = new StringReader(result.StandardOutput))
                {
                    string line;

                    while (true)
                    {
                        line = reader.ReadLine();
                        if (line == null)
                        {
                            Program.Error("[{0}] command did not return the changeset number.  Setting 0000 instead.", command);
                            changeSet = "0000";
                            break;
                        }

                        if (++lineNumber == 3)
                        {
                            changeSet = line.Split(' ')[0];

                            var changeSetNum = long.Parse(changeSet);

                            // Note that it is possible for changeset number to exceed 64K which is the
                            // maximum limit for a component of a .NET version.  This is unlikely to happen
                            // anytime soon for the LillTek TFS repository, but we'll break this up after
                            // we reach 10K, just to be safe.

                            changeSet = string.Format("{0}.{1:0###}", changeSetNum / 10000, changeSetNum % 10000);
                            break;
                        }
                    }
                }

            skipRead:

                // If a source file already exists check to see if it already defines the correct
                // changeset.  Don't generate a new file if it's already correct.  This will avoid
                // unnecessary project builds.

                if (File.Exists(sourcePath))
                {
                    if (File.ReadAllText(sourcePath).Contains(string.Format("internal const string Changeset = \"{0}\";", changeSet)))
                        return 0;
                }

                // Generate the source file.

                using (var writer = new StreamWriter(sourcePath, false, Encoding.ASCII))
                {
                    const string template =
@"//-----------------------------------------------------------------------------
// FILE:        {0}
// COPYRIGHT:   
// DESCRIPTION: Current TFS changeset number generated by VEGOMATIC.

namespace {2} 
{{
    {3} static partial class Build 
    {{
        /// <summary>
        /// The current TFS changeset number.
        /// </summary>
        internal const string Revision = ""{4}"";
    }}
}}
";
                    writer.Write(template, Path.GetFileName(sourcePath),
                                           Program.ToolName,
                                           nameSpace,
                                           generateInternal ? "internal" : "public",
                                           changeSet);
                }

                return 0;
            }
            catch (Exception e)
            {
                Program.Error("{0}: {1}", e.GetType().Name, e.Message);
                return -1;
            }
        }
    }
}
