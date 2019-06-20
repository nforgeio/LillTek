//-----------------------------------------------------------------------------
// FILE:        NamespaceDoc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Client side class used to access the GeoTracker services.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

using LillTek.Common;
using LillTek.GeoTracker.Msgs;
using LillTek.Messaging;

namespace LillTek.GeoTracker
{
    /// <summary>
    /// Client side class used to access the GeoTracker services.
    /// </summary>
    public class GeoTrackerClient
    {
        private MsgRouter                   router;
        private GeoTrackerClientSettings    settings;
        private MsgEP                       serverEP;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="router">The message router.</param>
        /// <param name="settings">The client side settings or <c>null</c> to use defaults.</param>
        public GeoTrackerClient(MsgRouter router, GeoTrackerClientSettings settings)
        {
            // Register the GeoTracker messages types with LillTek Messaging.

            LillTek.GeoTracker.Global.RegisterMsgTypes();

            // Other initialization

            if (settings == null)
                settings = new GeoTrackerClientSettings();

            this.router   = router;
            this.settings = settings;
            this.serverEP = new MsgEP(settings.ServerEP);
        }

        /// <summary>
        /// Synchronously queries the GeoTracker cluster to geocode an IP address into a <see cref="GeoFix" />.
        /// </summary>
        /// <param name="address">The IP address.</param>
        /// <returns>The <see cref="GeoFix" /> if a mapping was possible, <c>null</c> otherwise.</returns>
        /// <exception cref="TimeoutException">Thrown if the cluster did not respond in a reasonable amount of time.</exception>
        /// <exception cref="NotAvailableException">Thrown if IP Geocoding is disabled for the cluster.</exception>
        /// <exception cref="NotSupportedException">Thrown for non-IPv4 addresses.</exception>
        public GeoFix IPToGeoFix(IPAddress address)
        {
            var ar = BeginIPToGeoFix(address, null, null);

            return EndIPToGeoFix(ar);
        }

        /// <summary>
        /// Initiates an asynchronous query the GeoTracker cluster to geocode an IP address into a <see cref="GeoFix" />.
        /// </summary>
        /// <param name="address">The IP address.</param>
        /// <param name="callback">Delegate called when the operation completes or <c>null</c>.</param>
        /// <param name="state">Application-defined state or <c>null</c>.</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the state of the operation.</returns>
        /// <exception cref="NotSupportedException">Thrown for non-IPv4 addresses.</exception>
        /// <remarks>
        /// <note>
        /// All calls to <see cref="BeginIPToGeoFix" /> must eventually be followed by a call to <see cref="EndIPToGeoFix" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginIPToGeoFix(IPAddress address, AsyncCallback callback, object state)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork)
                throw new NotSupportedException(string.Format("GeoTracker: [{0}] network addresses cannot be geocoded. Only IPv4 addresses are supported.", address.AddressFamily));

            return router.BeginQuery(settings.ServerEP, new IPToGeoFixMsg(address), callback, state);
        }

        /// <summary>
        /// Completes an asynchronous <see cref="BeginIPToGeoFix "/> operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by the matching <see cref="BeginIPToGeoFix "/> call.</param>
        /// <returns>The <see cref="GeoFix" /> if a mapping was possible, <c>null</c> otherwise.</returns>
        /// <exception cref="TimeoutException">Thrown if the cluster did not respond in a reasonable amount of time.</exception>
        /// <exception cref="NotAvailableException">Thrown if IP Geocoding is disabled for the cluster.</exception>
        public GeoFix EndIPToGeoFix(IAsyncResult ar)
        {
            var ack = (IPToGeoFixAck)router.EndQuery(ar);

            return ack.GeoFix;
        }

        /// <summary>
        /// Synchronously submits a <see cref="GeoFix" /> for an entity to the GeoTracker cluster.
        /// </summary>
        /// <param name="entityID">The unique entity ID.</param>
        /// <param name="groupID">The group ID or <c>null</c>.</param>
        /// <param name="fix">The <see cref="GeoFix" /> being submitted.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entityID" /> or <paramref name="fix" /> are <c>null</c>.</exception>
        public void SubmitEntityFix(string entityID, string groupID, GeoFix fix)
        {
            var ar = BeginSubmitEntityFix(entityID, groupID, fix, null, null);

            EndSubmitEntityFix(ar);
        }

