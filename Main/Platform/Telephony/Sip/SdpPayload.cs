//-----------------------------------------------------------------------------
// FILE:        SdpPayload.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a media stream.

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Describes a media stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a partial implementation of the SDP description format as
    /// described by <a href="http://www.ietf.org/rfc/rfc4566.txt?number=4566">RFC 4566</a>.
    /// In particular, this class has the following limitations:
    /// </para>
    /// <list type="bullet">
    ///     <item>Only Internet IPv4 addressing is supported.</item>
    ///     <item>Multicast semantics are not supported.</item>
    ///     <item>Session repeat times are not supported.</item>
    ///     <item>Time Zones, Time, Encryption Key, Bandwidth lines are ignored.</item>
    /// </list>
    /// <para>
    /// The supported defined SDP session properties are exposed as
    /// properties.  Session attributes are available via the 
    /// <see cref="Attributes" /> property.
    /// </para>
    /// </remarks>
    public sealed class SdpPayload
    {
        const string BadSDP              = "Bad SDP: ";
        const string DescriptionExpected = "Bad SDP: Description [{0}] expected";
        const string BadDescription      = "Bad Description [{0}]: {1}";

        // v=<version>

        private int version = 0;

        // o=<username> <sess-id> <sess-version> <nettype> <addrtype> <unicast-address>

        private string userName       = "-";
        private string sessionID      = "0";
        private string sessionVersion = "0";
        private string unicastAddress = IPAddress.Loopback.ToString();

        // s=<sessionname>

        private string sessionName = "LillTek SIP";

        // i=<session description>

        private string sessionDescription = null;

        // u=<uri>

        private string uri = null;

        // e=<email address>

        private string emailAddress = null;

        // p=<phone number>

        private string phoneNumber = null;

        // c=<nettype> <addrtype> <connection-address>

        private IPAddress connectionAddress = IPAddress.Any;

        // Session attributes.

        private List<string> attributes = new List<string>(10);

        // Media descriptions.

        private List<SdpMediaDescription> media = new List<SdpMediaDescription>(10);

        /// <summary>
        /// Instantiates a <see cref="SdpPayload" /> with reasonable default
        /// session values but with no media descriptions.
        /// </summary>
        public SdpPayload()
        {
        }

        /// <summary>
        /// Parses an SDP packet from its binary representation.
        /// </summary>
        /// <param name="packet">The SDP packet.</param>
        public SdpPayload(byte[] packet)
            : this(Helper.FromUTF8(packet))
        {
        }

        /// <summary>
        /// Returns the next line of text from the source string.
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <param name="pos">The current position in the string.</param>
        /// <returns>The next string or <c>null</c> if we've reached the end.</returns>
        private string NextLine(string source, ref int pos)
        {
            string  line;
            int     pEnd;

            if (pos == source.Length)
                return null;

            pEnd = source.IndexOf('\n', pos);
            if (pEnd == -1)
                throw new SipException(BadSDP + "Missing line termination.");

            if (pEnd == 0)
                throw new SipException(BadSDP + "Empty description.");

            // Handle lines terminated with CRLF and just LF.

            if (source[pEnd - 1] == '\r')
                line = source.Substring(pos, pEnd - pos - 1);
            else
                line = source.Substring(pos, pEnd - pos);

            pos = pEnd + 1;
            return line;
        }

        /// <summary>
        /// Parses an SDP packet from its textual represention.
        /// </summary>
        /// <param name="text">The SDP text.</param>
        public SdpPayload(string text)
        {
            int         pos  = 0;
            string      line = NextLine(text, ref pos);
            string[]    fields;

            // v=  (protocol version)

            if (line == null || !line.StartsWith("v="))
                throw new SipException(DescriptionExpected, "v");

            if (!int.TryParse(line.Substring(2), out version))
                throw new SipException(BadDescription, "v", "Integer expected");

            line = NextLine(text, ref pos);

            // o=  (originator and session identifier)

            if (line == null || !line.StartsWith("o="))
                throw new SipException(DescriptionExpected, "v");

            fields = line.Substring(2).Split(' ');
            if (fields.Length != 6)
                throw new SipException(BadDescription, "o", "Six fields expected.");

            userName       = fields[0];
            sessionID      = fields[1];
            sessionVersion = fields[2];

            if (fields[3] != "IN")
                throw new SipException(BadDescription, "o", "Only [IN] network type is supported.");

            if (fields[4] != "IP4")
                throw new SipException(BadDescription, "o", "Only [IP4] address type is supported.");

            unicastAddress = fields[5];

            line = NextLine(text, ref pos);

            // s=  (session name)

            if (line == null || !line.StartsWith("s="))
                throw new SipException(DescriptionExpected, "s");

            sessionName = line.Substring(2);

            line = NextLine(text, ref pos);

            // i=* (session information)

            if (line != null && line.StartsWith("i="))
            {
                sessionDescription = line.Substring(2);
                line = NextLine(text, ref pos);
            }

            // u=* (URI of description)

            if (line != null && line.StartsWith("u="))
            {
                uri = line.Substring(2);
                line = NextLine(text, ref pos);
            }

            // e=* (email address)
            // p=* (phone number)

            while (line != null)
            {
                if (line.StartsWith("e="))
                {
                    emailAddress = line.Substring(2);
                    line = NextLine(text, ref pos);
                }
                else if (line.StartsWith("p="))
                {
                    phoneNumber = line.Substring(2);
                    line = NextLine(text, ref pos);
                }
                else
                    break;
            }

            // c=* (connection information -- not required if included in

            if (line != null && line.StartsWith("c="))
            {
                fields = line.Substring(2).Split(' ');
                if (fields.Length != 3)
                    throw new SipException(BadDescription, "c", "Three fields expected.");

                if (fields[0] != "IN")
                    throw new SipException(BadDescription, "c", "Only [IN] network type is supported.");

                if (fields[1] != "IP4")
                    throw new SipException(BadDescription, "c", "Only [IP4] address type is supported.");

                if (fields[2].IndexOf('/') != -1)
                    throw new SipException(BadDescription, "c", "Multicast TTL not supported in connection address.");

                if (!IPAddress.TryParse(fields[2], out connectionAddress))
                    throw new SipException(BadDescription, "c", "Invalid connection address.");

                line = NextLine(text, ref pos);
            }

            // b=* (zero or more bandwidth information lines)

            while (line != null && line.StartsWith("b="))
                line = NextLine(text, ref pos);

            // One or more time descriptions ("t=" and "r=" lines; see below)

            while (line != null && (line.StartsWith("t=") || line.StartsWith("r=")))
                line = NextLine(text, ref pos);

            // z=* (time zone adjustments)

            while (line != null && line.StartsWith("z="))
                line = NextLine(text, ref pos);

            // k=* (encryption key)

            while (line != null && line.StartsWith("k="))
                line = NextLine(text, ref pos);

            // a=* (zero or more session attribute lines)

            while (line != null && line.StartsWith("a="))
            {

                attributes.Add(line.Substring(2));
                line = NextLine(text, ref pos);
            }

            // Parse the media descriptions.

            while (line != null)
            {
                if (!line.StartsWith("m="))
                    throw new SipException(DescriptionExpected, "m=");

                SdpMediaDescription mediaDescription;

                mediaDescription = new SdpMediaDescription(line.Substring(2));

                line = NextLine(text, ref pos);
                while (line != null && line.StartsWith("a="))
                {
                    mediaDescription.Attributes.Add(line.Substring(2));
                    line = NextLine(text, ref pos);
                }

                media.Add(mediaDescription);
            }

            // Verify that the session specifies a connection or that every
            // media description has one.

            if (connectionAddress.Equals(IPAddress.Any))
            {
                bool found;

                foreach (var mediaDescription in media)
                {
                    found = false;
                    foreach (string attribute in mediaDescription.Attributes)
                        if (attribute.StartsWith("c:"))
                        {

                            found = true;
                            break;
                        }

                    if (!found)
                        throw new SipException(BadSDP + "If no session connection attribute is present then all media descriptions must have a connection attribute.");
                }
            }
        }

        /// <summary>
        /// Protocol version.
        /// </summary>
        public int Version
        {
            get { return version; }
            set { version = value; }
        }

        /// <summary>
        /// User name.
        /// </summary>
        public string UserName
        {
            get { return userName; }
            set { userName = value; }
        }

        /// <summary>
        /// The session ID.
        /// </summary>
        public string SessionID
        {
            get { return sessionID; }
            set { sessionID = value; }
        }

        /// <summary>
        /// The session version.
        /// </summary>
        public string SessionVersion
        {
            get { return sessionVersion; }
            set { sessionVersion = value; }
        }

        /// <summary>
        /// The IP address or FQDN of the machine that created the session.
        /// </summary>
        public string UnicastAddress
        {

            get { return unicastAddress; }
            set { unicastAddress = value; }
        }

        /// <summary>
        /// The session name.
        /// </summary>
        public string SessionName
        {
            get { return sessionName; }
            set { sessionName = value; }
        }

        /// <summary>
        /// A human readable session description (optional).
        /// </summary>
        public string SessionDescription
        {
            get { return sessionDescription; }
            set { sessionDescription = value; }
        }

        /// <summary>
        /// URI to additional information about the session on the world-wide-web (optional).
        /// </summary>
        public string Uri
        {
            get { return uri; }
            set { uri = value; }
        }

        /// <summary>
        /// The email address for the person responsible for the session (optional).
        /// </summary>
        public string EmailAddress
        {

            get { return emailAddress; }
            set { emailAddress = value; }
        }

        /// <summary>
        /// The phone number for the person responsible for the session (optional).
        /// </summary>
        public string PhoneNumber
        {
            get { return phoneNumber; }
            set { phoneNumber = value; }
        }

        /// <summary>
        /// IP address where the media will be available or <b>IPAddress.Any</b> if
        /// this will be specified in each media description.
        /// </summary>
        public IPAddress ConnectionAddress
        {
            get { return connectionAddress; }
            set { connectionAddress = value; }
        }

        /// <summary>
        /// Returns the list of session attributes (as strings).
        /// </summary>
        public List<string> Attributes
        {
            get { return attributes; }
        }

        /// <summary>
        /// Returns the list of media descriptions (as <see cref="SdpMediaDescription" /> instances).
        /// </summary>
        public List<SdpMediaDescription> Media
        {
            get { return media; }
        }

        /// <summary>
        /// Serializes the SDP payload into the format as defined by RFC 4566.
        /// </summary>
        /// <param name="sb">The output <see cref="StringBuilder" />.</param>
        public void Serialize(StringBuilder sb)
        {
            sb.AppendFormat("v={0}\r\n", version);
            sb.AppendFormat("o={0} {1} {2} IN IP4 {3}\r\n", userName, sessionID, sessionVersion, unicastAddress);
            sb.AppendFormat("s={0}\r\n", sessionName);

            if (sessionDescription != null)
                sb.AppendFormat("i={0}\r\n", sessionDescription);

            if (uri != null)
                sb.AppendFormat("u={0}\r\n", uri);

            if (phoneNumber != null)
                sb.AppendFormat("p={0}\r\n", phoneNumber);

            if (emailAddress != null)
                sb.AppendFormat("e={0}\r\n", emailAddress);

            if (connectionAddress != IPAddress.Any)
                sb.AppendFormat("c=IN IP4 {0}\r\n", connectionAddress);

            foreach (string attribute in attributes)
                sb.AppendFormat("a={0}\r\n", attribute);

            foreach (SdpMediaDescription mediaDescription in media)
                mediaDescription.Serialize(sb);
        }

        /// <summary>
        /// Serializes the SDP payload into the format as defined by RFC 4566.
        /// </summary>
        /// <returns>The output string.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder(256);

            Serialize(sb);
            return sb.ToString();
        }

        /// <summary>
        /// Serializes the SDP into a UTF-8 encoded array of bytes.
        /// </summary>
        /// <returns>The encoded SDP payload.</returns>
        public byte[] ToArray()
        {
            return Helper.ToUTF8(this.ToString());
        }
    }
}
