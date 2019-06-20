//-----------------------------------------------------------------------------
// FILE:        CsvTableReader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to parse the columns of a CSV table.

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Net;

namespace LillTek.Common
{
    /// <summary>
    /// Used to read a CSV table that includes row headers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class makes it easy to process tabular data loaded from a CSV file,
    /// where the first row of the file contains the row header strings that
    /// name the table columns.
    /// </para>
    /// <para>
    /// Initialize an instance by passing a <see cref="CsvReader" />, stream, string, or file path to the 
    /// constructor.  The constructor will read the first row of the file and initialize the <see cref="ColumnMap" />
    /// dictionary which maps the case insenstive column name to the zero based index of the
    /// column in the table.
    /// </para>
    /// <para>
    /// You'll process each data row by calling <see cref="ReadRow" />.  This returns a list
    /// with the next row of data or <c>null</c> if the end of the table has been reached.  You can
    /// process the row data returned directly or call the <b>Parse()</b> methods to extract column
    /// data from the current row.  You can also use the <see cref="GetColumn" /> method to
    /// access a column value on the current row directly.
    /// </para>
    /// <note>
    /// This class is tolerant of blank or duplicate column names.  In the case of duplicates, the
    /// first column matching the requested column name will be used when parsing data.
    /// </note>
    /// <para>
    /// Applications should call the reader's <see cref="Dispose" /> or <see cref="Close" />
    /// method when they are finished with the reader so that the underlying <see cref="CsvReader" />
    /// will be closed as well, promptly releasing any system resources (such as the stream).
    /// </para>
    /// </remarks>
    public class CsvTableReader : IDisposable
    {

        private CsvReader                   reader;     // The CSV reader
        private List<string>                columns;    // List of column names in the order read from the source
        private Dictionary<string, int>     columnMap;  // Maps case insensitive column names into column indicies
        private List<string>                row;        // The current row

        /// <summary>
        /// Constructs an instance to read from a <see cref="CsvReader" />.
        /// </summary>
        /// <param name="reader">The <see cref="CsvReader" /> to read from.</param>
        public CsvTableReader(CsvReader reader)
        {

            this.reader    = reader;
            this.columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            this.columns   = new List<string>();

            row = reader.Read();
            if (row == null)
                return;

            for (int i = 0; i < row.Count; i++)
            {
                columns.Add(row[i]);

                if (!columnMap.ContainsKey(row[i]))
                    columnMap.Add(row[i], i);
            }
        }

        /// <summary>
        /// Constructs an instance to read from a <see cref="TextReader" />.
        /// </summary>
        /// <param name="reader">The reader.</param>
        public CsvTableReader(TextReader reader)
            : this(new CsvReader(reader))
        {
        }

        /// <summary>
        /// Constructs an instance to read from a file.
        /// </summary>
        /// <param name="path">Path to the file.</param>
        /// <param name="encoding">The file's character <see cref="Encoding" />.</param>
        public CsvTableReader(string path, Encoding encoding)
            : this(new CsvReader(path, encoding))
        {
        }

        /// <summary>
        /// Constructs an instance to read from a stream.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="encoding">The stream's character <see cref="Encoding" />.</param>
        public CsvTableReader(Stream stream, Encoding encoding)
            : this(new CsvReader(stream, encoding))
        {
        }

        /// <summary>
        /// Constructs an instance to read from a CSV string.
        /// </summary>
        /// <param name="text"></param>
        public CsvTableReader(string text)
            : this(new CsvReader(text))
        {
        }

        /// <summary>
        /// Releases any system resources held by the instance,
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Closes the reader if it is still open.
        /// </summary>
        public void Close()
        {
            if (reader != null)
            {
                reader.Close();
                reader = null;
            }
        }

        /// <summary>
        /// Returns the underlying <see cref="CsvReader" /> or <c>null</c> if the reader is closed.
        /// </summary>
        public CsvReader Reader
        {
            get { return reader; }
        }

        /// <summary>
        /// Returns the list of table columns in the order read from the source.
        /// </summary>
        public List<string> Columns
        {
            get { return columns; }
        }

        /// <summary>
        /// Returns the dictionary that case insensitvely maps a column name to 
        /// the zero base index of the column.
        /// </summary>
        public Dictionary<string, int> ColumnMap
        {
            get { return columnMap; }
        }

