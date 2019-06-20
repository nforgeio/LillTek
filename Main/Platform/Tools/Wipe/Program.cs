//-----------------------------------------------------------------------------
// FILE:        Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the File Wipe tool.

using System;
using System.IO;

namespace LillTek.Tools.Wipe
{
    /// <summary>
    /// Application entry point.
    /// </summary>
    public static class Program
    {
        private const string usage =
@"
Writes across the extent of the file(s) specified and then
deletes the file(s).

usage: wipe [-r] <file>

    <file>  the folder and name of the file to be wiped.  The folder
            is optional.  The current folder will be assumed if this
            is not present.  The file name may include the (*) and (?)
            wildcard characters.

    -r      indicates that the <file> pattern should be applied
            recursively to subfolders
";
        private const int       cbBlock = 4096;
        private static byte[]   zeros;
        private static byte[]   ones;

        /// <summary>
        /// Overwrites files with zeros and ones and then deletes them.
        /// </summary>
        /// <param name="args">The command line arguments</param>
        /// <returns>0 on success, 1 on and error.</returns>
        public static int Main(string[] args)
        {
            bool        recursive = false;
            string      path;
            string      folder;
            string      pattern;
            int         pos;
            string[]    files;
            bool        ok;

            if (args.Length == 0 || args.Length > 2)
            {
                Console.Error.Write(usage);
                return 1;
            }

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].ToLowerInvariant() == "-r")
                    recursive = true;
                else
                {
                    Console.Error.Write(usage);
                    return 1;
                }
            }

            try
            {
                path = args[args.Length - 1];
                pos  = path.LastIndexOfAny(new char[] { ':', '\\', '/' });
                if (pos == -1)
                {
                    folder  = Environment.CurrentDirectory;
                    pattern = path.Substring(pos + 1);
                }
                else
                {
                    folder  = Path.GetFullPath(path.Substring(0, pos));
                    pattern = path.Substring(pos + 1);
                }

                if (pattern == string.Empty)
                {
                    Console.Error.WriteLine("Invalid file pattern");
                    return -1;
                }

                zeros = new byte[cbBlock];
                ones  = new byte[cbBlock];
                for (int i = 0; i < cbBlock; i++)
                {
                    zeros[i] = 0x00;
                    ones[i] = 0xFF;
                }

                ok   = true;
                files = Directory.GetFiles(folder, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                foreach (string file in files)
                {
                    Console.Out.WriteLine("Wiping [{0}]", file);
                    if (!Wipe(file))
                        ok = false;
                }

                if (!ok)
                {
                    Console.Error.WriteLine("One or more files could not be wiped.");
                    return 1;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Wipes and deletes the specified file.
        /// </summary>
        /// <param name="path">The fully qualified path to the file.</param>
        /// <returns>True on success.</returns>
        private static bool Wipe(string path)
        {
            FileStream  fs = null;
            int         cBlocks;
            int         cRemain;

            try
            {
                fs = new FileStream(path, FileMode.Open);

                cBlocks = (int)(fs.Length / cbBlock);
                cRemain = (int)(fs.Length % cbBlock);

                // Overwrite with zeros

                fs.Position = 0;
                for (int i = 0; i < cBlocks; i++)
                    fs.Write(zeros, 0, cbBlock);

                fs.Write(zeros, 0, cRemain);
                fs.Flush();

                // Overwrite with ones

                fs.Position = 0;
                for (int i = 0; i < cBlocks; i++)
                    fs.Write(ones, 0, cbBlock);

                fs.Write(ones, 0, cRemain);
                fs.Flush();

                fs.Close();
                fs = null;

                // Delete the file and we're done

                File.Delete(path);
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("**** Wipe Failed: {0}", e.Message);
                return false;
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }
        }
    }
}
