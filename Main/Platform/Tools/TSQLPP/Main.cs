//-----------------------------------------------------------------------------
// FILE:        Main.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the TSQL preprocessor.

// $todo(jeff.lill):
//
// This tool is a bit brain-dead right now.  I can imagine the preprocessor 
// generating error/exception handling code of some sort.

using System;
using System.IO;
using System.Text;
using System.Reflection;

using LillTek.Common;
using LillTek.Data.Install;

namespace LillTek.Tools.TSQLPP
{
    /// <summary>
    /// Implements the preprocessor
    /// See the application entry point method <see cref="TSQLPP.Main">Main</see>
    /// for a description of the command line parameters.
    /// </summary>
    public class TSQLPP
    {
        static void ThrowUsageException()
        {
            throw new Exception("usage: TSQLPP -in:<filespec> -out:<path> [-trans] [-sym:<files>] [-def:<file>]");
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <remarks>
        /// <see cref="OverviewDoc"/> for a detailed description of the operation
        /// and command line parameters for this tool.
        /// </remarks>
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                TSQLPreprocessor    processor = new TSQLPreprocessor();
                DirectoryInfo       inDir;
                string              inputDir, inputSpec;
                int                 pos;
                int                 count = 0;
                string              inPath = null;
                string              outPath = null;
                DirectoryInfo       outDir;
                FileInfo[]          files;

                args = CommandLine.ExpandFiles(args);

                Console.WriteLine();
                Console.WriteLine(string.Format("TSQLPP v{0}", Assembly.GetExecutingAssembly().GetName().Version));
                Console.WriteLine();

                // Process the command line arguments

                if (args.Length == 0)
                    ThrowUsageException();

                // Check for any invalid command line arguments

                foreach (string arg in args)
                {
                    if (!(arg.IndexOf("-in:") == 0 ||
                          arg.IndexOf("-out:") == 0 ||
                          arg.IndexOf("-sym:") == 0 ||
                          arg.IndexOf("-def:") == 0 ||
                          arg == "-trans"))

                        ThrowUsageException();
                }

                // Load symbols

                foreach (string arg in args)
                {
                    if (arg.IndexOf("-sym:") == 0)
                    {
                        foreach (string path in arg.Substring(5).Split(','))
                            processor.LoadSymbols(path);
                    }
                }

                // Load any macros from definition files, expanding any embedded symbols

                foreach (string arg in args)
                {
                    if (arg.IndexOf("-def:") == 0)
                    {
                        string defPath = arg.Substring(5);
                        string defContents;

                        using (var reader = new StreamReader(defPath))
                        {
                            defContents = reader.ReadToEnd();
                        }

                        using (var reader = new StringReader(processor.Process(defContents)))
                        {
                            processor.LoadMacros(reader, defPath);
                        }
                    }
                }

                // Process the remaining command line arguments

                foreach (string arg in args)
                {
                    if (arg.IndexOf("-in:") == 0)
                        inPath = Environment.ExpandEnvironmentVariables(arg.Substring(4).Trim());
                    else if (arg.IndexOf("-out:") == 0)
                    {
                        outPath = Environment.ExpandEnvironmentVariables(arg.Substring(5).Trim());
                        if (outPath == string.Empty || outPath.Length <= 3)
                            throw new Exception(string.Format("Invalid output directory [{0}].", outPath));
                    }
                    else if (arg == "-trans")
                        processor.Trans = true;
                }

                // Create the output directory (if necessary) and delete all files
                // found within it.

                outDir = Directory.CreateDirectory(outPath);
                files  = outDir.GetFiles("*.*");
                foreach (var fileInfo in files)
                {
                    if ((fileInfo.Attributes & (FileAttributes.Directory | FileAttributes.System)) != 0)
                        continue;   // Don't delete directories and system files

                    File.Delete(fileInfo.FullName);
                }

                // Process the input files

                pos       = inPath.LastIndexOf(Helper.PathSepChar);
                inputDir  = inPath.Substring(0, pos);
                inputSpec = inPath.Substring(pos + 1);

                if (!Directory.Exists(inputDir))
                    throw new Exception(string.Format("Input directory [{0}] does not exist.", inputDir));

                inDir = Directory.CreateDirectory(inputDir);
                files = inDir.GetFiles(inputSpec);
                foreach (var fileInfo in files)
                {
                    if ((fileInfo.Attributes & (FileAttributes.Directory | FileAttributes.System)) != 0)
                        continue;   // Don't process directories and system files

                    Console.WriteLine("    {0}", fileInfo.Name);
                    processor.Process(fileInfo.FullName, outDir.FullName + Helper.PathSepString + fileInfo.Name);
                    count++;
                }

                Console.WriteLine();
                Console.WriteLine("    ({0}) files processed", count);
            }
            catch (Exception e)
            {
                string cmdLine;

                cmdLine = "tsqlpp";
                foreach (string s in args)
                    cmdLine += " " + s;

                Console.WriteLine();
                Console.WriteLine("ERROR: {0}", e.Message);
                Console.WriteLine("CMD:   {0}", cmdLine);
                Environment.Exit(1);
            }

            Environment.Exit(0);
        }
    }
}
