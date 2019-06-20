//-----------------------------------------------------------------------------
// FILE:        HttpCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the HTTP.SYS commands.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using LillTek.Common;
using LillTek.Windows;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements the HTTP.SYS commands.
    /// </summary>
    public static class HttpCommand
    {
        /// <summary>
        /// Executes the specified INI command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {

            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic http reserve <uri-prefix> <user>

Commands the low-level HTTP.SYS Windows layer to add a prefix reservation
allowing the specified Windows account to create HTTP listeners on the 
specified URI prefix. Note that the prefix must include a scheme, host name 
or wildcard, a port number, and optionally, a virtual path, following the 
Windows prefix rules.

-------------------------------------------------------------------------------
vegomatic http unreserve <uri-prefix> <user>

Commands the low-level HTTP.SYS Windows layer to remove the prefix reservation 
for the specified Windows account.
";
            if (args.Length != 3)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "reserve":

                    HttpSys.AddPrefixReservation(args[1], args[2]);
                    Console.WriteLine("Reservation added for [{0}] and account [{1}].", args[1], args[2]);
                    return 0;

                case "unreserve":

                    HttpSys.RemovePrefixReservation(args[1], args[2]);
                    Console.WriteLine("Reservation removed for [{0}] and account [{1}].", args[1], args[2]);
                    return 0;

                default:

                    Program.Error(usage);
                    return 1;
            }
        }
    }
}
