//-----------------------------------------------------------------------------
// FILE:        SipValue.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the value of a SIP header.

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Describes the value of a SIP header by decomposing a raw text value
    /// into its constituent text and optional parameters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A SIP header value consists of a textual value and zero or more
    /// name/value parameters.  Here's an example:
    /// </para>
    /// <code language="none">
    /// Via: SIP/2.0/UDP pc33.atlanta.com;branch=z9hG4bK776asdhds;maddr=206.0.1.27
    /// </code>
    /// <para>
    /// The textual value in this case is "<b>SIP/2.0/UDP pc33.atlanta.com</b>" and
    /// the header value includes two parameters <b>branch</b> and <b>maddr</b>.
    /// </para>
    /// </remarks>
    public class SipValue
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Implicit cast of a <see cref="SipValue" /> into a string.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The value rendered as a string.</returns>
        public static implicit operator string(SipValue value)
        {
            return value.ToString();
        }

        /// <summary>
        /// Explicit cast of a string into a <see cref="SipValue" />.
        /// </summary>
        /// <param name="rawText">The raw header text.</param>
        /// <returns>The parsed <see cref="SipValue" />.</returns>
        /// <exception cref="SipException">Thrown if the header text cannot be parsed.</exception>
        public static explicit operator SipValue(string rawText)
        {
            return new SipValue(rawText);
        }

        //---------------------------------------------------------------------
        // Instance members

        private string                      text;
        private Dictionary<string, string>  args;

        /// <summary>
        /// Instantiates an empty <see cref="SipValue" /> with empty text or arguments.
        /// </summary>
        public SipValue()
        {
            text = string.Empty;
            args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Instantiates a <see cref="SipValue" /> by parsing the raw text
        /// passed for the text and optional parameters.
        /// </summary>
        /// <param name="rawText">The raw value text.</param>
        public SipValue(string rawText)
        {
            Parse(rawText);
        }

        /// <summary>
        /// Parses the value from the text passed.
        /// </summary>
        /// <param name="rawText">The input text.</param>
        internal virtual void Parse(string rawText)
        {
            int         p;
            string[]    arr;

            args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // The first step is to separate the value text into the value portion
            // and the header parameters portion.  In simple terms, this means looking
            // for the first ';' character (if there is one) since this demarcs the
            // beginning of the parameters section.
            //
            // Real life is more complicated than this though since we need to 
            // handle headers such as From which might look like:
            //
            //     From: "Jeff \"The Lill\"" <jeff@lilltek.com;transport=tcp>;q=10
            //
            // where we have a double quoted display name and an angle-bracket quoted
            // SIP URI with embedded semicolons used for URI parameters.

            // Scan for the parameter demarc and put its character index in p or
            // set p=-1 if there doesn't appear to be any parameters.  Note that
            // I'm not going to flag unbalanced quotes or angle-brackets as errors
            // here, leaving this to higher level code that actually understands
            // specific header values.

            p = 0;
            while (true)
            {
            continueMain:

                if (p == rawText.Length)
                {
                    p = -1;
                    break;
                }

                switch (rawText[p])
                {
                    case '"':  // Handle double-quoted strings

                        while (true)
                        {
                            p++;
                            if (p >= rawText.Length)
                            {
                                p = -1;
                                goto gotDemarc;
                            }

                            switch (rawText[p])
                            {
                                case '"':  // Closing double quote

                                    p++;
                                    goto continueMain;

                                case '\\': // Escaped character

                                    p++;
                                    if (p >= rawText.Length)
                                    {

                                        p = -1;
                                        goto gotDemarc;
                                    }

                                    p++;
                                    continue;
                            }
                        }

                    case '<':  // Handle angle-bracket quoted URIs

                        while (true)
                        {
                            p++;
                            if (p >= rawText.Length)
                            {
                                p = -1;
                                goto gotDemarc;
                            }

                            if (rawText[p] == '>')
                            {
                                p++;
                                goto continueMain;
                            }
                        }

                    case ';':

                        goto gotDemarc;

                    default:

                        p++;
                        break;
                }
            }

        gotDemarc:

            if (p == -1)
            {
                // No parameters

                text = rawText;
                return;
            }

            // Extract the value text

            text = rawText.Substring(0, p);

            // Parse the parameters

            arr = Helper.ParseList(rawText.Substring(p + 1), ';');
            for (int i = 0; i < arr.Length; i++)
            {
                string      arg = arr[i].Trim();
                string      name;
                string      value;

                p = arg.IndexOf('=');
                if (p == -1)
                {
                    // There's no equal sign so assume that what we have is
                    // a parameter name with a blank value.

                    if (arg.Length > 0)
                        args.Add(arg, string.Empty);

                    continue;
                }

                name  = arg.Substring(0, p).Trim();
                value = arg.Substring(p + 1);

                if (name.Length == 0)
                    continue;

                args.Add(name, value);
            }
        }

        /// <summary>
        /// The value text.
        /// </summary>
        public string Text
        {
            get { return text; }
            set { text = value; }
        }

        /// <summary>
        /// The value as an integer.
        /// </summary>
        public int IntValue
        {
            get { return int.Parse(text); }
            set { text = value.ToString(); }
        }

        /// <summary>
        /// Returns the collection of value parameters.
        /// </summary>
        public Dictionary<string, string> Parameters
        {
            get { return args; }
        }

        /// <summary>
        /// This indexer is used to set and get specific value parameter
        /// values.
        /// </summary>
        /// <param name="name">The parameter name (case insensitive).</param>
        /// <returns>The value.</returns>
        /// <remarks>
        /// When getting a parameter value for a parameter that does not
        /// exist, the indexer will return <c>null</c>.  Setting a <c>null</c>
        /// value will delete the parameter.
        /// </remarks>
        public string this[string name]
        {
            get
            {
                string value;

                if (args.TryGetValue(name, out value))
                    return value;

                return null;
            }

            set
            {
                if (value == null)
                {
                    if (args.ContainsKey(name))
                        args.Remove(name);

                    return;
                }

                args[name] = value;
            }
        }

        /// <summary>
        /// Renders the value into a string suitable as the header
        /// value for serializtion into a SIP message.
        /// </summary>
        /// <returns>The rendered value.</returns>
        public override string ToString()
        {
            if (args.Count == 0)
                return text;

            var sb = new StringBuilder(128);

            Serialize(sb);
            return sb.ToString();
        }

        /// <summary>
        /// Appends the value to a <see cref="StringBuilder" /> in a format
        /// suitable as the header value for serializtion into a SIP message.
        /// </summary>
        /// <param name="sb">The <see cref="StringBuilder" />.</param>
        public virtual void Serialize(StringBuilder sb)
        {
            sb.Append(text);

            foreach (string name in args.Keys)
            {
                string v = args[name];

                sb.Append(';');
                if (v == string.Empty)
                    sb.AppendFormat("{0}", name);
                else
                    sb.AppendFormat("{0}={1}", name, v);
            }
        }
    }
}
