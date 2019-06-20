﻿//-----------------------------------------------------------------------------
// FILE:        CsvReader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Parses CSV encoded rows from text.

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace LillTek.Common
{
    /// <summary>
    /// Parses CSV encoded rows from text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this class to parse CSV encoded tables.  Use one of the constructors
    /// to initialize in instance to read from a file, <see cref="Stream" /> , string, or 
    /// a <see cref="TextReader" /> and then call <see cref="Read" /> to read each
    /// row of the table.
    /// </para>
    /// <para>
    /// This class handles the all special cases for CSV parsing including quoted
    /// fields, escaped double quotes, and fields that include CR and LF characters.
    /// </para>
    /// <para>
    /// Be sure to call <see cref="Close" /> or <see cref="Dispose" /> when you
    /// are finished with the class to release any underlying resources.
    /// </para>
    /// <note>
    /// The underlying stream must support seeking for this class to work properly.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="false" />
    public sealed class CsvReader : IDisposable
    {
        private TextReader reader;

        /// <summary>
        /// Constructs a reader to parse a string.
        /// </summary>
        /// <param name="text">The text string.</param>
        public CsvReader(string text)
            : this(new StringReader(text))
        {
        }

        /// <summary>
        /// Constructs a reader to parse a file.
        /// </summary>
        /// <param name="path">Path to the file.</param>
        /// <param name="encoding">The file's character <see cref="Encoding" />.</param>
        public CsvReader(string path, Encoding encoding)
            : this(new StreamReader(path, encoding))
        {
        }

        /// <summary>
        /// Constructs a reader to parse a stream.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="encoding">The stream's character <see cref="Encoding" />.</param>
        public CsvReader(Stream stream, Encoding encoding)
            : this(new StreamReader(stream, encoding))
        {
        }

        /// <summary>
        /// Constructs a reader to parse text from a <see cref="TextReader" />.
        /// </summary>
        /// <param name="reader">The <see cref="TextReader" />.</param>
        public CsvReader(TextReader reader)
        {
            this.reader = reader;
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
        /// Releases any resources associated with the reader.
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Parses and returns the next row of fields.
        /// </summary>
        /// <returns>
        /// A list of parsed field strings or <c>null</c> if the end of the input stream
        /// has been reached.
        /// </returns>
        public List<string> Read()
        {
            List<string>    fields;
            StringBuilder   sb;
            char            ch;
            bool            eol;

            if (reader.Peek() == -1)
                return null;

            fields = new List<string>();
            eol    = false;

            while (!eol)
            {
                sb = new StringBuilder();

                while (true)
                {
                    ch = (char)reader.Read();
                    switch (ch)
                    {
                        case '"':

                            // Quoted field.

                            while (true)
                            {
                                if (reader.Peek() == -1)
                                {
                                    // At one point I throw a FormatException here because this really is
                                    // an error (a missing closing quote).  But, it appears that sometimes
                                    // Excel doesn't write the last quote, so we'll just consider this to
                                    // terminate the field.

                                    eol = true;
                                    goto gotField;
                                }

                                ch = (char)reader.Read();
                                if (ch == '"')
                                {
                                    if (reader.Peek() == (int)'"')
                                        sb.Append('"'); // Escaped quote
                                    else
                                        break;
                                }
                                else
                                    sb.Append(ch);
                            }
                            break;

                        case ',':

                            goto gotField;

                        case '\r':

                            if (reader.Peek() == (int)'\n')
                                reader.Read();

                            eol = true;
                            goto gotField;

                        case '\n':

                            eol = true;
                            goto gotField;

                        case unchecked((char)-1):

                            eol = true;
                            goto gotField;

                        default:

                            sb.Append(ch);
                            break;
                    }
                }

            gotField:

                fields.Add(sb.ToString());
            }

            return fields;
        }
    }
}
