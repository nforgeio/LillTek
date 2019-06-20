//-----------------------------------------------------------------------------
// FILE:        IniCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the INI commands.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements the INI commands.
    /// </summary>
    public static class IniCommand
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
vegomatic ini hash <file>

Generates a file named <file>.md5 with the MD5 hash
of the INI file passed and also makes a copy of the
original file called <file>.setup.

";
            if (args.Length < 2)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "hash":

                    return Hash(args[1]);

                default:

                    Program.Error(usage);
                    return 1;
            }
        }

        private static int Hash(string fileName)
        {
            try
            {
                byte[] hash;

                using (EnhancedStream es = new EnhancedFileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    hash = MD5Hasher.Compute(es, es.Length);
                }

                using (StreamWriter writer = new StreamWriter(fileName + ".md5", false, Helper.AnsiEncoding))
                {
                    writer.WriteLine("MD5={0}", Helper.ToHex(hash));
                }

                File.Copy(fileName, fileName + ".setup");

                return 0;
            }
            catch (Exception e)
            {
                Program.Error("Error ({0}): {1}", e.GetType().Name, e.Message);
                return 1;
            }
        }
    }
}
