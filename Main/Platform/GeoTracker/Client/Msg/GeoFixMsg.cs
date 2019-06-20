//-----------------------------------------------------------------------------
// FILE:        GeoFixMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Sent by GeoTrackerClient instances to the cluster to submit a set of
//              GeoFixes for an entity.

using System;
using System.Collections.Generic;
using System.Net;

using LillTek.Common;
using LillTek.GeoTracker;
using LillTek.Messaging;

namespace LillTek.GeoTracker.Msgs
{
    /// <summary>
    /// Sent by <see cref="GeoTrackerClient" /> instances to the cluster to submit a set of
    /// <see cref="GeoFix" />es for an entity.
    /// </summary>
    public sealed class GeoFixMsg : Msg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static string GetTypeID()
        {
            return "LT.Geo.FixMsg";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public GeoFixMsg()
        {
        }

        /// <summary>
        /// Constructs a message with a single <see cref="GeoFix" />.
        /// </summary>
        /// <param name="entityID">The unique entity ID.</param>
        /// <param name="groupID">The group ID or <c>null</c>.</param>
        /// <param name="fix">The <see cref="GeoFix" /> to be recorded.</param>
        public GeoFixMsg(string entityID, string groupID, GeoFix fix)
        {
            this.EntityID = entityID;
            this.GroupID  = groupID;
            this.Fixes    = new GeoFix[] { fix };
        }

        /// <summary>
        /// Constructs a message with multiple <see cref="GeoFix" />es.
        /// </summary>
        /// <param name="entityID">The unique entity ID.</param>
        /// <param name="groupID">The group ID or <c>null</c>.</param>
        /// <param name="fixes">The <see cref="GeoFix" />es to be recorded.</param>
        public GeoFixMsg(string entityID, string groupID, List<GeoFix> fixes)
        {
            this.EntityID = entityID;
            this.GroupID  = groupID;
            this.Fixes    = fixes.ToArray();
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            GeoFixMsg clone;

            clone = new GeoFixMsg();
            clone.CopyBaseFields(this, true);

            clone.EntityID = this.EntityID;
            clone.GroupID  = this.GroupID;
            clone.Fixes    = new GeoFix[this.Fixes.Length];

            Array.Copy(this.Fixes, clone.Fixes, this.Fixes.Length);

            return clone;
        }

        /// <summary>
        /// The unique entity ID.
        /// </summary>
        public string EntityID { get; set; }

        /// <summary>
        /// The group ID or <c>null</c>.
        /// </summary>
        public string GroupID { get; set; }

        /// <summary>
        /// The <see cref="GeoFix" />.
        /// </summary>
        public GeoFix[] Fixes { get; set; }

        /// <summary>
        /// Serializes the payload of the message into the stream passed.
        /// </summary>
        /// <param name="es">The enhanced stream where the output is to be written.</param>
        protected override void WritePayload(EnhancedStream es)
        {
            es.WriteString16(EntityID);
            es.WriteString16(GroupID);

            es.WriteInt32(Fixes.Length);
            for (int i = 0; i < Fixes.Length; i++)
                es.WriteString16(Fixes[i].ToString());
        }

        /// <summary>
        /// Loads the message payload from the stream passed.
        /// </summary>
        /// <param name="es">The enhanced stream where the output is to be written.</param>
        /// <param name="cbPayload">Number of bytes of payload data.</param>
        protected override void ReadPayload(EnhancedStream es, int cbPayload)
        {
            int cFixes;

            EntityID = es.ReadString16();
            GroupID  = es.ReadString16();

            cFixes   = es.ReadInt32();
            Fixes    = new GeoFix[cFixes];

            for (int i = 0; i < cFixes; i++)
                Fixes[i] = new GeoFix(es.ReadString16());
        }
    }
}
