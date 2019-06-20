//-----------------------------------------------------------------------------
// FILE:        IReliableMessenger.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the behavior of a reliable messenger.  Reliable
//              messengers persistently store messages that cannot be
//              delivered and periodically attempt to redeliver these
//              messages.

using System;
using System.Collections.Generic;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Messaging;

// $todo(jeff.lill): The design for this needs some rework.

// $todo(jeff.lill): 
//
// The IReliableMessenger model is somewhat broken for more advanced scenarios
// where remote server messenger implementations need to be able to deliver
// a delivery notification message back to the client.  In the current model,
// there's no way to specify and serialize the client side endpoint and/or
// the cluster where the response is to be sent.

// $todo(jeff.lill): 
//
// It would be cool to implement generic batching behavior in IReliableMessenger
// implementations.  The idea is that the messenger implementation would be
// able to batch a bunch of small messages into a single transmission, deliver
// the batch to the target, and then submit the individual items.  This would
// be a big performance gain without having to implement support for this
// in every application.

// $todo(jeff.lill): 
//
// I need to change the semantics of the Deliver() method to make it synchronous
// in the sense that it won't return until the message has been accepted for
// "safe" delivery.  This will typically mean that the message has been cached
// locally or elsewhere so that transmission can be retried later if necessary.
// This is essentially a flow control mechanism since I'm having problems with
// channel message queues being overwelmed by delivery messages in some instances.
// I should also add asynchronous equivalent methods BeginDelivery() and EndDelivery().

namespace LillTek.Messaging
{
    /// <summary>
    /// Delegate used to specified callbacks that handle <see cref="IReliableMessenger" />
    /// delivery confirmations.
    /// </summary>
    /// <param name="confirmation">The delivery confirmation.</param>
    public delegate void DeliveryConfirmCallback(DeliveryConfirmation confirmation);

