//-----------------------------------------------------------------------------
// FILE:        IMsgDispatcher.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the message dispatcher contract.

using System;
using System.Collections.Generic;
using System.Reflection;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Delegate used for specifying explicit message handlers
    /// for <see cref="IMsgDispatcher" /> instances.
    /// </summary>
    /// <param name="msg">The received message.</param>
    /// <remarks>
    /// Note that <see cref="IMsgDispatcher" /> implementations that need
    /// to use thread synchronization mechanisms should lock the object
    /// returned by the associated <see cref="MsgRouter" />'s <see cref="MsgRouter.SyncRoot" />
    /// property to avoid potential deadlocking problems.
    /// </remarks>
    public delegate void MsgHandlerDelegate(Msg msg);

    /// <summary>
    /// Dispatchers messages to the appropriate handler.
    /// </summary>
    public interface IMsgDispatcher
    {
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
        Guid LogicalEndpointSetID { get; }

        /// <summary>
        /// Assigns an new GUID to the dispatcher's logical endpoint set ID and
        /// forces the router to advertise the new set by multicasting a
        /// RouterAdvertiseMsg.
        /// </summary>
        void RefreshLogicalEndpointSetID();

        /// <summary>
        /// Returns the logical route table that map the application's
        /// logical message handlers.
        /// </summary>
        LogicalRouteTable LogicalRoutes { get; }

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
        /// their message parameter as the base <see cref="Msg" /> class so that the compiler can
        /// create MsgHandlerDelegate instances.  The message parameter can then be
        /// cast to the proper message type within the handler method.
        /// </note>
        /// </remarks>
        void AddPhysical(MsgHandlerDelegate callback, System.Type msgType, SessionHandlerInfo sessionInfo);

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
        /// their message parameter as the base <see cref="Msg" /> class so that the compiler can
        /// create MsgHandlerDelegate instances.  The message parameter can then be
        /// cast to the proper message type within the handler method.
        /// </note>
        /// </remarks>
        void AddLogical(MsgHandlerDelegate callback, MsgEP logicalEP, System.Type msgType,
                        bool defHandler, SessionHandlerInfo sessionInfo);

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
        /// Pass <b>suppressAdvertise=true</b> if the dispatcher and associated router should suppress
        /// the transmission of advertise messages to the other routers on the network.
        /// This is useful when multiple logical handlers are being added.  Once the handlers
        /// have been added, <see cref="LogicalAdvertise"/> can be called to force the
        /// advertise transmissions for all of the endpoints.
        /// </para>
        /// <note>
        /// Methods registered as handlers via this method will have to declare
        /// their message parameter as the base <see cref="Msg" /> class so that the compiler can
        /// create <see cref="MsgHandlerDelegate" /> instances.  The message parameter can then be
        /// cast to the proper message type within the handler method.
        /// </note>
        /// </remarks>
        void AddLogical(MsgHandlerDelegate callback, MsgEP logicalEP, System.Type msgType,
                        bool defHandler, SessionHandlerInfo sessionInfo, bool suppressAdvertise);

        /// <summary>
        /// Scans the target object passed for dispatchable methods and adds
        /// them to the dispatcher.
        /// </summary>
        /// <param name="target">The object to be scanned.</param>
        /// <remarks>
        /// Calling this method will result in the GUID being updated and
        /// then call the associated router's <see cref="MsgRouter.OnLogicalEndpointSetChange"/> 
        /// method.
        /// </remarks>
        void AddTarget(object target);

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
        void AddTarget(object target, string dynamicScope, IDynamicEPMunger munger, object targetGroup);

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
        void AddTarget(object target, IEnumerable<ScopeMunger> mungers, object targetGroup);

        /// <summary>
        /// Removes any logical endpoint message handlers referencing the target object from
        /// the message dispatcher.  Note that at this time only logical endpoints will
        /// be removed.  The method will throw an exception if any physical endpoints 
        /// map to the target.
        /// </summary>
        /// <param name="target">The target object whose message handlers are to be removed.</param>
        /// <exception cref="NotImplementedException">Thrown if a physical message handler maps to the target instance.</exception>
        void RemoveTarget(object target);

        /// <summary>
        /// Forces the update of the LogicalEndpointSetID and then the transmission of
        /// router advertise messages to notify other routers of the update.
        /// </summary>
        void LogicalAdvertise();

        /// <summary>
        /// Dispatches the message passed to the appropriate handler in
        /// the associated target instance.
        /// </summary>
        /// <param name="msg">The message to be dispatched.</param>
        /// <returns><c>true</c> if the message was succefully dispached to a handler.</returns>
        /// <remarks>
        /// <note>
        /// This method supports logical broadcasts by dispatching the
        /// message to handlers that match the logical endpoint as well as the
        /// message type.
        /// </note>
        /// </remarks>
        bool Dispatch(Msg msg);

        /// <summary>
        /// Clears the dispatch table.
        /// </summary>
        void Clear();
    }
}
