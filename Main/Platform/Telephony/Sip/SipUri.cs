//-----------------------------------------------------------------------------
// FILE:        SipUri.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Encapsulates a SIP or SIPS URI.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Encapsulates a SIP or SIPS URI.
    /// </summary>
    /// <remarks>
    /// This class implements several constructors.  Unless otherwise indicated, these
    /// constructors will default to creating a UDP URI.  Constructors that are passed
    /// a <see cref="SipTransportType.TLS" /> transport type will create a <i>secure</i>
    /// URI.  Constructors that are passed a <see cref="SipTransportType.TCP" /> transport type
    /// will generate a URI with a <b>transport=tcp</b> parameter.
    /// </remarks>
    public class SipUri
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Explicitly casts a <see cref="SipUri" /> into a string.
        /// </summary>
        /// <param name="uri">The <see cref="SipUri" />.</param>
        /// <returns>The rendered string.</returns>
        public static explicit operator string(SipUri uri)
        {
            if (uri == null)
                return null;

            return uri.ToString();
        }

        /// <summary>
        /// Explicitly casts a string into a <see cref="SipUri" />.
        /// </summary>
        /// <param name="uri">The URI string.</param>
        /// <returns>The rendered string.</returns>
        public static explicit operator SipUri(string uri)
        {
            if (uri == null)
                return null;

            return new SipUri(uri);
        }

        /// <summary>
        /// Attempts to parse a SIP URI from a string.
        /// </summary>
        /// <param name="text">The input text.</param>
        /// <param name="uri">The output <see cref="SipUri" />.</param>
        /// <returns><c>true</c> if the URI was parsed successfully.</returns>
        public static bool TryParse(string text, out SipUri uri)
        {
            uri = new SipUri();

            if (uri.Parse(text))
                return true;
            else
            {
                uri = null;
                return false;
            }
        }

        //---------------------------------------------------------------------
        // Instance members
        
        private bool                isSecure   = false;
        private string              user       = null;
        private string              host       = null;
        private int                 port       = 0;
        Dictionary<string, string>  parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        List<SipHeader>             headers    = new List<SipHeader>();

        /// <summary>
        /// Constructs an empty SIP URI. 
        /// </summary>
        public SipUri()
        {
        }

        /// <summary>
        /// Parses the SIP URI string passed.
        /// </summary>
        /// <param name="text">The URI string.</param>
        public SipUri(string text)
        {
            if (!Parse(text))
                throw new SipException("Invalid SIP URI: [{0}]", text);
        }

        /// <summary>
        /// Constructs a SIP URI from the parameters passed.
        /// </summary>
        /// <param name="user">The user (or <c>null</c>).</param>
        /// <param name="host">The host.</param>
        public SipUri(string user, string host)
        {
            this.user = user;
            this.host = host;
            this.port = 0;
        }

        /// <summary>
        /// Constructs a SIP URI from the parameters passed.
        /// </summary>
        /// <param name="user">The user (or <c>null</c>).</param>
        /// <param name="host">The host.</param>
        /// <param name="port">The port (or <b>0</b>).</param>
        public SipUri(string user, string host, int port)
        {
            this.user = user;
            this.host = host;
            this.port = port;
        }

        /// <summary>
        /// Constructs a SIP URI from the parameters passed.
        /// </summary>
        /// <param name="transportType">The transport type.</param>
        /// <param name="user">The user (or <c>null</c>).</param>
        /// <param name="host">The host.</param>
        /// <param name="port">The port (or <b>0</b>).</param>
        /// <remarks>
        /// Passing <paramref name="transportType"/> as <see cref="SipTransportType.TLS" />
        /// will result in a <i>secure</i> URI.  Passing <paramref name="transportType"/> as 
        /// <see cref="SipTransportType.TCP" /> will return a URI with a <b>transport=tcp</b>
        /// parameter.
        /// </remarks>
        public SipUri(SipTransportType transportType, string user, string host, int port)
        {
            this.isSecure = transportType == SipTransportType.TLS;
            this.user     = user;
            this.host     = host;
            this.port     = port;

            if (transportType == SipTransportType.TCP)
                parameters.Add("transport", "tcp");
        }

        /// <summary>
        /// Constructs a SIP URI from the parameters passed.
        /// </summary>
        /// <param name="transportType">The transport type.</param>
        /// <param name="binding">The <see cref="NetworkBinding" />.</param>
        /// <remarks>
        /// Passing <paramref name="transportType"/> as <see cref="SipTransportType.TLS" />
        /// will result in a <i>secure</i> URI.  Passing <paramref name="transportType"/> as 
        /// <see cref="SipTransportType.TCP" /> will return a URI with a <b>transport=tcp</b>
        /// parameter.
        /// </remarks>
        public SipUri(SipTransportType transportType, NetworkBinding binding)
        {
            this.isSecure = transportType == SipTransportType.TLS;
            this.host     = binding.IsHost ? binding.Host : binding.Address.ToString();
            this.port     = binding.Port;

            if (transportType == SipTransportType.TCP)
                parameters.Add("transport", "tcp");
        }

        /// <summary>
        /// Returns a deep clone of the URI.
        /// </summary>
        /// <returns>The cloned copy.</returns>
        public SipUri Clone()
        {
            var clone = new SipUri();

            clone.isSecure = this.isSecure;
            clone.user     = this.user;
            clone.host     = this.host;
            clone.port     = this.port;

            foreach (string key in this.parameters.Keys)
                clone.parameters.Add(key, parameters[key]);

            foreach (SipHeader header in this.headers)
                clone.headers.Add(header.Clone());

            return clone;
        }

        /// <summary>
        /// Attempts to parse the SIP URI from a string.
        /// </summary>
        /// <param name="text">The input text.</param>
        /// <returns><c>true</c> if the URI was parsed successfully.</returns>
        private bool Parse(string text)
        {
            string      lower;
            int         p, pEnd;
            string      s;
            string      sParams;
            string      sHeaders;
            string      name, value;
            bool        exit;
            string[]    args;

            lower = text.ToLowerInvariant();
            if (lower.StartsWith("sip:"))
            {
                isSecure = false;
                p        = 4;
            }
            else if (lower.StartsWith("sips:"))
            {
                isSecure = true;
                p        = 5;
            }
            else
                return false;

            pEnd = text.IndexOf('@', p);
            if (pEnd != -1)
            {
                user = Helper.UnescapeUri(text.Substring(p, pEnd - p));
                p    = pEnd + 1;
            }

            pEnd = text.IndexOfAny(new char[] { ':', ';', '?' }, p);
            if (pEnd == -1)
            {
                host = Helper.UnescapeUri(text.Substring(p));
                return true;
            }

            host = Helper.UnescapeUri(text.Substring(p, pEnd - p));
            p    = pEnd;

            switch (text[p])
            {
                case ':':

                    p++;
                    pEnd = text.IndexOfAny(new char[] { ';', '?' }, p);

                    if (pEnd == -1)
                        s = text.Substring(p);
                    else
                        s = text.Substring(p, pEnd - p);

                    if (!int.TryParse(s, out port) || port == 0 || port >= ushort.MaxValue)
                        return false;

                    if (pEnd == -1)
                        return true;

                    p = pEnd;
                    if (text[pEnd] == ';')
                        goto parseParams;
                    else
                        goto parseHeaders;

                case ';': goto parseParams;
                case '?': goto parseHeaders;
            }

        parseParams:

            Assertion.Test(text[p] == ';');
            p++;

            pEnd = text.IndexOf('?');
            if (pEnd == -1)
            {
                exit    = true;
                sParams = text.Substring(p);
            }
            else
            {
                exit     = false;
                sParams = text.Substring(p, pEnd - p);
            }

            args = sParams.Split(';');
            foreach (string arg in args)
            {
                int pEquals = arg.IndexOf('=');

                if (pEquals == -1)
                {
                    name  = arg;
                    value = string.Empty;
                }
                else
                {
                    name  = Helper.UnescapeUri(arg.Substring(0, pEquals));
                    value = Helper.UnescapeUri(arg.Substring(pEquals + 1));
                }

                parameters.Add(name, value);
            }

            if (exit)
                return true;

            p = pEnd;

        parseHeaders:

            Assertion.Test(text[p] == '?');
            p++;

            sHeaders = text.Substring(p);

            args = sHeaders.Split('&');
            foreach (string arg in args)
            {
                int pEquals = arg.IndexOf('=');

                if (pEquals == -1)
                {
                    name = arg;
                    value = string.Empty;
                }
                else
                {
                    name  = Helper.UnescapeUri(arg.Substring(0, pEquals));
                    value = Helper.UnescapeUri(arg.Substring(pEquals + 1));
                }

                headers.Add(new SipHeader(name, value));
            }

            return true;
        }

        /// <summary>
        /// <c>true</c> for a SIPS URI, <b> false</b> for SIP.
        /// </summary>
        public bool IsSecure
        {
            get { return isSecure; }
            set { isSecure = value; }
        }

        /// <summary>
        /// The SIP user (or <b>null)</b>).
        /// </summary>
        public string User
        {
            get { return user; }
            set { user = value; }
        }

        /// <summary>
        /// The host name or IP address.
        /// </summary>
        public string Host
        {
            get { return host; }
            set { host = value; }
        }

        /// <summary>
        /// The port number.
        /// </summary>
        public int Port
        {
            get
            {
                if (port != 0)
                    return port;
                else if (isSecure)
                    return NetworkPort.SIPS;
                else
                    return NetworkPort.SIP;
            }

            set { port = value; }
        }

        /// <summary>
        /// Returns the collection of SIP URI parameters keyed by case insensitive
        /// parameter name.
        /// </summary>
        public Dictionary<string, string> Parameters
        {
            get { return parameters; }
        }

        /// <summary>
        /// Accesses named URI parameters.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <returns>The parameter value or <c>null</c> if the parameter is not present.</returns>
        /// <remarks>
        /// The property getter returns <c>null</c> if the named property
        /// is not present in the URI.  Passing <c>null</c> to the setter
        /// will delete the parameter if it is present.
        /// </remarks>
        public string this[string name]
        {
            get
            {
                string value;

                if (parameters.TryGetValue(name, out value))
                    return value;
                else
                    return null;
            }

            set
            {
                if (value == null)
                {
                    if (parameters.ContainsKey(name))
                        parameters.Remove(name);

                    return;
                }

                parameters[name] = value;
            }
        }

        /// <summary>
        /// Returns the list of SIP headers.
        /// </summary>
        public List<SipHeader> Headers
        {
            get { return headers; }
        }

        /// <summary>
        /// Renders the URI into a string.
        /// </summary>
        /// <returns>The serialized URI.</returns>
        public override string ToString()
        {
            // $todo(jeff.lill): 
            //
            // This doesn't implement all of the escaping required
            // by the RFC.  In particular characters such as '@' in
            // the user name, and '=', ';', '?', and '&' in the
            // parameters and argument sections need to be escaped.

            StringBuilder sb = new StringBuilder();

            if (host == null)
                throw new SipException("Invalid SIP URI: The [host] field must be set.");

            sb.Append(isSecure ? "sips:" : "sip:");

            if (user != null)
                sb.AppendFormat("{0}@", Helper.EscapeUri(user));

            sb.Append(host);

            if (port != 0)
                sb.AppendFormat(":{0}", port);

            if (parameters.Count > 0)
            {
                foreach (string key in parameters.Keys)
                    sb.AppendFormat(";{0}={1}", Helper.EscapeUri(key), Helper.EscapeUri(parameters[key]));
            }

            if (headers.Count > 0)
            {
                sb.Append('?');

                for (int i = 0; i < headers.Count; i++)
                {
                    var header = headers[i];

                    if (i > 0)
                        sb.Append('&');

                    sb.AppendFormat("{0}={1}", Helper.EscapeUri(header.Name), Helper.EscapeUri(header.FullText));
                }
            }

            return sb.ToString();
        }
    }
}
