//-----------------------------------------------------------------------------
// FILE:        UnzipCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the UNZIP commands.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using LillTek.Common;
using LillTek.Compression.Zip;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements the UNZIP commands.
    /// </summary>
    public static class UnzipCommand
    {
        /// <summary>
        /// Executes the specified UNZIP command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {
            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic unzip [-o:<path>] [-r] [-fn] <pattern>

Unzips the specified files to the current directory.

    -o:<path>   specifies the folder where extracted files will
                be written.  Files will be written to the current
                directory if this option is not present.

    -r          indicates that the file pattern
                should be searched recursively.

    -fn         indicates that zip files will be extracted
                to a folder named by the zip archive file.

    <pattern>   specifies the wildcarded pattern for the
                files to be extracted.

";
            bool        recursive = false;
            bool        folderName = false;
            string      outFolder = Environment.CurrentDirectory;
            string      pattern;
            string[]    files;
            int         cExtracted;

            if (args.Length < 1)
            {
                Program.Error(usage);
                return 1;
            }

            for (int i = 0; i < args.Length - 1; i++)
            {
                string arg = args[i];

                if (arg.StartsWith("-o:"))
                {
                    outFolder = Path.GetFullPath(arg.Substring(3));
                }
                else if (arg == "-r")
                {
                    recursive = true;
                }
                else if (arg == "-fn")
                {
                    folderName = true;
                }
                else
                {
                    Program.Error(usage);
                    return 1;
                }
            }

            pattern = args[args.Length - 1];
            files   = Helper.GetFilesByPattern(pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

            cExtracted = 0;
            Program.Output("[{0}] files found.", files.Length);

            foreach (string file in files)
            {
                ZipFile     archive = null;
                string      outPath;

                try
                {
                    archive = new ZipFile(file);
                    outPath = Helper.AddTrailingSlash(outFolder);

                    if (folderName)
                        outPath += Helper.AddTrailingSlash(Helper.GetFileNameWithoutExtension(file));

                    foreach (ZipEntry entry in archive)
                    {
                        Stream          inStream = null;
                        EnhancedStream  outStream = null;
                        string          outFile;

                        outFile = outPath + entry.Name;
                        Helper.CreateFileTree(outFile);

                        Program.Output("Extract: {0}", entry.Name);

                        try
                        {
                            inStream  = archive.GetInputStream(entry);
                            outStream = new EnhancedFileStream(outFile, FileMode.Create, FileAccess.ReadWrite);

                            outStream.CopyFrom(inStream, -1);
                            cExtracted++;
                        }
                        finally
                        {
                            if (inStream != null)
                                inStream.Close();

                            if (outStream != null)
                                outStream.Close();

                            File.SetCreationTime(outFile, entry.DateTime);
                            File.SetLastWriteTime(outFile, entry.DateTime);
                        }
                    }
                }
                catch (Exception e)
                {
                    Program.Error("{0}: {1}", e.Message, file);
                }
                finally
                {
                    if (archive != null)
                        archive.Close();
                }
            }

            Program.Output("[{0}] files extracted from [{1}] zip archives.", cExtracted, files.Length);

            return 0;
        }
    }
}
