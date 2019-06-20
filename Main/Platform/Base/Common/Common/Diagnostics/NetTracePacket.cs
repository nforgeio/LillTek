//-----------------------------------------------------------------------------
// FILE:        NetTracePacket.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Trace information source.

using System;
using System.Text;
using System.Net;

namespace LillTek.Common
{
    /// <summary>
    /// Records trace information and handles the serialization of this
    /// information for transmission via UDP multicast.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><u>Trace Packet Format</u></b>
    /// </para>
    /// <para>
    /// Trace packets are transmitted within UDP packet.  Up to see <see cref="MaxPacket" /> 
    /// bytes of trace information will be packed into this packet.  The format for
    /// this information is:
    /// </para>
    /// <code language="none">
    /// +-----------+
    /// |   BYTE    |   Magic Number (0x99)
    /// |-----------|
    /// |   BYTE    |   Packet format (0)
    /// |-----------|
    /// |   DWORD   |   Source ID (see comment below)
    /// |-----------|
    /// |   DWORD   |   Packet sequence number
    /// |-----------|
    /// |  DDWORD   |   Send time (sender DateTime.UtcNow.Ticks)
    /// |-----------|
    /// |   WORD    |   cbSubsystem (# of bytes of subsystem name data)
    /// |-----------|
    /// |           |
    /// |   BYTE[]  |   subsystem name data: ANSI encoded (limited to 32 chars)
    /// |           |
    /// |-----------|
    /// |   BYTE    |   detail level (increases for more detail)
    /// |-----------|
    /// |   WORD    |   cbEvent (# of bytes of event data)
    /// |-----------|
    /// |           |
    /// |   BYTE[]  |   event data: ANSI encoded
    /// |           |
    /// |-----------|
    /// |   WORD    |   cbSummary (# of bytes of message data)
    /// |-----------|
    /// |           |
    /// |   BYTE[]  |   summary data: ANSI encoded
    /// |           |
    /// +-----------+
    /// |   WORD    |   cbDetails (# of bytes of message data)
    /// |-----------|
    /// |           |
    /// |   BYTE[]  |   details data: ANSI encoded
    /// |           |
    /// +-----------+
    /// </code>
    /// <para>
    /// All integers are stored in network order (big endian).
    /// </para>
    /// <para>
    /// Each trace source is uniquely identified on a specific machine by a
    /// 32-bit source ID.  This ID is obtained from current value of 
    /// Environment.TickCount at the time the source was started.  Trace
    /// source's a distinguished globally by combining this source ID with
    /// the network endpoint (IP:port) of the source's UDP socket.
    /// </para>
    /// <para>
    /// Each trace packet sent by a source includes a packet sequence
    /// number.  The first packet transmitted will have PacketNum=0 and
    /// this number will be incremented by 1 for each packet sent thereafter.
    /// </para>
    /// <para>
    /// The combination of the globally unique trace source ID and the
    /// packet sequence numbers will allow trace sinks to identify when
    /// packets are lost.
    /// </para>
    /// </remarks>
    public sealed class NetTracePacket
    {
        /// <summary>
        /// The magic number placed in the first byte of a trace packet
        /// to help filter valid trace packets from random network noise.
        /// </summary>
        public const int Magic = 0x98;

        /// <summary>
        /// The maximum trace packet size in bytes.
        /// </summary>
        public const int MaxPacket = 4096;

        private IPEndPoint      sourceEP;       // The source endpoint
        private int             originID;       // The source ID
        private int             packetNum;      // The packet sequence number
        private DateTime        sendTime;       // Time the packet was sent
        private DateTime        recvTime;       // Time the packet was received
        private string          subsystem;      // The sending subsystem name
        private int             detail;         // The packet detail level
        private string          tEvent;         // The trace event
        private string          summary;        // The trace summary
        private string          details;        // The trace details

