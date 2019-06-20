//-----------------------------------------------------------------------------
// FILE:        SipAuthenticateValue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Encapsulates a SIP WWW-Authenticate header value.

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;
using LillTek.Cryptography;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Encapsulates a SIP <b>WWW-Authenticate</b> header value.
    /// </summary>
    public sealed class SipAuthenticateValue : SipValue
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Implicit cast of a <see cref="SipHeader" />'s first value into a 
        /// <see cref="SipAuthenticateValue" />.
        /// </summary>
        /// <param name="header">The source header <see cref="SipHeader" />.</param>
        /// <returns>The parsed <see cref="SipAuthenticateValue" />.</returns>
        public static implicit operator SipAuthenticateValue(SipHeader header)
        {
            return new SipAuthenticateValue(header.Text);
        }

        /// <summary>
        /// Explicit cast of a string into a <see cref="SipAuthenticateValue" />.
        /// </summary>
        /// <param name="rawText">The raw header text.</param>
        /// <returns>The parsed <see cref="SipAuthenticateValue" />.</returns>
        /// <exception cref="SipException">Thrown if the header text cannot be parsed.</exception>
        public static explicit operator SipAuthenticateValue(string rawText)
        {
            return new SipAuthenticateValue(rawText);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SipAuthenticateValue()
        {
        }

        /// <summary>
        /// Parses a <b>WWW-Authenticate</b> header from the raw header text.
        /// </summary>
        /// <param name="rawText">The raw header text.</param>
        public SipAuthenticateValue(string rawText)
            : base()
        {
            Parse(rawText);
        }

        /// <summary>
        /// Parses the value from the text passed.
        /// </summary>
        /// <param name="rawText">The input text.</param>
        internal override void Parse(string rawText)
        {
            base.Parse(rawText);

            int         p, pEnd;
            string      name;
            string      value;

            base.Text = rawText;

            rawText = rawText.Trim();
            if (!rawText.ToUpper().StartsWith("DIGEST"))
                throw new SipException("Bad [WWW-Authenticate] header: Value must begin with [Digest].");

            base["algorithm"] = "MD5";  // Algorithm defaults to MD5

            p = 6;  // Skip past the "Digest"
            while (p < rawText.Length)
            {
                pEnd = rawText.IndexOf('=', p);
                if (pEnd == -1)
                    return;     // This has the effect of ignoring malformed fields without "=" signs

                name = rawText.Substring(p, pEnd - p).Trim();

                p = pEnd + 1;
                if (p >= rawText.Length)
                {
                    base[name] = string.Empty;
                    break;
                }

                if (Char.IsWhiteSpace(rawText[p]))
                {
                    base[name] = string.Empty;
                    p++;
                    continue;
                }

                if (rawText[p] == '"')
                {
                    // Quoted value

                    p++;
                    pEnd = rawText.IndexOf('"', p);
                    if (pEnd == -1)
                        throw new SipException("Bad [WWW-Authenticate] header: Missing quote in field [{0}].", name);

                    value = rawText.Substring(p, pEnd - p);
                    p = pEnd + 1;
                }
                else
                {
                    // Nonquoted value

                    pEnd = rawText.IndexOf(',', p);
                    if (pEnd == -1)
                    {
                        value = rawText.Substring(p);
                        p = rawText.Length;
                    }
                    else
                    {
                        value = rawText.Substring(p, pEnd - p);
                        p = pEnd;
                    }
                }

                base[name] = value;

                if (p < rawText.Length && rawText[p] != ',')
                    break;

                p++;
            }
        }

        /// <summary>
        /// The realm value which may be used to display to users
        /// so thay can identify which account and password to use.
        /// </summary>
        public string Realm
        {
            get { return base["realm"]; }
            set { base["realm"] = value; }
        }

        /// <summary>
        /// A comma separated list of URIs which specify the set of SIP
        /// endpoints for which the same authentication information may
        /// be sent.
        /// </summary>
        /// <remarks>
        /// SIP clients may choose to cache this list so that subsequent
        /// requests to any of these URIs may be prepopulated with the
        /// correct <b>Authorization</b> header.
        /// </remarks>
        public string Domain
        {
            get { return base["domain"]; }
            set { base["domain"] = value; }
        }

        /// <summary>
        /// The server generated challenge value to be included in the
        /// digest that will ultimately be added to the <b>Authorization</b>
        /// header when the request is resent to the server.
        /// </summary>
        public string Nonce
        {
            get { return base["nonce"]; }
            set { base["nonce"] = value; }
        }

        /// <summary>
        /// Opaque string generated by the server which must be included
        /// in the <b>Authorization</b> header when the request is resent
        /// to the server.
        /// </summary>
        public string Opaque
        {
            get { return base["opaque"]; }
            set { base["opaque"] = value; }
        }

        /// <summary>
        /// <c>true</c> if the request was rejected by the server because the 
        /// <b>Nonce</b> value included in the <b>Authorization</b>
        /// header was stale.
        /// </summary>
        public bool Stale
        {
            get { return base["stale"].ToLowerInvariant() == "true"; }
            set { base["stale"] = value ? "true" : "false"; }
        }

        /// <summary>
        /// Specifies the algorithm to be used to generate the digest.
        /// Currently only <b>MD5</b> is defined in RFC 3261.
        /// </summary>
        public string Algorithm
        {
            get { return base["algorithm"]; }
            set { base["algorithm"] = value.ToUpper(); }
        }

        /// <summary>
        /// Appends the value of the specified field to the <see cref="StringBuilder" />
        /// if the field is present.
        /// </summary>
        /// <param name="sb">The output <see cref="StringBuilder" />.</param>
        /// <param name="name">Name of the field.</param>
        /// <param name="quote">Pass <c>true</c> if the value should be quoted.</param>
        /// <param name="first">Indicates whether this is the first field appended.</param>
        private void AppendField(StringBuilder sb, string name, bool quote, ref bool first)
        {
            string value;

            value = base[name];
            if (value == null)
                return;

            if (first)
                first = false;
            else
                sb.Append(", ");

            if (quote)
                sb.AppendFormat("{0}=\"{1}\"", name, value);
            else
                sb.AppendFormat("{0}={1}", name, value);
        }

        /// <summary>
        /// Serializes the header value into a form suitable for including
        /// in a SIP WWW-Authenticate message header.
        /// </summary>
        public override void Serialize(StringBuilder sb)
        {
            bool first = true;

            sb.Append("Digest ");
            AppendField(sb, "algorithm", false, ref first);
            AppendField(sb, "realm", true, ref first);
            AppendField(sb, "domain", false, ref first);
            AppendField(sb, "nonce", true, ref first);
            AppendField(sb, "opaque", true, ref first);
            AppendField(sb, "stale", false, ref first);
        }
    }
}
