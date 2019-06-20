//-----------------------------------------------------------------------------
// FILE:        RadiusException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The exception thrown to indicate RADIUS errors.

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;

using LillTek.Common;

namespace LillTek.Net.Radius
{
    /// <summary>
    /// The exception thrown to indicate RADIUS errors.
    /// </summary>
    public sealed class RadiusException : ApplicationException, ICustomExceptionLogger
    {
        private byte[] packet = null;  // The raw received packet bytes (or null)

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message arguments.</param>
        public RadiusException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="packet">The raw received packet bytes (or <c>null</c>).</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message arguments.</param>
        public RadiusException(byte[] packet, string format, params object[] args)
            : base(string.Format(format, args))
        {
            this.packet = packet;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message arguments.</param>
        public RadiusException(Exception innerException, string format, params object[] args)
            : base(string.Format(format, args), innerException)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="packet">The raw bytes of RADIUS packet (or <c>null</c>).</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message arguments.</param>
        public RadiusException(byte[] packet, Exception innerException, string format, params object[] args)
            : base(string.Format(format, args), innerException)
        {
            this.packet = packet;
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
            if (packet == null)
                return;

            sb.AppendLine("Packet (formatted):");
            sb.AppendLine(Helper.HexDump(packet, 16, HexDumpOption.ShowAll));
            sb.AppendLine("");
            sb.AppendLine("Packet (raw):");
            sb.AppendLine(Helper.HexDump(packet, 16, HexDumpOption.None));
        }
    }
}
