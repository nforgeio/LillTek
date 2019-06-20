//-----------------------------------------------------------------------------
// FILE:        TextTable.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to generate columnar reports to be rendered with a fixed
//              pitch font.

using System;
using System.Text;
using System.Collections.Generic;

namespace LillTek.Common
{
    /// <summary>
    /// Used to generate columnar reports to be rendered with a fixed
    /// pitch font.
    /// </summary>
    [Obsolete("Use the more functional [TextGrid] class instead.")]
    public class TextTable
    {
        private string[]        headers;
        private List<string[]>  rows;

        /// <summary>
        /// Creates an empty table, initializing the cloumn headers.
        /// </summary>
        /// <param name="columns">The column header objects.</param>
        public TextTable(params object[] columns)
        {
           headers = null;
            rows = new List<string[]>();

            SetHeaders(columns);
        }

        /// <summary>
        /// Sets the table headers to the textual representation of the column objects passed.
        /// </summary>
        /// <param name="columns">The column header objects.</param>
        public void SetHeaders(params object[] columns)
        {
            if (columns.Length == 0)
            {

                headers = null;
                return;
            }

            headers = new string[columns.Length];

            for (int i = 0; i < columns.Length; i++)
            {

                if (columns[i] == null)
                    headers[i] = string.Empty;
                else
                    headers[i] = columns[i].ToString();
            }
        }

        /// <summary>
        /// Appends a new row of column values to the table.
        /// </summary>
        /// <param name="columns">The column objects.</param>
        public void AppendRow(params object[] columns)
        {
            string[] row;

            row = new string[columns.Length];

            for (int i = 0; i < columns.Length; i++)
            {

                if (columns[i] == null)
                    row[i] = string.Empty;
                else
                    row[i] = columns[i].ToString();
            }

            rows.Add(row);
        }

        /// <summary>
        /// Returns a column value with the appropriate amount of whitespace
        /// such that the number of characters in the string returned equal
        /// the with specified.
        /// </summary>
        /// <param name="value">The value to be formatted.</param>
        /// <param name="width">The column width.</param>
        /// <returns>The formatted string.</returns>
        private string Format(string value, int width)
        {
            int cPadding;

            cPadding = width - value.Length;
            if (cPadding == 0)
                return value;

            return value + new String(' ', cPadding);
        }

        /// <summary>
        /// Renders the table as text adding whitespace to ensure that the table columns
        /// line up when displayed with a fixed pitch font.  Rows are terminated with
        /// CRLF sequences.
        /// </summary>
        /// <returns>The rendered table.</returns>
        public override string ToString()
        {
            var     sb = new StringBuilder();
            int     cColumns;
            int[]   colWidths;

            // Determine the number of columns in the table as well as
            // the maximum width of the fields in each column.

            cColumns = 0;

            if (headers != null)
                cColumns = headers.Length;

            for (int i = 0; i < rows.Count; i++)
                cColumns = Math.Max(cColumns, rows[i].Length);

            colWidths = new int[cColumns];

            if (headers != null)
            {
                for (int i = 0; i < headers.Length; i++)
                    colWidths[i] = Math.Max(colWidths[i], headers[i].Length);
            }

            for (int i = 0; i < rows.Count; i++)
            {
                string[] row = rows[i];

                for (int j = 0; j < row.Length; j++)
                    colWidths[j] = Math.Max(colWidths[j], row[j].Length);
            }

            // Render the table headers.

            if (headers != null)
            {
                for (int i = 0; i < headers.Length; i++)
                {
                    if (i > 0)
                        sb.Append(' ');

                    sb.Append(Format(headers[i], colWidths[i]));
                }

                sb.Append("\r\n");

                for (int i = 0; i < headers.Length; i++)
                {

                    if (i > 0)
                        sb.Append(' ');

                    sb.Append(new String('-', colWidths[i]));
                }

                sb.Append("\r\n");
            }

            // Render the table rows

            foreach (var row in rows)
            {
                for (int i = 0; i < row.Length; i++)
                {
                    if (i > 0)
                        sb.Append(' ');

                    sb.Append(Format(row[i], colWidths[i]));
                }

                sb.Append("\r\n");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns the number of table rows (not counting the header, if present).
        /// </summary>
        public int RowCount
        {
            get { return rows.Count; }
        }
    }
}
