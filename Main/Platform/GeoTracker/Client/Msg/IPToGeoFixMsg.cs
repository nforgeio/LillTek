//-----------------------------------------------------------------------------
// FILE:        IPToGeoFixMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the query message sent by a GeoTrackerClient to the
//              cluster to map an IP address into a GeoFix.

using System;
using System.Net;

using LillTek.Common;
using LillTek.GeoTracker;
using LillTek.Messaging;

namespace LillTek.GeoTracker.Msgs
{
    /// <summary>
    /// Implements the query message sent by a <see cref="GeoTrackerClient" /> to the
    /// cluster to map an IP address into a <see cref="GeoFix" />.
    /// </summary>
    public sealed class IPToGeoFixMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return "LT.Geo.IPToGeoFixMsg";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public IPToGeoFixMsg()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="address">The IP address to be geocoded.</param>
        public IPToGeoFixMsg(IPAddress address)
        {
            this.Address = address;
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            IPToGeoFixMsg clone;

            clone = new IPToGeoFixMsg();
            clone.CopyBaseFields(this, true);

            return clone;
        }

        /// <summary>
        /// This host name of the client machine.
        /// </summary>
        public IPAddress Address
        {
            get { return IPAddress.Parse(base["address"]); }
            set { base["address"] = value.ToString(); }
        }
    }
}
