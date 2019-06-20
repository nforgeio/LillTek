//-----------------------------------------------------------------------------
// FILE:        SipHeaderCollection.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Manages SIP message headers.

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;

// $todo(jeff.lill): 
//
// The SIP header collection doesn't completely support RFC 3261.
// This implementation assumes that multiple headers with the same
// name can always be collected together into a single SipHeader
// instance by setting the header text to a comma separated list
// values.  This doesn't work for some headers like WWW-Authenticate
// which for some crazy reason, allow commas in header values.  I
// deal with this situation in SipHeader by implementing the concept
// of a special header that allows commas but which isn't multi-valued.
//
// The consequence of this is that this implementation will not allow
// multiple instances of a given special header in a message, which
// is allowed by the RFC.  I don't expect to see messages like this
// in real life, so I'm not going to worry about this for now.
//
// The ultimate solution is probably by modifying SipHeader so that
// it can encode multiple special headers without using a comma
// separated list.

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// A collection of <see cref="SipHeader" /> instances keyed by case insensitive 
    /// header name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The header collection used used to map SIP header names to <see cref="SipHeader" />
    /// instances.  Multi-valued headers are managed by the <see cref="SipHeader" />
    /// class so this class maintains just a one-to-one mapping of header names to
    /// header instances.
    /// </para>
    /// <para>
    /// The SIP specification provides for a compact form of some header names to
    /// reduce the size of SIP messages for transmission over size restricted
    /// protocols such as UDP. <see cref="SipHeaderCollection" /> supports this
    /// by converting compact header names into the equivalent long form before
    /// adding or referencing headers in the collection.  The <see cref="HasCompactHeaders" />
    /// property will return <c>true</c> if any compact form headers have been added
    /// to the collection since it was created.
    /// </para>
    /// <para>
    /// The <see cref="Serialize" /> and <see cref="ToString" /> methods will
    /// render headers using the long form by default.  But, if any compact headers
    /// were added to the collection, these methods will render <b>all</b> headers
    /// with compact forms as compact.  Here are the long to compact header names
    /// defined by RFC 3261 and supported by this stack:
    /// </para>
    /// <code language="none">
    /// Long Form         Short
    /// -----------------------
    /// Call-ID             i
    /// Contact             m
    /// Content-Encoding    e
    /// Content-Length      l
    /// Content-Type        c
    /// From                f
    /// Subject             s
    /// Supported           k
    /// To                  t
    /// Via                 v
    /// </code>
    /// </remarks>
    public sealed class SipHeaderCollection : Dictionary<string, SipHeader>
    {
        //-----------------------------------------------------------
        // Static members

        private static Dictionary<string, bool> specialHeaders;
        private static Dictionary<string, bool> priorityHeaders;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static SipHeaderCollection()
        {
            specialHeaders = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            specialHeaders.Add(SipHeader.WWWAuthenticate, true);
            specialHeaders.Add(SipHeader.Authorization, true);
            specialHeaders.Add(SipHeader.ProxyAuthenticate, true);
            specialHeaders.Add(SipHeader.ProxyAuthorization, true);
            specialHeaders.Add(SipHeader.Date, true);
            specialHeaders.Add(SipHeader.Subject, true);
            specialHeaders.Add(SipHeader.Supported, true);
            specialHeaders.Add(SipHeader.Unsupported, true);
            specialHeaders.Add(SipHeader.Require, true);
            specialHeaders.Add(SipHeader.UserAgent, true);

            priorityHeaders = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            priorityHeaders.Add(SipHeader.Via, true);
            priorityHeaders.Add(SipHeader.Route, true);
            priorityHeaders.Add(SipHeader.RecordRoute, true);
            priorityHeaders.Add(SipHeader.ProxyRequire, true);
            priorityHeaders.Add(SipHeader.MaxForwards, true);
            priorityHeaders.Add(SipHeader.ProxyAuthorization, true);
        }

        //-----------------------------------------------------------
        // Instance members

        private bool hasCompactHeaders = false;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SipHeaderCollection()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        /// <summary>
        /// Returns <c>true</c> if one or more headers with compact form
        /// names have been added to the collection.
        /// </summary>
        public bool HasCompactHeaders
        {
            get { return hasCompactHeaders; }
        }

        /// <summary>
        /// Adds a name/value pair to the collection, handling multi-value and
        /// special headers.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <returns>
        /// The <see cref="SipHeader" /> instance that actually is actually present collection.
        /// </returns>
        public SipHeader Add(string name, string value)
        {
            SipHeader   header;
            string      longForm;

            longForm = SipHelper.GetLongHeader(name);
            if (longForm != null)
            {
                hasCompactHeaders = true;
                name              = longForm;
            }

            if (specialHeaders.ContainsKey(name))
            {
                if (this.ContainsKey(name))
                    throw new NotImplementedException(string.Format("LillTek SIP stack does not support multiple instances of the header [{0}].", name));

                this.Add(name, header = new SipHeader(name, value, true));
                return header;
            }

            if (value.IndexOf(',') != -1)
            {
                // We have a multi-valued header.

                var values = value.Split(',');

                for (int i = 0; i < values.Length; i++)
                    values[i] = values[i].Trim();

                if (this.TryGetValue(name, out header))
                {
                    for (int i = 0; i < values.Length; i++)
                        header.Append(values[i]);

                    return header;
                }
                else
                {
                    this.Add(name, header = new SipHeader(name, values));
                    return header;
                }
            }
            else
            {
                // Single value header.

                if (this.TryGetValue(name, out header))
                {
                    header.Append(value);
                    return header;
                }
                else
                {
                    this.Add(name, header = new SipHeader(name, value));
                    return header;
                }
            }
        }

        /// <summary>
        /// Appends a name/value pair to the collection, handling multi-value and
        /// special headers.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <returns>
        /// The <see cref="SipHeader" /> instance that actually is actually present collection.
        /// </returns>
        /// <remarks>
        /// <note>This is equivalent to calling <see cref="Add(string,string)" />.</note>
        /// </remarks>
        public SipHeader Append(string name, string value)
        {
            return Add(name, value);
        }

        /// <summary>
        /// Prepends a name/value pair to the collection, handling multi-value and
        /// special headers.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <returns>
        /// The <see cref="SipHeader" /> instance that actually is actually present collection.
        /// </returns>
        /// <remarks>
        /// <note>The value added will be inserted <b>before</b> any existing values for this header.</note>
        /// </remarks>
        public SipHeader Prepend(string name, string value)
        {
            SipHeader   header;
            string      longForm;

            longForm = SipHelper.GetLongHeader(name);
            if (longForm != null)
            {
                hasCompactHeaders = true;
                name              = longForm;
            }

            if (specialHeaders.ContainsKey(name))
            {
                if (this.ContainsKey(name))
                    throw new NotImplementedException("LillTek SIP stack does not currently support multiple instances of special headers.");

                this.Add(name, header = new SipHeader(name, value, true));
                return header;
            }

            if (this.TryGetValue(name, out header))
            {
                header.Prepend(value);
                return header;
            }
            else
            {
                this.Add(name, header = new SipHeader(name, value));
                return header;
            }
        }

        /// <summary>
        /// Adds a name/header pair to the collection.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="header">The header.</param>
        /// <remarks>
        /// <note>
        /// This method will convert the header's name property
        /// to its long form if necessary.
        /// </note>
        /// </remarks>
        public new void Add(string name, SipHeader header)
        {
            string longForm;

            longForm = SipHelper.GetLongHeader(name);
            if (longForm != null)
            {
                hasCompactHeaders = true;
                name              = longForm;
                header.Name       = longForm;
            }

            base.Add(name, header);
        }

        /// <summary>
        /// References the named <see cref="SipHeader" /> if it's present in the collection.
        /// </summary>
        /// <param name="name">Case insensitive name of the desired header.</param>
        /// <returns>The header instance if one exists, <c>null</c> otherwise.</returns>
        public new SipHeader this[string name]
        {
            get
            {
                SipHeader   header;
                string      longForm;

                longForm = SipHelper.GetLongHeader(name);
                if (longForm != null)
                    name = longForm;

                if (this.TryGetValue(name, out header))
                    return header;
                else
                    return null;
            }

            set
            {
                string longForm;

                longForm = SipHelper.GetLongHeader(name);
                if (longForm != null)
                {
                    name = longForm;
                    if (!base.ContainsKey(name))
                        hasCompactHeaders = true;
                }

                base[name] = value;
            }
        }

        /// <summary>
        /// Removes a header from the collection if it exists.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <remarks>
        /// <note>
        /// No exception will be thrown if the header doesn't exist.
        /// </note>
        /// </remarks>
        public new void Remove(string name)
        {
            if (base.ContainsKey(name))
                base.Remove(name);
        }

        /// <summary>
        /// Renders the collection into a string suitable for serializing into
        /// a SIP message.
        /// </summary>
        /// <returns>The formatted output.</returns>
        /// <remarks>
        /// <note>The string returned includes the empty line that terminates the header section.</note>
        /// </remarks>
        public override string ToString()
        {
            var sb = new StringBuilder(2048);

            Serialize(sb);
            return sb.ToString();
        }

        /// <summary>
        /// Serializes the headers (including the terminating blank line) to the
        /// <see cref="StringBuilder" /> passed.
        /// </summary>
        /// <param name="sb">The output string builder.</param>
        public void Serialize(StringBuilder sb)
        {
            SipHeader header;

            // Output the priority headers first so they'll be at the top
            // of the message, potentially speeding proxy performance.

            if (this.TryGetValue(SipHeader.Via, out header))
                header.Serialize(sb, hasCompactHeaders);

            if (this.TryGetValue(SipHeader.Route, out header))
                header.Serialize(sb, hasCompactHeaders);

            if (this.TryGetValue(SipHeader.RecordRoute, out header))
                header.Serialize(sb, hasCompactHeaders);

            if (this.TryGetValue(SipHeader.ProxyRequire, out header))
                header.Serialize(sb, hasCompactHeaders);

            if (this.TryGetValue(SipHeader.MaxForwards, out header))
                header.Serialize(sb, hasCompactHeaders);

            if (this.TryGetValue(SipHeader.ProxyAuthorization, out header))
                header.Serialize(sb, hasCompactHeaders);

            // Output the remaining headers.

            foreach (string name in this.Keys)
                if (!priorityHeaders.ContainsKey(name))
                    this[name].Serialize(sb, hasCompactHeaders);

            sb.Append("\r\n");
        }
    }
}
