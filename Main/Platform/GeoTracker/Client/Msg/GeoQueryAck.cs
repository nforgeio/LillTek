//-----------------------------------------------------------------------------
// FILE:        GeoFixAck.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Encapsulates a GeoTracker query result for transmission from the 
//              server cluster to the client.

using System;
using System.Collections.Generic;
using System.Net;

using LillTek.Common;
using LillTek.GeoTracker;
using LillTek.Messaging;

namespace LillTek.GeoTracker.Msgs
{
    /// <summary>
    /// Encapsulates a GeoTracker query result for transmission from the 
    /// server cluster to the client.
    /// </summary>
    public sealed class GeoFixAck : BlobPropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public new static string GetTypeID()
        {
            return "LT.Geo.QueryAck";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public GeoFixAck()
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
            GeoFixAck clone;

            clone = new GeoFixAck();
            clone.CopyBaseFields(this, true);

            return clone;
        }
    }
}