        /// <summary>
        /// Constructor.
        /// </summary>
        public NetTracePacket()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="originID">The local trace source ID.</param>
        /// <param name="packetNum">The packet number.</param>
        /// <param name="subsystem">The sending subsystem name (limited to 32 characters).</param>
        /// <param name="detail">The event detail level (0..255).</param>
        /// <param name="tEvent">The trace event.</param>
        /// <param name="summary">The trace summary.</param>
        /// <param name="details">The trace details.</param>
        public NetTracePacket(int originID, int packetNum, string subsystem, int detail, string tEvent, string summary, string details)
        {
            if (subsystem.Length > 32)
                throw new ArgumentException("Subsystem name is limited to 32 characters.", "subsystem");

            if (detail < 0 || detail > 255)
                throw new ArgumentException("Detail level limited to range of 0..255", "detail");

            this.originID = originID;
            this.packetNum = packetNum;
            this.subsystem = subsystem;
            this.detail = detail;
            this.tEvent = tEvent;
            this.summary = summary;
            this.details = details;
        }

        /// <summary>
        /// Returns a globally unique trace source ID by combining
        /// the source's network endpoint with its originID.
        /// </summary>
        public string TraceOriginID
        {
            get { return sourceEP.ToString() + ":" + originID.ToString(); }
        }

        /// <summary>
        /// The trace origin ID.
        /// </summary>
        public int OriginID
        {
            get { return originID; }
            set { originID = value; }
        }

        /// <summary>
        /// The trace packet number.
        /// </summary>
        public int PacketNum
        {
            get { return packetNum; }
            set { packetNum = value; }
        }

        /// <summary>
        /// Time the packet was sent expressed in UTC based on the sender's clock.
        /// </summary>
        public DateTime SendTime
        {
            get { return sendTime; }
            set { sendTime = value; }
        }

        /// <summary>
        /// Time the packet was received expressed in UTC based on the receiver's clock.
        /// </summary>
        public DateTime ReceiveTime
        {
            get { return recvTime; }
            set { recvTime = value; }
        }

        /// <summary>
        /// The source network endpoint.
        /// </summary>
        public IPEndPoint SourceEP
        {
            get { return sourceEP; }
            set { sourceEP = value; }
        }

        /// <summary>
        /// The name of the sending subsystem.
        /// </summary>
        public string Subsystem
        {
            get { return subsystem; }

            set
            {
                if (subsystem.Length > 32)
                    throw new ArgumentException("Subsystem name is limited to 32 characters.");

                subsystem = value;
            }
        }

        /// <summary>
        /// The event detail level.
        /// </summary>
        public int Detail
        {
            get { return detail; }

            set
            {
                if (detail < 0 || detail > 255)
                    throw new ArgumentException("Detail level limited to range of )..255");

                detail = value;
            }
        }

        /// <summary>
        /// A string identifying the trace event.
        /// </summary>
        public string Event
        {
            get { return tEvent; }
            set { tEvent = value; }
        }

        /// <summary>
        /// The trace summary text.
        /// </summary>
        public string Summary
        {
            get { return summary; }
            set { summary = value; }
        }

        /// <summary>
        /// The trace details text.
        /// </summary>
        public string Details
        {
            get { return details; }
            set { details = value; }
        }

