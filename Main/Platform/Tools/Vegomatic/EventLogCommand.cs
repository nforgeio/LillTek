//-----------------------------------------------------------------------------
// FILE:        EventLogCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the EVENTLOG commands.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

using LillTek.Common;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements the EVENTLOG commands.
    /// </summary>
    public static class EventLogCommand
    {
        /// <summary>
        /// Executes the specified EVENTLOG command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {
            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic eventlog create <name>

Creates the named Windows application event log source if it 
doesn't exist already.

-------------------------------------------------------------------------------
vegomatic eventlog remove <name>

Deletes the names Windows application event log source if one exists.
";
            if (args.Length < 2)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "create":

                    return Create(args[1]);

                case "remove":

                    return Remove(args[1]);

                default:

                    Program.Error(usage);
                    return 1;
            }
        }

        private static int Create(string logSource)
        {
            try
            {
                NativeSysLogProvider.CreateLogs(logSource);
            }
            catch (Exception e)
            {
                Program.Error("Error ({0}): {1}", e.GetType().Name, e.Message);
                return 1;
            }

            return 0;
        }

        private static int Remove(string logSource)
        {
            try
            {
                NativeSysLogProvider.RemoveLogs(logSource);
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
