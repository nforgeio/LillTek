//-----------------------------------------------------------------------------
// FILE:        SqlBatch.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to create batches of SQL commands to be executed together
//              by a SQL server.

using System;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Collections;

using LillTek.Common;

namespace LillTek.Data
{
    /// <summary>
    /// Used to create batches of SQL commands to be executed together by a SQL server.
    /// </summary>
    /// <threadsafety instance="false" />
    public class SqlBatch
    {
        private StringBuilder sb;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SqlBatch()
        {
            this.sb    = new StringBuilder();
            this.Count = 0;
        }

        /// <summary>
        /// Returns the number of commands appended to the batch.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Appends a formatted SQL command to the batch.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The command arguments.</param>
        public void AppendCommand(string format, params object[] args)
        {
            if (sb.Length > 0)
                sb.Append(';');

            sb.AppendFormat(format, args);
            this.Count++;
        }

        /// <summary>
        /// Appends a SQL command with named parameters to the batch.
        /// </summary>
        /// <param name="command">The command string with embedded parameter references.</param>
        /// <param name="args">The command parameters.</param>
        /// <remarks>
        /// <para>
        /// This method uses the <see cref="MacroProcessor" /> class to replaced named
        /// parameter values in the command string with the literal parameter value.  Use
        /// the <b>$(param-name)</b> or archaic <b>%param-name%</b> syntax in the command
        /// string to reference specific arguments in the <paramref name="args"/> array.
        /// </para>
        /// </remarks>
        public void AppendCommand(string command, params SqlParam[] args)
        {
            if (sb.Length > 0)
                sb.Append(';');

            if (args == null || args.Length == 0)
            {
                sb.Append(command);
                return;
            }

            var processor = new MacroProcessor();

            foreach (var arg in args)
                processor.Add(arg.Name, arg.Literal);

            sb.Append(processor.Expand(command));
            this.Count++;
        }

        /// <summary>
        /// Appends a stored procedure call to the batch.
        /// </summary>
        /// <param name="sproc">Name of the stored procedure.</param>
        /// <param name="args">The stored procedure arguments.</param>
        public void AppendCall(string sproc, params SqlParam[] args)
        {
            if (sb.Length > 0)
                sb.Append(';');

            sb.AppendFormat("exec {0}", sproc);

            if (args == null || args.Length == 0)
                return;

            sb.Append(' ');

            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');

                sb.AppendFormat("@{0}={1}", args[i].Name, args[i].Literal);
            }

            this.Count++;
        }

        /// <summary>
        /// Renders the batched commands as a string.
        /// </summary>
        /// <returns>The command string.</returns>
        public override string ToString()
        {
            return sb.ToString();
        }
    }
}
