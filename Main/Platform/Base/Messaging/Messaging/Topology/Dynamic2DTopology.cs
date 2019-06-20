//-----------------------------------------------------------------------------
// FILE:        Dynamic2DTopology.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a dynamic two dimensional topology plug-in.

using System;

using LillTek.Common;

// $todo(jeff.lill): Implement this

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements a dynamic two dimensional <see cref="ITopologyProvider" /> plug-in.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class Dynamic2DTopology : ITopologyProvider
    {

        private MsgRouter   router;         // The application's message router
        private Guid        instanceID;     // The globally unique topology instance ID
        private MsgEP       clusterEP;      // The cluster endpoint
        private bool        isClient;       // True for client mode, false for server mode
        private string      serialized;     // Serialized client arguments

        /// <summary>
        /// Constructor.
        /// </summary>
        public Dynamic2DTopology()
        {
            this.router = null;
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

            this.router     = router;
            this.instanceID = Helper.NewGuid();
            this.clusterEP  = clusterEP;
            this.isClient   = true;

            ArgCollection argsCopy;

            argsCopy                  = args.Clone();
            argsCopy["topology-type"] = TopologyHelper.SerializeType(this.GetType());
            serialized                = argsCopy.ToString();
        }

        /// <summary>
        /// Opens the topology instance in server mode.
        /// </summary>
        /// <param name="router">The message router to be used by the topology.</param>
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

            this.router     = router;
            this.instanceID = Helper.NewGuid();
            this.clusterEP  = clusterEP;
            this.isClient   = false;

            if (target != null)
                router.Dispatcher.AddTarget(target, dynamicScope, this, null);
        }

        /// <summary>
        /// Closes the topology, releasing all resources.
        /// </summary>
        /// <remarks>
        /// <note>
        /// It is not an error to call this method if the topology
        /// is already closed.
        /// </note>
        /// </remarks>
        public virtual void Close()
        {
            router = null;
        }

        /// <summary>
        /// Returns a topology instance's globally unique ID.
        /// </summary>
        /// <remarks>
        /// The topology ID is used by implementations of the <see cref="IReliableMessenger" />
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
        public MsgEP ClusterEP
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
        /// <exception cref="InvalidOperationException">Thrown if the topology provider is not opened as a client.</exception>
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
        /// This implementation ignores the parameter and returns <c>null</c>.
        /// </remarks>
        public virtual string SerializeKey(object key)
        {
            return null;
        }

        /// <summary>
        /// Unserializes a topology parameter object from a string.
        /// </summary>
        /// <param name="key">The string version of the key (or <c>null</c>).</param>
        /// <returns>The unserialized object (or <c>null</c>).</returns>
        /// <remarks>
        /// This implementation ignores the parameter and returns null.
        /// </remarks>
        public virtual object UnserializeKey(string key)
        {
            return null;
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
        /// Performs a synchronous query on the cluster by selecting a cluster endpoint
        /// using the topology specific parameter <pararef name="key" /> and 
        /// then invoking the query on that endpoint. 
        /// </summary>
        /// <param name="key">The optional topology implementation specific key (or <c>null</c>).</param>
        /// <param name="query">The query message.</param>
        /// <returns>The query response.</returns>
        public virtual Msg Query(object key, Msg query)
        {
            if (router == null)
                throw new InvalidOperationException(TopologyHelper.ClosedMsg);

            return router.Query(clusterEP, query);
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

            return router.BeginQuery(clusterEP, query, callback, state);
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

            router.SendTo(clusterEP, msg);
        }

        /// <summary>
        /// Selects a cluster endpoint using the topology specific parameter <pararef name="key" /> and 
        /// then transmits a message with a specific source endpoint to the target endpoint. 
        /// </summary>
        /// <param name="param">The optional topology implementation specific parameter (or <c>null</c>).</param>
        /// <param name="fromEP">The endpoint to set in the message's <see cref="Msg._FromEP" /> property.</param>
        /// <param name="msg">The message to be sent.</param>
        public virtual void Send(object param, MsgEP fromEP, Msg msg)
        {
            if (router == null)
                throw new InvalidOperationException(TopologyHelper.ClosedMsg);

            router.SendTo(clusterEP, fromEP, msg);
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

            router.BroadcastTo(clusterEP, msg);
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
            foreach (var operation in parallelQuery.Operations)
                operation.QueryEP = clusterEP;

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
        public virtual MsgEP Munge(MsgEP logicalEP, MsgHandler handler)
        {
            if (!logicalEP.IsLogical)
                throw new ArgumentException(TopologyHelper.LogicalEPMsg, "logicalEP");

            if (isClient)
                throw new InvalidOperationException(TopologyHelper.NotServerMsg);

            return clusterEP;
        }
    }
}