    /// <summary>
    /// Defines the behavior of a plugable reliable messenger.  Most reliable
    /// messengers implementations persistently store messages that cannot be 
    /// delivered and periodically attempt to redeliver these messages.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="IReliableMessenger" /> interface defines a programming model similar to
    /// that of plugable topology providers.  Classes implementing this interface provide
    /// functionality for both the client and server sides of the messenger.
    /// <see cref="OpenClient" /> is called to open an instance in client mode
    /// and <see cref="OpenServer" /> is called to open an instance in server
    /// mode.  <see cref="Close" /> should be called when the messenger is no
    /// longer needed to release any resources associated with it.
    /// </para>
    /// <para>
    /// Reliable messengers can deliver messages to either an endpoint or to
    /// an <see cref="ITopologyProvider" />.  In either case, messages are sent using
    /// the Query/Response pattern.  The query message is sent to the endpoint
    /// or cluster and the messenge waits for a response message.  If no response
    /// is received and a <see cref="TimeoutException" /> is caught, then the
    /// messenger will persist the message so redelivery can be attempted at
    /// a later time.  The exact persisting and retry behavior is defined by
    /// the messenger implementation.
    /// </para>
    /// <para>
    /// Use the <see cref="Deliver(MsgEP,Msg,bool)" /> and <see cref="Deliver(ITopologyProvider,object,Msg,bool)" /> 
    /// methods to synchronously submit a message for delivery.  This method will 
    /// not return until it has confirmed that the message has been safely queued 
    /// for delivery where the interpretation of "safely queued" is determined by 
    /// the actual implementation of the <see cref="IReliableMessenger" /> interface.  
    /// Some implementations won't return until after the message is actually delivered, 
    /// others will wait until the message has been queued to persistent storage or 
    /// perhaps to an intermediate message queuing server.  Implementations must take
    /// some care though to implement some sort of reasonable queuing flow control
    /// mechanism here so that low-overhead processes that can submit a high volume
    /// of messages to a reliable messenger will ultimately be throttled by the
    /// performance of the messenger.
    /// </para>
    /// <para>
    /// The <see cref="BeginDelivery(MsgEP,Msg,bool,AsyncCallback,object)" />,  
    /// <see cref="BeginDelivery(ITopologyProvider,object,Msg,bool,AsyncCallback,object)" />
    /// and <see cref="EndDelivery" /> methods implement the asynchronous form of 
    /// <see cref="Deliver(MsgEP,Msg,bool)" /> and <see cref="Deliver(ITopologyProvider,object,Msg,bool)" />.
    /// </para>
    /// <para>
    /// Applications using a reliable messenger client specify a confirmation
    /// endpoint when calling <see cref="OpenClient" />.  This is the endpoint
    /// the messenger should use internally for receiving delivery confirmations
    /// from the server side of the messenger.  Applications can also pass a
    /// <see cref="DeliveryConfirmCallback" /> delegate specifying the method
    /// to be called when a confirmation is received.  A <see cref="DeliveryConfirmation" />
    /// parameter will be passed to this callback detailing the final disposition
    /// of the delivery.
    /// </para>
    /// <para>
    /// Client applications specify whether a confirmation is required by passing
    /// <b>confirmDelivery=true</b> to the <see cref="Deliver(MsgEP,Msg,bool)" /> or
    /// <see cref="Deliver(ITopologyProvider,object,Msg,bool)" /> methods.  If confirmation
    /// is requested and the server side of a messenger receives confirmation of the 
    /// successful delivery of a message or finally gives up trying, it will send 
    /// a confirmation message back to the confirmation endpoint specified in the
    /// <see cref="OpenClient" /> call.  The messenger client will receive this
    /// message and call the confirmation delegate with the appropriate parameter.
    /// Note that confirmation call backs may occur at any time after a delivery
    /// is initiated (e.g. before or after <b>Deliver()</b> returns).
    /// </para>
    /// <para>
    /// <see cref="IReliableMessenger" /> implementations will typically try as hard 
    /// to deliver a confirmation back to the original application as they tried to
    /// deliver the original message to the destination.  The confirmation
    /// will be received by the client side of the messenger and a most
    /// implementations will send some kind of <see cref="IAck" /> back to
    /// the server side to confirm that the confirmation was received.
    /// </para>
    /// <para><b><u>Reliable Messenging and Clustering</u></b></para>
    /// <para>
    /// One of the cooler aspects of <see cref="IReliableMessenger" /> is that it
    /// exposes the concept of reliable messaging to a plugable cluster topology.  The 
    /// tricky part of this is that the topology provider client state may need to be persisted
    /// and reconstituted at a later time or even on a different computer (ie. on
    /// the machine running the server side of the messenger).
    /// </para>
    /// <para>
    /// Reliable messenger implementations will use the <see cref="ITopologyProvider.SerializeClient" />,
    /// <see cref="ITopologyProvider.OpenClient(MsgRouter,MsgEP,ArgCollection)" />, <see cref="ITopologyProvider.SerializeKey" /> 
    /// and <see cref="ITopologyProvider.UnserializeKey" /> to achieve this.
    /// </para>
    /// <para>
    /// The idea is for the client side of the messenger to call the cluster's
    /// <see cref="ITopologyProvider.SerializeClient" /> method to serialize the clusters type and
    /// parameters into a string that can be persisted or transmitted to the server
    /// side of the messenger.  The topology provider client instance can then be reconstituted by
    /// calling <see cref="ITopologyProvider.OpenClient(MsgRouter,MsgEP,ArgCollection)" />.
    /// </para>
    /// <para>
    /// The only other tricky thing is persisting the optional topology parameter
    /// to be passed to the cluster's Query(), Send(), or Broadcast() methods.
    /// non-<c>null</c> topology parameters should be passed to the providers's <see cref="ITopologyProvider.SerializeKey" />
    /// to convert it to a string holding the information necessary reconstitute the
    /// parameter later and then the cluster's <see cref="ITopologyProvider.UnserializeKey" />
    /// method when the time comes to reconstitute the parameter.  Note that both of
    /// these methods may return null if the topology implementation wishes to ignore
    /// the parameter.
    /// </para>
    /// <para>
    /// This all works pretty well.  The biggest assumption is that if the server side of
    /// a messenger implement resides on a different machine than the client side that an 
    /// assembly implementing the topology type must be present on that machine and must be
    /// located in the same folder as the current executable file.
    /// </para>
    /// </remarks>
    public interface IReliableMessenger
    {
        /// <summary>
        /// Opens a client side instance of a reliable messenger implementation.
        /// </summary>
        /// <param name="router">The message router to be used by the messenger.</param>
        /// <param name="confirmEP">
        /// The logical endpoint the instance created should use to receive delivery confirmations
        /// or <c>null</c> if confirmations are to be disabled.
        /// </param>
        /// <param name="args">Implementation specific configuration information.</param>
        /// <param name="confirmCallback">
        /// The delegate to call when delivery confirmations are received from the
        /// server side of the messenger or <c>null</c> if no special processing is necessary.
        /// </param>
        /// <remarks>
        /// <note>
        /// <see cref="Close" /> should be called promptly when a messenger instance
        /// is not longer required to release any unneeded resource.
        /// </note>
        /// </remarks>
        void OpenClient(MsgRouter router, MsgEP confirmEP, ArgCollection args, DeliveryConfirmCallback confirmCallback);

