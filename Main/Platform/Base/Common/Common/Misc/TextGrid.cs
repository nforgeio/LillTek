//-----------------------------------------------------------------------------
// FILE:        TextGrid.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Lightweight class for rendering tabular status information.

using System;
using System.IO;
using System.Text;
using System.Collections;

// $todo(jeff.lill): 
//
// Add a constructor that parses some string representation of the
// grid layout, format attributes, named cells, and default values to make it
// easier to specify a report.

namespace LillTek.Common
{
    /// <summary>
    /// The possible alignment formats for a cell.
    /// </summary>
    public enum TextGridAlign
    {
        /// <summary>
        /// Use the default alignment for the column or table.
        /// </summary>
        Default,

        /// <summary>
        /// Left align the cell contents.
        /// </summary>
        Left,

        /// <summary>
        /// Center the cell contents.
        /// </summary>
        Center,

        /// <summary>
        /// Right align the cell contents.
        /// </summary>
        Right
    }

    /// <summary>
    /// The format information for a status column or cell.
    /// </summary>
    public sealed class TextGridFormat
    {
        /// <summary>
        /// Text alignment.
        /// </summary>
        public TextGridAlign Align;

        /// <summary>
        /// Width in characters.
        /// </summary>
        public int Width;

        /// <summary>
        /// The character to be used to pad cell contents.
        /// </summary>
        public char FillChar;

        /// <summary>
        /// Constructor.
        /// </summary>
        public TextGridFormat()
            : this(TextGridAlign.Default, -1)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="align">The cell alignment.</param>
        /// <param name="width">The cell width.</param>
        /// <remarks>
        /// Specify align=TextGridAlign.Default and width=-1 to
        /// use the default values defined for a column.
        /// </remarks>
        public TextGridFormat(TextGridAlign align, int width)
        {
            this.Align    = align;
            this.Width    = width;
            this.FillChar = '\0';
        }

        /// <summary>
        /// Constructs a format that simply fills the cell with
        /// the character passed.
        /// </summary>
        /// <param name="fillChar">The fill character.</param>
        public TextGridFormat(char fillChar)
            : this()
        {
            this.FillChar = fillChar;
        }

        /// <summary>
        /// Returns a copy of the instance.
        /// </summary>
        public TextGridFormat Clone()
        {
            var clone = new TextGridFormat();

            clone.Align = this.Align;
            clone.Width = this.Width;
            clone.FillChar = this.FillChar;

            return clone;
        }
    }

    /// <summary>
    /// Implements a simple grid class to be used generating tabular string
    /// output for fixed pitched fonts.
    /// </summary>
    /// <remarks>
    /// The idea here is to create a matrix of name/value pairs stored within 
    /// the underlying matrix class.  This matrix will be rendered into a string
    /// via the <see cref="ToString" /> method and then this string can easily be displayed 
    /// in a text box.  The text box will have to have a fixed pitch font selected
    /// for the formatting to work out properly.
    /// </remarks>
    public sealed class TextGrid : Matrix2D<string>
    {
        private sealed class Cell
        {
            public int X;
            public int Y;

            public Cell(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }
        }

        private Hashtable   colFormats;     // Default cell formats indexed by column number
        private Hashtable   cellFormats;    // Cell formats indexed by string: [<x>,<y>]
        private Hashtable   cellNames;      // Maps cell names to coordinates
        private string      naString;       // String to display for null cells

        /// <summary>
        /// Constructor.
        /// </summary>
        public TextGrid()
            : base()
        {
            this.cellNames   = new Hashtable();
            this.colFormats  = new Hashtable();
            this.cellFormats = new Hashtable();
            this.naString    = "-na-";
        }

        /// <summary>
        /// The string to display for <c>null</c> cells.  This defaults to "-na-".
        /// </summary>
        public string NAString
        {
            get { return naString; }
            set { naString = value; }
        }

        /// <summary>
        /// Clears the grid data, defined names, and column widths.
        /// </summary>
        public new void Clear()
        {
            base.Clear();
            cellNames.Clear();
            colFormats.Clear();
            cellFormats.Clear();
        }

        /// <summary>
        /// Clears the cell data but keeps the column widths and
        /// cell names intact.
        /// </summary>
        public void ClearCells()
        {
            base.Clear();
        }

        /// <summary>
        /// Clears the named cells.
        /// </summary>
        public void ClearNamedCells()
        {
            foreach (Cell cell in cellNames.Values)
                base[cell.X, cell.Y] = null;
        }

        /// <summary>
        /// This method assigns a name to a specified cell in the grid.
        /// </summary>
        /// <param name="name">The cell name.</param>
        /// <param name="x">The cell's X coordinate.</param>
        /// <param name="y">The cell's Y coordinate.</param>
        public void NameCell(string name, int x, int y)
        {
            cellNames[name.ToLowerInvariant()] = new Cell(x, y);
        }

        /// <summary>
        /// Sets the default cell format for a column.
        /// </summary>
        /// <param name="x">The column.</param>
        /// <param name="format">The cell format.</param>
        public void SetColumnFormat(int x, TextGridFormat format)
        {
            colFormats[x] = format;
        }

        /// <summary>
        /// Sets the format to use for a specific cell.
        /// </summary>
        /// <param name="x">The cell's X coordinate.</param>
        /// <param name="y">The cell's Y coordinate.</param>
        /// <param name="format">The cell format.</param>
        public void SetCellFormat(int x, int y, TextGridFormat format)
        {
            cellFormats[string.Format("[{0},{1}]", x, y)] = format;
        }

