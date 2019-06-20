//-----------------------------------------------------------------------------
// FILE:        SvnCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the SVN commands.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

using LillTek.Common;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements the SVN commands.
    /// </summary>
    public static class SvnCommand
    {
        /// <summary>
        /// Executes the specified SVN command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {
            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic svn logvars <input> <output>

Scans a file containing the output of an SVN log command and 
produces an output batch file that sets environment variables to
a couple values that are useful for including in automated source
control email notices.

The variables generated are:

    SVNUSER:        The name of the user who made the commit.
    SVNCOMMENT:     A summary of the commit comment.
    SVNFILECOUNT:   Number of files changed.

-------------------------------------------------------------------------------
vegomatic svn build-rev <repo-path> <src-path> <namespace>

Generates a C# source file that defines a constant with the
current Subversion revision number for a repository.  This is
used to automatically include a revision in a build.

    repo-path:      File system path to the SVN repository
    src-path:       File system path to the output C# source file
    namespace:      C# namespace to be used when generating the source

";
            if (args.Length < 1)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "logvars":

                    if (args.Length != 3)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return LogVars(args[1], args[2]);

                case "build-rev":

                    if (args.Length != 4)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return BuildRev(args[1], args[2], args[3]);

                default:

                    Program.Error(usage);
                    return 1;
            }
        }

        private static int LogVars(string inputPath, string outputPath)
        {
            try
            {
                string      svnUser    = string.Empty;
                string      svnComment = string.Empty;
                int         cFiles     = 0;
                string      line;
                int         p1, p2;

                using (var reader = new StreamReader(inputPath))
                {
                    // Skip over lines until we get a line starting with several dashes.
                    // This marks the beginning of the dump.

                    do
                    {
                        line = reader.ReadLine();
                        if (line == null)
                            throw new FormatException("SVN log file missing first line of dashes.");

                    } while (!line.StartsWith("------------------"));


                    // The next line contains commit information including the user
                    // name between the first and second '|' characters.

                    line = reader.ReadLine();
                    if (line == null)
                        throw new FormatException("SVN log file missing commit information line.");

                    p1 = line.IndexOf('|');
                    if (p1 == -1)
                        throw new FormatException("SVN log file commit information line is not valid.");

                    p2 = line.IndexOf('|', p1 + 1);
                    if (p2 == -1)
                        throw new FormatException("SVN log file commit information line is not valid.");

                    svnUser = line.Substring(p1 + 1, p2 - p1 - 1).Trim();

                    // The next set of lines up to the first empty line describe the files
                    // changed by the commit.  Count these.

                    while (true)
                    {
                        line = reader.ReadLine();
                        if (line == null)
                            throw new FormatException("SVN log file missing first line of dashes.");

                        line = line.Trim();
                        if (string.IsNullOrWhiteSpace(line))
                            break;

                        if (!line.StartsWith("Changed paths:"))
                            cFiles++;
                    }

                    // The next line should hold the comment.  Limit its size to 128 characters and
                    // strip out any double or single quotes.

                    line = reader.ReadLine();
                    if (line == null)
                        line = string.Empty;

                    line = line.Replace("'", string.Empty);
                    line = line.Replace("\"", string.Empty);

                    if (line.Length > 128)
                        line = line.Substring(0, 125) + "...";

                    svnComment = line;
                }

                // Write the batch file

                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                using (var writer = new StreamWriter(outputPath, false, Encoding.ASCII))
                {
                    writer.WriteLine("SET SVNUSER={0}", svnUser);
                    writer.WriteLine("SET SVNCOMMENT={0}", svnComment);
                    writer.WriteLine("SET SVNFILECOUNT={0}", cFiles);
                }
            }
            catch (Exception e)
            {
                Program.Error(e.Message);
                return 1;
            }

            return 0;
        }

        private static int BuildRev(string repoPath, string srcPath, string nameSpace)
        {
            try
            {
                repoPath = Environment.ExpandEnvironmentVariables(repoPath);
                srcPath  = Environment.ExpandEnvironmentVariables(srcPath);

                // Get the current SVN revision number.

                var     args    = string.Format("info \"{0}\"", repoPath);
                var     command = string.Format("svn.exe {0}", args);
                var     result  = Helper.ExecuteCaptureStreams("svn.exe", args, TimeSpan.FromSeconds(10));
                string  revision;

                if (result.ExitCode != 0)
                {
                    Program.Error(command);
                    Program.Error(string.Empty);
                    Program.Error(result.StandardError);
                    return 1;
                }

                using (var reader = new StringReader(result.StandardOutput))
                {
                    string line;

                    while (true)
                    {
                        line = reader.ReadLine();
                        if (line == null)
                        {
                            Program.Error("[{0}] command did not return the revision number.", command);
                            return 1;
                        }

                        if (line.StartsWith("Revision:"))
                        {
                            revision = line.Substring("Revision:".Length).Trim();
                            break;
                        }
                    }
                }

                // If a source file already exists check to see if it already defines the correct
                // revision.  Don't generate a new file if it's already correct.  This will avoid
                // unnecessary project builds.

                if (File.Exists(srcPath))
                {
                    if (File.ReadAllText(srcPath).Contains(string.Format("public const string SvnRevision = \"{0}\";", revision)))
                        return 0;
                }

                // Generate the source file.

                using (var writer = new StreamWriter(srcPath, false, Encoding.ASCII))
                {
                    const string template =
@"//-----------------------------------------------------------------------------
// FILE:        {0}
// CONTRIBUTOR: {1}
// COPYRIGHT:   
// DESCRIPTION: Automated generated source defining the Subversion revision number.

namespace {2} {{

    public static partial class Build {{

        /// <summary>
        /// The current Subversion revision number.
        /// </summary>
        public const string SvnRevision = ""{3}"";
    }}
}}
";
                    writer.Write(template, Path.GetFileName(srcPath),
                                          Program.ToolName,
                                          nameSpace,
                                          revision);
                }
            }
            catch (Exception e)
            {
                Program.Error(e.Message);
                return 1;
            }

            return 0;
        }
    }
}
