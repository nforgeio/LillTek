//-----------------------------------------------------------------------------
// FILE:        HttpHeader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a HTTP header.

using System;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Http
{
    /// <summary>
    /// Implements a HTTP header.
    /// </summary>
    public sealed class HttpHeader
    {

        private string name;       // Header name
        private string value;      // Raw header value string

        /// <summary>
        /// Constructs a header from a name and string value.
        /// </summary>
        /// <param name="name">Header name.</param>
        /// <param name="value">Header value.</param>
        /// <remarks>
        /// Note that the value will be trimmed of leading and
        /// trailing whitespace before saving it.
        /// <note>
        /// The value will be trimmed of leading and
        /// trailing whitespace before saving it.
        /// </note>
        /// </remarks>
        public HttpHeader(string name, string value)
        {
            this.name  = name;
            this.value = value.Trim();
        }

        /// <summary>
        /// Constructs a header from a name and integer value.
        /// </summary>
        /// <param name="name">Header name.</param>
        /// <param name="value">Header value.</param>
        public HttpHeader(string name, int value)
        {
            this.name  = name;
            this.value = value.ToString();
        }

        /// <summary>
        /// Constructs a header from a name and date time (UTC) value.
        /// </summary>
        /// <param name="name">Header name.</param>
        /// <param name="value">Header value.</param>
        /// <remarks>
        /// The date will be rendered into a string as described
        /// in RFC 1123.
        /// </remarks>
        public HttpHeader(string name, DateTime value)
        {
            this.name  = name;
            this.value = Helper.ToInternetDate(value);
        }

        /// <summary>
        /// Constructs a header from a name and URI value.
        /// </summary>
        /// <param name="name">Header name.</param>
        /// <param name="uri">Header value.</param>
        public HttpHeader(string name, Uri uri)
        {
            this.name  = name;
            this.value = value.ToString();
        }

        /// <summary>
        /// Appends the value onto the end of an existing value using
        /// creating a comma separated list of values.
        /// </summary>
        /// <param name="value">Value to be appended.</param>
        public void Append(string value)
        {
            value = value.Trim();

            if (this.value == string.Empty)
                this.value = value;
            else
                this.value += ", " + value;
        }

        /// <summary>
        /// Appends a string to the end of the existing value
        /// as if it were a continuation line.
        /// </summary>
        /// <param name="value">The value to be appended.</param>
        public void AppendContinuation(string value)
        {
            this.value += " " + value.Trim();
        }

        /// <summary>
        /// Returns the header name.
        /// </summary>
        public string Name
        {
            get { return name; }
        }

        /// <summary>
        /// Returns the header value as a raw string.
        /// </summary>
        public string Value
        {
            get { return value; }
        }

        /// <summary>
        /// Returns the header value as a UTC date.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property attempts to parse the header value 
        /// using the commonly used date formats:
        /// </para>
        /// <code language="none">
        ///     Sun, 06 Nov 1994 08:49:37 GMT  ; RFC 822, updated by RFC 1123
        ///     Sunday, 06-Nov-94 08:49:37 GMT ; RFC 850, obsoleted by RFC 1036
        ///     Sun Nov  6 08:49:37 1994       ; ANSI C's asctime() format
        /// </code>
        /// </remarks>
        public DateTime AsDate
        {
            get { return Helper.ParseInternetDate(value); }
        }

        /// <summary>
        /// Returns the header value as an integer.
        /// </summary>
        public int AsInt
        {
            get { return int.Parse(value); }
        }

        /// <summary>
        /// Returns the header value as a URI.
        /// </summary>
        public Uri AsUri
        {
            get { return new Uri(value); }
        }
    }
}
