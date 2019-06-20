//-----------------------------------------------------------------------------
// FILE:        SdpMediaDescription.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes how to establish a media stream.

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Encapsulates a SDP media description within a <see cref="SdpPayload" /> instance.
    /// </summary>
    public sealed class SdpMediaDescription
    {
        private string          mediaText;
        private int             port;
        private int             portCount;
        private string          protocolText;
        private string          format;
        private List<string>    attributes = new List<string>(10);

        /// <summary>
        /// Constructs a <see cref="SdpMediaDescription" /> instance from a set of parameters.
        /// </summary>
        /// <param name="media">The type of media (audio, video,...).</param>
        /// <param name="port">The first media port number.</param>
        /// <param name="portCount">The number of media ports used.</param>
        /// <param name="protocol">The transport protocol.</param>
        /// <param name="format">The media format fields separated by spaces.</param>
        public SdpMediaDescription(SdpMediaType media, int port, int portCount, MediaProtocol protocol, string format)
        {
            if (media == SdpMediaType.Unknown)
                throw new SipException("Cannot assign an unknown media type.");

            this.Media     = media;
            this.port      = port;
            this.portCount = portCount;
            this.Protocol  = protocol;
            this.format    = format;
        }

        /// <summary>
        /// Constructs a <see cref="SdpMediaDescription" /> instance by parsing the contents 
        /// of a SDP media description line.
        /// </summary>
        /// <param name="text">The media description (without the leading "m=").</param>
        public SdpMediaDescription(string text)
        {
            Parse(text);
        }

        /// <summary>
        /// Parses the description from text.
        /// </summary>
        /// <param name="text">The source text.</param>
        private void Parse(string text)
        {
            string[]        fields = text.Split(' ');
            string[]        portInfo;
            StringBuilder   sb;

            if (fields.Length < 3)
                throw new SipException("At least three fields expected in SDP payload: [{0}]", text);

            mediaText = fields[0];
            if (mediaText.Length == 0)
                throw new SipException("Invalid [media] field SDP payload: [{0}]", text);

            portInfo = fields[1].Split('/');
            if (portInfo.Length == 1)
            {
                if (!int.TryParse(portInfo[0], out port) || port <= 0 || port > ushort.MaxValue)
                    throw new SipException("Invalid [port] field SDP payload: [{0}]", text);

                portCount = 0;
            }
            else
            {
                if (portInfo.Length != 2 ||
                    !int.TryParse(portInfo[0], out port) ||
                    !int.TryParse(portInfo[1], out portCount) ||
                    port <= 0 || port > ushort.MaxValue ||
                    portCount <= 0 || port + portCount > ushort.MaxValue)

                    throw new SipException("Invalid [port] field SDP payload: [{0}]", text);
            }

            protocolText = fields[2];
            if (protocolText.Length == 0)
                throw new SipException("Invalid [protocol] field SDP payload: [{0}]", text);

            sb = new StringBuilder();
            for (int i = 3; i < fields.Length; i++)
            {
                if (i > 3)
                    sb.Append(' ');

                sb.Append(fields[i]);
            }

            format = sb.ToString();
        }

        /// <summary>
        /// <para>
        /// The media description contents in textual form suitable for serializing into a
        /// SDP payload.
        /// </para>
        /// <note>Does not include the leading "m=".</note>
        /// </summary>
        public string Text
        {
            get
            {
                if (portCount > 0)
                    return string.Format("{0} {1}/{2} {3} {4}", mediaText, port, portCount, protocolText, format);
                else
                    return string.Format("{0} {1} {2} {3}", mediaText, port, protocolText, format);
            }

            set { Parse(value); }
        }

        /// <summary>
        /// The media type.
        /// </summary>
        public SdpMediaType Media
        {
            get
            {
                switch (mediaText.ToLowerInvariant())
                {
                    case "audio":           return SdpMediaType.Audio;
                    case "video":           return SdpMediaType.Video;
                    case "text":            return SdpMediaType.Text;
                    case "application":     return SdpMediaType.Application;
                    case "message":         return SdpMediaType.Message;
                    default:                return SdpMediaType.Unknown;
                }
            }

            set
            {
                switch (value)
                {
                    case SdpMediaType.Audio:        mediaText = "audio"; break;
                    case SdpMediaType.Video:        mediaText = "video"; break;
                    case SdpMediaType.Text:         mediaText = "text"; break;
                    case SdpMediaType.Application:  mediaText = "application"; break;
                    case SdpMediaType.Message:      mediaText = "message"; break;
                    case SdpMediaType.Unknown:      throw new SipException("Cannot assign an unknown media type.");
                }
            }
        }

        /// <summary>
        /// The media type as text.
        /// </summary>
        public string MediaText
        {
            get { return mediaText; }
            set { mediaText = value; }
        }

        /// <summary>
        /// The first media port number.
        /// </summary>
        public int Port
        {
            get { return port; }
            set { port = value; }
        }

        /// <summary>
        /// The number of ports used starting from <see cref="Port" /> (or zero).
        /// </summary>
        /// <remarks>
        /// A zero value indicates that there was no explicit port count in the media description.
        /// </remarks>
        public int PortCount
        {
            get { return portCount; }
            set { portCount = value; }
        }

        /// <summary>
        /// The media transmission protocol.
        /// </summary>
        public MediaProtocol Protocol
        {
            get
            {
                switch (protocolText.ToUpper())
                {
                    case "UDP":         return MediaProtocol.Udp;
                    case "RTP/AVP":     return MediaProtocol.RtpAvp;
                    case "RTP/SAVP":    return MediaProtocol.RtpSavp;
                    default:            return MediaProtocol.Unknown;
                }
            }

            set
            {
                switch (value)
                {
                    case MediaProtocol.Udp:         protocolText = "udp"; break;
                    case MediaProtocol.RtpAvp:      protocolText = "RTP/AVP"; break;
                    case MediaProtocol.RtpSavp:     protocolText = "RTP/SAVP"; break;
                    case MediaProtocol.Unknown:     throw new SipException("Cannot assign an unknown protocol type.");
                }
            }
        }

        /// <summary>
        /// The media transmission protocol as text.
        /// </summary>
        public string ProtocolText
        {
            get { return protocolText; }
            set { protocolText = value; }
        }

        /// <summary>
        /// The media format fields separated by single spaces.
        /// </summary>
        public string Format
        {
            get { return format; }
            set { format = value; }
        }

        /// <summary>
        /// Returns the list of media attributes.
        /// </summary>
        public List<string> Attributes
        {
            get { return attributes; }
        }

        /// <summary>
        /// Renders the media description into a form suitable for
        /// adding to a SDP payload.
        /// </summary>
        /// <param name="sb">The output <see cref="StringBuilder" />.</param>
        public void Serialize(StringBuilder sb)
        {
            sb.AppendLine("m=" + this.Text);

            foreach (string attribute in attributes)
                sb.AppendLine("a=" + attribute);
        }

        /// <summary>
        /// Renders the media description into a string suitable for serializing into
        /// a SDP payload.
        /// </summary>
        /// <returns>The serialized description.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder(256);

            Serialize(sb);
            return sb.ToString();
        }
    }
}
