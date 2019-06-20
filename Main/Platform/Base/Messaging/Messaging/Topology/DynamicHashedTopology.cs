//-----------------------------------------------------------------------------
// FILE:        DynamicHashedTopology.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a dynamic hashed topology plug-in.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using LillTek.Common;

// $todo(jeff.lill): 
//
// The current implementation relies on monitoring changes to
// a message router's routing table and requires that the
// router be in P2P mode and that all service instances are
// on the same subnet.  I could relax all of these restrictions
// if I made use of the new ClusterMember class.  Revisit this
// when the time comes to integrate topologies into ClusterMember.

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements a dynamic hashed <see cref="ITopologyProvider" /> plug-in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This topology implementation is designed to hash traffic against dynamically
    /// generated and changing cluster service instance endpoints.  This is useful
    /// for service topologies where it is not necessary to enforce a strict relationship
    /// between hashed object and service instances.  Note that this cluster implementation
    /// relies on the message router to be configured for peer-to-peer routing and
    /// for the cluster service instances to be on the same subnet to work properly.
    /// </para>
    /// <para>
    /// The <b>topology-args</b> parameter specifies the additional parameters passed
    /// to the <see cref="OpenClient" /> and <see cref="OpenServer" /> methods.
    /// This parameter specifies a set of name/value pairs formatted for as described
    /// in <see cref="ArgCollection" />.  The <b>clusterEP</b> parameter is 
    /// required and specifies the cluster endpoint to use when opening the cluster.
    /// </para>
    /// <para><b><u>How this works</u></b></para>
    /// <para>
    /// When a service server implementation calls <see cref="TopologyHelper.OpenServer" /> it 
    /// passes the fully qualified configuration key for the topology arguments and the dynamic 
    /// scope name.  <see cref="TopologyHelper.OpenServer" /> will instantiate a <see cref="DynamicHashedTopology" /> 
    /// instance and then load the arguments into a <see cref="ArgCollection" /> and then call the 
    /// topology's <see cref="OpenServer" /> method, passing arguments as well as the <b>clusterEP</b> 
    /// endpoint agument explicitly extracted from the arguments.
    /// </para>
    /// <para>
    /// The topology generates a unique service endpoint by appending a generated GUID
    /// segment unto the end of the <b>clusterEP</b> and then registers this with the
    /// router for any message handlers in the target object that match the specified
    /// dynamic scope.
    /// </para>
    /// <para>
    /// Client side <see cref="DynamicHashedTopology" /> instances are created by calling
    /// <see cref="TopologyHelper.OpenClient(MsgRouter,string,string)" />, passing the router, the 
    /// topology arguments as well as the cluster endpoint extracted from the arguments.  The 
    /// client topology queries the router's logical route table for endpoints that match "clusterEP/*"
    /// and registers an callback to handle notifications from the router's
    /// <see cref="MsgRouter.LogicalRouteChange" /> event.  This information is used to
    /// create an internal sorted array of cluster endpoints.
    /// </para>
    /// <para>
    /// The <b>param</b> parameter passed to the <b>Query</b>, <b>Send</b>, and other
    /// client side methods is used to generate a hash code that is then used to
    /// distribute requests across the cluster's service instances.  The <b>param</b>
    /// object's <see cref="object.GetHashCode" /> method will be called to calculate
    /// a 32-bit hash code.  If <b>param</b> is <c>null</c>, then a random number will be
    /// generated instead.  The modulus of the hash code and number of service instance
    /// endpoints will be used as an index into the array of service instance endpoints
    /// and the request will be sent to the corresponding service instance.
    /// </para>
    /// <para><b><u>Topology Arguments</u></b></para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Argument</th>        
    /// <th width="1">Required For</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>clusterEP</td><td>Client &amp; Server</td><td>Specifies the cluster endpoint</td>
    /// </tr>
    /// </table>
    /// </div>
    /// </remarks>
    public class DynamicHashedTopology : ITopologyProvider, ILockable
    {
        private const string NoP2PMsg = "Router is not P2P enabled. [DynamicHashedTopology] requires P2P to enable full functionality.";

        private MsgRouter       router;             // The application's message router
        private Guid            instanceID;         // The globally unique instance ID
        private MsgEP           clusterEP;          // The cluster endpoint
        private bool            isClient;           // True for client mode, false for server mode
        private string          serialized;         // Serialized client arguments
        private MsgEP[]         instances;          // The service instances
        private MsgEP           instanceEP;         // This instance's endpoint
        private MsgEP           broadcastEP;        // The cluster broadcast endpoint
        private MethodDelegate  onLogicalChange;    // Handles logical route table change notifications

        /// <summary>
        /// Constructor.
        /// </summary>
        public DynamicHashedTopology()
        {
            this.router          = null;
            this.onLogicalChange = new MethodDelegate(OnLogicalChange);
        }

        //---------------------------------------------------------------------
        // ITopologyProvider implementation

        /// <summary>
        /// Returns flag bits describing optional topology specific capabilities.
        /// </summary>
        public TopologyCapability Capabilities
        {
            get { return TopologyCapability.Locality | TopologyCapability.Dynamic; }
        }

        /// <summary>
        /// Returns the endpoint to be used for cluster requests taking the parameter passed
        /// into account.
        /// </summary>
        /// <param name="key">The optional topology implementation specific key (or <c>null</c>).</param>
        /// <returns>The endpoint of the cluster service instance where the request is to be directed.</returns>
        private MsgEP GetRequestEP(object key)
        {
            int hash;

            using (TimedLock.Lock(this))
            {
                if (instances.Length == 0)
                    return broadcastEP;     // We don't know of any specific endpoints
                                            // yet so send this to any possible service instance.

                hash = key == null ? Helper.Rand() : key.GetHashCode();
                return instances[hash % instances.Length];
            }
        }

        /// <summary>
        /// Handles updates to the message router's logical route table by updating the
        /// set of cluster service instance endpoints.
        /// </summary>
        private void OnLogicalChange()
        {
            List<LogicalRoute>  routes;
            string[]            sorted;

            try
            {
                using (TimedLock.Lock(this))
                {
                    if (router == null)
                        return;     // Looks like the cluster was closed somewhere along the line
                                    // so ignore this notification

                    routes = router.LogicalRoutes.GetRoutes(broadcastEP);
                    sorted = new string[routes.Count];
                    for (int i = 0; i < routes.Count; i++)
                        sorted[i] = routes[i].LogicalEP;

                    Array.Sort(sorted, StringComparer.OrdinalIgnoreCase);

                    instances = new MsgEP[sorted.Length];
                    for (int i = 0; i < sorted.Length; i++)
                        instances[i] = sorted[i];
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Opens the topology instance in client mode.
        /// </summary>
        /// <param name="router">The message router to be used by the cluster.</param>
        /// <param name="clusterEP">The cluster's logical endpoint.</param>
        /// <param name="args">Topology implementation specific parameters (ignored for this implementation).</param>
        /// <remarks>
        /// </remarks>
        public virtual void OpenClient(MsgRouter router, MsgEP clusterEP, ArgCollection args)
        {
            if (!clusterEP.IsLogical)
                throw new ArgumentException(TopologyHelper.ClusterEPNotLogicalMsg);

            if (!router.EnableP2P)
                SysLog.LogWarning(NoP2PMsg);

            this.router      = router;
            this.instanceID  = Helper.NewGuid();
            this.clusterEP   = clusterEP;
            this.broadcastEP = new MsgEP(clusterEP, "*");
            this.instanceEP  = null;
            this.isClient    = true;

            ArgCollection argsCopy;

            argsCopy                  = args.Clone();
            argsCopy["topology-type"] = TopologyHelper.SerializeType(this.GetType());
            serialized                = argsCopy.ToString();

            OnLogicalChange();      // Forces the initial load of the instance EPs
        }

        /// <summary>
        /// Opens the topology instance in server mode.
        /// </summary>
        /// <param name="router">The message router to be used by the cluster.</param>
        /// <param name="dynamicScope">The dynamic scope name.</param>
        /// <param name="target">The target object whose dynamic message handlers are to be registered (or <c>null</c>).</param>
        /// <param name="clusterEP">The cluster's logical endpoint.</param>
        /// <param name="args">Topology implementation specific parameters (ignored for this implementation).</param>
        /// <remarks>
        /// <para>
        /// This method also registers the dynamic message handlers that case-insensitvely
        /// match the dynamicScope parameter passed that are found within the
        /// target object passed with the router's message dispatcher, after performing
        /// any necessary munging of their message endpoints.  This is done by
        /// matching the dynamicScope parameter passed against the <see cref="MsgHandler.DynamicScope" />
        /// property in the <see cref="MsgHandler" /> attribute tagging the message handler.
        /// </para>
        /// <para>
        /// The matching message handler endpoints will be set to clusterEP.
        /// </para>
        /// </remarks>
        public virtual void OpenServer(MsgRouter router, string dynamicScope, MsgEP clusterEP, object target, ArgCollection args)
        {
            if (!clusterEP.IsLogical)
                throw new ArgumentException(TopologyHelper.ClusterEPNotLogicalMsg);

            if (!router.EnableP2P)
                SysLog.LogWarning(NoP2PMsg);

            this.router      = router;
            this.instanceID  = Helper.NewGuid();
            this.clusterEP   = clusterEP;
            this.broadcastEP = new MsgEP(clusterEP, "*");
            this.instanceEP  = new MsgEP(clusterEP, Helper.NewGuid().ToString());
            this.isClient    = false;

            if (target != null)
                router.Dispatcher.AddTarget(target, dynamicScope, this, null);

            OnLogicalChange();      // Forces the initial load of the instance EPs
        }

        /// <summary>
        /// Closes the topology, releasing all resources.
        /// </summary>
        /// <remarks>
        /// <note>
        /// It is not an error to call this method if the cluster
        /// is already closed.
        /// </note>
        /// </remarks>
        public virtual void Close()
        {
            using (TimedLock.Lock(this))
            {
                if (router != null)
                {
                    router.LogicalRouteChange -= onLogicalChange;   // Disable these notifications
                    router = null;
                }
            }
        }

        /// <summary>
        /// Returns a topology instance's globally unique ID.
        /// </summary>
        /// <remarks>
        /// The instance ID is used by implementations of the <see cref="IReliableMessenger" />
        /// interface to implement an efficient mechanism for caching client side
        /// topology instance references on the server side of messengers.
        /// </remarks>
        public virtual Guid InstanceID
        {
            get { return instanceID; }
        }

        /// <summary>
        /// Returns the cluster's endpoint.
        /// </summary>
        public virtual MsgEP ClusterEP
        {
            get { return clusterEP; }
        }

        /// <summary>
        /// Serializes the instantiation parameters necessary to transmit and then 
        /// reconstitute a client topology instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is useful for situations like reliable messaging implementations where
        /// knowledge of the target topology must be transmitted to a reliable messaging
        /// service and perhaps be persistently stored.
        /// </para>
        /// <para>
        /// This property returns a set of name/value pairs formatted as described
        /// in <see cre="ArgCollection" />.  These are the same set of arguments passed
        /// to the <see cref="OpenClient" /> method with the addition of the <b>topology-type</b>
        /// argument.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the cluster is not opened as a client.</exception>
        public virtual string SerializeClient()
        {
            if (!IsClient)
                throw new InvalidOperationException(TopologyHelper.NotClientMsg);

            return serialized;
        }

        /// <summary>
        /// Serialize a topology parameter object to a string.
        /// </summary>
        /// <param name="key">The topology key object (or <c>null</c>).</param>
        /// <returns>A string representation of the parameter or <c>null</c>.</returns>
        /// <remarks>
        /// This implementation returns the serialized form of <see cref="HashedTopologyKey" />
        /// or <c>null</c> if <paramref name="key" /> is <c>null</c>.
        /// </remarks>
        public virtual string SerializeKey(object key)
        {
            if (key == null)
                return null;

            return new HashedTopologyKey(key).ToString();
        }

        /// <summary>
        /// Unserializes a typology parameter object from a string.
        /// </summary>
        /// <param name="key">The string version of the key (or <c>null</c>).</param>
        /// <returns>The unserialized object (or <c>null</c>).</returns>
        /// <remarks>
        /// This implementation returns an unserialized instance of <see cref="HashedTopologyKey" />
        /// or <c>null</c> if the string passed is <c>null</c>.
        /// </remarks>
        public virtual object UnserializeKey(string key)
        {
            if (key == null)
                return null;

            return new HashedTopologyKey(key);
        }

        /// <summary>
        /// Returns <c>true</c> if the topology provider was opened in client mode
        /// false for server mode.
        /// </summary>
        public virtual bool IsClient
        {
            get { return isClient; }
        }

        /// <summary>
        /// Returns the current set of cluster service instance endpoints.
        /// </summary>
        public virtual MsgEP[] InstanceEPs
        {
            get
            {
                MsgEP[] copy;

                copy = new MsgEP[instances.Length];
                Array.Copy(instances, copy, instances.Length);
                return copy;
            }
        }

        /// <summary>
        /// Performs a synchronous query on the cluster by selecting a cluster endpoint
        /// using the topology specific parameter <pararef name="key" /> and 
        /// then invoking the query on that endpoint. 
        /// </summary>
        /// <param name="key">An optional cluster implementation specific key (or <c>null</c>).</param>
        /// <param name="query">The query message.</param>
        /// <returns>The query response.</returns>
        public virtual Msg Query(object key, Msg query)
        {
            if (router == null)
                throw new InvalidOperationException(TopologyHelper.ClosedMsg);

            return router.Query(GetRequestEP(key), query);
        }

        /// <summary>
        /// Performs a asynchronous query on the cluster by selecting a cluster endpoint
        /// using the topology specific parameter <pararef name="key" /> and 
        /// then invoking the query on that endpoint. 
        /// </summary>
        /// <param name="key">The optional topology implementation specific key (or <c>null</c>).</param>
        /// <param name="query">The query message.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">The application defined state to be associated with the operation.</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation.</returns>
        /// <remarks>
        /// <note>
        /// All calls to <see cref="BeginQuery" /> must be matched with a call to <see cref="EndQuery" />.
        /// </note>
        /// </remarks>
        public virtual IAsyncResult BeginQuery(object key, Msg query, AsyncCallback callback, object state)
        {
            if (router == null)
                throw new InvalidOperationException(TopologyHelper.ClosedMsg);

            return router.BeginQuery(GetRequestEP(key), query, callback, state);
        }

        /// <summary>
        /// Completes an asynchronous <see cref="BeginQuery" /> operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginQuery" />.</param>
        /// <returns>The response message.</returns>
        /// <exception cref="TimeoutException">Thrown if the timeout limit to receive a response has been exceeded.</exception>
        public virtual Msg EndQuery(IAsyncResult ar)
        {
            if (router == null)
                throw new InvalidOperationException(TopologyHelper.ClosedMsg);

            return router.EndQuery(ar);
        }

        /// <summary>
        /// Selects a cluster endpoint using the topology specific parameter <pararef name="key" /> and 
        /// then transmits a message to that endpoint. 
        /// </summary>
        /// <param name="key">The optional topology implementation specific key (or <c>null</c>).</param>
        /// <param name="msg">The message to be sent.</param>
        public virtual void Send(object key, Msg msg)
        {
            if (router == null)
                throw new InvalidOperationException(TopologyHelper.ClosedMsg);

            router.SendTo(GetRequestEP(key), msg);
        }

        /// <summary>
        /// Selects a cluster endpoint using the topology specific parameter <pararef name="key" /> and 
        /// then transmits a message with a specific source endpoint to the target endpoint. 
        /// </summary>
        /// <param name="key">The optional topology implementation specific key (or <c>null</c>).</param>
        /// <param name="fromEP">The endpoint to set in the message's <see cref="Msg._FromEP" /> property.</param>
        /// <param name="msg">The message to be sent.</param>
        public virtual void Send(object key, MsgEP fromEP, Msg msg)
        {
            if (router == null)
                throw new InvalidOperationException(TopologyHelper.ClosedMsg);

            router.SendTo(GetRequestEP(key), fromEP, msg);
        }

        /// <summary>
        /// Broadcasts a message to the cluster.
        /// </summary>
        /// <param name="key">The optional topology implementation specific key (or <c>null</c>).</param>
        /// <param name="msg">The message to be broadcast.</param>
        public virtual void Broadcast(object key, Msg msg)
        {
            if (router == null)
                throw new InvalidOperationException(TopologyHelper.ClosedMsg);

            router.BroadcastTo(broadcastEP, msg);
        }

        /// <summary>
        /// Performs a synchronous parallel query on the cluster.
        /// </summary>
        /// <param name="key">The optional topology implementation specific key (or <c>null</c>).</param>
        /// <param name="parallelQuery">Holds the query operations to be performed in parallel.</param>
        /// <returns>The parallel query operations with the query results.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no query operations are specified.</exception>
        /// <exception cref="InvalidOperationException">Thrown if a query operation or query message is passed more than once.</exception>
        public ParallelQuery ParallelQuery(object key, ParallelQuery parallelQuery)
        {
            var ar = BeginParallelQuery(key, parallelQuery, null, null);

            return EndParallelQuery(ar);
        }

        /// <summary>
        /// Initiates an asynchronous parallel query on the cluster.
        /// </summary>
        /// <param name="key">The optional topology implementation specific key (or <c>null</c>).</param>
        /// <param name="parallelQuery">Holds the query operations to be performed in parallel.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">The application defined state to be associated with the operation.</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no query operations are specified.</exception>
        /// <exception cref="InvalidOperationException">Thrown if a query operation or query message is passed more than once.</exception>
        /// <remarks>
        /// <note>
        /// All calls to <see cref="BeginParallelQuery" /> must be matched with a call to
        /// <see cref="EndParallelQuery" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginParallelQuery(object key, ParallelQuery parallelQuery, AsyncCallback callback, object state)
        {
            var requestEP = GetRequestEP(key);

            foreach (var operation in parallelQuery.Operations)
                operation.QueryEP = requestEP;

            return router.BeginParallelQuery(parallelQuery, callback, state);
        }

        /// <summary>
        /// Completes an asynchronous <see cref="BeginQuery" /> operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginQuery" />.</param>
        /// <returns>The parallel query operations with the query results.</returns>
        /// <exception cref="TimeoutException">Thrown if the timeout limit to receive a response has been exceeded.</exception>
        public ParallelQuery EndParallelQuery(IAsyncResult ar)
        {
            return router.EndParallelQuery(ar);
        }

        //---------------------------------------------------------------------
        // IDynamicEPMunger implementation

        /// <summary>
        /// Dynamically modifies a message handler's endpoint just before it is registered
        /// with a <see cref="MsgRouter" />'s <see cref="IMsgDispatcher" />.  This method
        /// may only be called for topology providers opened in server mode.
        /// </summary>
        /// <param name="logicalEP">The message handler's logical endpoint.</param>
        /// <param name="handler">The message handler information.</param>
        /// <returns>The logical endpoint to actually register for the message handler.</returns>
        /// <remarks>
        /// This returns the instance endpoint determined by parsing the cluster
        /// arguments in <see cref="OpenServer" />.
        /// </remarks>
        public virtual MsgEP Munge(MsgEP logicalEP, MsgHandler handler)
        {
            if (!logicalEP.IsLogical)
                throw new ArgumentException(TopologyHelper.LogicalEPMsg, "logicalEP");

            if (isClient)
                throw new InvalidOperationException(TopologyHelper.NotServerMsg);

            return instanceEP;
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better deadlock
        /// diagnostic information.
        /// </summary>
        /// <returns>The process unique lock key for this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
