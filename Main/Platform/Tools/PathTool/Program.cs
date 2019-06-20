//-----------------------------------------------------------------------------
// FILE:        Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Command line tool for managing the PATH environment variable.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Win32;

namespace LillTek.Tools.PathTool
{
    /// <summary>
    /// Command line tool for managing the PATH environment variable.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Manages adding and deleting items from the PATH environment variable.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        public static void Main(string[] args)
        {
            const string usage =
@"
Usage: 

    pathtool [-user | -system] [-dedup] [-create] -add <path>
    -----------------------------------------------
    Adds a path segment to the user or system path if it doesn't already
    exist.  Note that the path must specify an existing folder for the
    command to succeed.

    pathtool [-user | -system] [-dedup] -del <path>
    -----------------------------------------------
    Removes a path segment from the user or system path if it's present.
    
    -user   - is assumed if -system is not specified
    -dedup  - removes duplicate paths
    -create - creates the folder path if it doesn't exist

    Note: This tool requires admin privileges and it also
          does not expand environment variables.

";

            bool        userPath = true;
            bool        dedup    = false;
            bool        create   = false;
            string      command  = null;
            string      path     = null;

            if (args.Length < 2)
            {
                Console.WriteLine(usage);
                Environment.Exit(1);
                return;
            }

            path = args[args.Length - 1].Trim();

            for (int i = 0; i < args.Length - 1; i++)
            {
                switch (args[i])
                {
                    case "-add":

                        command = "add";
                        break;

                    case "-del":

                        command = "del";
                        break;

                    case "-dedup":

                        dedup = true;
                        break;

                    case "-create":

                        create = true;
                        break;

                    case "-user":

                        userPath = true;
                        break;

                    case "-system":

                        userPath = false;
                        break;
                }
            }

            if (command == null)
            {
                Console.WriteLine(usage);
                Environment.Exit(1);
                return;
            }

            string keyName;

            if (userPath)
            {
                keyName = @"HKEY_CURRENT_USER\Environment";
            }
            else
            {
                keyName = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
            }

            switch (command)
            {
                case "add":
                    {
                        if (!Directory.Exists(path))
                        {
                            if (create)
                            {
                                CreateFolderTree(Path.GetFullPath(path));
                            }
                            else
                            {
                                // Don't add to the path if the target directory doesn't exist.

                                Console.WriteLine("*** [{0}] does not exist.", path);
                                Environment.Exit(1);
                                return;
                            }
                        }

                        var value = Registry.GetValue(keyName, "Path", null) as string;

                        if (value == null)
                            value = string.Empty;

                        var sb     = new StringBuilder();
                        var paths  = value.Split(';');
                        var exists = false;

                        if (dedup)
                            paths = DeDup(paths);

                        for (int i = 0; i < paths.Length; i++)
                        {
                            paths[i] = paths[i].Trim();

                            if (sb.Length > 0)
                                sb.Append(';');

                            sb.Append(paths[i]);

                            if (string.Compare(paths[i], path, true) == 0)
                                exists = true;
                        }

                        if (!exists)
                        {
                            if (sb.Length > 0)
                                sb.Append(';');

                            sb.Append(path);
                        }

                        Registry.SetValue(keyName, "Path", sb.ToString(), RegistryValueKind.ExpandString);
                    }
                    break;

                case "del":
                    {
                        var value = Registry.GetValue(keyName, "Path", null) as string;

                        if (value == null)
                        {
                            // Variable doesn't exist.

                            Environment.Exit(0);
                            return;
                        }

                        var sb    = new StringBuilder();
                        var paths = value.Split(';');

                        if (dedup)
                            paths = DeDup(paths);

                        for (int i = 0; i < paths.Length; i++)
                        {
                            paths[i] = paths[i].Trim();

                            if (string.Compare(paths[i], path, true) != 0)
                            {
                                if (sb.Length > 0)
                                    sb.Append(';');

                                sb.Append(paths[i]);
                            }
                        }

                        Registry.SetValue(keyName, "Path", sb.ToString(), RegistryValueKind.ExpandString);
                    }
                    break;

                default:

                    Console.WriteLine(usage);
                    Environment.Exit(1);
                    return;
            }
        }

        /// <summary>
        /// Removes duplicate paths.
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        private static string[] DeDup(string[] paths)
        {
            var existing = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var output   = new List<string>();

            for (int i = 0; i < paths.Length; i++)
            {
                var path = paths[i].Trim();

                if (existing.Contains(path))
                    continue;

                existing.Add(path);
                output.Add(path);
            }

            return output.ToArray();
        }

        /// <summary>
        /// Ensures that all the directories in the path passed exist by creating
        /// any directories that are missing.
        /// </summary>
        /// <param name="path">The directory path.</param>
        public static void CreateFolderTree(string path)
        {
            char[]  pathSep = new char[] { '\\', '/' };
            string  prefix;
            int     pos, posEnd;

            path = Path.GetFullPath(path);

            if (path.StartsWith("\\\\"))
            {
                // Must be a UNC path.  Advance the position past the "\\share\folder" and
                // the next "\" or "/" if there is one.

                pos = 2;
                pos = path.IndexOfAny(pathSep, pos);
                if (pos == -1)
                    throw new ArgumentException(@"UNC path must have the form \\share\folder.");

                pos = path.IndexOfAny(pathSep, pos + 1);
                if (pos == -1)
                    pos = path.Length;
                else
                    pos++;

                if (pos == path.Length)
                    return;

                pos++;
            }
            else
            {
                pos = path.IndexOf(':');
                if (pos == -1)
                    throw new ArgumentException(string.Format("Invalid directory path [{0}].", path));

                pos += 2;   // Skip past the colon and the root "\" or "/"
            }

            posEnd = path.IndexOfAny(pathSep, pos + 1);
            while (true)
            {
                if (posEnd == -1)
                {
                    prefix = path.Substring(pos).Trim();
                    if (prefix == string.Empty)
                        break;

                    prefix = path.Substring(0).Trim();
                }
                else
                    prefix = path.Substring(0, posEnd).Trim();

                if (!Directory.Exists(prefix))
                    Directory.CreateDirectory(prefix);

                if (posEnd == -1)
                    break;

                pos = posEnd + 1;
                posEnd = path.IndexOfAny(pathSep, pos);
            }
        }
    }
}
