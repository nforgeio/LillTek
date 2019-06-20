//-----------------------------------------------------------------------------
// FILE:        SqlScriptRunner.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Runs SQL Scripts with Query Analyzer style "go" statements.

using System;
using System.IO;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Collections;

using LillTek.Common;

namespace LillTek.Data
{
    /// <summary>
    /// Runs SQL Scripts with Query Analyzer style "go" statements.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Microsoft's Query Analyzer and OSQL.EXE tools are capable of running
    /// T-SQL script files containing multiple queries separated by "go" statements.
    /// These go statements are not part of the T-SQL language and thus these
    /// script files cannot be submitted directly to a SQL server.
    /// </para>
    /// <para>
    /// This class implements the equivalent functionality by splitting up a
    /// script with embedded go statements and running them separately.
    /// </para>
    /// <note>
    /// Note that the class is not smart enough to actually parse T-SQL.
    /// It separates the queries by looking for "go" statements by themselves
    /// on a line of text after stripping off and leading or trailing
    /// whitespace.
    /// </note>
    /// </remarks>
    public class SqlScriptRunner : IEnumerable
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// A separated script query instance.
        /// </summary>
        private sealed class QueryInfo
        {
            public int      LineNumber;     // Starting line number of the script in the
                                            // original script.
            public string   Query;          // The T-SQL query

            public QueryInfo(int lineNumber, string query)
            {
                this.LineNumber = lineNumber;
                this.Query = query;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private QueryInfo[] queries;    // The separated queries.

        /// <summary>
        /// Parses the SQL script passed by parsing the individual
        /// queries separated by "go" statements.
        /// </summary>
        /// <param name="script">The T-SQL script source.</param>
        public SqlScriptRunner(string script)
        {
            StringReader        reader = new StringReader(script);
            ArrayList           items  = new ArrayList();
            string              line;
            int                 lineNum;
            int                 startLine;
            StringBuilder       sb;
            string              query;

            lineNum   = 0;
            startLine = lineNum + 1;
            sb        = new StringBuilder();

            while (true)
            {
                line = reader.ReadLine();
                if (line == null)
                    break;

                lineNum++;

                line = line.Trim();
                if (line.ToLowerInvariant() == "go")
                {
                    query = sb.ToString();
                    items.Add(new QueryInfo(startLine, query));

                    sb = new StringBuilder();
                    startLine = lineNum + 1;
                }
                else
                {
                    sb.Append(line);
                    sb.Append("\r\n");
                }
            }

            query = sb.ToString();
            if (query.Trim() != string.Empty)
                items.Add(new QueryInfo(startLine, query));

            queries = new QueryInfo[items.Count];
            items.CopyTo(0, queries, 0, items.Count);
        }

        /// <summary>
        /// Returns the number of queries parsed.
        /// </summary>
        public int Count
        {
            get { return queries.Length; }
        }

        /// <summary>
        /// Returns the indexed query.
        /// </summary>
        public string this[int index]
        {
            get { return queries[index].Query; }
        }

        /// <summary>
        /// Returns an enumerator that walks through the parsed queries
        /// from first to last.
        /// </summary>
        public IEnumerator GetEnumerator()
        {
            string[] items;

            items = new string[queries.Length];
            for (int i = 0; i < queries.Length; i++)
                items[i] = queries[i].Query;

            return items.GetEnumerator();
        }

        /// <summary>
        /// Runs the script queries on a database connection.
        /// </summary>
        /// <param name="sqlCon">The database connection.</param>
        /// <returns>
        /// An array of QueryDisposition instances indicating what
        /// happened when each query was executed.
        /// </returns>
        public QueryDisposition[] Run(SqlConnection sqlCon)
        {
            return Run(sqlCon, false);
        }

        /// <summary>
        /// Runs the script queries on a database connection.
        /// </summary>
        /// <param name="sqlCon">The database connection.</param>
        /// <param name="ignoreErrors">Pass as <c>true</c> to continue executing queries if any fail.</param>
        /// <returns>
        /// An array of QueryDisposition instances indicating what
        /// happened when each query was executed.
        /// </returns>
        public QueryDisposition[] Run(SqlConnection sqlCon, bool ignoreErrors)
        {
            ArrayList               list = new ArrayList(); ;
            QueryDisposition[]      dispositions;
            QueryInfo               query;

            for (int i = 0; i < queries.Length; i++)
            {
                query = queries[i];
                try
                {
                    SqlCommand      sqlCmd;
                    SqlDataReader   sqlReader;

                    if (query.Query.Trim() == string.Empty)
                    {
                        // Skip over blank queries

                        list.Add(new QueryDisposition());
                        continue;
                    }

                    sqlCmd                = sqlCon.CreateCommand();
                    sqlCmd.CommandType    = CommandType.Text;
                    sqlCmd.CommandText    = query.Query;
                    sqlCmd.CommandTimeout = 0;  // Disable timout

                    sqlReader = sqlCmd.ExecuteReader();
                    try
                    {
                        // Skip over any result sets returned.

                        do
                        {
                            while (sqlReader.Read()) ;
                        } 
                        while (sqlReader.NextResult());
                    }
                    finally
                    {
                        sqlReader.Close();
                    }

                    list.Add(new QueryDisposition());
                }
                catch (SqlException e)
                {
                    var sb = new StringBuilder();

                    foreach (SqlError error in e.Errors)
                        sb.AppendFormat("Error[code={0},line={1}]: {2}\r\n",
                                        error.Number, error.LineNumber + query.LineNumber, error.Message);

                    list.Add(new QueryDisposition(e, sb.ToString()));

                    if (!ignoreErrors)
                        break;
                }
                catch (Exception e)
                {
                    list.Add(new QueryDisposition(e, e.Message));

                    if (!ignoreErrors)
                        break;
                }
            }

            dispositions = new QueryDisposition[list.Count];
            list.CopyTo(0, dispositions, 0, list.Count);

            return dispositions;
        }
    }
}
