//-----------------------------------------------------------------------------
// FILE:        MsgDispatcher.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Dispatches messages to the appropriate handler.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

using LillTek.Common;

// $todo(jeff.lill): 
//
// This code currently enforces a limit of only one logical or physical
// endpoint mapping per dispatcher, even if different object instances
// want to expose the endpoint.  This restriction is artificial and
// should be dropped.  The code should modified to load balance and broadcast 
// to multiple endpoint instances from the same dispatcher.

namespace LillTek.Messaging
{
    /// <summary>
    /// Dispatchers messages to the appropriate handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class handles the dispatching of messages received by a
    /// MsgRouter to the appropriate method of an associated object
    /// instance.  Pass the object instance to this object's constructor.
    /// <para>
    /// </para>
    /// The class works by reflecting the object, looking for any 
    /// methods void methods accepting a single parameter derived from
    /// the <see cref="Msg">Msg</see> class and tagged with 
    /// <c>[<see cref="MsgHandlerAttribute">MsgHandler</see>]</c>.  The class
    /// can dispatch messages targeted at both physical and logical
    /// endpoints.  Logical endpoint handlers are specified by
    /// adding LogicalEP parameter to the [MsgHandler] attribute.
    /// Here are examples of physical and logical endpoint handlers:
    /// </para>
    /// <code language="cs">
    /// [MsgHandler]                                    // Physical endpoint handler
    /// public void OnMsg(Msg msg)
    /// {
    /// }
    /// 
    /// [MsgHandler(LogicalEP="logicalEP://MyApps/Foo"] // Logical endpoint handler
    /// public void OnMsg(Msg msg)
    /// {
    /// }
    /// </code>
    /// <note>
    /// <b>handler methods must be declared as public</b> to be 
    /// discovered by the dispatcher and also that a single handler method
    /// can be tagged with multiple <c>[MsgHandler]</c> attributes to indicate that
    /// the method handles messages for multiple endpoints.
    /// </note>
    /// <para>
    /// Explicit message handler delegates can also be added to the dispatcher
    /// via the <see cref="AddPhysical"/>, 
    /// <see cref="AddLogical(MsgHandlerDelegate,MsgEP,System.Type,bool,SessionHandlerInfo)"/> and
    /// <see cref="AddLogical(MsgHandlerDelegate,MsgEP,System.Type,bool,SessionHandlerInfo,bool)"/> methods.
    /// </para>
    /// <para><b><u>Physical Endpoint Implementation</u></b></para>
    /// <para>
    /// For logical endpoint handlers, MsgDispatcher maintains a hash table 
    /// mapping each handler method to the method's message parameter type.  
    /// When the router receives a message targeted at a physical endpoint,
    /// the router calls <see cref="Dispatch" />.  Dispatch()
    /// then calling handler method whose parameter type matches that of 
    /// the message being dispatched and <see cref="Dispatch" /> will return true.
    /// </para>
    /// <para>
    /// If no match is found and a method is tagged with <c>[MsgHandler(Default=true)]</c>
    /// then the message will be dispatched there.  If there's no default
    /// handler, then the message will be dropped and <see cref="Dispatch" />
    /// will return false.
    /// </para>
    /// <para>
    /// The message's <see cref="MsgFlag">MsgFlag.Broadcast</see> flag is ignored when 
    /// dispatching messages targeted at physical endpoints.
    /// </para>
    /// <para><b><u>Logical Endpoint Implementation</u></b></para>
    /// <para>
    /// The logical endpoint implementation is a bit trickier.  MsgDispatcher
    /// includes a <see cref="LogicalRouteTable" /> instance of the logical endpoints
    /// it collects from [MsgHandler] attributes.  For each distinct logical
    /// endpoint, the class maintains a hash table mapping the message type
    /// of each tagged message handler method to the method instance.
    /// These hash tables are stored in the <see cref="LogicalRoute.Handlers" />
    /// property of the routes stored in the LogicalRouteTable.
    /// </para>
    /// <para>
    /// When the router receives a message targeted at a logical endpoint,
    /// the router call <see cref="Dispatch">Dispatch()</see>.  Dispatch()
    /// searches the logical route table for one or more matching logical routes.  
    /// If any are found, then <see cref="Dispatch" /> will randomly select one 
    /// and then examine the hash table saved in the route's <see cref="LogicalRoute.Handlers" />
    /// property looking for a method handler whose parameter type matches the message type
    /// received, calling the handler if a match is found and <see cref="Dispatch" />
    /// will return false.
    /// </para>
    /// <para>
    /// If no match is found and a method is tagged with <c>[MsgHandler(Default=true)]</c>
    /// then the message will be dispatched there.  If there's no default
    /// handler, then the message will not be dispatched and Dispatch() will
    /// return false.
    /// </para>
    /// <para>
    /// <see cref="Dispatch" /> works a bit differently for messages targeted at
    /// logical endpoints if the <see cref="MsgFlag">MsgFlag.Broadcast</see> flag is set.
    /// In this situation, instead of randomly selecting a single matching
    /// route and dispatching the message there, <see cref="Dispatch" /> dispatch the
    /// message to all matching routes.  Note though, that only one message
    /// handler associated with each route will be called (based on the
    /// type of the message).
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class MsgDispatcher : IMsgDispatcher
    {
        /// <summary>
        /// Used internally for queuing messages for processing on worker threads.
        /// </summary>
        private sealed class DispatchInfo
        {

            public Msg          Msg;
            public MsgHandler   Handler;
            public object[]     Args;

            public DispatchInfo(Msg msg, MsgHandler handler, object[] args)
            {
                this.Msg     = msg;
                this.Handler = handler;
                this.Args    = args;
            }
        }

        private const string        DefaultHandler = "*default*";   // Used to indicate a default logical endpoint handler

        private Guid                logicalEndpointSetID;           // The logical endpoint set GUID
        private MsgRouter           router;                         // The associated router
        private object              syncLock;                       // The thread synchronization object
        private LogicalRouteTable   logicalRoutes;                  // Logical routes
        private MsgHandler          defPhysHandler;                 // The default physical handler (or null)
        private WaitCallback        onDispatch;                     // The worker thread dispatch handler

        // Table mapping message type to physical endpoint Handler records

        private Dictionary<System.Type, MsgHandler> physHandlers;

        /// <summary>
        /// Initializes the dispatcher by associating it with a new default router instance.
        /// </summary>
        /// <remarks>
        /// This is to be used only for unit testing purposes.
        /// </remarks>
        internal MsgDispatcher()
            : this(new MsgRouter())
        {
        }

        /// <summary>
        /// Initializes the dispatcher.
        /// </summary>
        /// <param name="router">The router that owns this dispatcher (or <c>null</c>).</param>
        public MsgDispatcher(MsgRouter router)
        {
            this.router               = router;
            this.syncLock             = router.SyncRoot;
            this.logicalEndpointSetID = Helper.NewGuid();
            this.physHandlers         = new Dictionary<Type, MsgHandler>();
            this.logicalRoutes        = new LogicalRouteTable(this.router);
            this.defPhysHandler       = null;
            this.onDispatch           = new WaitCallback(OnDispatch);
        }

        /// <summary>
        /// Returns a unique GUID identifying the set logical endpoints
        /// currently managed by the dispatcher.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Any changes made to this set of endpoints will
        /// result in a new GUID being generated.
        /// </note>
        /// </remarks>
        public Guid LogicalEndpointSetID
        {
            get { return logicalEndpointSetID; }
        }

        /// <summary>
        /// Returns the logical route table that map the application's
        /// logical message handlers.
        /// </summary>
        public LogicalRouteTable LogicalRoutes
        {
            get { return logicalRoutes; }
        }

        /// <summary>
        /// Adds an explicit physical message handler to the dispatcher.
        /// </summary>
        /// <param name="callback">The handler callback.</param>
        /// <param name="msgType">The message type to be associated with the handler.</param>
        /// <param name="sessionInfo">
        /// The session related information to be related to this handler (or <c>null</c>).
        /// </param>
        /// <remarks>
        /// <note>
        /// Methods registered as handlers via this method will have to declare
        /// their message parameter as the base Msg class so that the compiler can
        /// create MsgHandlerDelegate instances.  The message parameter can then be
        /// cast to the proper message type within the handler method.
        /// </note>
        /// </remarks>
        public void AddPhysical(MsgHandlerDelegate callback, System.Type msgType, SessionHandlerInfo sessionInfo)
        {
            MethodInfo      method;
            MsgHandler      handler;

            method  = callback.Method;
            handler = new MsgHandler(callback.Target, method, msgType, null, sessionInfo);

            using (TimedLock.Lock(syncLock))
            {
                if (physHandlers.ContainsKey(msgType))
                    throw new MsgException("Multiple physical handlers defined for message type [{0}].", msgType);

                physHandlers.Add(msgType, handler);
            }
        }

        /// <summary>
        /// Assigns an new GUID to the dispatcher's logical endpoint set ID and
        /// forces the router to advertise the new set by multicasting a
        /// RouterAdvertiseMsg.
        /// </summary>
        public void RefreshLogicalEndpointSetID()
        {
            logicalEndpointSetID = Helper.NewGuid();
            router.OnLogicalEndpointSetChange(logicalEndpointSetID);
        }

        /// <summary>
        /// Adds a, explicit logical endpoint message handler to the dispatcher.
        /// </summary>
        /// <param name="callback">The handler callback.</param>
        /// <param name="logicalEP">The logical endpoint.</param>
        /// <param name="msgType">The message type to be associated with the handler.</param>
        /// <param name="defHandler"><c>true</c> if this is the default handler for the endpoint.</param>
        /// <param name="sessionInfo">
        /// The session related information to be related to this handler (or <c>null</c>).
        /// </param>
        /// <remarks>
        /// <note>
        /// Methods registered as handlers via this method will have to declare
        /// their message parameter as the base Msg class so that the compiler can
        /// create MsgHandlerDelegate instances.  The message parameter can then be
        /// cast to the proper message type within the handler method.
        /// </note>
        /// </remarks>
        public void AddLogical(MsgHandlerDelegate callback, MsgEP logicalEP, System.Type msgType, bool defHandler, SessionHandlerInfo sessionInfo)
        {
            AddLogical(callback, logicalEP, msgType, defHandler, sessionInfo, false);
        }

        /// <summary>
        /// Adds an explicit logical endpoint message handler to the dispatcher.
        /// </summary>
        /// <param name="callback">The handler callback.</param>
        /// <param name="logicalEP">The logical endpoint.</param>
        /// <param name="msgType">The message type to be associated with the handler.</param>
        /// <param name="defHandler"><c>true</c> if this is the default handler for the endpoint.</param>
        /// <param name="sessionInfo">
        /// The session related information to be related to this handler (or <c>null</c>).
        /// </param>
        /// <param name="suppressAdvertise"><c>true</c> if the advertise messages should be suppressed for this endpoint.</param>
        /// <remarks>
        /// <para>
        /// Pass suppressAdvertise=<c>true</c> if the dispatcher and associated router to suppress
        /// the transmission of advertise messages to the other routers on the network.
        /// This is useful when multiple logical handlers are being added.  Once the handlers
        /// have been added, <see cref="LogicalAdvertise"/> can be called to force the
        /// advertise.
        /// </para>
        /// <note>
        /// Methods registered as handlers via this method will have to declare
        /// their message parameter as the base <see cref="Msg" /> class so that the compiler can
        /// create <see cref="MsgHandlerDelegate" /> instances.  The message parameter can then be
        /// cast to the proper message type within the handler method.
        /// </note>
        /// </remarks>
        public void AddLogical(MsgHandlerDelegate callback, MsgEP logicalEP, System.Type msgType, bool defHandler, SessionHandlerInfo sessionInfo, bool suppressAdvertise)
        {
            string      msgTypeName;
            string      key;
            MethodInfo  method;
            MsgHandler  handler;

            method      = callback.Method;
            msgTypeName = msgType.FullName;
            key         = defHandler ? DefaultHandler : msgTypeName;
            handler     = new MsgHandler(callback.Target, method, msgType, null, sessionInfo);

            using (TimedLock.Lock(syncLock))
            {
                if (!logicalRoutes.Add(new LogicalRoute(logicalEP, key, new MsgHandler(callback.Target, method, msgType, null, sessionInfo)), key))
                {
                    if (defHandler)
                        throw new MsgException("A default logical handler is already defined for [{0}].", logicalEP);
                    else
                        throw new MsgException("A logical message handler is alreay defined for endpoint [{0}] and message type [{1}].", logicalEP, msgType);
                }

                if (!suppressAdvertise)
                    logicalEndpointSetID = Helper.NewGuid();
            }

            if (!suppressAdvertise && router != null)
                router.OnLogicalEndpointSetChange(logicalEndpointSetID);
        }

        /// <summary>
        /// Reflects the target object passed for methods tagged with <c>[MsgHandler]</c> and adds
        /// them to the dispatcher.
        /// </summary>
        /// <param name="target">The object to be scanned.</param>
        /// <remarks>
        /// If the methods adds any logical routers then LogicalEndpointSetID will be
        /// regenerated and the associated router's <see cref="MsgRouter.OnLogicalEndpointSetChange"/> 
        /// method will be called.
        /// </remarks>
        public void AddTarget(object target)
        {
            AddTarget(target, null, null, null);
        }

        /// <summary>
        /// Scans the target object passed for dispatchable methods and adds
        /// them to the dispatcher, using the <see cref="IDynamicEPMunger" />
        /// instance passed to dynamically modify endpoints for message handlers
        /// marked with <c>[MsgHandler(DynamicScope="scope-name")]</c>.
        /// </summary>
        /// <param name="target">The object to be scanned.</param>
        /// <param name="dynamicScope">Specifies the dynamic scope name for the message handlers to be processed (or <c>null</c>).</param>
        /// <param name="munger">The dynamic endpoint munger (or <c>null</c>).</param>
        /// <param name="targetGroup">Optional dispatch target grouping instance (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// Calling this method will result in the GUID being updated and
        /// then call the associated router's <see cref="MsgRouter.OnLogicalEndpointSetChange"/> 
        /// method.
        /// </para>
        /// <para>
        /// The <paramref name="targetGroup" /> parameter can be used to group
        /// together message dispatch handlers implemented by a different
        /// target object instances.  This functionality is important when 
        /// the impleentation of message type specific handlers is spread
        /// across multiple target classes.
        /// </para>
        /// <para>
        /// An example of this use is how the <see cref="ClusterMember" />
        /// class' <see cref="ClusterMember.AddTarget" /> method to group the
        /// new target's message handler with the <see cref="ClusterMember" />
        /// handlers since they'll share the same logical endpoint.
        /// </para>
        /// <para>
        /// If <paramref name="targetGroup" /> is passed as <c>null</c> then
        /// separate routes will be maintained for each target instance resulting
        /// in messages be load balanced randomly across the instances.
        /// </para>
        /// </remarks>
        public void AddTarget(object target, string dynamicScope, IDynamicEPMunger munger, object targetGroup)
        {
            if (dynamicScope == null || munger == null)
                AddTarget(target, new ScopeMunger[0], targetGroup);
            else
                AddTarget(target, new ScopeMunger[] { new ScopeMunger(dynamicScope, munger) }, targetGroup);
        }

        /// <summary>
        /// Scans the target object passed for dispatchable methods and adds
        /// them to the dispatcher, using the <see cref="IDynamicEPMunger" />
        /// instances passed in the <paramref name="mungers"/> set to dynamically 
        /// modify endpoints for message handlers marked with <c>[MsgHandler(DynamicScope="scope-name")]</c>.
        /// </summary>
        /// <param name="target">The object to be scanned.</param>
        /// <param name="mungers">
        /// The collection of <see cref="ScopeMunger" /> instances that specify
        /// zero or more endpoint mungers.  You may also pass <c>null</c>.
        /// </param>
        /// <param name="targetGroup">Optional dispatch target grouping instance (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// Calling this method will result in the GUID being updated and
        /// then call the associated router's <see cref="MsgRouter.OnLogicalEndpointSetChange"/> 
        /// method.
        /// </para>
        /// <para>
        /// The <paramref name="targetGroup" /> parameter can be used to group
        /// together message dispatch handlers implemented by a different
        /// target object instances.  This functionality is important when 
        /// the impleentation of message type specific handlers is spread
        /// across multiple target classes.
        /// </para>
        /// <para>
        /// An example of this use is how the <see cref="ClusterMember" />
        /// class' <see cref="ClusterMember.AddTarget" /> method to group the
        /// new target's message handler with the <see cref="ClusterMember" />
        /// handlers since they'll share the same logical endpoint.
        /// </para>
        /// <para>
        /// If <paramref name="targetGroup" /> is passed as <c>null</c> then
        /// separate routes will be maintained for each target instance resulting
        /// in messages be load balanced randomly across the instances.
        /// </para>
        /// </remarks>
        public void AddTarget(object target, IEnumerable<ScopeMunger> mungers, object targetGroup)
        {
            const string msgBadMethodSignature = "Illegal [MsgHandler] method signature for [{0}.{1}()]: {2}.";

            var                 scopeToMunger = new Dictionary<string, IDynamicEPMunger>(StringComparer.OrdinalIgnoreCase);
            System.Type         targetType = target.GetType();
            System.Type         msgType;
            MethodInfo[]        methods;
            MsgHandler          duplicate;
            ParameterInfo[]     args;
            object[]            handlerAttrs;
            object[]            sessionAttrs;
            SessionHandlerInfo  sessionInfo;
            bool fNewID;

            if (mungers != null)
            {
                foreach (var item in mungers)
                    scopeToMunger[item.DynamicScope] = item.Munger;
            }

            using (TimedLock.Lock(syncLock))
            {
                fNewID  = false;
                methods = targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public);

                foreach (MethodInfo method in methods)
                {

                    // Ignore methods not tagged with [MsgHandler].

                    handlerAttrs = method.GetCustomAttributes(typeof(MsgHandlerAttribute), false);
                    if (handlerAttrs.Length == 0)
                        continue;

                    // Get the handler's session information (if any).

                    sessionAttrs = method.GetCustomAttributes(typeof(MsgSessionAttribute), false);
                    if (sessionAttrs.Length == 0)
                        sessionInfo = null;
                    else
                        sessionInfo = new SessionHandlerInfo((MsgSessionAttribute)sessionAttrs[0]);

                    // Validate the method signature: void OnMsg(Msg msg);

                    args = method.GetParameters();

                    if (method.ReturnType.UnderlyingSystemType != typeof(void))
                        throw new MsgException(msgBadMethodSignature, targetType.Name, method.Name, "Must return void.");

                    if (method.ReturnType.UnderlyingSystemType != typeof(void))
                        throw new MsgException(msgBadMethodSignature, targetType.Name, method.Name, "Must return void.");

                    if (args.Length != 1)
                        throw new MsgException(msgBadMethodSignature, targetType.Name, method.Name, "Must accept a single parameter.");

                    if (!args[0].ParameterType.IsDerivedFrom(typeof(Msg)))
                        throw new MsgException(msgBadMethodSignature, targetType.Name, method.Name, "Parameter must derive from Msg.");

                    foreach (MsgHandlerAttribute attr in handlerAttrs)
                    {
                        if (attr.IsPhysicalHandler)
                        {
                            // Deal with [MsgHandler(Default=true)] message handlers

                            if (attr.Default)
                            {
                                if (defPhysHandler != null)
                                {
                                    if (defPhysHandler.Target == target && defPhysHandler.Method == method)
                                        continue;   // Looks like we reflected the same method twice

                                    throw new MsgException("Multiple physical default message handlers: [{0}.{1}()] and [{2}.{3}()].",
                                                           defPhysHandler.Target.GetType().Name, defPhysHandler.Method.Name, targetType.Name, method.Name);
                                }

                                defPhysHandler = new MsgHandler(target, method, args[0].ParameterType, null, sessionInfo);
                                continue;
                            }

                            // Add the handler to the map, making sure that there's only one 
                            // handler per message type

                            msgType = args[0].ParameterType;
                            physHandlers.TryGetValue(msgType, out duplicate);

                            if (duplicate != null)
                            {
                                if (duplicate.Target == target && duplicate.Method == method)
                                    continue;   // Looks like we reflected the same method twice

                                throw new MsgException("Multiple physical handlers defined for message type [{0}]: [{1}{2}()] and [{1}{3}()].",
                                                       msgType.Name, targetType.Name, duplicate.Method.Name, method.Name);
                            }

                            physHandlers[msgType] = new MsgHandler(target, method, msgType, null, sessionInfo);
                        }
                        else
                        {
                            // This is a logical endpoint handler.

                            string      paramType = attr.Default ? DefaultHandler : args[0].ParameterType.FullName;
                            MsgHandler  handler = new MsgHandler(target, method, args[0].ParameterType, attr, sessionInfo);
                            MsgEP       handlerEP;

                            handlerEP = attr.LogicalEP;
                            if (attr.DynamicScope != null)
                            {
                                IDynamicEPMunger munger;

                                if (scopeToMunger.TryGetValue(attr.DynamicScope, out munger))
                                    handlerEP = munger.Munge(attr.LogicalEP, handler);
                                else
                                    continue;   // Ignore this message handler
                            }

                            if (!logicalRoutes.Add(new LogicalRoute(handlerEP, paramType, handler), paramType, targetGroup))
                            {
                                if (attr.Default)
                                    throw new MsgException("A default logical handler is already defined for [{0}].", attr.LogicalEP);
                                else
                                    throw new MsgException("A logical message handler is already defined for endpoint [{0}] and message type [{1}].", attr.LogicalEP, paramType);
                            }

                            if (!fNewID)
                            {
                                fNewID = true;
                                logicalEndpointSetID = Helper.NewGuid();
                            }
                        }
                    }
                }
            }

            if (fNewID && router != null)
                router.OnLogicalEndpointSetChange(logicalEndpointSetID);
        }

