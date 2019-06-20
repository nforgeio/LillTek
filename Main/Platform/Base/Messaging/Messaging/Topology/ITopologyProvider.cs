//-----------------------------------------------------------------------------
// FILE:        ITopologyProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines a plugable cluster service instance topology interface.

using System;
using System.Net;
using System.Reflection;

using LillTek.Common;
using LillTek.Net.Sockets;

// $todo(jeff.lill):
//
// There's lots of additional stuff to think about here.  I think this
// could be the place where queries and messages could be automatically
// replicated across banks of servers.  We  may also look into some 
// kind of clustered transactions long term.
//
// I also need to think harder about the concept of banks of servers
// in a cluster.  The idea is that the server banks essentially hold
// replicated content that is somehow kept synchronized.  I'm not entirely
// sure if I need to surface the bank concept in this interface definition
// or if the topology implementations should help with the replication
// or if this needs to be handled by the application.

namespace LillTek.Messaging
{
    /// <summary>
    /// Defines a plugable cluster service instance topology interface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Service topologies abstract a collection of service
    /// instances on the network.  The main purpose of a service topology
    /// is to abstract the implementation of advanced service deployment
    /// topologies on both the client and server sides of an application.
    /// The <see cref="ITopologyProvider" /> interface defines the basic 
    /// functionality for plugable topology implementations.
    /// </para>
    /// <para>
    /// <see cref="ITopologyProvider" /> instances operate in either <b>client</b> or <b>server</b>
    /// mode.  Client instances are used by cluster client applications
    /// to distribute queries across the cluster.  Server instances are
    /// used by applications to aid in the implementation of the server 
    /// side of a cluster.
    /// </para>
    /// <para>
    /// After instantiating an <see cref="ITopologyProvider" /> instance, call <see cref="OpenClient" />
    /// or <see cref="OpenServer" /> to open the instance in the desired mode.
    /// You'll need to pass the application's message router, a logical endpoint
    /// identifying the cluster and a collection of topology type specific arguments.
    /// </para>
    /// <note>
    /// <see cref="ITopologyProvider" /> instances that are successfully opened must eventually
    /// be closed by calling <see cref="Close" /> to promptly release any resources associated
    /// with the cluster.
    /// </note>
    /// <para>
    /// Clusters are identified by the <see cref="MsgEP" /> passed to the
    /// <see cref="OpenClient" /> or <see cref="OpenServer" /> methods.  The 
    /// interpretation and use of this endpoint is up to the specific <see cref="ITopologyProvider" />
    /// implementation.  For many implementations the cluster endpoint will
    /// become the root of an endpoint tree with instance endpoints being
    /// added as leaves.  Other implementations are also possible.
    /// </para>
    /// <para>
    /// The basic purpose for a cluster is to abstract the distribution of message
    /// traffic across multiple servers in a topology specific way.  LillTek Messaging
    /// provides several built-in topology providers and it is also possible for applications
    /// to build their own.
    /// </para>
    /// <para>
    /// For example, the built-in <see cref="DynamicHashedTopology" /> is designed for
    /// situations where message traffic should have affinity to particular servers.
    /// A good example of this is where and application desires to keep a specific user's
    /// chat state and message traffic targeted at a single server in a cluster rather than
    /// having this be distributed automatically across servers sharing a single 
    /// endpoint.  <see cref="DynamicHashedTopology" /> dynamically tracks the collection
    /// of active participating servers and then uses the a topology specific parameter to
    /// route message traffic to a specific server in the cluster.  For the chat example, 
    /// the application would use the user's globally unique account ID as the topology specific 
    /// parameter passed to methods such as <see cref="Query" /> and <see cref="Send(object,Msg)" />.
    /// The topology would then hash account ID, mapping it to a specific server in the
    /// cluster and then performing the operation.
    /// </para>
    /// <para>
    /// Here is the complete list of built-in LillTek providers:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><see cref="BasicTopology"/></term>
    ///         <description>
    ///         A simply topology that relies on underlying LillTek Messaging message routing.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="Dynamic2DTopology"/></term>
    ///         <description>
    ///         <b>Not Implemented:</b> Load balances traffic against a dynamic banks of replicated servers.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="DynamicHashedTopology"/></term>
    ///         <description>
    ///         Maps traffic to a dynamic collection of servers by hashing the tolology parameter.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="Static2DTopology"/></term>
    ///         <description>
    ///         Load balances traffic against a static banks of replicated servers.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="StaticHashedTopology"/></term>
    ///         <description>
    ///         Maps traffic to a static collection of servers by hashing the tolology parameter.
    ///         </description>
    ///     </item>
    /// </list>
    /// <para>
    /// Some topologies implement special capabilities.  Use the the <see cref="Capabilities" />
    /// property returns zero or more <see cref="TopologyCapability" /> flag bit values
    /// that describe these capabilities.
    /// </para>
    /// <para><b><u>Server Side Implementation</u></b></para>
    /// <para>
    /// Server side topology support is typically very easy to add to an application.
    /// In most cases, all that is necessary is to call <see cref="OpenServer" />
    /// with the appropriate parameters and then this method takes care of registering
    /// the necessary logical routes and message handlers with the router and 
    /// performing any background activities.
    /// </para>
    /// <para>
    /// All <see cref="ITopologyProvider" /> implementations must also implement <see cref="IDynamicEPMunger" />.
    /// This interface exposes the <see cref="IDynamicEPMunger.Munge" /> method which
    /// dynamically modifies the application logical endpoints for the message handlers 
    /// tagged with <c>[MsgHandler(DynamicScope="scope-name")]</c> so that they support the service
    /// topology required by the <see cref="ITopologyProvider" /> implementation.
    /// </para>
    /// <para><b><u>Client Side Implementation</u></b></para>
    /// <para>
    /// Client side cluster support is also pretty easy to add to an application.
    /// Simply call <see cref="OpenClient" /> passing the appropriate parameters
    /// and then use the <see cref="Query" />, <see cref="BeginQuery" />,
    /// <see cref="EndQuery" />, <see cref="Send(object,Msg)" /> and <see cref="Broadcast" />
    /// methods to communicate with the cluster.
    /// </para>
    /// <para>
    /// Each of these methods expose a <b>key</b> object parameter.  This parameter
    /// is used to pass additional information to client topology implementations to
    /// help them distribute requests across cluster service instances.  The meaning
    /// and use of this parameter is specified by the individual cluster implementation.
    /// Some implementations will ignore this parameter completely.  All implementations
    /// must be able to accept a <c>null</c> value being passed in this parameter and 
    /// must be tolerant of unexpected values and types.
    /// </para>
    /// </remarks>
    public interface ITopologyProvider : IDynamicEPMunger
    {
        /// <summary>
        /// Returns flag bits describing optional topology specific capabilities.
        /// </summary>
        TopologyCapability Capabilities { get; }

