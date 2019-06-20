//-----------------------------------------------------------------------------
// FILE:        ZipCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the ZIP commands.

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
    /// Implements the ZIP commands.
    /// </summary>
    public static class ZipCommand
    {
        /// <summary>
        /// Executes the specified ZIP command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {
            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic zip [-r] [-h] <zipfile> <path-0> <path-1>... <path-N>

Creates a zip archive and copies files to it.

    -r          indicates that subfolders are to be searched
                recursively for files to be added to the 
                archive.

    -h          include hidden files and folders

    <zipfile>   path of the ZIP archive to be created.  Any
                existing file will be deleted.

    <path-#>    path of the files or directories to be added to
                the archive.  File specifications may include
                wildcards.

";
            bool recursive = false;
            bool hidden = false;
            string zipPath = null;
            ZipOutputStream zipStream = null;
            int argPos;
            int cAdded;

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
                else if (arg == "-h")
                    hidden = true;
                else
                {
                    Program.Error(usage);
                    return 1;
                }
            }

            zipPath = Path.GetFullPath(args[argPos++]);
            if (Path.GetExtension(zipPath) == string.Empty)
                zipPath += ".zip";

            if (argPos == args.Length)
            {
                Program.Error(usage);
                return 1;
            }

            Helper.CreateFileTree(zipPath);
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            zipStream = new ZipOutputStream(File.Create(zipPath));
            cAdded = 0;

            try
            {
                zipStream.SetLevel(9);

                for (; argPos < args.Length; argPos++)
                {
                    string  path = args[argPos];
                    bool    hasWildcards;
                    bool    isFolder;
                    string  basePath;
                    int     pos;

                    hasWildcards = path.IndexOfAny(Helper.FileWildcards) != -1;
                    if (!hasWildcards)
                        isFolder = (File.GetAttributes(path) & FileAttributes.Directory) != 0;
                    else
                        isFolder = false;

                    basePath = Path.GetFullPath(path.Replace('?', 'x').Replace('*', 'x'));
                    pos      = basePath.LastIndexOf(Helper.PathSepChar);
                    if (pos == -1)
                        throw new Exception(string.Format("Invalid file path [{0}].", path));

                    basePath = basePath.Substring(0, pos);

                    if (isFolder)
                    {
                        if (!hidden && Helper.IsFileHidden(path))
                            continue;

                        Program.Output("Adding: {0}", path);
                        foreach (string file in Helper.GetFilesByPattern(path + Helper.PathSepString + "*.*", SearchOption.AllDirectories))
                        {
                            if (String.Compare(zipPath, file, true) == 0)
                                continue;   // Ignore the zip archive being created

                            if (!hidden && Helper.IsFileHidden(file))
                                continue;

                            Program.Output("Adding: {0}", file);
                            AddFile(zipStream, file, basePath);
                            cAdded++;
                        }
                    }
                    else
                    {
                        foreach (string file in Helper.GetFilesByPattern(path, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                        {
                            if (String.Compare(zipPath, file, true) == 0)
                                continue;   // Ignore the zip archive being created

                            if (!hidden && Helper.IsFileHidden(file))
                                continue;

                            Program.Output("Adding: {0}", file);
                            AddFile(zipStream, file, basePath);
                            cAdded++;
                        }
                    }
                }

                zipStream.Finish();
                zipStream.Close();
            }
            catch (Exception e)
            {
                Program.Error("{0}: {1}", e.GetType().Name, e.Message);
            }
            finally
            {
                zipStream.Dispose();
            }

            Program.Output("");
            Program.Output("[{0}] files added.", cAdded);

            return 0;
        }

        private static void AddFile(ZipOutputStream zipStream, string path, string basePath)
        {
            byte[]      buf = new byte[4096];
            ZipEntry    entry;
            int         cbRead;

            entry = new ZipEntry(path.Substring(basePath.Length));
            entry.DateTime = File.GetLastWriteTime(path);
            zipStream.PutNextEntry(entry);

            using (FileStream fs = File.OpenRead(path))
            {
                do
                {
                    cbRead = fs.Read(buf, 0, buf.Length);
                    zipStream.Write(buf, 0, cbRead);

                } while (cbRead > 0);
            }
        }
    }
}