        /// <summary>
        /// Removes any logical endpoint message handlers referencing the target object from
        /// the message dispatcher.  Note that at this time only logical endpoints will
        /// be removed.  The method will throw an exception if any physical endpoints 
        /// map to the target.
        /// </summary>
        /// <param name="target">The target object whose message handlers are to be removed.</param>
        /// <exception cref="NotImplementedException">Thrown if a physical message handler maps to the target instance.</exception>
        public void RemoveTarget(object target)
        {
            bool fNewID;

            using (TimedLock.Lock(syncLock))
            {
                // Check for mapped physical handlers

                foreach (var handler in physHandlers.Values)
                {
                    if (handler.Target == target)
                        throw new NotImplementedException("Cannot remove a target with physical endpoint handlers.");
                }

                // Remove any logical handlers

                fNewID = logicalRoutes.RemoveTarget(target);
                if (fNewID)
                    logicalEndpointSetID = Helper.NewGuid();
            }

            if (fNewID && router != null)
                router.OnLogicalEndpointSetChange(logicalEndpointSetID);
        }

        /// <summary>
        /// Forces the update of the LogicalEndpointSetID and then the transmission of
        /// router advertise messages to notify other routers of the update.
        /// </summary>
        public void LogicalAdvertise()
        {
            using (TimedLock.Lock(syncLock))
            {
                logicalEndpointSetID = Helper.NewGuid();
            }

            if (router != null)
                router.OnLogicalEndpointSetChange(logicalEndpointSetID);
        }

