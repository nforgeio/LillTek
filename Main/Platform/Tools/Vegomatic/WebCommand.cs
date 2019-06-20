//-----------------------------------------------------------------------------
// FILE:        WebCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the WEB commands.

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
    /// Implements the WEB commands.
    /// </summary>
    public static class WebCommand
    {
        /// <summary>
        /// Executes the specified WEB command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {
            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic web mungecss <site-root> <css-file> <markup0>...

Munges a website's CSS and HTML markup files by renaming CSS classes to pseudo
random values and then updating references to the classes in the specified
HTML markup files.

    <site-root>     - Path to the site's root folder
    <css-file>      - Relative path to the site's CSS file
    <markup0>...    - Relative paths to the HTML markup files, possibly
                      including wildcards

This command is pretty simplistic in its implementation.  Essentially, it
looks for CSS class names that are prefixed by ""cssmunge-"" and replaces
them with random values.  CSS classes without this prefix are left alone.
The command performs a simple text search and replace and does not attempt
to actually parse the HTML or CSS.

The command also removes comments from the CSS file.

Example: 

vegomatic web mungecss c:\MySite css\main.css *.aspx *.htm Controls\*.ascx

";
            if (args.Length < 2)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "mungecss":

                    if (args.Length < 3)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return MungeCss(args);

                default:

                    Program.Error(usage);
                    return 1;
            }
        }

        /// <summary>
        /// Munges a web site's CSS class names.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success.</returns>
        private static int MungeCss(string[] args)
        {
            var         classMap = new Dictionary<string, int>();
            var         idMap    = new Dictionary<int, bool>();
            Random      rand     = new Random((int)DateTime.Now.Ticks);
            string      rootPath = args[1];
            string      cssPath  = Path.Combine(rootPath, args[2]);

            try
            {
                // Load the CSS file and delete all comments: /* .... */

                var cssText = File.ReadAllText(cssPath, Encoding.UTF8);
                var regex   = new Regex(@"/\*.*?\*/", RegexOptions.Singleline);

                cssText = regex.Replace(cssText, string.Empty);

                // Scan the CSS for class definitions beginning with "cssmunge-" and replace them
                // with randomly assigned numbers.

                regex = new Regex(@"cssmunge-[a-z,A-Z,0-9,-]*\s");
                cssText = regex.Replace(cssText,
                    match =>
                    {
                        string      className  = match.Value;
                        char        whitespace = className[className.Length - 1];
                        int         classID;

                        if (Char.IsWhiteSpace(whitespace))
                            className = className.Substring(0, className.Length - 1);

                        if (!classMap.TryGetValue(className, out classID))
                        {
                            // Loop until we get an ID that's not already being used.

                            while (true)
                            {
                                classID = rand.Next();
                                if (!idMap.ContainsKey(classID))
                                    break;
                            }

                            classMap.Add(className, classID);
                            idMap.Add(classID, true);
                        }

                        return "_" + classID.ToString() + (Char.IsWhiteSpace(whitespace) ? whitespace.ToString() : string.Empty);
                    });

                // Output the munged CSS file.

                File.WriteAllText(cssPath, cssText, Encoding.UTF8);

                // Now process all of the matching HTML markup files.

                for (int i = 3; i < args.Length; i++)
                {
                    foreach (var path in Helper.GetFilesByPattern(Path.Combine(rootPath, args[i]), SearchOption.TopDirectoryOnly))
                        MungeHtml(classMap, path);
                }
            }
            catch (Exception e)
            {
                Program.Error(e.Message);
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Munges the CSS class names in a HTML file.
        /// </summary>
        /// <param name="classMap">The class name map.</param>
        /// <param name="path">Path to the HTML file.</param>
        private static void MungeHtml(Dictionary<string, int> classMap, string path)
        {
            Regex       regex = new Regex(@"cssmunge-[a-z,A-Z,0-9,-]*(\s|""|')", RegexOptions.Compiled);
            string      htmlText;

            htmlText = File.ReadAllText(path, Encoding.UTF8);

            htmlText = regex.Replace(htmlText,
                match =>
                {
                    // The last character of the value will be a whitespace or quote.  Everything
                    // else is the class name.

                    var className = match.Value;
                    int classID;

                    className = className.Substring(0, className.Length - 1);

                    if (classMap.TryGetValue(className, out classID))
                        return "_" + classID.ToString() + match.Value[match.Value.Length - 1];
                    else
                        return match.Value;
                });

            File.WriteAllText(path, htmlText, Encoding.UTF8);
        }
    }
}
