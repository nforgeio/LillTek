//-----------------------------------------------------------------------------
// FILE:        PackCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the PACK commands.

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

// $todo(jeff.lill): 
//
// Need to add the ability to automatically add dependant 
// assemblies to the package.

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements the PACK commands.
    /// </summary>
    public static class PackCommand
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
vegomatic pack [-r] [-out:<path>] <appref> <path-0> <path-1>... <path-N>

Creates an application package from a set of assembly and data files.

    -r          Indicates that subfolders are to be searched
                recursively for files to be added to the 
                archive.

    -out:path   Specifies where the generated packaged file is
                to be written.  If this specifies a folder then
                the package will be named using the standard
                package file naming convention in that folder.
                If this option is not present, then the package
                will be saved in the current folder using the
                standard name.

    <appref>    The application reference to be used for this package.
                See the note below describing how the version number
                can be loaded from an assembly.

    <path-#>    Path of the files or directories to be added to
                the archive.  File specifications may include
                wildcards.

AppRef Format
-------------

Application references are URIs formatted as:

    appref://Root/Segment0/Segment1/../SegmentN.zip?version=1.2.3.4

where the Root and Segment#s uniquely identify the application
and the version query parameter specifies the version number.
Note that vegomatic supports a specialized notation for apprefs
that indicate that the version should be loaded from an assembly's
version number rather than being specified directly on the command
line.  This can make it easy to automate the creation of application
packages during the build process.

To accomplish this, set the version to an ""@"" followed by the
path to the assembly file.  Here's an example:

    vegomatic pack appref://myapps/app01.zip?version=@myassembly.dll

In this case, vegomatic will load myassembly.dll from the current folder
and use the assembly's version number when creating the package.

Package.ini File
----------------

One of the files that must be included in all applications is the
PACKAGE.INI file.  This is a text file formatted using the standard
LillTek Platform config conventions that describes the metadata
to be associated with the package.  One required setting is the
APPREF setting which should be set to the same appref value
specified on the command line.  This can be accomplished via
the $(appref) macro as in:

    AppRef = $(appref)

Vegomatic will replace this macro with the appref specified on
the command line before adding the PACKAGE.INI file to the
package.
";
            AppPackage      package   = null;
            bool            recursive = false;
            string          outPath   = null;
            string          sAppRef;
            AppRef          appRef;
            int             argPos;
            int             cAdded;
            bool            packageIni;

            if (args.Length < 2)
            {
                Program.Error(usage);
                return 1;
            }

            for (argPos = 0; argPos < args.Length; argPos++)
            {
                string arg = args[argPos];

                if (!arg.StartsWith("-"))
                    break;
                else if (arg == "-r")
                    recursive = true;
                else if (arg.StartsWith("-out:"))
                    outPath = arg.Substring(5);
                else
                {
                    Program.Error(usage);
                    return 1;
                }
            }

            // Parse the application reference, reading the version number
            // from the referenced assembly file if requested.

            // $todo(jeff.lill): 
            //
            // The version parsing code is really simplistic.
            // It assumes that "version" is the only possible
            // query parameter or than it appears first in
            // the appref URI.

            string      assemblyPath;
            string      appName;
            int         pos;

            sAppRef = args[argPos++];
            pos = sAppRef.ToLowerInvariant().IndexOf("?version=@");

            if (pos == -1)
                appRef = AppRef.Parse(sAppRef);
            else
            {
                appName      = sAppRef.Substring(0, pos);
                assemblyPath = sAppRef.Substring(pos + 10);
                appRef       = AppRef.Parse(appName + "?version=" + Helper.GetVersion(Assembly.ReflectionOnlyLoadFrom(assemblyPath)).ToString());
            }

            if (outPath == null)
                outPath = appRef.FileName;
            else if (Directory.Exists(outPath))
                outPath = Helper.AddTrailingSlash(outPath) + appRef.FileName;

            if (argPos == args.Length)
            {
                Program.Error(usage);
                return 1;
            }

            Helper.CreateFileTree(outPath);
            if (File.Exists(outPath))
                File.Delete(outPath);

            package    = AppPackage.Create(outPath, appRef, null);
            cAdded     = 0;
            packageIni = false;

            try
            {
                for (; argPos < args.Length; argPos++)
                {
                    string      path = args[argPos];
                    bool        hasWildcards;
                    bool        isFolder;
                    string      basePath;

                    hasWildcards = path.IndexOfAny(Helper.FileWildcards) != -1;
                    if (!hasWildcards)
                        isFolder = (File.GetAttributes(path) & FileAttributes.Directory) != 0;
                    else
                        isFolder = false;

                    basePath = Path.GetFullPath(path.Replace('?', 'x').Replace('*', 'x'));
                    pos = basePath.LastIndexOf(Helper.PathSepChar);
                    if (pos == -1)
                        throw new Exception(string.Format("Invalid file path [{0}].", path));

                    basePath = basePath.Substring(0, pos);

                    if (isFolder)
                    {
                        Program.Output("Adding: {0}", path);
                        foreach (string file in Helper.GetFilesByPattern(path + Helper.PathSepString + "*.*", SearchOption.AllDirectories))
                        {
                            if (String.Compare(outPath, file, true) == 0)
                                continue;   // Ignore the zip archive being created

                            Program.Output("Adding: {0}", file);
                            packageIni = AddFile(package, file, basePath) || packageIni;
                            cAdded++;
                        }
                    }
                    else
                    {
                        foreach (string file in Helper.GetFilesByPattern(path, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                        {
                            if (String.Compare(outPath, file, true) == 0)
                                continue;   // Ignore the zip archive being created

                            Program.Output("Adding: {0}", file);
                            packageIni = AddFile(package, file, basePath) || packageIni;
                            cAdded++;
                        }
                    }
                }

                package.Close();

                if (!packageIni)
                    throw new Exception("PACKAGE.INI file with the package metadata is missing.");
            }
            catch (Exception e)
            {
                Program.Error("{0}: {1}", e.GetType().Name, e.Message);
            }
            finally
            {
                if (package != null)
                    package.Close();
            }

            Program.Output("");
            Program.Output("[{0}] files added.", cAdded);

            return 0;
        }

        /// <summary>
        /// HAdds the a file to the package, handling any special processing necessary when 
        /// adding the PACKAGE.INI file.
        /// </summary>
        /// <param name="package">The application package.</param>
        /// <param name="path">Path to he file.</param>
        /// <param name="basePath">Base path.</param>
        /// <returns><c>true</c> if the file processed was the PACKAGE.INI file.</returns>
        private static bool AddFile(AppPackage package, string path, string basePath)
        {
            string file;

            if (path.ToLowerInvariant().StartsWith(basePath.ToLowerInvariant() + Helper.PathSepString))
                file = path.Substring(basePath.Length + 1);
            else
                file = path;

            if (String.Compare(file, "package.ini", true) != 0)
            {
                package.AddFile(path, basePath);
                return false;
            }
            else
            {
                // Handle special processing of the PACKAGE.INI file.

                StreamReader    reader = new StreamReader(path, Encoding.UTF8);
                string          settings;
                MacroProcessor  processor;

                try
                {
                    processor = new MacroProcessor();
                    processor.Add("appref", package.AppRef.ToString());

                    settings = reader.ReadToEnd();
                    settings = processor.Expand(settings);

                    package.AddFile("Package.ini", Helper.ToUTF8(settings));
                }
                finally
                {
                    reader.Close();
                }

                return true;
            }
        }
    }
}