        /// <summary>
        /// Initiates an asynchronous operation to submits a <see cref="GeoFix" />
        /// for an entity to the GeoTracker cluster.
        /// </summary>
        /// <param name="entityID">The unique entity ID.</param>
        /// <param name="groupID">The group ID or <c>null</c>.</param>
        /// <param name="fix">The <see cref="GeoFix" /> being submitted.</param>
        /// <param name="callback">The completion callback or <c>null</c>.</param>
        /// <param name="state">Application state or <c>null</c>.</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the progress of the operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entityID" /> or <paramref name="fix" /> is <c>null</c>.</exception>
        /// <remarks>
        /// <note>
        /// All calls to <see cref="BeginSubmitEntityFix" /> must eventually be followed by a call to <see cref="EndSubmitEntityFix" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginSubmitEntityFix(string entityID, string groupID, GeoFix fix, AsyncCallback callback, object state)
        {
            if (entityID == null)
                throw new ArgumentNullException("entityID");

            if (fix == null)
                throw new ArgumentNullException("fix");

            return router.BeginQuery(settings.ServerEP, new GeoFixMsg(entityID, groupID, fix), callback, state);
        }

        /// <summary>
        /// Completes an asynchronous single entity <see cref="GeoFix" /> submission operation initiated by a
        /// call to <see cref="BeginSubmitEntityFix" />.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginSubmitEntityFix" />.</param>
        public void EndSubmitEntityFix(IAsyncResult ar)
        {
            router.EndQuery(ar);
        }

        /// <summary>
        /// Synchronously submits a set of <see cref="GeoFix" /> for an entity to the GeoTracker cluster.
        /// </summary>
        /// <param name="entityID">The unique entity ID.</param>
        /// <param name="groupID">The group ID or <c>null</c>.</param>
        /// <param name="fixes">The set of <see cref="GeoFix" />es being submitted.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entityID" /> or <paramref name="fixes" /> is <c>null</c>.</exception>
        public void SubmitEntityFixes(string entityID, string groupID, List<GeoFix> fixes)
        {
            var ar = BeginSubmitEntityFixes(entityID, groupID, fixes, null, null);

            EndSubmitEntityFixes(ar);
        }

        /// <summary>
        /// Initiates an asynchronous operation to submits set of <see cref="GeoFix" />es
        /// for an entity to the GeoTracker cluster.
        /// </summary>
        /// <param name="entityID">The unique entity ID.</param>
        /// <param name="groupID">The group ID or <c>null</c>.</param>
        /// <param name="fixes">The set of <see cref="GeoFix" />es being submitted.</param>
        /// <param name="callback">The completion callback or <c>null</c>.</param>
        /// <param name="state">Application state or <c>null</c>.</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the progress of the operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entityID" /> or <paramref name="fixes" /> are <c>null</c>.</exception>
        /// <remarks>
        /// <note>
        /// All calls to <see cref="BeginSubmitEntityFixes" /> must eventually be followed by a call to <see cref="EndSubmitEntityFixes" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginSubmitEntityFixes(string entityID, string groupID, List<GeoFix> fixes, AsyncCallback callback, object state)
        {
            if (entityID == null)
                throw new ArgumentNullException("entityID");

            if (fixes == null)
                throw new ArgumentNullException("fixes");

            return router.BeginQuery(settings.ServerEP, new GeoFixMsg(entityID, groupID, fixes), callback, state);
        }

        /// <summary>
        /// Completes an asynchronous multiple entity <see cref="GeoFix" /> submission operation initiated by a
        /// call to <see cref="BeginSubmitEntityFixes" />.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginSubmitEntityFixes" />.</param>
        public void EndSubmitEntityFixes(IAsyncResult ar)
        {
            router.EndQuery(ar);
        }

        /// <summary>
        /// Performs a synchronous location based query against the GeoTracker cluster.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="query" /> is <c>null</c>.</exception>
        /// <returns>The query results.</returns>
        public GeoQueryResults Query(GeoQuery query)
        {
            if (query == null)
                throw new ArgumentNullException("query");

            throw new NotImplementedException();
        }

        /// <summary>
        /// Initiates an asynchronous location based query against the GeoTracker cluster.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="callback">The completion callback or <c>null</c>.</param>
        /// <param name="state">Application state or <c>null</c>.</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the progress of the operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="query" /> is <c>null</c>.</exception>
        /// <remarks>
        /// <note>
        /// All calls to <see cref="BeginQuery" /> must eventually be followed by a call to <see cref="EndQuery" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginQuery(GeoQuery query, AsyncCallback callback, object state)
        {
            if (query == null)
                throw new ArgumentNullException("query");

            throw new NotImplementedException();
        }

        /// <summary>
        /// Completes an asynchronous query operation initiated by a call to <see cref="BeginQuery" />.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginQuery" />.</param>
        public GeoQueryResults EndQuery(IAsyncResult ar)
        {
            throw new NotImplementedException();
        }
    }
}
