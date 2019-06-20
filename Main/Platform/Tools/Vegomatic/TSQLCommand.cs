//-----------------------------------------------------------------------------
// FILE:        TSQLCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the T-SQL script generation commands.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using LillTek.Common;
using LillTek.Data;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements the T-SQL script generation commands.
    /// </summary>
    public static class TSQLCommand
    {
        /// <summary>
        /// Executes the specified T-SQL command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {
            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic tsql import <table name> <source csv> <script>

Generates a T-SQL script file that adds the data from a CSV file to the 
specified table.  Note that the first row of the source must name the
table columns.

    <table name>    - Name of the SQL table column
    <source CSV>    - Path to the input CSV file
    <script>        - Path to the output T-SQL script file

";
            if (args.Length < 1)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "import":

                    if (args.Length != 4)
                    {

                        Program.Error(usage);
                        return 1;
                    }

                    return Import(args[1], args[2], args[3]);

                default:

                    Program.Error(usage);
                    return 1;
            }
        }


        private static int Import(string tableName, string csvPath, string scriptPath)
        {
            const int blockLines = 1000;

            try
            {
                using (var csvReader = new CsvTableReader(csvPath, Encoding.UTF8))
                {
                    using (var writer = new StreamWriter(scriptPath, false, Encoding.UTF8))
                    {
                        int cRowsWritten = 0;

                        for (var row = csvReader.ReadRow(); row != null; row = csvReader.ReadRow())
                        {
                            writer.Write("insert into {0} (", tableName);

                            for (int i = 0; i < csvReader.Columns.Count; i++)
                            {
                                if (i > 0)
                                    writer.Write(", ");

                                writer.Write(csvReader.Columns[i]);
                            }

                            writer.Write(") values (");

                            for (int i = 0; i < csvReader.Columns.Count; i++)
                            {
                                if (i > 0)
                                    writer.Write(", ");

                                writer.Write(SqlHelper.Literal(row[i]));
                            }

                            writer.WriteLine(")");

                            cRowsWritten++;
                            if (cRowsWritten % blockLines == 0)
                                writer.WriteLine("go");     // Write a "go" every [blockLines] lines
                        }

                        if (cRowsWritten > 0 && cRowsWritten % blockLines != 0)
                            writer.WriteLine("go");     // Terminate with a "go" if necessary
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