        /// <summary>
        /// Opens the topology instance in client mode.
        /// </summary>
        /// <param name="router">The message router to be used by the cluster.</param>
        /// <param name="clusterEP">The cluster's logical endpoint.</param>
        /// <param name="args">Cluster implementation specific parameters.</param>
        void OpenClient(MsgRouter router, MsgEP clusterEP, ArgCollection args);

        /// <summary>
        /// Opens the topology instance in server mode.
        /// </summary>
        /// <param name="router">The message router to be used by the cluster.</param>
        /// <param name="dynamicScope">The dynamic scope name.</param>
        /// <param name="target">The target object whose dynamic message handlers are to be registered (or <c>null</c>).</param>
        /// <param name="clusterEP">The cluster's logical endpoint.</param>
        /// <param name="args">Cluster implementation specific parameters.</param>
        /// <remarks>
        /// <para>
        /// This method also registers the dynamic message handlers that case-insensitvely
        /// match the dynamicScope parameter passed that are found within the
        /// target object passed with the router's message dispatcher, after performing
        /// any necessary munging of their message endpoints.  This is done by
        /// matching the dynamicScope parameter passed against the <see cref="MsgHandler.DynamicScope" />
        /// property in the <see cref="MsgHandler" /> attribute tagging the message handler.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the topology provider is not opened as a client.</exception>
        void OpenServer(MsgRouter router, string dynamicScope, MsgEP clusterEP, object target, ArgCollection args);

        /// <summary>
        /// Closes the topology, releasing all resources.
        /// </summary>
        /// <remarks>
        /// <note>
        /// It is not an error to call this method if the topology
        /// is already closed.
        /// </note>
        /// </remarks>
        void Close();

        /// <summary>
        /// Returns a topology instance's globally unique ID.
        /// </summary>
        /// <remarks>
        /// The topology instance ID is used by implementations of the <c>IReliableMessenger</c>
        /// interface to implement an efficient mechanism for caching client side
        /// topology instance references on the server side of messengers.
        /// </remarks>
        Guid InstanceID { get; }

        /// <summary>
        /// Returns the cluster's endpoint.
        /// </summary>
        MsgEP ClusterEP { get; }

        /// <summary>
        /// Returns <c>true</c> if the topology provider was opened in client mode
        /// false for server mode.
        /// </summary>
        bool IsClient { get; }