        /// <summary>
        /// Reads the next row of table.
        /// </summary>
        /// <returns>The list of column values or <c>null</c> if the end of the table has been reached.</returns>
        public List<string> ReadRow()
        {

            row = reader.Read();
            return row;
        }

        /// <summary>
        /// Returns the value for the named column in the current row.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <returns>The column value or <c>null</c> if the column (or row) does not exist.</returns>
        public string GetColumn(string columnName)
        {
            int index;

            if (row == null)
                return null;

            if (!columnMap.TryGetValue(columnName, out index))
                return null;

            return row[index];
        }

        /// <summary>
        /// Determines whether a cell in a named column in the current row is empty or
        /// if the column does not exist.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <returns><c>true</c> if the cell is empty or the named column is not present.</returns>
        public bool IsEmpty(string columnName)
        {
            return string.IsNullOrWhiteSpace(GetColumn(columnName));
        }

        /// <summary>
        /// Parses the named column of the current row or a default value if the
        /// column doesn't exist or the value cannot be parsed.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public string Parse(string columnName, string def)
        {
            return Serialize.Parse(GetColumn(columnName), def);
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// <note>
        /// <para>
        /// This method supports "K", "M", and "G" suffixes
        /// where "K" multiples the value parsed by 1024, "M multiplies
        /// the value parsed by 1048576, and "G" multiples the value
        /// parsed by 1073741824.  The "T" suffix is not supported
        /// by this method because it exceeds the capacity of a
        /// 32-bit integer.
        /// </para>
        /// <para>
        /// The following constant values are also supported:
        /// </para>
        /// <list type="table">
        ///     <item><term><b>short.min</b></term><description>-32768</description></item>
        ///     <item><term><b>short.max</b></term><description>32767</description></item>
        ///     <item><term><b>ushort.max</b></term><description>65533</description></item>
        ///     <item><term><b>int.min</b></term><description>-2147483648</description></item>
        /// </list>
        /// </note>
        /// </remarks>
        public int Parse(string columnName, int def)
        {
            return Serialize.Parse(GetColumn(columnName), def);
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// <note>
        /// <para>
        /// This method supports "K", "M", "G", and "T" suffixes
        /// where "K" multiples the value parsed by 1024, "M multiplies
        /// the value parsed by 1048576, "G" multiples the value
        /// parsed by 1073741824, and "T" multiplies the value by 
        /// 1099511627776.
        /// </para>
        /// <para>
        /// The following constant values are also supported:
        /// </para>
        /// <list type="table">
        ///     <item><term><b>short.min</b></term><description>-32768</description></item>
        ///     <item><term><b>short.max</b></term><description>32767</description></item>
        ///     <item><term><b>ushort.max</b></term><description>65533</description></item>
        ///     <item><term><b>int.min</b></term><description>-2147483648</description></item>
        ///     <item><term><b>int.max</b></term><description>2147483647</description></item>
        ///     <item><term><b>uint.max</b></term><description>4294967295</description></item>
        ///     <item><term><b>long.max</b></term><description>2^63 - 1</description></item>
        /// </list>
        /// </note>
        /// </remarks>
        public long Parse(string columnName, long def)
        {
            return Serialize.Parse(GetColumn(columnName), def);
        }

        /// <summary>
        /// Parses an enumeration value where the value is case insenstive.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <param name="enumType">The enumeration type.</param>
        /// <param name="def">The default enumeration value.</param>
        /// <returns>The value loaded from the configuration or the default value.</returns>
        public object Parse(string columnName, System.Type enumType, object def)
        {
            return Serialize.Parse(GetColumn(columnName), enumType, def);
        }

        /// <summary>
        /// Parses an enumeration value where the value is case insenstive.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type being parsed.</typeparam>
        /// <param name="columnName">The column name.</param>
        /// <param name="def">The default enumeration value.</param>
        /// <returns>The value loaded from the configuration or the default value.</returns>
        public TEnum Parse<TEnum>(string columnName, object def)
        {
            return Serialize.Parse<TEnum>(GetColumn(columnName), def);
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public Uri Parse(string columnName, Uri def)
        {
            return Serialize.Parse(GetColumn(columnName), def);
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// <para>
        /// This method recognises the following boolean values:
        /// </para>
        /// <code language="none">
        /// False Values        True Values
        /// ------------        -----------
        ///     0                   1
        ///     no                  yes
        ///     off                 on
        ///     low                 high
        ///     false               true
        ///     disable             enable
        /// </code>
        /// </remarks>
        public bool Parse(string columnName, bool def)
        {
            return Serialize.Parse(GetColumn(columnName), def);
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// <note>
        /// <para>
        /// This method supports "K", "M", "G", and "T" suffixes
        /// where "K" multiples the value parsed by 1024, "M multiplies
        /// the value parsed by 1048576, "G" multiples the value
        /// parsed by 1073741824, and "T" multiplies the value by 
        /// 1099511627776.
        /// </para>
        /// <para>
        /// The following constant values are also supported:
        /// </para>
        /// <list type="table">
        ///     <item><term><b>short.min</b></term><description>-32768</description></item>
        ///     <item><term><b>short.max</b></term><description>32767</description></item>
        ///     <item><term><b>ushort.max</b></term><description>65533</description></item>
        ///     <item><term><b>int.min</b></term><description>-2147483648</description></item>
        ///     <item><term><b>int.max</b></term><description>2147483647</description></item>
        ///     <item><term><b>uint.max</b></term><description>4294967295</description></item>
        ///     <item><term><b>long.max</b></term><description>2^63 - 1</description></item>
        /// </list>
        /// </note>
        /// </remarks>
        public double Parse(string columnName, double def)
        {
            return Serialize.Parse(GetColumn(columnName), def);
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// <para>
        /// Timespan values are encoded as floating point values terminated with
        /// one of the unit codes: "d" (days), "h" (hours), "m" (minutes), "s"
        /// (seconds), or "ms" (milliseconds).  If the unit code is missing then 
        /// seconds will be assumed.  An infinite timespan is encoded using the 
        /// literal "infinite".
        /// </para>
        /// <para>
        /// Timespan values can also be specified as:
        /// </para>
        /// <para>
        /// <c>[ws][-]{ d | d.hh:mm[:ss[.ff]] | hh:mm[:ss[.ff]] }[ws]</c>
        /// </para>
        /// <para>where:</para>
        /// <list type="table">
        ///     <item>
        ///         <term>ws</term>
        ///         <definition>is whitespace</definition>
        ///     </item>
        ///     <item>
        ///         <term>d</term>
        ///         <definition>specifies days.</definition>
        ///     </item>
        ///     <item>
        ///         <term>hh</term>
        ///         <definition>specifies hours</definition>
        ///     </item>
        ///     <item>
        ///         <term>mm</term>
        ///         <definition>specifies minutes</definition>
        ///     </item>
        ///     <item>
        ///         <term>ss</term>
        ///         <definition>specifies seconds</definition>
        ///     </item>
        ///     <item>
        ///         <term>ff</term>
        ///         <definition>specifies fractional seconds</definition>
        ///     </item>
        /// </list>
        /// </remarks>
        public TimeSpan Parse(string columnName, TimeSpan def)
        {
            return Serialize.Parse(GetColumn(columnName), def);
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// Dates are encoded into strings as described in RFC 1123.
        /// </remarks>
        public DateTime Parse(string columnName, DateTime def)
        {
            return Serialize.Parse(GetColumn(columnName), def);
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// IP addresses are formatted as &lt;dotted-quad&gt;
        /// </remarks>
        public IPAddress Parse(string columnName, IPAddress def)
        {
            return Serialize.Parse(GetColumn(columnName), def);
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        /// <remarks>
        /// Network bindings are formatted as &lt;dotted-quad&gt;:&lt;port&gt; or
        /// &lt;host&gt;:&lt;port&gt;
        /// </remarks>
        public NetworkBinding Parse(string columnName, NetworkBinding def)
        {
            return Serialize.Parse(GetColumn(columnName), def);
        }

        /// <summary>
        /// Parses the string passed unless the string is <c>null</c> or
        /// the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public Guid Parse(string columnName, Guid def)
        {
            return Serialize.Parse(GetColumn(columnName), def);
        }

        /// <summary>
        /// Parses the hex encoded string passed unless the string is <c>null</c>
        /// or the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public byte[] Parse(string columnName, byte[] def)
        {
            return Serialize.Parse(GetColumn(columnName), def);
        }

        /// <summary>
        /// Parses the base-64 encoded string passed unless the string is 
        /// null or the parse failed, in which case the default value
        /// will be returned.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The parsed value on success, the default value on failure.</returns>
        public byte[] ParseBase64(string columnName, byte[] def)
        {
            return Serialize.ParseBase64(GetColumn(columnName), def);
        }
    }
}
