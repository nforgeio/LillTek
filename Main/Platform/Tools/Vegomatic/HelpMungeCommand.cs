//-----------------------------------------------------------------------------
// FILE:        HelpMungeCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the HELPMUNGE commands.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Compression.Zip;
using LillTek.Datacenter;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements the HELPMUNGE commands.
    /// </summary>
    public static class HelpMungeCommand
    {
        /// <summary>
        /// Executes the specified PACK command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {

            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic helpmunge <folder> <google-token>

Munges the HTML help files in the specified folder and subfolders
by adding the Google Analytics scripts.

    folder          - File system path to the root folder holding
                      the HTM files.

    google-token    - The Google Analytics token.

";
            string[] files;
            string script;

            if (args.Length != 2)
            {
                Program.Error(usage);
                return 1;
            }

            // I'm going to insert the Google Analytics script just above the
            // </body> element in the HTM files.

            script = string.Format(
@"
<script src=""http://www.google-analytics.com/urchin.js"" type=""text/javascript""></script>
<script type=""text/javascript"">_uacct=""{0}""; urchinTracker();</script>
</body>
", args[1]);

            files = Helper.GetFilesByPattern(Helper.AddTrailingSlash(args[0]) + "*.htm", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                string text;

                Program.Output("Processing: {0}", file);
                using (StreamReader input = new StreamReader(file))
                {
                    text = input.ReadToEnd();
                }

                text = text.Replace("</body>", script);
                Helper.WriteToFile(file, text);
            }

            Program.Output("");
            Program.Output("[{0}] files processed.", files.Length);

            return 0;
        }
    }
}
