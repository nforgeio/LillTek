//-----------------------------------------------------------------------------
// FILE:        Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Command line tool for adding a timestamp to a file's name.

using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace LillTek.Tools.Timestamp {

    /// <summary>
    /// Application entry point.
    /// </summary>
    public static class Program {

        /// <summary>
        /// Renames the file passed as the argument by appending the current time and date.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        public static int Main(string[] args) {

            if (args.Length == 0 || args[0].Trim().Length == 0) {

                Console.Error.WriteLine("Usage: timestamp <file>");
                return 1;
            }

            try {

                int         pos;
                string      newName;
                string      timestamp;

                timestamp = DateTime.UtcNow.ToString("s").Replace(':','-') + "-utc";

                pos = args[0].LastIndexOf('.');
                if (pos == -1)
                    newName = args[0] + "-" + timestamp;
                else
                    newName = args[0].Substring(0,pos) + "-" + timestamp + args[0].Substring(pos);

                // For some weird reason wzzip.exe seems to hold zip files open for a long
                // time after the command returns (maybe it launches winzip in the background
                // or something).  I'm going to hack this by waiting up to 5 minutes before
                // giving up, retrying every 30 seconds.

                for (int i=0;;i++) {

                    try {

                        File.Move(args[0],newName);
                        return 0;
                    }
                    catch (Exception e) {

                        if (e.Message.IndexOf("another process") == -1)
                            throw e;

                        Console.WriteLine(string.Format("Warning ({0}): {1}",e.GetType().Name,e.Message));

                        if (i == 10)
                            throw new Exception("File lock not released for 5 minutes. Giving up now.");

                        Thread.Sleep(30000);
                    }
                }
            }
            catch (Exception e) {

                Console.Error.WriteLine("Error: " + e.Message);
                return 1;
            }
        }
    }
}