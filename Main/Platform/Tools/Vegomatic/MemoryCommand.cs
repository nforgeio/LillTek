//-----------------------------------------------------------------------------
// FILE:        MemoryCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the MEMORY commands.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Net;
using LillTek.Net.Sockets;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements the MEMORY commands.
    /// </summary>
    public static class MemoryCommand
    {
        /// <summary>
        /// Executes the specified NET command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {
            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic memmory alloc <size MB>

Allocates the specified number of megabytes of memory and then waits for a
key press.  This is useful for testing how applications react to low 
memory situations.

";
            if (args.Length < 1)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "alloc":

                    if (args.Length < 2)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return Allocate(args[1]);

                default:

                    Program.Error(usage);
                    return 1;
            }
        }

        /// <summary>
        /// Allocates the memory requested and waits for the user to press a key.
        /// </summary>
        /// <param name="sizeMB">The string holding the memory size.</param>
        /// <returns>Zero on success.</returns>
        private static int Allocate(string sizeMB)
        {
            try
            {
                int cMB;

                if (!int.TryParse(sizeMB, out cMB) || cMB <= 0)
                    throw new ArgumentException("Invalid memory size.");

                Console.WriteLine("Allocating [{0}MB] of memory...", cMB);

                var blockList = new List<byte[]>(cMB);

                for (int i = 0; i < cMB; i++)
                    blockList.Add(new byte[1024 * 1024]);

                Console.WriteLine("Done");
                Console.WriteLine();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return 0;
            }
            catch (Exception e)
            {
                Program.Error(e.Message);
                return 1;
            }
        }
    }
}
