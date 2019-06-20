//-----------------------------------------------------------------------------
// FILE:        IisCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the IIS commands.

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
    /// Implements the IIS commands.
    /// </summary>
    public static class IisCommand
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
vegomatic iis siteadd <name> <rootpath> <binding>

Adds a named website to IIS, replacing any exisiting site with the same name.

    name        - Name of the website
    rootpath    - Physical path to the site root directory
    bindings    - Host bindings separated by commas (e.g. http://mysite.com:80

-------------------------------------------------------------------------------
vegomatic iis sitedelete <name> 

Deletes a named website from IIS, if the site exists.

    name        - Name of the website

";
            if (args.Length < 2)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "siteadd":

                    if (args.Length != 4)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return SiteAdd(args[1], args[2], args[3]);

                case "sitedelete":

                    if (args.Length != 2)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return SiteDelete(args[1]);

                default:

                    Program.Error(usage);
                    return 1;
            }
        }

        /// <summary>
        /// Returns the fully qualified path the the IIS APPCMD.EXE file.
        /// </summary>
        /// <returns>The command path.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
        private static string GetAppCmdPath()
        {
            var winPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var appPath = Path.Combine(winPath, @"WOW64\inetsrv\appcmd.exe");

            if (File.Exists(appPath))
                return appPath;

            appPath = Path.Combine(winPath, @"System32\inetsrv\appcmd.exe");

            if (File.Exists(appPath))
                return appPath;

            throw new FileNotFoundException("Unable to locate the IIS APPCMD.EXE utility.  Verify that IIS is installed.", "APPCMD.EXE");
        }

        /// <summary>
        /// Adds or replaces a named website.
        /// </summary>
        /// <param name="siteName">The website name.</param>
        /// <param name="rootPath">The physical path the the site's root folder.</param>
        /// <param name="bindings">The site host bindings seprated by commas.</param>
        /// <returns>Zero on success.</returns>
        private static int SiteAdd(string siteName, string rootPath, string bindings)
        {
            try
            {
                var appCmdPath = GetAppCmdPath();

                if (SiteDelete(siteName) != 0)
                    return 1;

                var args    = string.Format("add site /name:{0} /physicalPath:{1} /bindings:{2}", siteName, rootPath, bindings);
                var command = string.Format("{0} {1}", appCmdPath, args);
                var result  = Helper.ExecuteCaptureStreams(appCmdPath, args, TimeSpan.FromSeconds(10));

                if (result.ExitCode != 0)
                {
                    Program.Error("[{0}] command failed with error:\r\n{1}\r\n", command, result.StandardOutput);
                    return 1;
                }

                Program.Output("Site [{0}] added", siteName);

                return 0;
            }
            catch (Exception e)
            {
                Program.Error(e.Message);
                return 1;
            }
        }

        /// <summary>
        /// Delete a named website if one exists.
        /// </summary>
        /// <param name="siteName">The website name.</param>
        /// <returns>Zero on success.</returns>
        private static int SiteDelete(string siteName)
        {
            try
            {
                var appCmdPath = GetAppCmdPath();
                var siteID     = -1;

                // Obtain the site ID if the site exists.

                var args    = "list site";
                var command = string.Format("{0} {1}", appCmdPath, args);
                var result  = Helper.ExecuteCaptureStreams(appCmdPath, args, TimeSpan.FromSeconds(10));

                if (result.ExitCode != 0)
                {
                    Program.Error("[{0}] command failed with error:\r\n{1}\r\n", command, result.StandardOutput);
                    return 1;
                }

                using (var reader = new StringReader(result.StandardOutput))
                {
                    siteName = siteName.ToLower();

                    for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
                    {
                        line = line.ToLower();
                        if (line.StartsWith(string.Format("site \"{0}\"", siteName)))
                        {
                            int p = line.IndexOf("(id:");
                            int pEnd;

                            if (p == -1)
                                continue;

                            p += 4;
                            pEnd = line.IndexOf(',', p);

                            if (pEnd == -1)
                                continue;

                            siteID = int.Parse(line.Substring(p, pEnd - p));
                            break;
                        }
                    }
                }

                // Remove the site if one exists.

                if (siteID != -1)
                {
                    args    = string.Format("delete site {0}", siteName);
                    command = string.Format("{0} {1}", appCmdPath, args);
                    result  = Helper.ExecuteCaptureStreams(appCmdPath, args, TimeSpan.FromSeconds(10));

                    if (result.ExitCode != 0)
                    {
                        Program.Error("[{0}] command failed with error:\r\n{1}\r\n", command, result.StandardOutput);
                        return 1;
                    }

                    Program.Output("Site [{0}] removed", siteName);
                }
                else
                    Program.Output("Site [{0}] not found", siteName);

                return 0;
            }
            catch (Exception e)
            {
                Program.Error(e.Message);
                return 1;
            }
        }
    }
}