        /// <summary>
        /// Serializes the packet to a byte buffer.
        /// </summary>
        /// <param name="buf">The destination array.</param>
        /// <returns>The number of bytes written.</returns>
        /// <remarks>
        /// The buffer must be allocated to <see cref="MaxPacket" /> bytes and
        /// that the event, summary and detail text will be clipped to fit
        /// into the buffer.
        /// </remarks>
        internal int Write(byte[] buf)
        {
            Assertion.Test(buf.Length == MaxPacket);

            Encoding    ansi = Helper.AnsiEncoding;
            long        ticks = DateTime.UtcNow.Ticks;
            int         pos;
            int         cb, cbMax;
            byte[]      text;

            pos = 0;

            // Write the magic number and format bytes

            Helper.WriteByte(buf, ref pos, Magic);
            Helper.WriteByte(buf, ref pos, 0);

            // Write the source ID and packet number

            Helper.WriteInt32(buf, ref pos, originID);
            Helper.WriteInt32(buf, ref pos, packetNum);

            // Write the send time

            Helper.WriteInt64(buf, ref pos, ticks);

            // Write the subsystem name.  Note that I'm going to write
            // out a maximum of 32 characters.

            text = ansi.GetBytes(subsystem);
            Helper.WriteInt16(buf, ref pos, text.Length);
            Array.Copy(text, 0, buf, pos, text.Length);
            pos += text.Length;

            // Write the detail level

            Helper.WriteByte(buf, ref pos, (byte)detail);

            // Write the event string, clipping it if necessary
            // to ensure that there's enough room left in the buffer
            // for at least the length word of the message string.

            if (tEvent == null)
                tEvent = string.Empty;

            cbMax = MaxPacket - pos - 2 - 2 - 2;    // Maximum tEvent data I can write while 
                                                    // leaving 4 bytes for the summary and
                                                    // details data length words
            text = ansi.GetBytes(tEvent);
            if (text.Length <= cbMax)
                cb = text.Length;
            else
                cb = cbMax;

            Helper.WriteInt16(buf, ref pos, cb);
            Array.Copy(text, 0, buf, pos, cb);
            pos += cb;

            // Write the summary string.

            cbMax = MaxPacket - pos - 2 - 2;        // Be sure to leave room for the details
                                                    // length word
            if (summary == null)
                summary = string.Empty;

            text = ansi.GetBytes(summary);
            if (text.Length <= cbMax)
                cb = text.Length;
            else
                cb = cbMax;

            Helper.WriteInt16(buf, ref pos, cb);
            Array.Copy(text, 0, buf, pos, cb);
            pos += cb;

            // Write the details string.

            cbMax = MaxPacket - pos - 2;            // Be sure to leave room for the details
                                                    // length word
            if (details == null)
                details = string.Empty;

            text = ansi.GetBytes(details);
            if (text.Length <= cbMax)
                cb = text.Length;
            else
                cb = cbMax;

            Helper.WriteInt16(buf, ref pos, cb);
            Array.Copy(text, 0, buf, pos, cb);
            pos += cb;

            return pos;
        }

        /// <summary>
        /// Parses the serializes the packet from the buffer passed.
        /// </summary>
        /// <param name="buf">Buffer holding the serialized packet.</param>
        internal void Read(byte[] buf)
        {
            Encoding    ansi = Helper.AnsiEncoding;
            int         pos;
            int         cb;
            long        ticks;

            pos = 0;

            // Parse the magic number and format

            if (Helper.ReadByte(buf, ref pos) != Magic)
                throw new ArgumentException("Invalid magic number.");

            if (Helper.ReadByte(buf, ref pos) != 0)
                throw new ArgumentException("Unknown packet format.");

            // Parse the originID and packet number

            originID  = Helper.ReadInt32(buf, ref pos);
            packetNum = Helper.ReadInt32(buf, ref pos);

            // Parse the send time and set the receive time to now.

            recvTime = DateTime.UtcNow;
            ticks    = Helper.ReadInt64(buf, ref pos);
            sendTime = new DateTime(ticks);

            // Parse the subsystem name

            cb        = Helper.ReadInt16(buf, ref pos);
            subsystem = ansi.GetString(buf, pos, cb);
            pos      += cb;

            // Parse the detail level

            detail = Helper.ReadByte(buf, ref pos);

            // Parse the event string

            cb     = Helper.ReadInt16(buf, ref pos);
            tEvent = ansi.GetString(buf, pos, cb);
            pos   += cb;

            // Parse the summary string

            cb      = Helper.ReadInt16(buf, ref pos);
            summary = ansi.GetString(buf, pos, cb);
            pos    += cb;

            // Parse the details string

            cb      = Helper.ReadInt16(buf, ref pos);
            details = ansi.GetString(buf, pos, cb);
            pos    += cb;
        }
    }
}
