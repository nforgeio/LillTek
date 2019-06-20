//-----------------------------------------------------------------------------
// FILE:        DeadRouterMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Multicast when it appears that a router is dead because it failed
//              to respond to a session initiation in time.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

using LillTek.Common;

namespace LillTek.Messaging.Internal
{
    /// <summary>
    /// Multicast when it appears that a router is dead because it failed
    /// to respond to a session initiation in time.
    /// </summary>
    public sealed class DeadRouterMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return ".DeadRouter";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public DeadRouterMsg()
        {
            this._Flags = MsgFlag.Priority;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="routerEP">The dead router's physical endpoint.</param>
        /// <param name="logicalEndpointSetID">The GUID identifying the current set of logical endpoints implemented by the router.</param>
        public DeadRouterMsg(MsgEP routerEP, Guid logicalEndpointSetID)
        {
            this._Flags               = MsgFlag.Priority;
            this.RouterEP             = routerEP;
            this.LogicalEndpointSetID = logicalEndpointSetID;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private DeadRouterMsg(Stub param)
            : base(param)
        {
            this._Flags = MsgFlag.Priority;
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            DeadRouterMsg clone;

            clone = new DeadRouterMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        /// <summary>
        /// The dead router's physical endpoint.
        /// </summary>
        public MsgEP RouterEP
        {
            get { return base._Get("router-ep"); }

            set
            {
                Assertion.Test(value.IsPhysical);
                base._Set("router-ep", value.ToString(-1, false));
            }
        }

        /// <summary>
        /// The GUID identifying the current set of logical endpoints implemented by the router.
        /// </summary>
        public Guid LogicalEndpointSetID
        {
            get { return base._Get("logical-epset-id", Guid.Empty); }
            set { base._Set("logical-epset-id", value); }
        }
    }
}