        /// <summary>
        /// Serializes the instantiation parameters necessary to transmit and then 
        /// reconstitute a client topology instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is useful for situations like reliable messaging implementations where
        /// knowledge of the target cluster must be transmitted to a reliable messaging
        /// service and perhaps be persistently stored.
        /// </para>
        /// <para>
        /// This property returns a set of name/value pairs formatted as described
        /// in <see cre="ArgCollection" />.  These are the same set of arguments passed
        /// to the <see cref="OpenClient" /> method with the addition of the <b>topology-type</b>
        /// argument.
        /// </para>
        /// </remarks>
        string SerializeClient();

        /// <summary>
        /// Serialize a topology parameter object to a string.
        /// </summary>
        /// <param name="key">The cluster key object (or <c>null</c>).</param>
        /// <returns>A string representation of the parameter or <c>null</c>.</returns>
        /// <remarks>
        /// <para>
        /// This method is used when an <c>IReliableMessenger</c> implementation
        /// needs to forward or persist a cluster request.  <see cref="ITopologyProvider" /> implementations
        /// that make use of the parameter should serialize the topology parameter object
        /// in a way that can be unserialized via a call to <see cref="UnserializeKey" />
        /// of the client side cluster that has been reconstituted by an 
        /// <c>IReliableMessenger</c> instance.
        /// </para>
        /// <para>
        /// <c>IReliableMessenger</c> implementations that don't make use of 
        /// the topology parameter should return null.
        /// </para>
        /// </remarks>
        string SerializeKey(object key);

        /// <summary>
        /// Unserializes a topology parameter object from a string.
        /// </summary>
        /// <param name="key">The string version of the key (or <c>null</c>).</param>
        /// <returns>The unserialized object (or <c>null</c>).</returns>
        object UnserializeKey(string key);

        /// <summary>
        /// Performs a synchronous query on the cluster by selecting a cluster endpoint
        /// using the topology specific parameter <pararef name="key" /> and 
        /// then invoking the query on that endpoint. 
        /// </summary>
        /// <param name="key">The optional topology implementation specific key (or <c>null</c>).</param>
        /// <param name="query">The query message.</param>
        /// <returns>The query response.</returns>
        Msg Query(object key, Msg query);

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
        /// <see cref="EndQuery" />.
        /// </remarks>
        IAsyncResult BeginQuery(object key, Msg query, AsyncCallback callback, object state);

        /// <summary>
        /// Completes an asynchronous <see cref="BeginQuery" /> operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginQuery" />.</param>
        /// <returns>The response message.</returns>
        /// <exception cref="TimeoutException">Thrown if the timeout limit to receive a response has been exceeded.</exception>
        Msg EndQuery(IAsyncResult ar);

        /// <summary>
        /// Selects a cluster endpoint using the topology specific parameter <pararef name="key" /> and 
        /// then transmits a message to that endpoint. 
        /// </summary>
        /// <param name="key">The optional topology implementation specific key (or <c>null</c>).</param>
        /// <param name="msg">The message to be sent.</param>
        void Send(object key, Msg msg);

        /// <summary>
        /// Selects a cluster endpoint using the topology specific parameter <pararef name="key" /> and 
        /// then transmits a message with a specific source endpoint to the target endpoint. 
        /// </summary>
        /// <param name="key">The optional topology implementation specific key (or <c>null</c>).</param>
        /// <param name="fromEP">The endpoint to set in the message's <see cref="Msg._FromEP" /> property.</param>
        /// <param name="msg">The message to be sent.</param>
        void Send(object key, MsgEP fromEP, Msg msg);

        /// <summary>
        /// Broadcasts a message to the cluster.
        /// </summary>
        /// <param name="key">The optional topology implementation specific key (or <c>null</c>).</param>
        /// <param name="msg">The message to be broadcast.</param>
        void Broadcast(object key, Msg msg);

        /// <summary>
        /// Performs a synchronous parallel query on the cluster.
        /// </summary>
        /// <param name="key">The optional topology implementation specific key (or <c>null</c>).</param>
        /// <param name="parallelQuery">Holds the query operations to be performed in parallel.</param>
        /// <returns>The parallel query operations with the query results.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no query operations are specified.</exception>
        /// <exception cref="InvalidOperationException">Thrown if a query operation or query message is passed more than once.</exception>
        ParallelQuery ParallelQuery(object key, ParallelQuery parallelQuery);

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
        IAsyncResult BeginParallelQuery(object key, ParallelQuery parallelQuery, AsyncCallback callback, object state);

        /// <summary>
        /// Completes an asynchronous <see cref="BeginQuery" /> operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginQuery" />.</param>
        /// <returns>The parallel query operations with the query results.</returns>
        /// <exception cref="TimeoutException">Thrown if the timeout limit to receive a response has been exceeded.</exception>
        ParallelQuery EndParallelQuery(IAsyncResult ar);
    }
}
