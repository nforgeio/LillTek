//-----------------------------------------------------------------------------
// FILE:        SourceCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the SOURCE commands.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using LillTek.Common;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements the SOURCE commands.
    /// </summary>
    public static class SourceCommand
    {
        /// <summary>
        /// Executes the specified SOURCE command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {

            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic source pragma <pragma body> <folder> <file-pattern>

Scans the folder <path> specified (and any subfolders) for source code
files that match the specified pattern and then examines the file for the 
presence of a C# pragma statement with the specified body, adding the pragma
to the file if it doesn't exist.

This is useful for adding pragmas that disable warnings for auto
generated source files.

Example:

    vegomatic source pragma ""warning disable 1591"" C:\Source Reference.cs

will scan for files named Reference.cs in C:\Source or below to see if they
include the following pragma, adding it if it is not present.

    #pragma warning disable 1591
";
            if (args.Length < 2)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "pragma":

                    if (args.Length != 4)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return Pragma(args[1], args[2], args[3]);

                default:

                    Program.Error(usage);
                    return 1;
            }
        }

        private static int Pragma(string pragmaBody, string folder, string filePattern)
        {
            string  pragma    = "#pragma " + pragmaBody.ToLower();
            int     cModified = 0;

            foreach (var path in Directory.GetFiles(folder, filePattern, SearchOption.AllDirectories))
            {
                bool        foundPragma = false;
                string[]    lines;

                Program.Output("Scanning: {0}", path);

                lines = File.ReadAllLines(path, Encoding.UTF8);

                foreach (var line in lines)
                {
                    if (line.Trim().ToLower().StartsWith(pragma))
                    {
                        foundPragma = true;
                        break;
                    }
                }

                if (!foundPragma)
                {
                    Program.Output("*** Adding pragma");
                    cModified++;

                    // Add the pragma at the top of the source file.

                    using (var writer = new StreamWriter(path, false, Encoding.UTF8))
                    {

                        writer.WriteLine(pragma);
                        writer.WriteLine();

                        foreach (var line in lines)
                            writer.WriteLine(line);
                    }
                }
            }

            Program.Output(string.Empty);
            Program.Output("[{0}] files modified", cModified);
            Program.Output(string.Empty);

            return 0;
        }
    }
}
