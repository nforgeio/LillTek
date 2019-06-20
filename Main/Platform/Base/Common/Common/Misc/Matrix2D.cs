//-----------------------------------------------------------------------------
// FILE:        Matrix2D.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Provides a simple implementation of a 2D semi-sparse matrix.

using System;
using System.Collections.Generic;

namespace LillTek.Common
{
    /// <summary>
    /// Provides a simple implementation of a 2D semi-sparse matrix.
    /// </summary>
    /// <typeparam name="TItem">Type of the item being stored as a matrix element.</typeparam>
    /// <threadsafety instance="false" />
    public class Matrix2D<TItem>
    {
        private const string BadCoordinate = "Coordinate must be >= 0.";

        private List<List<TItem>> rows;       // List of matrix rows
        private int colCount;   // Maximum column count

        /// <summary>
        /// Constructs an empty matrix.
        /// </summary>
        public Matrix2D()
        {
            this.rows     = new List<List<TItem>>();
            this.colCount = 0;
        }

        /// <summary>
        /// Returns the number of matrix rows.
        /// </summary>
        public int RowCount
        {
            get { return rows.Count; }
        }

        /// <summary>
        /// Returns the number of matrix columns.
        /// </summary>
        public int ColumnCount
        {
            get { return colCount; }
        }

        /// <summary>
        /// Clears the contents of the matrix.
        /// </summary>
        public void Clear()
        {
            rows     = new List<List<TItem>>();
            colCount = 0;
        }

        /// <summary>
        /// Accesses the object at the specified coordinates of the
        /// matrix.
        /// </summary>
        /// <param name="x">The zero-based x coodrdinate.</param>
        /// <param name="y">The zero-based y coodrdinate.</param>
        /// <remarks>
        /// This will return <c>default(TItem)</c> if the coordinates are outside the
        /// current bounds of the matrix.
        /// </remarks>
        public TItem this[int x, int y]
        {
            get
            {
                List<TItem> row;

                if (x < 0)
                    throw new ArgumentException(BadCoordinate, "x");
                if (y < 0)
                    throw new ArgumentException(BadCoordinate, "y");

                if (x >= colCount || y >= rows.Count)
                    return default(TItem);

                row = rows[y];
                if (row == null)
                    return default(TItem);

                if (x >= row.Count)
                    return default(TItem);

                return row[x];
            }

            set
            {
                List<TItem>     row;
                int             addCount;

                if (x < 0)
                    throw new ArgumentException(BadCoordinate, "x");
                if (y < 0)
                    throw new ArgumentException(BadCoordinate, "y");

                // Make sure that rows extends to y.

                if (y >= rows.Count)
                {
                    addCount = (y - rows.Count) + 1;
                    for (int i = 0; i < addCount; i++)
                        rows.Add(null);
                }

                // Make sure that a row exists at y.

                row = rows[y];
                if (row == null)
                    rows[y] = row = new List<TItem>(x + 1);

                // Make sure that the row extends to x.

                if (x >= row.Count)
                {
                    addCount = (x - row.Count) + 1;
                    for (int i = 0; i < addCount; i++)
                        row.Add(default(TItem));
                }

                // Save the value

                if (x >= colCount)
                    colCount = x + 1;

                row[x] = value;
            }
        }

        /// <summary>
        /// Trims off any null values off the right and bottom edges of the matrix.
        /// </summary>
        public void Trim()
        {
            List<TItem>     row;
            int             c;

            // Remove all null values from the end of column rows

            colCount = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                row = rows[i];
                if (row == null)
                    continue;

                c = 0;
                for (int j = row.Count - 1; j >= 0; j--)
                    if (row[j] != null)
                    {
                        c = j + 1;
                        break;
                    }

                row.RemoveRange(c, row.Count - c);

                if (row.Count > 0)
                    row.TrimExcess();

                if (c > colCount)
                    colCount = c;
            }

            // Remove all empty rows from the end of the list

            c = 0;
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                row = rows[i];
                if (row == null)
                    continue;

                if (row.Count > 0)
                {
                    c = i + 1;
                    break;
                }
                else
                    rows[i] = null;
            }

            rows.RemoveRange(c, rows.Count - c);
        }
    }
}
