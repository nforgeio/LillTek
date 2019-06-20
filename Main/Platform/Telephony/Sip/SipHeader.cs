//-----------------------------------------------------------------------------
// FILE:        SipHeader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a SIP message header.

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;

// $todo(jeff.lill): Implement real header value parsing as defined by the RFC 3261 grammar.

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Describes a SIP message header.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A SIP message header is basically a name/value pair.  Simple headers such as
    /// <b>Content-Length</b> are single valued.  Other headers such as <b>Via</b> oftem have 
    /// multiple values.  The <see cref="Name" /> property is the SIP header name, and the
    /// <see cref="FullText" /> property holds the header value.  The <see cref="Text" /> 
    /// property accesses the first header value.
    /// </para>
    /// <para>
    /// The <see cref="FullText" /> property returns multi-value headers in a comma separated 
    /// list.  The <see cref="Values" /> property also returns an array of the property
    /// values associated with the header name.
    /// </para>
    /// <para>
    /// Use <see cref="ToString" /> to render the header into a form suitable for
    /// serializing into a SIP message.
    /// </para>
    /// <para>
    /// A handful of SIP headers (such as <b>WWW-Authenticate</b> and <b>Authorization</b> define 
    /// value strings that include commas, breaking the convention used by all other headers
    /// where multiple headers values with the same header can be concatenated into a comma
    /// separated list of values.  Use the <see cref="SipHeader(string,string,bool)" />
    /// form of the constructor to instantiate one of these, passing <b>special=true</b>.
    /// For these instances, <see cref="IsSpecial" /> will return <c>true</c> and you
    /// won't be allowed to create a multi-value header.
    /// </para>
    /// <note>
    /// The current implementation does not attempt to implement the full SIP message
    /// grammar as defined by RFC 3261.  This may impact the comma-separated multi-valued
    /// headers, if header values contain commas and colons escaped using a form not
    /// supported by the stack.  At this point, this class supports <b>no escaping</b>.
    /// </note>
    /// </remarks>
    public class SipHeader
    {
        //---------------------------------------------------------------------
        // Well-known SIP header names

        /// <summary>Well known header name: "Accept"</summary>
        public const string Accept = "Accept";
        /// <summary>Well known header name: "Accept-Encoding"</summary>
        public const string AcceptEncoding = "Accept-Encoding";
        /// <summary>Well known header name: "Accept-Language"</summary>
        public const string AcceptLanguage = "Accept-Language";
        /// <summary>Well known header name: "Alert-Info"</summary>
        public const string AlertInfo = "Alert-Info";
        /// <summary>Well known header name: "Allow"</summary>
        public const string Allow = "Allow";
        /// <summary>Well known header name: "Authentication-Info"</summary>
        public const string AuthenticationInfo = "Authentication-Info";
        /// <summary>Well known header name: "Authorization"</summary>
        public const string Authorization = "Authorization";
        /// <summary>Well known header name: "Call-ID"</summary>
        public const string CallID = "Call-ID";
        /// <summary>Well known header name: "Call-Info"</summary>
        public const string CallInfo = "Call-Info";
        /// <summary>Well known header name: "Contact"</summary>
        public const string Contact = "Contact";
        /// <summary>Well known header name: "Content-Disposition"</summary>
        public const string ContentDisposition = "Content-Disposition";
        /// <summary>Well known header name: "Content-Encoding"</summary>
        public const string ContentEncoding = "Content-Encoding";
        /// <summary>Well known header name: "Content-Language"</summary>
        public const string ContentLanguage = "Content-Language";
        /// <summary>Well known header name: "Content-Length"</summary>
        public const string ContentLength = "Content-Length";
        /// <summary>Well known header name: "Content-Type"</summary>
        public const string ContentType = "Content-Type";
        /// <summary>Well known header name: "CSeq"</summary>
        public const string CSeq = "CSeq";
        /// <summary>Well known header name: "Date"</summary>
        public const string Date = "Date";
        /// <summary>Well known header name: "Error-Info"</summary>
        public const string ErrorInfo = "Error-Info";
        /// <summary>Well known header name: "Expires"</summary>
        public const string Expires = "Expires";
        /// <summary>Well known header name: "From"</summary>
        public const string From = "From";
        /// <summary>Well known header name: "In-Reply-To"</summary>
        public const string InReplyTo = "In-Reply-To";
        /// <summary>Well known header name: "Max-Forwards"</summary>
        public const string MaxForwards = "Max-Forwards";
        /// <summary>Well known header name: "Min-Expires"</summary>
        public const string MinExpires = "Min-Expires";
        /// <summary>Well known header name: "MIME-Version"</summary>
        public const string MIMEVersion = "MIME-Version";
        /// <summary>Well known header name: "Organization"</summary>
        public const string Organization = "Organization";
        /// <summary>Well known header name: "Priority"</summary>
        public const string Priority = "Priority";
        /// <summary>Well known header name: "Proxy-Authenticate"</summary>
        public const string ProxyAuthenticate = "Proxy-Authenticate";
        /// <summary>Well known header name: "Proxy-Authorization"</summary>
        public const string ProxyAuthorization = "Proxy-Authorization";
        /// <summary>Well known header name: "Proxy-Require"</summary>
        public const string ProxyRequire = "Proxy-Require";
        /// <summary>Well known header name: "Record-Route"</summary>
        public const string RecordRoute = "Record-Route";
        /// <summary>Well known header name: "Reply-To"</summary>
        public const string ReplyTo = "Reply-To";
        /// <summary>Well known header name: "Require"</summary>
        public const string Require = "Require";
        /// <summary>Well known header name: "Retry-After"</summary>
        public const string RetryAfter = "Retry-After";
        /// <summary>Well known header name: "Route"</summary>
        public const string Route = "Route";
        /// <summary>Well known header name: "Server"</summary>
        public const string Server = "Server";
        /// <summary>Well known header name: "Subject"</summary>
        public const string Subject = "Subject";
        /// <summary>Well known header name: "Supported"</summary>
        public const string Supported = "Supported";
        /// <summary>Well known header name: "Timestamp"</summary>
        public const string Timestamp = "Timestamp";
        /// <summary>Well known header name: "To"</summary>
        public const string To = "To";
        /// <summary>Well known header name: "Unsupported"</summary>
        public const string Unsupported = "Unsupported";
        /// <summary>Well known header name: "User-Agent"</summary>
        public const string UserAgent = "User-Agent";
        /// <summary>Well known header name: "Via"</summary>
        public const string Via = "Via";
        /// <summary>Well known header name: "Warning"</summary>
        public const string Warning = "Warning";
        /// <summary>Well known header name: "WWW-Authenticate"</summary>
        public const string WWWAuthenticate = "WWW-Authenticate";

        //---------------------------------------------------------------------
        // Static members

        // This table controls how multi-valued non-special headers will be
        // rendered in a SIP message.  If the header name is added to
        // this table, then multi-valued headers will be rendered on
        // separate header lines otherwise, for non-special headers, the
        // values will be rendered separated by commas on a single line.
        //
        // Special headers are always rendered on separate lines.

        private static Dictionary<string, bool> renderMultiLine;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static SipHeader()
        {
            renderMultiLine = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            renderMultiLine.Add("Via", true);
            renderMultiLine.Add("Route", true);
        }

        //---------------------------------------------------------------------
        // Implementation

        private const string CommaErrorMsg = "SIP header value cannot include a comma.";

        private bool        isSpecial;
        private string      name;
        private string[]    values;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        public SipHeader(string name, string value)
            : this(name, value, false)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="values">The header values.</param>
        public SipHeader(string name, string[] values)
        {
            this.isSpecial = false;
            this.name      = name;
            this.values    = values;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].IndexOf(',') != -1)
                    throw new ArgumentException(CommaErrorMsg, "values");
            }
        }

        /// <summary>
        /// Used to optionally initialize a special header that doesn't
        /// allow multiple values.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <param name="special"><c>true</c> if the header is special.</param>
        public SipHeader(string name, string value, bool special)
        {
            if (!special)
            {
                if (value.IndexOf(',') != -1)
                    throw new ArgumentException(CommaErrorMsg, "value");

                this.isSpecial = false;
                this.name = name;
                this.values = new string[] { value };
            }
            else
            {

                this.isSpecial = true;
                this.name      = name;
                this.values    = new string[] { value };
            }
        }

        /// <summary>
        /// Constructor used by Clone().
        /// </summary>
        private SipHeader()
        {
        }

        /// <summary>
        /// Returns a deep copy of the instance.
        /// </summary>
        /// <returns>The cloned <see cref="SipHeader" />.</returns>
        public SipHeader Clone()
        {
            var clone = new SipHeader();

            clone.isSpecial = this.isSpecial;
            clone.name = this.name;
            clone.values = new string[this.values.Length];

            Array.Copy(this.values, clone.values, this.values.Length);

            return clone;
        }

        /// <summary>
        /// Returns <c>true</c> if the header is one of the few that permit
        /// commas in the header values and don't allow multiple header 
        /// values.
        /// </summary>
        public bool IsSpecial
        {
            get { return isSpecial; }
        }

        /// <summary>
        /// Adds a value to the <b>beginning</b> of the list of header values.
        /// </summary>
        /// <param name="value">The value to be added.</param>
        public void Prepend(string value)
        {
            string[] v;

            if (isSpecial)
                throw new SipException("Cannot append a header value to the special header [{0}].", name);

            if (value.IndexOf(',') != -1)
                throw new ArgumentException(CommaErrorMsg, "value");

            v = new string[this.values.Length + 1];
            Array.Copy(this.values, 0, v, 1, this.values.Length);
            v[0] = value;

            values = v;
        }

        /// <summary>
        /// Adds a value to the <b>end</b> of the list of header values.
        /// </summary>
        /// <param name="value">The value to be added.</param>
        public void Append(string value)
        {
            string[] v;

            if (isSpecial)
                throw new SipException("Cannot append a header value to the special header [{0}].", name);

            if (value.IndexOf(',') != -1)
                throw new ArgumentException(CommaErrorMsg, "value");

            v = new string[this.values.Length + 1];
            Array.Copy(this.values, 0, v, 0, this.values.Length);
            v[v.Length - 1] = value;

            values = v;
        }

        /// <summary>
        /// Removes the first value in a multi-valued header (if one exists).
        /// </summary>
        public void RemoveFirst()
        {
            if (values.Length == 0)
                return;

            string[] v;

            v = new string[values.Length - 1];
            Array.Copy(values, 1, v, 0, v.Length);
            values = v;
        }

        /// <summary>
        /// Removes the last value in a multi-valued header (if one exists).
        /// </summary>
        public void RemoveLast()
        {
            if (values.Length == 0)
                return;

            string[] v;

            v = new string[values.Length - 1];
            Array.Copy(values, 0, v, 0, v.Length);
            values = v;
        }

        /// <summary>
        /// Returns the name of the header.
        /// </summary>
        public string Name
        {
            get { return name; }
            internal set { name = value; }
        }

        /// <summary>
        /// The header value(s).  Multi-valued headers will return as a comma
        /// separated list.
        /// </summary>
        public string FullText
        {
            get
            {
                if (isSpecial)
                    return values[0];

                var sb = new StringBuilder(128);

                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0)
                        sb.Append(", ");

                    if (values[i].IndexOf(',') != -1)
                        throw new ArgumentException(CommaErrorMsg, "values");

                    sb.Append(values[i]);
                }

                return sb.ToString();
            }

            set
            {
                if (isSpecial)
                    values = new string[] { value };
                else
                    values = Helper.ParseList(value, ',');
            }
        }

        /// <summary>
        /// Accesses the first header value.
        /// </summary>
        /// <remarks>
        /// This property returns the first value in a multi-valued
        /// header.  When set, this property clears all values in
        /// a multi-valued header and assigns the new single value.
        /// </remarks>
        public string Text
        {
            get { return values[0]; }

            set
            {
                if (isSpecial && value.IndexOf(',') != -1)
                    throw new ArgumentException(CommaErrorMsg, "value");

                this.values = new string[] { value };
            }
        }

        /// <summary>
        /// Returns the array of header values.
        /// </summary>
        public string[] Values
        {
            get { return values; }
        }

        /// <summary>
        /// Renders the header into a form suitable for serializing into
        /// a SIP message.
        /// </summary>
        /// <remarks>
        /// <returns>The rendered header.</returns>
        /// <note>Multi-valued headers will be rendered as multiple lines of SIP headers.</note>
        /// <note>Header names will always be rendered using their long forms.</note>
        /// <note>Headers with no values will rendered as an empty string.</note>
        /// </remarks>
        public override string ToString()
        {
            var sb = new StringBuilder(128);

            Serialize(sb, false);
            return sb.ToString();
        }

        /// <summary>
        /// Appends the header onto a <see cref="StringBuilder" /> in a format
        /// suitable for serializing into a SIP message.
        /// </summary>
        /// <param name="sb">The <see cref="StringBuilder" />.</param>
        /// <param name="useCompactForm">Pass <c>true</c> to render header names using the long form.</param>
        /// <remarks>
        /// <note>Multi-valued headers will be rendered as multiple lines of SIP headers.</note>
        /// <note>Headers with no values will not be rendered.</note>
        /// </remarks>
        public void Serialize(StringBuilder sb, bool useCompactForm)
        {
            string      headerName;
            string      compactName;

            if (values.Length == 0)
                return;

            headerName = name;
            if (useCompactForm)
            {
                compactName = SipHelper.GetCompactHeader(headerName);
                if (compactName != null)
                    headerName = compactName;
            }

            if (isSpecial || renderMultiLine.ContainsKey(name))
            {
                // Render as separate header lines

                for (int i = 0; i < values.Length; i++)
                    sb.AppendFormat("{0}: {1}\r\n", headerName, values[i]);
            }
            else
            {
                // Render as comma separated values

                sb.AppendFormat("{0}: ", headerName);
                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0)
                        sb.Append(", ");

                    sb.Append(values[i]);
                }

                sb.Append("\r\n");
            }
        }
    }
}