        /// <summary>
        /// Dispatches the message passed to the appropriate handler in
        /// the associated target instance.  Note that the dispatch call will
        /// be performed on a worker thread.
        /// </summary>
        /// <param name="msg">The message to be dispatched.</param>
        /// <returns><c>true</c> if the message was successfully dispatched.</returns>
        /// <remarks>
        /// <para>
        /// This method uses two basic implementation techniques depending on whether
        /// the message's target endpoint is physical or logical.
        /// </para>
        /// <para><b><u>Physical Endpoint Implementation</u></b></para>
        /// <para>
        /// For logical endpoint handlers, MsgDispatcher maintains a hash table 
        /// mapping each handler method to the method's message parameter type.  
        /// When the router receives a message targeted at a physical endpoint,
        /// the router calls <see cref="Dispatch" /> which then calls the handler 
        /// method whose parameter type matches that of the message being dispatched.
        /// <see cref="Dispatch" /> will return true in this case.
        /// </para>
        /// <para>
        /// If no match is found and a method is tagged with <c>[MsgHandler(Default=true)]</c>
        /// then the message will be dispatched there.  If there's no default
        /// handler, then the message will be dropped and <see cref="Dispatch" />
        /// will return false.
        /// </para>
        /// <para>
        /// The message's <see cref="MsgFlag">MsgFlag.Broadcast</see> flag is ignored when dispatching 
        /// messages targeted at physical endpoints.
        /// </para>
        /// <para><b><u>Logical Endpoint Implementation</u></b></para>
        /// <para>
        /// The logical endpoint implementation is a bit trickier.  MsgDispatcher
        /// includes a LogicalRouteTable instance of the logical endpoints it
        /// collects from [MsgHandler] attributes.  For each distinct logical
        /// endpoint, the class maintains a hash table mapping the message type
        /// of each tagged message handler method to the method instance.
        /// These hash tables are stored in the <see cref="LogicalRoute.Handlers" /> property
        /// of the routes stored in the LogicalRouteTable.
        /// </para>
        /// <para>
        /// When the router receives a message targeted at a logical endpoint,
        /// the router call <see cref="Dispatch" />, which will then
        /// search the logical route table for one or more matching logical routes.  
        /// If any are found, then <see cref="Dispatch" /> will randomly select one and then examine 
        /// the hash table saved in the route's <see cref="LogicalRoute.Handlers" /> property looking
        /// for a method handler whose parameter type matches the message type
        /// received, calling the handler if a match is found and <see cref="Dispatch" />
        /// will return false.
        /// </para>
        /// <para>
        /// If no match is found and a method is tagged with <c>[MsgHandler(Default=true)]</c>
        /// then the message will be dispatched there.  If there's no default
        /// handler, then the message will not be dispatched and <see cref="Dispatch" />
        /// will return false.
        /// </para>
        /// <para>
        /// <see cref="Dispatch" /> works a bit differently for messages targeted at
        /// logical endpoints if the <see cref="MsgFlag">MsgFlag.Broadcast</see> flag is set.
        /// In this situation, instead of randomly selecting a single matching
        /// route and dispatching the message there, <see cref="Dispatch" /> dispatch the
        /// message to all matching routes.  Note though, that only one message
        /// handler associated with each route will be called (based on the
        /// type of the message).
        /// </para>
        /// </remarks>
        public bool Dispatch(Msg msg)
        {
            // Look up the approriate handler for this message.

            MsgHandler handler = null;

            if (msg._ToEP == null || msg._ToEP.IsPhysical)
            {
                // We don't dispatch EnvelopeMsgs.

                if (msg is EnvelopeMsg)
                    return false;

                // Get the physical handler

                using (TimedLock.Lock(syncLock))
                {
                    physHandlers.TryGetValue(msg.GetType(), out handler);

                    if (handler == null)
                        handler = defPhysHandler;
                }

                // If we couldn't find a handler for this message type and the
                // message isn't associated with a session then discard
                // the message.  Messages with no handler that are associated
                // with a session will be routed to the session instance
                // to be handled there.

                if (handler == null && msg._SessionID == Guid.Empty)
                {
                    if (router.IsOpen)
                        NetTrace.Write(MsgRouter.TraceSubsystem, 1, "Dispatch Physical", router.GetType().Name + ": " + msg.GetType().Name + " router=" + router.RouterEP.ToString(), " No handler");

                    return false;
                }

                if (router.IsOpen)
                    NetTrace.Write(MsgRouter.TraceSubsystem, 1, "Dispatch Physical", router.GetType().Name + ": " + msg.GetType().Name + " router=" + router.RouterEP.ToString(), " Target: " + (handler == null ? "(none)" : handler.Target.GetType().Name));
            }
            else if ((msg._Flags & MsgFlag.Broadcast) != 0)
            {
                // A broadcast message is being targeted at a logical endpoint.

                List<LogicalRoute>  routes;
                LogicalRoute        route;

                using (TimedLock.Lock(syncLock))
                {
                    routes = logicalRoutes.GetRoutes(msg._ToEP);
                    if (routes.Count == 0)
                        return false;
                }

                for (int i = 0; i < routes.Count; i++)
                {
                    route   = routes[i];
                    handler = null;

                    if (!route.Handlers.TryGetValue(msg.GetType().FullName, out handler))
                        route.Handlers.TryGetValue(DefaultHandler, out handler);

                    if (handler == null)
                        continue;

                    NetTrace.Write(MsgRouter.TraceSubsystem, 1, "Dispatch Logical", msg.GetType().Name + "[" + msg._ToEP + "]", "Target: " + (handler == null ? "(none)" : handler.Target.GetType().Name));

                    // Verify that the message type is actually compatible with the
                    // handler's message parameter.

                    if (!handler.MsgType.IsAssignableFrom(msg.GetType()))
                    {
                        SysLog.LogWarning("Unable to dispatch message type [{0}] to [{0}.{1}()] because the handler parameter type is not compatible.",
                                          msg.GetType(), handler.Target.GetType().FullName, handler.Method.Name);
                        return false;
                    }

                    if ((msg._Flags & MsgFlag.Priority) != 0)
                        router.ThreadPool.QueuePriorityTask(onDispatch, new DispatchInfo(msg, handler, new object[] { msg }));
                    else
                        router.ThreadPool.QueueTask(onDispatch, new DispatchInfo(msg, handler, new object[] { msg }));
                }

                return true;
            }
            else
            {
                // A non-broadcast message is being targeted at a logical endpoint.

                List<LogicalRoute>  routes;
                LogicalRoute        route;

                Assertion.Test((msg._Flags & MsgFlag.Broadcast) == 0);
                Assertion.Test(msg._ToEP.IsLogical);

                using (TimedLock.Lock(syncLock))
                {
                    routes = logicalRoutes.GetRoutes(msg._ToEP);
                    if (routes.Count == 0)
                        return false;

                    route   = routes[Helper.RandIndex(routes.Count)];
                    handler = null;

                    if (!route.Handlers.TryGetValue(msg.GetType().FullName, out handler))
                        route.Handlers.TryGetValue(DefaultHandler, out handler);
                }

                // If we couldn't find a handler for this message type and the
                // message isn't associated with a session then discard
                // the message.  Messages with no handler that are associated
                // with a session will be routed to the session instance
                // to be handled there.

                if (handler == null && msg._SessionID == Guid.Empty)
                {
                    NetTrace.Write(MsgRouter.TraceSubsystem, 1, "Dispatch Logical", msg.GetType().Name, "No handler");
                    return false;
                }

                NetTrace.Write(MsgRouter.TraceSubsystem, 1, "Dispatch Logical", msg.GetType().Name + "[" + msg._ToEP + "]", "Target: " + (handler == null ? "(none)" : handler.Target.GetType().Name));
            }

            // Verify that the message type is actually compatible with the
            // handler's message parameter.

            if (handler != null && !handler.MsgType.IsAssignableFrom(msg.GetType()))
            {
                SysLog.LogWarning("Unable to dispatch message type [{0}] to [{0}.{1}()] because the handler parameter type is not compatible.",
                                  msg.GetType(), handler.Target.GetType().FullName, handler.Method.Name);
                return false;
            }

            if ((msg._Flags & MsgFlag.Priority) != 0)
                router.ThreadPool.QueuePriorityTask(onDispatch, new DispatchInfo(msg, handler, new object[] { msg }));
            else
                router.ThreadPool.QueueTask(onDispatch, new DispatchInfo(msg, handler, new object[] { msg }));

            return true;
        }

