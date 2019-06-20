//-----------------------------------------------------------------------------
// FILE:        FileCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements misc file utilities.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

using LillTek.Common;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements misc file utilities.
    /// </summary>
    public static class FileCommand
    {
        //---------------------------------------------------------------------
        // Private classes

        private sealed class FileInfo : IComparable
        {
            private string      FileName;
            private DateTime    ModifyTime;

            public FileInfo(string fileName, DateTime modifyTime)
            {
                this.FileName   = fileName;
                this.ModifyTime = modifyTime;
            }

            public int CompareTo(object other)
            {
                FileInfo info = (FileInfo)other;

                if (this.ModifyTime < info.ModifyTime)
                    return -1;
                else if (this.ModifyTime > info.ModifyTime)
                    return +1;
                else
                    return 0;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Executes the specified FILE command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {
            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic file prune <count> <file-pattern>

Prunes a set of files that match the specified pattern by deleting
the oldest files until the number of files remaining equals the
specified count.

    <count>             The desired number of files

    <file-pattern>      Specifies the file pattern using '?' and
                        '*' wildcards

-------------------------------------------------------------------------------
vegomatic file sync <src-pattern> <folder>

Copies the source file specified to the target folder if the
file doesn't exist in the target or if the source file is newer.

-------------------------------------------------------------------------------
vegomatic file rename [-r] -old:<string> -new:<string> <file-pattern>

Renames the specified set of files by replacing any occurences 
of an <old> string of characters with a new string.

    -r                  Indicates that files in subfolders should
                        be searched and renamed.

    -old:<chars>        Specifies original string to be replaced.  This
                        must include at least one character.

    -new:<chars>        Specifies the replacement string.  This may be
                        empty.

    <file-pattern>      Specifies the file pattern using '? and '*'
                        wildcards.

-------------------------------------------------------------------------------
vegomatic file delete [-r] <file-pattern0>...

Deletes files and optionally folders that match a wildcard file
pattern if the files exist.  Note that to avoid damaging the computer
with buggy scripts, this command will not delete all files within
the root folder of a drive or within the Program Files or Windows
System folders.

    -r                  Indicates the folders will be recusively
                        deleted.

    <file-pattern#>     Specifies the file/folder patterns using '?' and
                        '*' wildcards.

-------------------------------------------------------------------------------
vegomatic file createpath <path>

Verifies that the specified directory path exists, creating any
missing directories as required.

    <path>              Relative or absolute folder path

-------------------------------------------------------------------------------
vegomatic file versioncopy <assembly> <output-folder> <file-pattern0>...

Makes copies of one or more files, renaming the files to include a version 
number loaded from a .NET assembly file.  The command renames the file by 
replacing any ""#"" characters found in the original file name with the 
version string and then copies the file to the output folder.  The command
creates the output folder if it doesn't already exist.

This command is useful in post-build events to generate output files 
(typically setup MSI files) that include the build version.

-------------------------------------------------------------------------------
vegomatic file textcat <file1> <file2> [...<fileN> ] <output>

Concats the contents of two or more text files and writes them to a UTF-8
encoded output file.  Use this command instead of the DOS COPY command
when files may contain leading encoding bytes.

    <file1>         First input file path
    <file2>         Second input file path
    <fileN>         Any additional input file paths
    <output>        Output file path

Note: The source files may include wildcards.
";
            if (args.Length < 1)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "prune":

                    if (args.Length != 3)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return Prune(args[1], args[2]);

                case "sync":

                    if (args.Length != 3)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return Sync(args[1], args[2]);

                case "rename":

                    if (args.Length < 4)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return Rename(args);

                case "createpath":

                    if (args.Length != 2)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return CreatePath(args[1]);

                case "versioncopy":

                    if (args.Length < 4)
                    {

                        Program.Error(usage);
                        return 1;
                    }

                    return VersionCopy(args);

                case "delete":

                    if (args.Length < 1)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return Delete(args);

                case "textcat":

                    if (args.Length < 3)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    var inputFiles = new List<String>();

                    for (int i = 0; i < args.Length - 2; i++)
                    {
                        var path = args[i + 1];

                        if (path.IndexOfAny(new char[] { '*', '?' }) != -1)
                        {
                            foreach (var file in Helper.GetFilesByPattern(path, SearchOption.TopDirectoryOnly))
                                inputFiles.Add(file);
                        }
                        else
                            inputFiles.Add(path);
                    }

                    return TextCat(inputFiles.ToArray(), args[args.Length - 1]);

                default:

                    Program.Error(usage);
                    return 1;
            }
        }

        private static int Prune(string sCount, string pattern)
        {
            int         count;
            int         cDelete;
            string[]    files;
            FileInfo[]  info;

            if (!int.TryParse(sCount, out count) || count <= 0)
            {
                Program.Error("<count> argument must be an integer >= 0.", sCount);
                return 0;
            }

            try
            {
                files = Helper.GetFilesByPattern(pattern, SearchOption.TopDirectoryOnly);
                if (files.Length <= count)
                    return 0;

                info = new FileInfo[files.Length];
                for (int i = 0; i < files.Length; i++)
                    info[i] = new FileInfo(files[i], File.GetLastAccessTimeUtc(files[i]));

                Array.Sort(info);

                cDelete = files.Length - count;
                for (int i = 0; i < cDelete; i++)
                {
                    Program.Output("Deleting [{0}]", files[i]);
                    File.Delete(files[i]);
                }

                Program.Output("[{0}] files deleted.", cDelete);
            }
            catch (Exception e)
            {
                Program.Error("Error ({0}): {1}", e.GetType().Name, e.Message);
                return 1;
            }

            return 0;
        }

        private static int Sync(string pattern, string folder)
        {
            string[]    files;
            int         cCopied = 0;

            if (!folder.EndsWith("\\") && !folder.EndsWith("/"))
                folder += Helper.PathSepString;

            try
            {
                files = Helper.GetFilesByPattern(pattern, SearchOption.TopDirectoryOnly);
                foreach (string srcFile in files)
                {
                    string destFile = folder + Path.GetFileName(srcFile);

                    if (!File.Exists(destFile) ||
                        File.GetLastWriteTimeUtc(srcFile) > File.GetLastWriteTimeUtc(destFile))
                    {
                        Program.Output("Copying [{0}]", Path.GetFileName(srcFile));
                        if (File.Exists(destFile))
                            File.Delete(destFile);

                        File.Copy(srcFile, destFile);
                        cCopied++;
                    }
                }

                Program.Output("[{0}] files copied.", cCopied);
            }
            catch (Exception e)
            {
                Program.Error("Error ({0}): {1}", e.GetType().Name, e.Message);
                return 1;
            }

            return 0;
        }

        private static int Rename(string[] args)
        {
            bool        recursive = false;
            string      oldPat    = null;
            string      newPat    = null;
            string      pattern   = null;
            string[]    files;
            int         cRenamed;

            for (int i = 1; i < args.Length - 1; i++)
            {
                string arg = args[i];

                if (arg == "-r")
                    recursive = true;
                else if (arg.StartsWith("-old:"))
                {
                    oldPat = arg.Substring(5);
                }
                else if (arg.StartsWith("-new:"))
                {
                    newPat = arg.Substring(5);
                }
                else
                {
                    Program.Error("Invalid argument [{0}]", arg);
                    return 1;
                }
            }

            pattern = args[args.Length - 1];

            if (oldPat == null || oldPat.Length == 0)
            {
                Program.Error("-old:<chars> is missing or empty.");
                return 1;
            }

            if (newPat == null)
            {
                Program.Error("-new:<chars> is missing or empty.");
                return 1;
            }

            try
            {
                files    = Helper.GetFilesByPattern(pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                cRenamed = 0;

                foreach (string oldName in files)
                {
                    string newName = oldName.Replace(oldPat, newPat);

                    if (oldName != newName)
                    {
                        Program.Output("Renaming [{0}] --> [{1}]", oldName, newName);
                        File.Move(oldName, newName);

                        cRenamed++;
                    }
                }

                Program.Output("[{0}] files renamed.", cRenamed);
            }
            catch (Exception e)
            {
                Program.Error("Error ({0}): {1}", e.GetType().Name, e.Message);
                return 1;
            }

            return 0;
        }

        private static int CreatePath(string path)
        {
            try
            {
                Helper.CreateFolderTree(path);
            }
            catch (Exception e)
            {
                Program.Error("Error ({0}): {1}", e.GetType().Name, e.Message);
                return 1;
            }

            return 0;
        }

        private static int VersionCopy(string[] args)
        {
            List<string>    files   = new List<string>();
            int             cCopied = 0;
            string          outputFolder;
            Assembly        assembly;
            string          version;

            try
            {
                assembly     = Assembly.LoadFile(args[1]);
                version      = Helper.GetVersionString(assembly);
                outputFolder = args[2];

                Helper.CreateFolderTree(outputFolder);

                for (int i = 3; i < args.Length; i++)
                    foreach (var file in CommandLine.ExpandWildcards(args[i]))
                        files.Add(file);

                foreach (var file in files)
                {
                    string orgName;
                    string newName;

                    if (!file.Contains("#"))
                        continue;

                    orgName = Helper.GetFilesByPattern(file, SearchOption.TopDirectoryOnly)[0];
                    newName = Path.GetFileName(orgName.Replace("#", Helper.GetVersionString(assembly)));
                    newName = Path.Combine(outputFolder, newName);

                    Program.Output("Copy [{0}] --> [{1}]", orgName, newName);

                    if (File.Exists(newName))
                        File.Delete(newName);

                    Helper.CopyFile(file, newName, false);
                    cCopied++;
                }

                Program.Output("[{0}] files copied.", cCopied);
            }
            catch (Exception e)
            {
                Program.Error("Error ({0}): {1}", e.GetType().Name, e.Message);
                return 1;
            }

            return 0;
        }

        private static int Delete(string[] args)
        {
            bool    recursive = false;
            int     pos = 1;

            if (args[pos] == "-r")
            {
                recursive = true;
                pos++;
            }

            if (pos >= args.Length)
            {
                Program.Error("At least one file must be specified.");
                return 1;
            }

            try
            {
                for (int i = pos; i < args.Length; i++)
                    Helper.DeleteFile(args[i], recursive);
            }
            catch (Exception e)
            {
                Program.Error("Error ({0}): {1}", e.GetType().Name, e.Message);
                return 1;
            }

            return 0;
        }

        private static int TextCat(string[] inputFiles, string outputFile)
        {
            try
            {
                using (var writer = new StreamWriter(outputFile, false, Encoding.UTF8))
                {
                    foreach (var file in inputFiles)
                    {
                        using (var reader = new StreamReader(file))
                        {
                            for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
                                writer.WriteLine(line);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Program.Error("Error ({0}): {1}", e.GetType().Name, e.Message);
                return 1;
            }

            return 0;
        }
    }
}
