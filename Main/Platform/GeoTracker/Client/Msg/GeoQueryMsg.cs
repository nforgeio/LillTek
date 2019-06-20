//-----------------------------------------------------------------------------
// FILE:        GeoFixMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Encapsulates a GeoTracker query for transmission from the client to
//              the server cluster.

using System;
using System.Collections.Generic;
using System.Net;

using LillTek.Common;
using LillTek.GeoTracker;
using LillTek.Messaging;

namespace LillTek.GeoTracker.Msgs
{
    /// <summary>
    /// Encapsulates a GeoTracker query for transmission from the client to
    /// the server cluster.
    /// </summary>
    public sealed class GeoQueryMsg : BlobPropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public new static string GetTypeID()
        {
            return "LT.Geo.QueryMsg";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public GeoQueryMsg()
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
            GeoQueryMsg clone;

            clone = new GeoQueryMsg();
            clone.CopyBaseFields(this, true);

            return clone;
        }
    }
}
