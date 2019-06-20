//-----------------------------------------------------------------------------
// FILE:        SwitchPacket.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds an event or command response received from the switch.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Holds an event or command response received from the switch.
    /// </summary>
    internal class SwitchPacket
    {
        //---------------------------------------------------------------------
        // Static members

        private static byte[] LFLF = new byte[] { Helper.LF, Helper.LF };

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Identifies the type of the packet.
        /// </summary>
        public SwitchPacketType PacketType { get; internal set; }

        /// <summary>
        /// Set to the text of a command to be sent along with a subsequent event.  Used internally 
        /// to queue <b>sendevent</b> commands to the switch.
        /// </summary>
        internal string CommandText { get; set; }

        /// <summary>
        /// Returns as the <see cref="SwitchEventCode" /> for event packets and is undefined for
        /// response packets.
        /// </summary>
        public SwitchEventCode EventCode { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if this packet holds a command execution acknowledgement and 
        /// the switch accepted the command for processing.
        /// </summary>
        internal bool ExecuteAccepted { get; private set; }

        /// <summary>
        /// Returns dictionary of the packet's name/value headers or <c>null</c> if the
        /// packet does not include any headers.
        /// </summary>
        public ArgCollection Headers { get; private set; }

        /// <summary>
        /// The packet content type or <c>null</c>.
        /// </summary>
        public string ContentType { get; private set; }

        /// <summary>
        /// Returns any content data received or <c>null</c>.
        /// </summary>
        public byte[] Content { get; set; }

        /// <summary>
        /// Returns the content data as text if the the content type was determined to be text.
        /// Returns <c>null</c> if there was no content or if it wasn't text.
        /// </summary>
        public string ContentText { get; private set; }

        /// <summary>
        /// Constructs a packet to be used internally for queuing commands for submission
        /// to the switch.
        /// </summary>
        /// <param name="commandText">The command text (without the terminating LFLF characters).</param>
        /// <param name="headers">The headers for <b>sendevent</b> commands or <c>null</c>.</param>
        /// <param name="contentType">The content type or <c>null</c>.</param>
        /// <param name="content">The content data for<b>sendevent</b> commands or <c>null</c>.</param>
        internal SwitchPacket(string commandText, ArgCollection headers, string contentType, byte[] content)
        {
            this.PacketType  = SwitchPacketType.Command;
            this.CommandText = commandText;
            this.Headers     = headers;
            this.Content     = content;
            this.ContentType = contentType;

            if (content != null && content.Length > 0 && contentType != null && contentType.StartsWith("text"))
                this.ContentText = Helper.ASCIIEncoding.GetString(content);
        }

        /// <summary>
        /// Constructs a packet from properties and optional content received from
        /// the switch.
        /// </summary>
        /// <param name="headers">The header dictionary.</param>
        /// <param name="content">The content data or <c>null</c>.</param>
        internal SwitchPacket(ArgCollection headers, byte[] content)
        {
            string value;

            this.Headers = headers;
            this.Content = content;

            if (headers.TryGetValue("Content-Type", out value))
                this.ContentType = value.ToLower();

            // Figure out whether this is an event or a command response.

            switch (this.ContentType)
            {
                case "auth/request":

                    this.PacketType = SwitchPacketType.ExecuteAck;
                    this.ExecuteAccepted = true;
                    break;

                case null:
                case "command/reply":

                    this.PacketType = SwitchPacketType.ExecuteAck;

                    // Figure out whether the command was successful by looking at the
                    // Reply-Text parameter if it exists.  Assume success if it doesn't 
                    // exist or doesn't start with "-".

                    if (headers.TryGetValue("Reply-Text", out value))
                        this.ExecuteAccepted = !value.StartsWith("-");
                    else
                        this.ExecuteAccepted = true;

                    break;

                case "api/response":

                    this.PacketType = SwitchPacketType.ExecuteResponse;

                    if (content != null)
                        this.ContentText = Helper.ASCIIEncoding.GetString(content);

                    break;

                case "text/event-plain":

                    this.PacketType = SwitchPacketType.Event;
                    break;

                case "log/data":

                    this.PacketType = SwitchPacketType.Log;
                    break;

                default:

                    SysLog.LogWarning("Unexpected switch packet content type [{0}].", ContentText);
                    this.PacketType = SwitchPacketType.Unknown;
                    break;
            }

            // Set the content body if the content is text.

            if (content != null && (PacketType == SwitchPacketType.ExecuteAck || PacketType == SwitchPacketType.Log || ContentType.StartsWith("text")))
                ContentText = Helper.ASCIIEncoding.GetString(content);

            // For event packets, the event properties are actually encoded in the content.
            // I'm going to parse the content and replace the instance properties as makes
            // sense.  This is a bit of a hack but I think it'll be OK.

            if (PacketType == SwitchPacketType.Event)
                ParseEvent();
        }

        /// <summary>
        /// Handles the parsing of the event properties and subcontent from the packet content.  The packet's
        /// <see cref="Headers"/> dictionary, as well as the <see cref="ContentType"/>, <see cref="Content" />,
        /// and <see cref="ContentText" /> property values will be overwritten with the parsed event
        /// properties.
        /// </summary>
        private void ParseEvent()
        {
            Assertion.Test(PacketType == SwitchPacketType.Event);

            if (Content == null)
            {
                SysLog.LogWarning("Encountered switch event packet with no content.");
                PacketType = SwitchPacketType.Unknown;
                return;
            }

            string      headers;
            int         cbContent;
            int         pos;
            string[]    lines;
            string[]    fields;
            string      name;
            string      value;

            pos = Helper.IndexOf(Content, LFLF);
            if (pos == -1)
            {
                SysLog.LogWarning("Ignoring a switch event packet with headers not terminated by LFLF.");
                PacketType = SwitchPacketType.Unknown;
                return;
            }

            // Parse the event headers.

            headers      = Helper.ASCIIEncoding.GetString(Content, 0, pos);
            this.Headers = new ArgCollection(ArgCollectionType.Unconstrained);
            lines        = headers.Split('\n');

            foreach (var line in lines)
            {
                fields = line.Split(new char[] { ':' }, 2);
                if (fields.Length < 2)
                    continue;

                name          = fields[0].Trim();
                value         = Helper.UrlDecode(fields[1].Trim());
                Headers[name] = value;
            }

            if (!Headers.TryGetValue("Event-Name", out value))
            {
                SysLog.LogWarning("Ignoring a switch event packet without an [Event-Name] header.");
                PacketType = SwitchPacketType.Unknown;
                return;
            }

            EventCode = SwitchHelper.ParseEventCode(value);

            // Extract the event content data.

            pos += 2;   // Skip over the LFLF after the headers.

            ContentType = null;
            ContentText = null;

            if (Headers.TryGetValue("Content-Length", out value))
            {

                cbContent = int.Parse(value);
                Content   = Helper.Extract(Content, pos);

                if (Content.Length != cbContent)
                {
                    SysLog.LogWarning("Ignoring malformed switch event packet. Expected [{0}] bytes of event content but received [{1}] bytes.", cbContent, Content.Length);
                    PacketType = SwitchPacketType.Unknown;
                    return;
                }

                if (Headers.TryGetValue("Content-Type", out value))
                {
                    ContentType = value.ToLower();
                    if (ContentType != null && ContentType.StartsWith("text"))
                        ContentText = Helper.ASCIIEncoding.GetString(Content);
                }
            }
            else
                Content = null;
        }
    }
}
