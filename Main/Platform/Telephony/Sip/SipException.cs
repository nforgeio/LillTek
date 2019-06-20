//-----------------------------------------------------------------------------
// FILE:        SipException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The exception thrown to indicate SIP related errors.

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// The exception thrown to indicate SIP related errors.
    /// </summary>
    public class SipException : ApplicationException, ICustomExceptionLogger
    {
        private byte[]      badPacket = null;  // Bad received data (or null)
        private SipMessage  badMessage = null;  // Bad received message (or null)
        private IPEndPoint  sourceEP;           // Source of the bad data
        private string      transport;          // Name of the transport

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The message.</param>
        public SipException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public SipException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message arguments.</param>
        public SipException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }

        /// <summary>
        /// Returns the name of the transport that detected an invalid
        /// packet (or <c>null</c>).
        /// </summary>
        public string Transport
        {
            get { return transport; }
            set { transport = value; }
        }

        /// <summary>
        /// For SIP exceptions resulting from attempting to parse
        /// invalid data received on a transport, this property is
        /// set to the received data.
        /// </summary>
        public byte[] BadPacket
        {
            get { return badPacket; }
            set { badPacket = value; }
        }

        /// <summary>
        /// This property can be set to an improper <see cref="SipMessage" />.
        /// </summary>
        public SipMessage BadMessage
        {
            get { return badMessage; }
            set { badMessage = value; }
        }

        /// <summary>
        /// The network endpoint of the source of the bad packet.
        /// </summary>
        public IPEndPoint SourceEndpoint
        {
            get { return sourceEP; }
            set { sourceEP = value; }
        }

        /// <summary>
        /// Writes custom information about the exception to the string builder
        /// instance passed which will eventually be written to the event log.
        /// </summary>
        /// <param name="sb">The output string builder.</param>
        /// <remarks>
        /// Implementations of this method will typically write the exception's
        /// stack trace out to the string builder before writing out any custom
        /// information.
        /// </remarks>
        public void Log(StringBuilder sb)
        {
            if (badMessage != null)
            {
                sb.AppendLine("Message:\r\n\r\n");
                sb.Append(badMessage.ToString());
            }

            if (badPacket != null)
            {
                sb.AppendFormat("Transport:     {0}\r\n", transport == null ? "<unknown>" : transport);
                sb.AppendFormat("Packet Source: {0}\r\n\r\n", sourceEP);
                sb.AppendLine("Packet (formatted):");
                sb.AppendLine(Helper.HexDump(badPacket, 16, HexDumpOption.ShowAll));
                sb.AppendLine("");
                sb.AppendLine("Packet (raw):");
                sb.AppendLine(Helper.HexDump(badPacket, 16, HexDumpOption.None));
            }
        }
    }
}