        /// <summary>
        /// This method references a grid cell via its name.
        /// </summary>
        public string this[string name]
        {
            get
            {
                Cell cell;

                cell = (Cell)cellNames[name.ToLowerInvariant()];
                if (cell == null)
                    throw new ArgumentException(string.Format("Cannot find a cell named [{0}].", name), "name");

                return base[cell.X, cell.Y];
            }

            set
            {
                Cell cell;

                cell = (Cell)cellNames[name.ToLowerInvariant()];
                if (cell == null)
                    throw new ArgumentException(string.Format("Cannot find a cell named [{0}].", name), "name");

                base[cell.X, cell.Y] = value;
            }
        }

        /// <summary>
        /// Returns the formatting to use for the specified cell.
        /// </summary>
        /// <param name="formatCache">
        /// This variable is used by the method for caching information between calls.
        /// The referenced variable should be set to null before calling this method
        /// for the first time.
        /// </param>
        /// <param name="x">The cell's X coordinate.</param>
        /// <param name="y">The cell's Y coordinate.</param>
        private TextGridFormat GetCellFormat(ref object formatCache, int x, int y)
        {
            TextGridFormat      format;
            TextGridFormat      cellFormat;
            ArrayList           defColFormats;

            // The format cache is simply an ArrayList of StatusCellFormat objects
            // indexed by column number.  These objects are generated by combining
            // any default column formats with the actual widths of the cell strings.

            if (formatCache != null)
                defColFormats = (ArrayList)formatCache;
            else
            {
                // Make sure we have default formats for all columns
                // taking into account the actual length of the
                // cell values in the column.

                formatCache = defColFormats = new ArrayList();

                for (int i = 0; i < base.ColumnCount; i++)
                {
                    format = (TextGridFormat)colFormats[i];
                    if (format != null)
                        defColFormats.Add(format);
                    else
                        defColFormats.Add(new TextGridFormat());
                }

                for (int col = 0; col < base.ColumnCount; col++)
                {
                    TextGridFormat  colFormat;
                    int             maxWidth = 0;
                    int             width;
                    object          value;

                    colFormat = (TextGridFormat)defColFormats[col];
                    if (colFormat.Width == -1)
                    {
                        for (int row = 0; row < base.RowCount; row++)
                        {
                            cellFormat = (TextGridFormat)cellFormats[string.Format("[{0},{1}]", col, row)];
                            if (cellFormat != null && cellFormat.Width != -1)
                                width = cellFormat.Width;
                            else if (cellFormat != null && cellFormat.FillChar != '\0')
                                width = 0;
                            else
                            {
                                value = base[col, row];
                                if (value == null)
                                    width = naString.Length;
                                else
                                    width = value.ToString().Length;

                                if (width > maxWidth)
                                    maxWidth = width;
                            }
                        }

                        if (colFormat.Width == -1)
                            colFormat.Width = maxWidth;
                    }
                }
            }

            // Get the default format for this column and then modify
            // it with any specific formatting for this cell.

            format     = ((TextGridFormat)defColFormats[x]).Clone();
            cellFormat = (TextGridFormat)cellFormats[string.Format("[{0},{1}]", x, y)];
            if (cellFormat != null)
            {
                if (cellFormat.Align != TextGridAlign.Default)
                    format.Align = cellFormat.Align;

                if (cellFormat.Width != -1)
                    format.Width = cellFormat.Width;

                if (cellFormat.FillChar != '\0')
                    format.FillChar = cellFormat.FillChar;
            }

            return format;
        }

        /// <summary>
        /// Writes the grid to the text writer passed.
        /// </summary>
        /// <param name="writer">The text writer.</param>
        public void Write(TextWriter writer)
        {
            object          formatCache = null;
            TextGridFormat  format;
            object          value;
            string          cell;

            // Generate the output one row at a time, separating each column with
            // a single blank.

            for (int y = 0; y < base.RowCount; y++)
            {
                for (int x = 0; x < base.ColumnCount; x++)
                {
                    format = GetCellFormat(ref formatCache, x, y);
                    value  = base[x, y];

                    if (format.FillChar != '\0')
                        cell = new String(format.FillChar, format.Width);
                    else if (value == null)
                        cell = naString;
                    else
                        cell = value.ToString();

                    if (cell.Length > format.Width)
                    {
                        if (format.Width <= 3)
                            cell = new String('.', format.Width);
                        else
                            cell = cell.Substring(0, format.Width - 3) + "...";
                    }
                    else if (cell.Length != format.Width)
                    {
                        int     padding = format.Width - cell.Length;
                        int     left, right;

                        // Pad the cell text based on the alignment

                        switch (format.Align)
                        {
                            case TextGridAlign.Default:
                            case TextGridAlign.Left:

                                cell += new String(' ', padding);
                                break;

                            case TextGridAlign.Center:

                                left  = padding / 2;
                                right = padding - left;

                                cell = new String(' ', left) + cell + new String(' ', right);
                                break;

                            case TextGridAlign.Right:

                                cell = new String(' ', padding) + cell;
                                break;
                        }
                    }

                    // Append the cell contents and the column separator.

                    writer.Write(cell);
                    if (x < base.ColumnCount - 1)
                        writer.Write(' ');
                }

                writer.WriteLine();
            }
        }

        /// <summary>
        /// Returns the grid formatted as a string, suitable for displaying in a text box
        /// with a fixed pitch font selected.
        /// </summary>
        public override string ToString()
        {
            var sb     = new StringBuilder();
            var writer = new StringWriter(sb);

            Write(writer);
            writer.Flush();

            return sb.ToString();
        }
    }
}
