//-----------------------------------------------------------------------------
// FILE:        IPToGeoFixAck.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the message that returns a GeoFix in response to a
//              IPToGeoFixMsg query.

using System;
using System.Net;

using LillTek.Common;
using LillTek.GeoTracker;
using LillTek.Messaging;

namespace LillTek.GeoTracker.Msgs
{

    /// <summary>
    /// Implements the message that returns a <see cref="GeoFix" /> in response to a
    /// <see cref="IPToGeoFixMsg" /> query.
    /// </summary>
    public sealed class IPToGeoFixAck : PropertyMsg, IAck
    {
        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public new static string GetTypeID()
        {
            return "LT.Geo.IPToGeoFixAck";
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public IPToGeoFixAck()
        {
        }

        /// <summary>
        /// Constructs the ack instance to be used to communicate an
        /// exception back to the client.
        /// </summary>
        /// <param name="e">The exception.</param>
        public IPToGeoFixAck(Exception e)
        {
            base["_exception"]      = e.Message;
            base["_exception-type"] = e.GetType().FullName;
        }

        /// <summary>
        /// Constructs the ack instance to be used to communicate the configuration text
        /// text back to the client.
        /// </summary>
        /// <param name="fix">The <see cref="GeoFix" /> to be returned or <c>null</c>.
        /// </param>
        public IPToGeoFixAck(GeoFix fix)
        {
            this.GeoFix = fix;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private IPToGeoFixAck(Stub param)
            : base(param)
        {
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            IPToGeoFixAck clone;

            clone = new IPToGeoFixAck(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        /// <summary>
        /// The <see cref="GeoFix" /> instance being returned or <c>null</c> if the
        /// GeoTracker server could not map the IP address to a location.
        /// </summary>
        public GeoFix GeoFix
        {
            get { return GeoFix.Parse(base["fix"]); }
            set { base["fix"] = value == null ? null : value.ToString(); }
        }

        //---------------------------------------------------------------------
        // IAck Implementation

        /// <summary>
        /// The exception's message string if the was an exception detected
        /// on by the server (null or the empty string if there was no error).
        /// </summary>
        public string Exception
        {
            get { return base["_exception"]; }
            set { base["_exception"] = value; }
        }

        /// <summary>
        /// The fully qualified name of the exception type.
        /// </summary>
        public string ExceptionTypeName
        {
            get { return (string)base["_exception-type"]; }
            set { base["_exception-type"] = value; }
        }
    }
}
