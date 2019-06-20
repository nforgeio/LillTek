//-----------------------------------------------------------------------------
// FILE:        StaticHashedTopology.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a static hashed topology provider.

using System;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements a static hashed <see cref="ITopologyProvider" /> plug-in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This cluster implementation is designed to load a static topology from
    /// the application's configuration.  The topology is simply an ordered 
    /// collection of logical endpoints describing the possible service instances
    /// along with a key that indicates which endpoint maps to the current 
    /// service server instance.  Here's a sample configuration file:
    /// </para>
    /// <code language="none">
    ///     service-instance[-] = 0
    ///     service-instance[-] = 1
    ///     service-instance[-] = 2
    /// 
    ///     topology-args       = instances=service-instances;this-instance=0;cluster-ep=logical://foo
    /// </code>
    /// <para>
    /// This configuration specifies that there are to be three fixed service instances,
    /// each with an endpoint suffixes defined an entry in the <b>service-instance</b>
    /// array.  This setting will typically be shared across the service server
    /// and client instances.
    /// </para>
    /// <para>
    /// The <b>topology-args</b> parameter specifies the additional parameters passed
    /// to the <see cref="OpenClient" /> and <see cref="OpenServer" /> methods.
    /// This parameter specifies a set of name/value pairs formatted for as described
    /// in <see cref="ArgCollection" />.  The <b>instances</b> argument needs to be
    /// set to the fully qualified configuration setting key of the service instance
    /// array, and for server side topology instances, <b>this-instance</b> specifies the index
    /// into the <b>instances</b> array to indicate the endpoint suffix to be used
    /// by the service server cluster instance.  The <b>clusterEP</b> parameter is 
    /// required and specifies the cluster endpoint to use when opening the cluster.
    /// </para>
    /// <para>
    /// The <b>this-instance</b> argument is not required for cluster clients and will 
    /// be ignored.
    /// </para>
    /// <para><b><u>How this works</u></b></para>
    /// <para>
    /// When a service server implementation calls <see cref="ITopologyProvider.OpenServer" /> it 
    /// passes the fully qualified configuration key for the topology arguments and the dynamic 
    /// scope name.  <see cref="ITopologyProvider.OpenServer" /> will instantiate a <see cref="StaticHashedTopology" /> 
    /// instance and then load the arguments into a <see cref="ArgCollection" /> and then call the 
    /// topology provider's <see cref="ITopologyProvider.OpenServer" /> method, passing arguments as well as the clusterEP 
    /// endpoint agument explicitly extracted from the arguments.
    /// </para>
    /// <para>
    /// The server cluster loads the instance array specified by the
    /// <b>instances</b> argument in the topology arguments and then uses the
    /// <b>this-instance</b> argument to index into the instance array to retrieve
    /// the server topology instance suffix.  This stuffix will be appended onto
    /// the cluster endpoint passed to create a cluster instance endpoint and then
    /// this endpoint will be used to register the dynamic message handlers in the
    /// target object that match the specified dynamic scope with the router's
    /// message dispatcher.  All of this is necessary to ready the server cluster
    /// instance to accept hashed requests.
    /// </para>
    /// <para>
    /// Client side <see cref="StaticHashedTopology" /> instancess are created by calling
    /// <see cref="TopologyHelper.OpenClient(MsgRouter,string,string)" />, passing the topology 
    /// arguments as well as the cluster endpoint extracted from the arguments.  The only 
    /// additional argument required for client <see cref="StaticHashedTopology" />
    /// instances is the <b>instances</b> array reference. 
    /// </para>
    /// <para>
    /// The <b>param</b> parameter passed to the <b>Query</b>, <b>Send</b>, and other
    /// client side methods is used to generate a hash code that is then used to
    /// distribute requests across the cluster's service instances.  The <b>param</b>
    /// object's <see cref="object.GetHashCode" /> method will be called to calculate
    /// a 32-bit hash code.  If <b>param</b> is <c>null</c>, then a random number will be
    /// generated instead.  The modulus of the hash code and number of service instance
    /// endpoints will be used as an index into the set of service instance endpoints
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
    /// <tr valign="top">
    ///     <td>instances</td>
    ///     <td>Client &amp; Server</td>
    ///     <td>
    ///     The fully qualified name of the configuration setting for an array
    ///     of service instance endpoint suffixes.
    ///     </td>
    ///     <td>this-instance</td>
    ///     <td>Server</td>
    ///     <td>
    ///     The integer index into the <b>instances</b> array of the suffix for
    ///     the current service instance.
    ///     </td>
    /// </tr>
    /// </table>
    /// </div>
    /// </remarks>
    public class StaticHashedTopology : ITopologyProvider
    {
        private MsgRouter   router;         // The application's message router
        private Guid        instanceID;     // The globally unique instance ID
        private MsgEP       clusterEP;      // The cluster endpoint
        private bool        isClient;       // True for client mode, false for server mode
        private MsgEP[]     instances;      // The service instances
        private MsgEP       instanceEP;     // This instance's endpoint
        private MsgEP       broadcastEP;    // The cluster broadcast endpoint
        private string      serialized;     // Serialized client arguments

        /// <summary>
        /// Constructor.
        /// </summary>
        public StaticHashedTopology()
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
            get { return TopologyCapability.Locality; }
        }

        /// <summary>
        /// Initializes the service instances.
        /// </summary>
        /// <param name="clusterEP">The cluster endpoint.</param>
        /// <param name="args">The topology arguments.</param>
        private void GetServiceInstances(MsgEP clusterEP, ArgCollection args)
        {
            string      arg;
            string[]    arr;

            arg = args["instances"];
            if (arg == null)
                throw new ArgumentException("[instances] argument not found in the topology arguments.");

            arr = Config.Global.GetArray(arg);
            if (arr == null || arr.Length == 0)
                throw new ArgumentException(string.Format("Configuration key [{0}] does not reference a valid topology instance array."));

            instances = new MsgEP[arr.Length];
            for (int i = 0; i < arr.Length; i++)
                instances[i] = MsgEP.Parse(clusterEP, arr[i]);
        }

        /// <summary>
        /// Returns the endpoint to be used for cluster requests taking the parameter passed
        /// into account.
        /// </summary>
        /// <param name="key">The optional topology implementation specific key (or <c>null</c>).</param>
        /// <returns>The endpoint of the cluster service instance where the request is to be directed.</returns>
        private MsgEP GetRequestEP(object key)
        {
            int hash = key == null ? Helper.Rand() : key.GetHashCode();

            return instances[hash % instances.Length];
        }

        /// <summary>
        /// Opens the topology instance in client mode.
        /// </summary>
        /// <param name="router">The message router to be used by the cluster.</param>
        /// <param name="clusterEP">The cluster's logical endpoint.</param>
        /// <param name="args">Topology implementation specific parameters.</param>
        /// <remarks>
        /// </remarks>
        public virtual void OpenClient(MsgRouter router, MsgEP clusterEP, ArgCollection args)
        {
            if (!clusterEP.IsLogical)
                throw new ArgumentException(TopologyHelper.ClusterEPNotLogicalMsg);

            this.router      = router;
            this.instanceID  = Helper.NewGuid();
            this.clusterEP   = clusterEP;
            this.broadcastEP = new MsgEP(clusterEP, "*");
            this.isClient    = true;

            ArgCollection argsCopy;

            argsCopy                  = args.Clone();
            argsCopy["topology-type"] = TopologyHelper.SerializeType(this.GetType());
            serialized                = argsCopy.ToString();

            GetServiceInstances(clusterEP, args);
        }

        /// <summary>
        /// Opens the topology instance in server mode.
        /// </summary>
        /// <param name="router">The message router to be used by the cluster.</param>
        /// <param name="dynamicScope">The dynamic scope name.</param>
        /// <param name="target">The target object whose dynamic message handlers are to be registered (or <c>null</c>).</param>
        /// <param name="clusterEP">The cluster's logical endpoint.</param>
        /// <param name="args">Topology implementation specific parameters.</param>
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
            string      arg;
            int         index;

            if (!clusterEP.IsLogical)
                throw new ArgumentException(TopologyHelper.ClusterEPNotLogicalMsg);

            this.router      = router;
            this.instanceID  = Helper.NewGuid();
            this.clusterEP   = clusterEP;
            this.broadcastEP = new MsgEP(clusterEP, "*");
            this.isClient    = false;

            GetServiceInstances(clusterEP, args);

            arg = args["this-instance"];
            if (arg == null || !int.TryParse(arg, out index) || index < 0 || index >= instances.Length)
                throw new ArgumentException("[this-instance] topology argument is not a valid index into the instances array.");

            instanceEP = instances[index];

            if (target != null)
                router.Dispatcher.AddTarget(target, dynamicScope, this, null);
        }

        /// <summary>
        /// Closes the topology instance, releasing all resources.
        /// </summary>
        /// <remarks>
        /// <note>
        /// It is not an error to call this method if the instance
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
        /// knowledge of the target topology instance must be transmitted to a reliable messaging
        /// service and perhaps be persistently stored.
        /// </para>
        /// <para>
        /// This property returns a set of name/value pairs formatted as described
        /// in <see cre="ArgCollection" />.  These are the same set of arguments passed
        /// to the <see cref="OpenClient" /> method with the addition of the <b>cluster-type</b>
        /// argument.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the topology instance is not opened as a client.</exception>
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
        /// Unserializes a topology parameter object from a string.
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
        /// Returns <c>true</c> if the topology instance was opened in client mode
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
        /// may only be called for topology instances opened in server mode.
        /// </summary>
        /// <param name="logicalEP">The message handler's logical endpoint.</param>
        /// <param name="handler">The message handler information.</param>
        /// <returns>The logical endpoint to actually register for the message handler.</returns>
        /// <remarks>
        /// This returns the instance endpoint determined by parsing the topology
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
    }
}
