//-----------------------------------------------------------------------------
// FILE:        SipAuthorizationValue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Encapsulates a SIP Authorization header value.

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;
using LillTek.Cryptography;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Encapsulates a SIP <b>Authorization</b> header value.
    /// </summary>
    public sealed class SipAuthorizationValue : SipValue
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Implicit cast of a <see cref="SipHeader" />'s first value into a 
        /// <see cref="SipAuthorizationValue" />.
        /// </summary>
        /// <param name="header">The source header <see cref="SipHeader" />.</param>
        /// <returns>The parsed <see cref="SipAuthorizationValue" />.</returns>
        public static implicit operator SipAuthorizationValue(SipHeader header)
        {
            return new SipAuthorizationValue(header.Text);
        }

        /// <summary>
        /// Explicit cast of a string into a <see cref="SipAuthorizationValue" />.
        /// </summary>
        /// <param name="rawText">The raw header text.</param>
        /// <returns>The parsed <see cref="SipAuthorizationValue" />.</returns>
        /// <exception cref="SipException">Thrown if the header text cannot be parsed.</exception>
        public static explicit operator SipAuthorizationValue(string rawText)
        {
            return new SipAuthorizationValue(rawText);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SipAuthorizationValue()
        {
        }

        /// <summary>
        /// Parses an <b>Authorization</b> header value from the raw header text.
        /// </summary>
        /// <param name="rawText">The raw header text.</param>
        public SipAuthorizationValue(string rawText)
            : base(rawText)
        {
            Parse(rawText);
        }

        /// <summary>
        /// Constructs an <b>Authorization</b> header value by computing the
        /// digest as defined by <a href="http://www.ietf.org/rfc/rfc2069.txt?number=2069">RFC 2069</a>
        /// and modified by <a href="http://www.ietf.org/rfc/rfc3261.txt?number=3261">RFC 3261</a>
        /// from the user credentials and <b>WWW-Authenticate</b> header values
        /// passed.
        /// </summary>
        /// <param name="authenticate">The <see cref="SipAuthenticateValue" /> specifying the authentication challenge.</param>
        /// <param name="userName">The user name.</param>
        /// <param name="password">The user's password.</param>
        /// <param name="method">The SIP method exactly as it </param>
        /// <param name="digestUri">The URI of the SIP server entity where the authorization value will be presented.</param>
        public SipAuthorizationValue(SipAuthenticateValue authenticate,
                                     string userName,
                                     string password,
                                     string method,
                                     string digestUri)
        {
            if (String.Compare(authenticate.Algorithm, "MD5") != 0)
                throw new SipException("Unsupported digest algorithm [{0}].", authenticate.Algorithm, true);

            base["algorithm"] = "MD5";      // Algorithm defaults to MD5
            base["username"]  = userName;
            base["realm"]     = authenticate.Realm;
            base["uri"]       = digestUri;
            base["nonce"]     = authenticate.Nonce;

            // Compute the authorization digest.

            string      key;
            byte[]      hash;
            string      ha1;
            string      ha2;
            string      response;

            // Compute: HA1 = H(username + ":" + realm + ":" + password)

            key  = userName + ":" + authenticate.Realm + ":" + password;
            hash = MD5Hasher.Compute(Helper.ToUTF8(key));     // $todo(jeff.lill): Not complety sure of the encoding
            ha1  = Helper.ToHex(hash);

            // Compute: HA2 = H(method + ":" + digesturi)

            key  = method.ToUpper() + ":" + digestUri;
            hash = MD5Hasher.Compute(Helper.ToUTF8(key));     // $todo(jeff.lill): Not complety sure of the encoding
            ha2  = Helper.ToHex(hash);

            // Compute: response = H(HA1 + ":" + nonce + ":" + HA2) 

            key      = ha1 + ":" + authenticate.Nonce + ":" + ha2;
            hash     = MD5Hasher.Compute(Helper.ToUTF8(key));     // $todo(jeff.lill): Not complety sure of the encoding
            response = Helper.ToHex(hash);

            base["response"] = response;
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
                throw new SipException("Bad [Authorization] header: Value must begin with [Digest].");

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
                        throw new SipException("Bad [Authorization] header: Missing quote in field [{0}].", name);

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
        /// The user's account name.
        /// </summary>
        public string UserName
        {
            get { return base["username"]; }
            set { base["username"] = value; }
        }

        /// <summary>
        /// The URI of the SIP server entity where the authorization value will be presented. 
        /// </summary>
        public string DigestUri
        {
            get { return base["uri"]; }
            set { base["uri"] = value; }
        }

        /// <summary>
        /// The authentication challenge response.
        /// </summary>
        public string Response
        {
            get { return base["response"]; }
            set { base["response"] = value; }
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
        /// The digest used for protecting against tampering with the SIP message.
        /// </summary>
        public string Digest
        {
            get { return base["digest"]; }
            set { base["digest"] = value; }
        }

        /// <summary>
        /// Specifies the algorithm to be used to generate the digest.
        /// Currently only <b>MD5</b> is defined in RFC 3261.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This field doesn't appear in RFC 2069 but I'm seeing it in some
        /// of the network traces, so I'm going to add this just to be safe.
        /// This defaults to <b>MD5</b>.
        /// </note>
        /// </remarks>
        public string Algorithm
        {
            get { return base["algorithm"]; }
            set { base["algorithm"] = value.ToUpper(); }
        }

        /// <summary>
        /// Specifies the realm displayed to the user.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This field doesn't appear in RFC 2069 but I'm seeing it in some
        /// of the network traces, so I'm going to add this just to be safe.
        /// </note>
        /// </remarks>
        public string Realm
        {
            get { return base["realm"]; }
            set { base["realm"] = value.ToUpper(); }
        }

        /// <summary>
        /// Appends the value of the specified field to the <see cref="StringBuilder" />
        /// if the field is present.
        /// </summary>
        /// <param name="sb">The ouput <see cref="StringBuilder" />.</param>
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
            AppendField(sb, "username", false, ref first);
            AppendField(sb, "realm", true, ref first);
            AppendField(sb, "nonce", true, ref first);
            AppendField(sb, "uri", true, ref first);
            AppendField(sb, "response", true, ref first);
            AppendField(sb, "digest", true, ref first);
            AppendField(sb, "algorithm", false, ref first);
        }
    }
}
