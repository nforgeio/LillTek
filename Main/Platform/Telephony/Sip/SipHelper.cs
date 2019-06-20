//-----------------------------------------------------------------------------
// FILE:        SipHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: SIP related utilities.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Net.Sockets;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// SIP related utilities.
    /// </summary>
    /// <threadsafety static="true" />
    public static class SipHelper
    {
        /// <summary>
        /// <see cref="NetTrace" /> subsystem name.
        /// </summary>
        public const string TraceSubsystem = "LillTek.SIP";

        /// <summary>
        /// String used to prefix <b>Via</b> header <b>branch</b> parameters for SIP 2.0 compatibility.
        /// </summary>
        public const string BranchPrefix = "z9hG4bK-";

        /// <summary>
        /// Used for breaking message traces by transports.
        /// </summary>
        internal const string TraceBreak = "-------------------------------------------------------------------------------";

        /// <summary>
        /// The SIP 2.0 version string.
        /// </summary>
        public const string SIP20 = "SIP/2.0";

        /// <summary>
        /// The SDP MIME type: <b>application/sdp</b>.
        /// </summary>
        public const string SdpMimeType = "application/sdp";

        /// <summary>
        /// The default "Max-Forwards" header value.
        /// </summary>
        public const string MaxForwards = "70";

        // These tables map long and short forms of SIP message headers.

        private static Dictionary<string, string> compactToLongHeader;
        private static Dictionary<string, string> longToCompactHeader;

        // The default "Allow" header.

        private static SipHeader allowDefault;

        // Used for internal thread synchronization

        private static object syncLock = new object();

        // The next value to be used for generating process unique tag IDs.

        private static long nextTagID;

        /// <summary>
        /// Used in the constructor when generating the compact to long
        /// header mapping tables.
        /// </summary>
        private struct L2C
        {
            public string Long;
            public string Compact;

            public L2C(string l, string c)
            {
                this.Long = l;
                this.Compact = c;
            }
        }

        /// <summary>
        /// Static constructor.
        /// </summary>
        static SipHelper()
        {
            L2C[] l2c = new L2C[] {

                new L2C(SipHeader.CallID,"i"),
                new L2C(SipHeader.Contact,"m"),
                new L2C(SipHeader.ContentEncoding,"e"),
                new L2C(SipHeader.ContentLength,"l"),
                new L2C(SipHeader.ContentType,"c"),
                new L2C(SipHeader.From,"f"),
                new L2C(SipHeader.Subject,"s"),
                new L2C(SipHeader.Supported,"k"),
                new L2C(SipHeader.To,"t"),
                new L2C(SipHeader.Via,"v"),
            };

            compactToLongHeader = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            longToCompactHeader = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (L2C map in l2c)
            {
                compactToLongHeader.Add(map.Compact, map.Long);
                longToCompactHeader.Add(map.Long, map.Compact);
            }

            allowDefault = new SipHeader("Allow", new string[] {

                "INVITE","ACK","CANCEL","OPTIONS","BYE","REFER","NOTIFY","MESSAGE","SUBSCRIBE","INFO"
            });

            int pos = 0;

            nextTagID = Helper.ReadInt64(Crypto.Rand(8), ref pos);
        }

        /// <summary>
        /// The default "Allow" header value to be used in messages generated
        /// by the stack.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The SIP stack automatically adds an "Allow" header to requests
        /// if the application hasn't already added this header.
        /// </para>
        /// <para>
        /// This defaults to a basic set of headers:
        /// </para>
        /// <code language="none">
        /// "INVITE","ACK","CANCEL","OPTIONS","BYE","REFER","NOTIFY","MESSAGE","SUBSCRIBE","INFO"
        /// </code>
        /// <para>
        /// Applications that need to expand or restrict this list may set
        /// this property to a new default or explicitly add an "Allow" header
        /// to each SIP request.
        /// </para>
        /// <note>
        /// This property returns a clone of the default header.  This
        /// means that you cannot modify this header.  Applications that
        /// wish to change the default, must set a new header.
        /// </note>
        /// </remarks>
        public static SipHeader AllowDefault
        {
            get { return allowDefault.Clone(); }

            set
            {
                if (String.Compare(value.Name, "Allow", true) != 0)
                    throw new ArgumentException("Header name must be [Allow].");

                allowDefault = value.Clone();
            }
        }

        /// <summary>
        /// Attempts to map the compact form of a SIP message header
        /// into the long form.
        /// </summary>
        /// <param name="header">A potential compact header.</param>
        /// <returns>The long form of the header if a mapping exists, <c>null</c> otherwise.</returns>
        public static string GetLongHeader(string header)
        {
            string value;

            if (compactToLongHeader.TryGetValue(header, out value))
                return value;

            return null;
        }

        /// <summary>
        /// Attempts to map the long form of a SIP message header
        /// into the compact form.
        /// </summary>
        /// <param name="header">A potential long header.</param>
        /// <returns>The compact form of the header if a mapping exists, <c>null</c> otherwise.</returns>
        public static string GetCompactHeader(string header)
        {
            string value;

            if (longToCompactHeader.TryGetValue(header, out value))
                return value;

            return null;
        }

        /// <summary>
        /// Returns the default network port for the specified transport.
        /// </summary>
        /// <param name="transportType">Indicates the transport type.</param>
        /// <returns>The port number.</returns>
        public static int GetDefaultPort(SipTransportType transportType)
        {
            switch (transportType)
            {
                case SipTransportType.UDP:
                case SipTransportType.TCP:

                    return NetworkPort.SIP;

                case SipTransportType.TLS:

                    return NetworkPort.SIPS;

                default:

                    throw new ArgumentException("Unspecified transport type.");
            }
        }

        /// <summary>
        /// Parses a <see cref="SipMethod" /> from its textual form.
        /// </summary>
        /// <param name="methodText">The method text (e.g. "INVITE").</param>
        /// <returns>The corresponging <see cref="SipMethod" /> value.</returns>
        public static SipMethod ParseMethod(string methodText)
        {
            switch (methodText.Trim().ToUpper())
            {
                case "INVITE":  return SipMethod.Invite;
                case "REINVITE":    return SipMethod.Reinvite;
                case "REGISTER":    return SipMethod.Register;
                case "ACK":         return SipMethod.Ack;
                case "CANCEL":      return SipMethod.Cancel;
                case "BYE":         return SipMethod.Bye;
                case "OPTIONS":     return SipMethod.Options;
                case "INFO":        return SipMethod.Info;
                case "NOTIFY":      return SipMethod.Notify;
                case "SUBSCRIBE":   return SipMethod.Subscribe;
                case "UNSUBSCRIBE": return SipMethod.Unsubscribe;
                case "UPDATE":      return SipMethod.Update;
                case "MESSAGE":     return SipMethod.Message;
                case "REFER":       return SipMethod.Refer;
                case "PRACK":       return SipMethod.Prack;
                case "PUBLISH":     return SipMethod.Publish;
                default:            return SipMethod.Unknown;
            }
        }

        /// <summary>
        /// Returns the textual reason phrase for a <see cref="SipStatus" /> value.
        /// </summary>
        /// <param name="status">The status.</param>
        /// <returns>The phrase.</returns>
        public static string GetReasonPhrase(SipStatus status)
        {
            return GetReasonPhrase((int)status);
        }

        /// <summary>
        /// Returns the textual reason phrase for a SIP response status code.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <returns>The reason phrase.</returns>
        public static string GetReasonPhrase(int statusCode)
        {
            switch (statusCode)
            {
                // Stack specific errors

                case (int)SipStatus.Stack_ProtocolError:

                    return "SIP stack detected a protocol error with a remote endpoint";

                case (int)SipStatus.Stack_NoAvailableTransport:

                    return "SIP stack cannot find a transport suitable for delivering a message";

                // Standard errors

                case 100: return "Trying";
                case 180: return "Ringing";
                case 181: return "Call Is Being Forwarded";
                case 182: return "Queued";
                case 183: return "Session Progress";
                case 200: return "OK";
                case 300: return "Multiple Choices";
                case 301: return "Moved Permanently";
                case 302: return "Moved Temporarily";
                case 305: return "Use Proxy";
                case 380: return "Alternative Service";
                case 400: return "Bad Request";
                case 401: return "Unauthorized";
                case 402: return "Payment Required";
                case 403: return "Forbidden";
                case 404: return "Not Found";
                case 405: return "Method Not Allowed";
                case 406: return "Not Acceptable";
                case 407: return "Proxy Authentication Required";
                case 408: return "Request Timeout";
                case 410: return "Gone";
                case 413: return "Request Entity Too Large";
                case 414: return "Request-URI Too Long";
                case 415: return "Unsupported Media Type";
                case 416: return "Unsupported URI Scheme";
                case 420: return "Bad Extension";
                case 421: return "Extension Required";
                case 423: return "Interval Too Brief";
                case 480: return "Temporarily Unavailable";
                case 481: return "Call/Transaction Does Not Exist";
                case 482: return "Loop Detected";
                case 483: return "Too Many Hops";
                case 484: return "Address Incomplete";
                case 485: return "Ambiguous";
                case 486: return "Busy Here";
                case 487: return "Request Terminated";
                case 488: return "Not Acceptable Here";
                case 491: return "Request Pending";
                case 493: return "Undecipherable";
                case 500: return "Server Internal Error";
                case 501: return "Not Implemented";
                case 502: return "Bad Gateway";
                case 503: return "Service Unavailable";
                case 504: return "Server Timeout";
                case 505: return "Version Not Supported";
                case 513: return "Message Too Large";
                case 600: return "Busy Everywhere";
                case 603: return "Decline";
                case 604: return "Does Not Exist Anywhere";
                case 606: return "Not Acceptable";

                default: return string.Format("Status ({0})", statusCode);
            }
        }

        /// <summary>
        /// Returns a semi-random sequence number to be used when starting a transaction.
        /// </summary>
        /// <returns>The sequence number.</returns>
        public static int GenCSeq()
        {
            return 1;

            // $todo(jeff.lill): Uncomment this once we've finished debugging

            //int     c;

            //c = Environment.TickCount & 0x0000FFFF;
            //if (c == 0)
            //    c = 1;  // zero is not a valid CSeq number

            //return c;
        }

        /// <summary>
        /// Converts a contact style header value into the <see cref="NetworkBinding" />
        /// and optional <see cref="SipTransportType" /> to be used when figuring out
        /// how to deliver an outbound SIP message.
        /// </summary>
        /// <param name="contactText">The <b>Contact</b> header text.</param>
        /// <param name="binding">Returns as the <see cref="NetworkBinding" /> to use.</param>
        /// <param name="transportType">Returns the <see cref="SipTransportType" /> to use</param>
        /// <returns></returns>
        /// <remarks>
        /// <para>
        /// The <paramref name="contactText" /> parameter should be passed as the SIP request URI,
        /// or the value of a <b>To</b>, <b>From</b>, or a <b>Contact</b> header value.  The method
        /// parses this value, looking for the IP address, port, and transport information to be used
        /// when selecting a transport to communicate with this entity.  The method will perform an
        /// necessary DNS lookups to resolve host names into IP addresses.
        /// </para>
        /// <para>
        /// If the operation was successful, the method returns <c>true</c> and sets the <paramref name="binding" />
        /// and <paramref name="transportType" /> values.
        /// </para>
        /// <note>
        /// <paramref name="transportType" /> will return as <see cref="SipTransportType.Unspecified" /> if the
        /// contact information didn't explicitly specify a transport.
        /// </note>
        /// </remarks>
        public static bool TryGetRemoteBinding(string contactText, out NetworkBinding binding, out SipTransportType transportType)
        {
            SipContactValue     v = new SipContactValue(contactText);
            IPAddress           address;
            int                 port;
            SipUri              uri;
            string              transport;
            string              sHost, sPort;
            int                 p;

            binding       = null;
            transportType = SipTransportType.Unspecified;

            // First try to parse the text as a SIP URI.

            if (SipUri.TryParse(v.Uri, out uri))
            {
                if (!IPAddress.TryParse(uri.Host, out address))
                {
                    try
                    {
                        address = Dns.GetHostEntry(uri.Host).AddressList[0];
                    }
                    catch
                    {
                        return false;
                    }
                }

                binding = new NetworkBinding(address, uri.Port);

                if (uri.Parameters.TryGetValue("transport", out transport))
                {
                    switch (transport.ToUpper())
                    {
                        case "UDP":

                            transportType = SipTransportType.UDP;
                            break;

                        case "TCP":

                            transportType = SipTransportType.TCP;
                            break;

                        case "TLS":

                            transportType = SipTransportType.TLS;
                            break;
                    }
                }

                return true;
            }

            // Now look for <ip-address> [":" <port>]

            p = v.Uri.IndexOf(':');
            if (p == -1)
            {
                if (!IPAddress.TryParse(v.Uri, out address))
                {
                    try
                    {
                        address = Dns.GetHostEntry(v.Uri).AddressList.IPv4Only()[0];
                    }
                    catch
                    {
                        return false;
                    }
                }

                binding = new NetworkBinding(address, NetworkPort.SIP);
                return true;
            }

            sHost = v.Uri.Substring(0, p).Trim();
            sPort = v.Uri.Substring(p + 1).Trim();

            if (sHost.Length == 0 || sPort.Length == 0)
                return false;

            if (!int.TryParse(sPort, out port) || port <= 0 || port > ushort.MaxValue)
                return false;

            if (IPAddress.TryParse(sHost, out address))
            {

                binding = new NetworkBinding(address, port);
                return true;
            }

            // The host portion is not an IP address so perform a host lookup

            if (!IPAddress.TryParse(sHost, out address))
            {
                try
                {
                    address = Dns.GetHostEntry(sHost).AddressList.IPv4Only()[0];
                }
                catch
                {
                    return false;
                }
            }

            binding = new NetworkBinding(address, port);
            return true;
        }

        /// <summary>
        /// Returns <c>true</c> if <see cref="SipStatus" /> code is in
        /// the range of 200-699, indicating a final response.
        /// </summary>
        /// <param name="status">The <see cref="SipStatus" /> code to check.</param>
        public static bool IsFinal(SipStatus status)
        {
            return (int)status >= 200;
        }

        /// <summary>
        /// Returns <c>true</c> if <see cref="SipStatus" /> code is in
        /// the range of 200-299, indicating a successful response.
        /// </summary>
        /// <param name="status">The <see cref="SipStatus" /> code to check.</param>
        public static bool IsSuccess(SipStatus status)
        {
            return 200 <= (int)status && (int)status <= 299;
        }

        /// <summary>
        /// Returns <c>true</c> if <see cref="SipStatus" /> code is in
        /// the range of 400-699, indicating an error response.
        /// </summary>
        /// <param name="status">The <see cref="SipStatus" /> code to check.</param>
        public static bool IsError(SipStatus status)
        {
            return (int)status >= 400;
        }

        /// <summary>
        /// Returns <c>true</c> if <see cref="SipStatus" /> code is in
        /// the range of 100-199, indicating a provisional response.
        /// </summary>
        /// <param name="status">The <see cref="SipStatus" /> code to check.</param>
        public static bool IsProvisional(SipStatus status)
        {
            return (int)status <= 199;
        }

        /// <summary>
        /// Generates a diagnostic trace of the message passed.
        /// </summary>
        /// <param name="title">The trace title.</param>
        /// <param name="message">The message.</param>
        public static void Trace(string title, SipMessage message)
        {
            StringBuilder   sb = new StringBuilder(1024);
            SipRequest      request;
            SipResponse     response;
            string          summary;
            string          contentType;
            SipCSeqValue    vCSeq;
            string          cseqMethod;
            string          cseqNumber;

            vCSeq = message.GetHeader<SipCSeqValue>(SipHeader.CSeq);
            if (vCSeq == null)
            {
                cseqMethod = "????";
                cseqNumber = "CSeq:????";
            }
            else
            {
                cseqMethod = vCSeq.Method;
                cseqNumber = "CSeq:" + vCSeq.Number.ToString();
            }

            request = message as SipRequest;
            if (request != null)
            {
                summary = string.Format("{0} Request: {1} {2}", request.MethodText, request.Uri, cseqNumber);
            }
            else
            {
                response = (SipResponse)message;
                summary  = string.Format("{0} Response: {1} ({2}) {3}", cseqMethod, response.StatusCode, response.ReasonPhrase, cseqNumber);
            }

            sb.AppendLine(title);
            sb.AppendLine();
            sb.Append(message.ToString());

            contentType = message.GetHeaderText(SipHeader.ContentType);
            if (message.ContentLength > 0 && message.HasContentType(SipHelper.SdpMimeType))
                sb.AppendLine(Helper.FromUTF8(message.Contents));
            else
                sb.AppendLine(Helper.HexDump(message.Contents, 16, HexDumpOption.ShowAll));

            NetTrace.Write(SipHelper.TraceSubsystem, 0, "SIP: " + title, summary, sb.ToString());
        }

        /// <summary>
        /// Generates a diagnostic trace of an unparsable message.
        /// </summary>
        /// <param name="title">The trace title.</param>
        /// <param name="headerText">The message header text.</param>
        public static void Trace(string title, string headerText)
        {
            NetTrace.Write(SipHelper.TraceSubsystem, 0, "SIP: " + title, "UNPARSABLE message", headerText);
        }

        /// <summary>
        /// Converts an array of bytes into a compact string sutiable for 
        /// use as a Call-ID header, a Via branch parameter or a dialog tag parameter.
        /// </summary>
        /// <param name="id">The ID as a byte array.</param>
        /// <returns>The rendered string.</returns>
        private static string EncodeID(byte[] id)
        {
            // I'm going to return the ID base64 encoded with a slight modification.
            // First, I'm going to remove any padding "=" characters from the end.
            // Second, I'm going to replace any "/" characters with a "-" and any
            // "+" characters with "."
            //
            // The first change is purely to reduce the number of bytes on the wire.
            // The second is more subtle.  Although the Call-ID header can include
            // a "/", the SIP grammar for does not allow this character in tag
            // or branch parameters.  "-" is allowed and is not generated as by
            // base64 encoding.
            //
            // As for "+", it turns out that Microsoft Speech Server escapes plus
            // signs it sees in the Call-ID header when it adds the x-mss-call-id
            // parameter to the contact URI for an INVITE redirect and then requires 
            // that this parameter exactly matches the Call-ID in subsequent 
            // redirected INVITE.  MSS does not escape "." and period is not
            // generated by the base64 encoding.

            var encoded = Convert.ToBase64String(id);
            var sb      = new StringBuilder(encoded.Length);
            
            for (int i = 0; i < encoded.Length; i++)
            {
                if (encoded[i] == '/')
                    sb.Append('-');
                else if (encoded[i] == '+')
                    sb.Append('.');
                else if (encoded[i] != '=')
                    sb.Append(encoded[i]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates a globally unique ID suitable for use as a SIP message <b>Call-ID</b> header.
        /// </summary>
        /// <returns>The ID string.</returns>
        public static string GenerateCallID()
        {
            return EncodeID(Guid.NewGuid().ToByteArray());
        }

        /// <summary>
        /// Generates a globally unique ID suitable for use as a SIP dialog tag on a <b>To</b> or <b>From</b> header.
        /// </summary>
        /// <returns>The ID string.</returns>
        public static string GenerateTagID()
        {
            byte[]  buf = new byte[8];
            int     pos = 0;

            Helper.WriteInt64(buf, ref pos, Interlocked.Increment(ref nextTagID));
            return EncodeID(buf);
        }

        /// <summary>
        /// Generates a globally unique <b>Via</b> header branch ID.
        /// </summary>
        /// <returns>The ID string.</returns>
        public static string GenerateBranchID()
        {
            return BranchPrefix + EncodeID(Guid.NewGuid().ToByteArray());
        }
    }
}