        // $todo(jeff.lill):
        //
        // I'm not really sure if we need this to be dispached
        // on a worker thread since MsgRouter.OnProcessMsg()
        // (who calls MsgDispatcher.Dispatch()) is already
        // running on a fresh worker thread.  We'd probably see
        // a performance boost by having Dispatch() call the
        // handler directly.  Something to look into when I
        // have more time.

        /// <summary>
        /// Handles the dispatch on a worker thread.
        /// </summary>
        /// <param name="state">The DispatchInfo.</param>
        private void OnDispatch(object state)
        {
            DispatchInfo        info        = (DispatchInfo)state;
            MsgHandler          handler     = info.Handler;
            Msg                 msg         = info.Msg;
            SessionHandlerInfo  sessionInfo = handler == null ? null : handler.SessionInfo;
            ISessionManager     sessionMgr;

            try
            {
                // If there's a router associated with this instance and the message
                // has a non-empty _SessionID then we'll either initiate a server side 
                // session or simply route the message to the session, depending 
                // on the MsgFlag.OpenSession bit.
                //
                // If neither of these conditions are true then route the message
                // directly to the handler.

                if (router != null && msg._SessionID != Guid.Empty)
                {
                    sessionMgr = router.SessionManager;
                    if ((msg._Flags & MsgFlag.OpenSession) != 0)
                    {
                        if (handler == null)
                        {
                            NetTrace.Write(MsgRouter.TraceSubsystem, 1, "Dispatch: Message Discarded", "No message handler for: " + msg.GetType().Name, string.Empty);
                            return;
                        }

                        sessionMgr.ServerDispatch(msg, handler.Target, handler.Method, sessionInfo);
                    }
                    else
                        sessionMgr.OnMsg(msg, sessionInfo);
                }
                else
                    handler.Method.Invoke(handler.Target, info.Args);
            }
            catch (Exception e)
            {
                NetTrace.Write(MsgRouter.TraceSubsystem, 0, "App Exception", e);
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Clears the dispatch table.
        /// </summary>
        public void Clear()
        {
            using (TimedLock.Lock(syncLock))
            {
                physHandlers.Clear();
                defPhysHandler = null;

                logicalRoutes.Clear();
            }
        }
    }
}