        /// <summary>
        /// Opens a server side instance of a reliable messenger implementation.
        /// </summary>
        /// <param name="router">The message router to be used by the messenger.</param>
        /// <param name="args">Implementation specific configuration information.</param>
        /// <remarks>
        /// <note>
        /// <see cref="Close" /> should be called promptly when a messenger instance
        /// is not longer required to release any unneeded resource.
        /// </note>
        /// </remarks>
        void OpenServer(MsgRouter router, ArgCollection args);

        /// <summary>
        /// Closes the messenger, releasing all associated resources.
        /// </summary>
        void Close();

        /// <summary>
        /// Indicates whether the messenger was opened in client or server mode.
        /// </summary>
        bool IsClient { get; }

        /// <summary>
        /// Initiates a synchronous attempt to reliably deliver a message to a target endpoint.
        /// </summary>
        /// <param name="targetEP">The target endpoint.</param>
        /// <param name="query">The query message.</param>
        /// <param name="confirmDelivery">Indicates whether or not the delivery should be confirmed.</param>
        /// <remarks>
        /// <note>
        /// This method will not return until the message has been safely
        /// submitted to the queuing message implemented by the class.  This is done
        /// to ensure that the message is actually accepted by the messenger and
        /// also as a flow control mechanism.
        /// </note>
        /// </remarks>
        void Deliver(MsgEP targetEP, Msg query, bool confirmDelivery);

        /// <summary>
        /// Initiates a synchronous attempt to reliably deliver a message to a cluster.
        /// </summary>
        /// <param name="targetCluster">The target cluster.</param>
        /// <param name="key">The optional topology key.</param>
        /// <param name="query">The query message.</param>
        /// <param name="confirmDelivery">Indicates whether or not the delivery should be confirmed.</param>
        /// <remarks>
        /// <note>
        /// This method will not return until the message has been safely
        /// submitted to the queuing message implemented by the class.  This is done
        /// to ensure that the message is actually accepted by the messenger and
        /// also as a flow control mechanism.
        /// </note>
        /// </remarks>
        void Deliver(ITopologyProvider targetCluster, object key, Msg query, bool confirmDelivery);

        /// <summary>
        /// Initiates an asynchronous attempt to reliably deliver a message to a target endpoint.
        /// </summary>
        /// <param name="targetEP">The target endpoint.</param>
        /// <param name="query">The query message.</param>
        /// <param name="confirmDelivery">Indicates whether or not the delivery should be confirmed.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state.</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation's progress.</returns>
        /// <remarks>
        /// <note>
        /// The operation doesn't complete until the message has been safely
        /// submitted to the queuing message implemented by the class.  This is done
        /// to ensure that the message is actually accepted by the messenger and
        /// also as a flow control mechanism.
        /// </note>
        /// <note>
        /// Every successful call to <see cref="BeginDelivery(MsgEP,Msg,bool,AsyncCallback,object)" /> must eventually
        /// be followed by a call to <see cref="EndDelivery" />.
        /// </note>
        /// </remarks>
        IAsyncResult BeginDelivery(MsgEP targetEP, Msg query, bool confirmDelivery,
                                   AsyncCallback callback, object state);

        /// <summary>
        /// Initiates an asynchronous attempt to reliably deliver a message to a cluster.
        /// </summary>
        /// <param name="targetCluster">The target cluster.</param>
        /// <param name="key">The optional topology key.</param>
        /// <param name="query">The query message.</param>
        /// <param name="confirmDelivery">Indicates whether or not the delivery should be confirmed.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state.</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation's progress.</returns>
        /// <remarks>
        /// <note>
        /// The operation doesn't complete until the message has been safely
        /// submitted to the queuing message implemented by the class.  This is done
        /// to ensure that the message is actually accepted by the messenger and
        /// also as a flow control mechanism.
        /// </note>
        /// <note>
        /// Every successful call to <see cref="BeginDelivery(MsgEP,Msg,bool,AsyncCallback,object)" /> must eventually
        /// be followed by a call to <see cref="EndDelivery" />.
        /// </note>
        /// </remarks>
        IAsyncResult BeginDelivery(ITopologyProvider targetCluster, object key, Msg query, bool confirmDelivery,
                                   AsyncCallback callback, object state);

        /// <summary>
        /// Completes and asynchronous delivery operation started by a call to one of the
        /// <b>BeginDelivery()</b> methods.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <b>BeginDelivery()</b>.</param>
        void EndDelivery(IAsyncResult ar);
    }
}
