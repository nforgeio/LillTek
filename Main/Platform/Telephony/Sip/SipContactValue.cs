//-----------------------------------------------------------------------------
// FILE:        SipContactValue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Encapsulates header fields that specify a URI and an
//              optional display name, handling all of the quoting issues.

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;
using LillTek.Cryptography;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Encapsulates header fields that specify a URI and an
    /// optional display name, handling all of the quoting issues.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is useful for the following standard SIP headers: <b>To</b>,
    /// <b>From</b>, <b>Contact</b>, <b>Record-Route</b>, <b>Reply-To</b>,
    /// and <b>Route</b>.
    /// </para>
    /// <note>
    /// This class normalizes the value text such that display names are
    /// quoted if present and URIs are quoted with angle brackets.
    /// </note>
    /// </remarks>
    public sealed class SipContactValue : SipValue
    {
        //---------------------------------------------------------------------
        // Static members

        private static char[] lwsOrLAngle = new char[] { ' ', '\t', '<' };
        private static char[] angles      = new char[] { '<', '>' };
        private static char[] esc         = new char[] { '\\', '"' };

        /// <summary>
        /// Implicit cast of a <see cref="SipHeader" />'s first value into a 
        /// <see cref="SipContactValue" />.
        /// </summary>
        /// <param name="header">The source header <see cref="SipHeader" />.</param>
        /// <returns>The parsed <see cref="SipContactValue" />.</returns>
        /// <exception cref="SipException">Thrown if the header text cannot be parsed.</exception>
        public static implicit operator SipContactValue(SipHeader header)
        {
            return new SipContactValue(header.Text);
        }

        /// <summary>
        /// Implicit cast of a <see cref="SipUri" /> into a  <see cref="SipContactValue" />.
        /// </summary>
        /// <param name="uri">The source <see cref="SipUri" />.</param>
        /// <returns>The equivalent <see cref="SipContactValue" />.</returns>
        public static implicit operator SipContactValue(SipUri uri)
        {
            return new SipContactValue(null, (string)uri);
        }

        /// <summary>
        /// Explicit cast of a string into a <see cref="SipContactValue" />.
        /// </summary>
        /// <param name="rawText">The raw header text.</param>
        /// <returns>The parsed <see cref="SipContactValue" />.</returns>
        /// <exception cref="SipException">Thrown if the header text cannot be parsed.</exception>
        public static explicit operator SipContactValue(string rawText)
        {
            return new SipContactValue(rawText);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SipContactValue()
        {
        }

        /// <summary>
        /// Parses a URI with optional name header value from the raw header text.
        /// </summary>
        /// <param name="rawText">The raw header text.</param>
        /// <exception cref="SipException">Thrown if the header text cannot be parsed.</exception>
        public SipContactValue(string rawText)
            : base(rawText)
        {
            string displayName;
            string uri;

            GetProps(out displayName, out uri);
            SetText(displayName, uri);
        }

        /// <summary>
        /// Constructs a value from a URI and an optional display name.
        /// </summary>
        /// <param name="displayName">The display name (or <c>null</c>).</param>
        /// <param name="uri">The URI.</param>
        /// <exception cref="SipException">Thrown if the header text cannot be parsed.</exception>
        public SipContactValue(string displayName, string uri)
            : base()
        {
            SetText(displayName, uri);
        }

        /// <summary>
        /// Parses the value from the text passed.
        /// </summary>
        /// <param name="rawText">The input text.</param>
        internal override void Parse(string rawText)
        {
            base.Parse(rawText);

            string displayName;
            string uri;

            GetProps(out displayName, out uri);
            SetText(displayName, uri);
        }

        /// <summary>
        /// Renders the URI and an optional display name into the
        /// value's Text property.
        /// </summary>
        /// <param name="displayName">The display name (or <c>null</c>).</param>
        /// <param name="uri">The URI.</param>
        private void SetText(string displayName, string uri)
        {
            var sb = new StringBuilder(64);

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                // $todo(jeff.lill): I'm only going to worry about escaping double quotes and
                //               the backslash characters for now.  Is this enough?

                string escaped;

                if (displayName.IndexOfAny(esc) != -1)
                {
                    var sbEsc = new StringBuilder(displayName.Length + 10);

                    for (int i = 0; i < displayName.Length; i++)
                    {
                        char ch = displayName[i];

                        if (ch == '\\' || ch == '"')
                            sbEsc.Append('\\');

                        sbEsc.Append(ch);
                    }

                    escaped = sbEsc.ToString();
                }
                else
                    escaped = displayName;

                sb.AppendFormat("\"{0}\"", escaped);
            }

            sb.AppendFormat("<{0}>", uri);

            base.Text = sb.ToString();
        }

        /// <summary>
        /// Parses the value's Text property into the URI and optional
        /// display name.
        /// </summary>
        /// <param name="displayName">Returns as the parsed display name (or <c>null</c>).</param>
        /// <param name="uri">Returns as the parsed URI.</param>
        /// <exception cref="SipException">Thrown if the header text cannot be parsed.</exception>
        private void GetProps(out string displayName, out string uri)
        {
            const string BadContactMsg = "Cannot parse display name and URI.";

            string  text;
            string  rawUri;
            int     p;

            displayName = null;
            uri = string.Empty;

            // Here's how I'm going to do this:
            //
            //      1. Trim the base value text on both ends.
            //
            //      2. If the first character is a double quote then
            //         we have a quoted display name and a URI
            //
            //      3. If the first character is not a double quote
            //         then scan the string for an "<".  If we find
            //         one, we have a quoted URI potentially with
            //         a leading display name.
            //
            //      4. If the first character is not a double quote
            //         and there was no "<" rhen scan the string for 
            //         whitespace.  If we find any then we have an 
            //         unquoted display name and a URI and the whitespace 
            //         marks the boundry.
            //
            //      4. If there is no whitespace or angle bracket then
            //         we must have an unquoted URI and no display name.

            text = base.Text.Trim();
            if (text.Length == 0)
                throw new SipException(BadContactMsg);

            // Determine if there's a display name then parse it
            // as necessary

            if (text[0] == '"')
            {
                var sb = new StringBuilder(32);

                p = 1;
                while (true)
                {
                    if (p >= text.Length)
                        throw new SipException(BadContactMsg);

                    switch (text[p])
                    {
                        case '"':

                            p++;
                            goto gotDisplayName;

                        case '\\':

                            p++;
                            if (p >= text.Length)
                                throw new SipException(BadContactMsg);

                            sb.Append(text[p++]);
                            break;

                        default:

                            sb.Append(text[p++]);
                            break;
                    }
                }

            gotDisplayName:

                displayName = sb.ToString();
                rawUri = text.Substring(p).Trim();
            }
            else
            {
                p = text.IndexOfAny(lwsOrLAngle);
                if (p == -1)
                    rawUri = text;
                else
                {
                    if (text[p] != '<')
                    {
                        // Favor an angle bracket as the separator over whitespace
                        // if an angle bracket is present.  I don't believe that
                        // this is actually part of the SIP URI specification but
                        // Microsoft Speech Server generates URIs with unquoted
                        // display names with embedded blanks in some situations.

                        int pAngle = text.IndexOf('<', p);

                        if (pAngle != -1)
                            p = pAngle;
                    }

                    displayName = text.Substring(0, p).Trim();
                    if (displayName.Length == 0)
                        displayName = null;

                    if (text[p] == '<')
                        rawUri = text.Substring(p).Trim();
                    else
                        rawUri = text.Substring(p + 1).Trim();
                }
            }

            // rawUri now holds the URI.  Strip off any angle quotes.

            if (rawUri.Length == 0)
                throw new SipException(BadContactMsg);

            if (rawUri[0] == '<')
            {
                if (rawUri[rawUri.Length - 1] != '>')
                    throw new SipException(BadContactMsg);

                uri = rawUri.Substring(1, rawUri.Length - 2);
            }
            else
                uri = rawUri;

            if (uri.IndexOfAny(angles) != -1)
                throw new SipException(BadContactMsg);
        }

        /// <summary>
        /// The contact display name (or <c>null</c>).
        /// </summary>
        public string DisplayName
        {
            get
            {
                string uri;
                string displayName;

                GetProps(out displayName, out uri);
                return displayName;
            }

            set
            {
                string uri;
                string displayName;

                GetProps(out displayName, out uri);
                SetText(value, uri);
            }
        }

        /// <summary>
        /// The contact URI.
        /// </summary>
        public string Uri
        {
            get
            {
                string uri;
                string displayName;

                GetProps(out displayName, out uri);
                return uri;
            }

            set
            {
                string uri;
                string displayName;

                GetProps(out displayName, out uri);
                SetText(displayName, value);
            }
        }
    }
}
