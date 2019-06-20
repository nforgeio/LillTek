//-----------------------------------------------------------------------------
// FILE:        FlightEvent.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes an event that can be saved by the FlightRecorder.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Describes an event that can be saved by the FlightRecorder.
    /// </summary>
    public class FlightEvent
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public FlightEvent()
        {
        }

        /// <summary>
        /// Constructs an instance by reading from a <see cref="Stream" />.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        public FlightEvent(EnhancedStream stream)
        {
            this.TimeUtc        = new DateTime(stream.ReadInt64());
            this.OrganizationID = stream.ReadString16();
            this.UserID         = stream.ReadString16();
            this.SessionID      = stream.ReadString16(); ;
            this.Source         = stream.ReadString16();

            var value           = stream.ReadString16();
            this.SourceVersion  = value == null ? null : new Version(value);

            this.Operation     = stream.ReadString16();
            this.IsError       = stream.ReadBool();
            this.Details       = stream.ReadString32();

        }

        /// <summary>
        /// The event record time (UTC).
        /// </summary>
        public DateTime TimeUtc { get; set; }

        /// <summary>
        /// The related organization identifier (or <c>null</c>).
        /// </summary>
        public string OrganizationID { get; set; }

        /// <summary>
        /// The related user identifier (or <c>null</c>).
        /// </summary>
        public string UserID { get; set; }

        /// <summary>
        /// The related session identifier (or <c>null</c>).
        /// </summary>
        public string SessionID { get; set; }

        /// <summary>
        /// Identifies the event source (typically the name of the client application).
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Identifies the event source version (typically the version of the client application).
        /// </summary>
        public Version SourceVersion { get; set; }

        /// <summary>
        /// Identifies the operation performed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// As a convention, the operation property can be formatted to include extended
        /// information, including the type, context, and additional arguments using 
        /// colon (:) and brackets ([) and {]}.  Here are some examples:
        /// </para>
        /// <example>
        /// operation = <i>type</i><br></br>
        /// operation = <i>type</i>:<i>args</i><br></br>
        /// operation = <i>type</i>:<i>context</i>[<i>args</i>]
        /// </example>
        /// </remarks>
        public string Operation { get; set; }

        /// <summary>
        /// Indicates whether the operation succeeded or failed.
        /// </summary>
        public bool IsError { get; set; }

        /// <summary>
        /// The operation details.
        /// </summary>
        public string Details { get; set; }

        /// <summary>
        /// Persists the event to a stream.
        /// </summary>
        /// <param name="stream">The output stream.</param>
        public void Write(EnhancedStream stream)
        {
            stream.WriteInt64(this.TimeUtc.Ticks);
            stream.WriteString16(this.OrganizationID);
            stream.WriteString16(this.UserID);
            stream.WriteString16(this.SessionID);
            stream.WriteString16(this.Source);
            stream.WriteString16(this.SourceVersion != null ? this.SourceVersion.ToString() : null);
            stream.WriteString16(this.Operation);
            stream.WriteBool(this.IsError);
            stream.WriteString32(this.Details);
        }
    }
}
