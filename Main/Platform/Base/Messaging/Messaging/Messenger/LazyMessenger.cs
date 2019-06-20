//-----------------------------------------------------------------------------
// FILE:        LazyMessenger.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A basic implementation of IReliableMessenger that does not try
//              very hard to deliver the message.

using System;
using System.Collections.Generic;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.Messaging
{
    /// <summary>
    /// A basic implementation of <see cref="IReliableMessenger" /> that does not try
    /// very hard to deliver the message.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is probably the most limited implementation of <see cref="IReliableMessenger" />
    /// possible.  This class sends the query to the endpoint or cluster and waits for
    /// a response.  If a response is received or the query times out then the confirmation
    /// callback will be called.  The class does not persist the query or attempt any retry
    /// behavior other than those built into the messaging library or the cluster implementation.
    /// </para>
    /// <para>
    /// The Deliver() methods provided by this <see cref="IReliableMessenger" /> implementation 
    /// will not return until an acknowledgement is received from the remote endpoint or
    /// the query timeout is detected.
    /// </para>
    /// <note>
    /// Note that the server side implementation of this messenger is basically a NOP.
    /// </note>
    /// </remarks>
    public sealed class LazyMessenger : IReliableMessenger
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// Used to hold private state information during an async query to
        /// an endpoint or cluster.
        /// </summary>
        private sealed class QueryState
        {
            public readonly AsyncResult         DeliverAR;
            public readonly bool                ConfirmDelivery;
            public readonly ITopologyProvider   TopologyProvider;

            public QueryState(AsyncResult deliverAR, bool confirmDelivery)
            {
                this.DeliverAR       = deliverAR;
                this.ConfirmDelivery = confirmDelivery;
            }

            public QueryState(AsyncResult deliverAR, bool confirmDelivery, ITopologyProvider topologyProvider)
            {
                this.DeliverAR        = deliverAR;
                this.ConfirmDelivery  = confirmDelivery;
                this.TopologyProvider = topologyProvider;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private MsgRouter                   router;
        private bool                        isClient;
        private MsgEP                       confirmEP;
        private DeliveryConfirmCallback     confirmCallback;
        private AsyncCallback               onEndpointDelivery;
        private AsyncCallback               onClusterDelivery;

        /// <summary>
        /// Opens a client side instance of a reliable messenger implementation.
        /// </summary>
        /// <param name="router">The message router to be used by the messenger.</param>
        /// <param name="confirmEP">
        /// The logical endpoint the instance created should use to receive delivery confirmations
        /// or <c>null</c> if confirmations are to be disabled.
        /// </param>
        /// <param name="args">Messenger implementation specific parameters (ignored for this implementation).</param>
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
        public void OpenClient(MsgRouter router, MsgEP confirmEP, ArgCollection args, DeliveryConfirmCallback confirmCallback)
        {
            if (confirmEP != null && !confirmEP.IsLogical)
                throw new ArgumentException(ReliableMessenger.ConfirmEPNotLogicalMsg);

            this.router             = router;
            this.isClient           = true;
            this.onEndpointDelivery = new AsyncCallback(OnEndpointDelivery);
            this.onClusterDelivery  = new AsyncCallback(OnClusterDelivery);

            if (confirmEP == null || confirmCallback == null)
            {
                this.confirmEP       = null;
                this.confirmCallback = null;
            }
            else
            {
                this.confirmEP       = confirmEP;
                this.confirmCallback = confirmCallback;
            }
        }

        /// <summary>
        /// Opens a server side instance of a reliable messenger implementation.
        /// </summary>
        /// <param name="router">The message router to be used by the messenger.</param>
        /// <param name="args">Messenger implementation specific parameters (ignored for this implementation).</param>
        /// <remarks>
        /// <see cref="LazyMessenger" /> does not implement a server side.  This method
        /// will throw an exception if called.
        /// </remarks>
        /// <exception cref="NotImplementedException">Thrown when called.</exception>
        public void OpenServer(MsgRouter router, ArgCollection args)
        {
            this.isClient = false;
        }

        /// <summary>
        /// Closes the messenger, releasing all associated resources.
        /// </summary>
        public void Close()
        {
            // Nothing to do here
        }

        /// <summary>
        /// Indicates whether the messenger was opened in client or server mode.
        /// </summary>
        public bool IsClient
        {
            get { return isClient; }
        }

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
        public void Deliver(MsgEP targetEP, Msg query, bool confirmDelivery)
        {
            var ar = BeginDelivery(targetEP, query, confirmDelivery, null, null);

            EndDelivery(ar);
        }

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
        /// Note that the operation doesn't complete until the message has been safely
        /// submitted to the queuing message implemented by the class.  This is done
        /// to ensure that the message is actually accepted by the messenger and
        /// also as a flow control mechanism.
        /// </note>
        /// <note>
        /// Every successful call to <see cref="BeginDelivery(MsgEP,Msg,bool,AsyncCallback,object)" /> must eventually
        /// be followed by a call to <see cref="EndDelivery" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginDelivery(MsgEP targetEP, Msg query, bool confirmDelivery,
                                          AsyncCallback callback, object state)
        {
            if (!router.IsOpen)
                throw new ArgumentException(ReliableMessenger.RouterClosedMsg);

            var confirmation = new DeliveryConfirmation();
            var deliverAR    = new AsyncResult(null, callback, state);

            confirmation.TargetEP = targetEP;
            confirmation.Query    = query;
            confirmation.State    = new QueryState(deliverAR, confirmDelivery && confirmCallback != null);

            router.BeginQuery(targetEP, query, onEndpointDelivery, confirmation);
            deliverAR.Started();
            return deliverAR;
        }

        /// <summary>
        /// Handles endpoint delivery completions.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnEndpointDelivery(IAsyncResult ar)
        {
            var confirmation = (DeliveryConfirmation)ar.AsyncState;
            var state        = (QueryState)confirmation.State;
            Msg response;

            try
            {
                response = router.EndQuery(ar);

                state.DeliverAR.Notify();

                if (state.ConfirmDelivery)
                {
                    confirmation.Timestamp = DateTime.UtcNow;
                    confirmation.Response = response;

                    confirmCallback(confirmation);
                }
            }
            catch (Exception e)
            {
                state.DeliverAR.Notify(e);

                if (state.ConfirmDelivery)
                {
                    confirmation.Timestamp = DateTime.UtcNow;
                    confirmation.Exception = e;

                    confirmCallback(confirmation);
                }
            }
        }

        /// <summary>
        /// Initiates a synchronous attempt to reliably deliver a message to a cluster.
        /// </summary>
        /// <param name="topologyProvider">The target cluster topology provider.</param>
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
        public void Deliver(ITopologyProvider topologyProvider, object key, Msg query, bool confirmDelivery)
        {
            var ar = BeginDelivery(topologyProvider, key, query, confirmDelivery, null, null);

            EndDelivery(ar);
        }

        /// <summary>
        /// Initiates an asynchronous attempt to reliably deliver a message to a cluster.
        /// </summary>
        /// <param name="topologyProvider">The cluster's topology provider.</param>
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
        public IAsyncResult BeginDelivery(ITopologyProvider topologyProvider, object key, Msg query, bool confirmDelivery,
                                          AsyncCallback callback, object state)
        {
            if (!router.IsOpen)
                throw new ArgumentException(ReliableMessenger.RouterClosedMsg);

            var confirmation = new DeliveryConfirmation();
            var deliverAR    = new AsyncResult(null, callback, state);

            confirmation.TargetEP      = topologyProvider.ClusterEP;
            confirmation.Query         = query;
            confirmation.TopologyID    = topologyProvider.InstanceID;
            confirmation.TopologyInfo  = topologyProvider.SerializeClient();
            confirmation.TopologyParam = null;
            confirmation.State         = new QueryState(deliverAR, confirmDelivery && confirmCallback != null, topologyProvider);

            topologyProvider.BeginQuery(key, query, onClusterDelivery, confirmation);
            deliverAR.Started();
            return deliverAR;
        }

        /// <summary>
        /// Handles cluster delivery completions.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnClusterDelivery(IAsyncResult ar)
        {
            var confirmation = (DeliveryConfirmation)ar.AsyncState;
            var state        = (QueryState)confirmation.State;
            Msg response;

            try
            {
                response = state.TopologyProvider.EndQuery(ar);

                state.DeliverAR.Notify();

                if (state.ConfirmDelivery)
                {
                    confirmation.Timestamp = DateTime.UtcNow;
                    confirmation.Response  = response;

                    confirmCallback(confirmation);
                }
            }
            catch (Exception e)
            {
                state.DeliverAR.Notify(e);

                if (state.ConfirmDelivery)
                {
                    confirmation.Timestamp = DateTime.UtcNow;
                    confirmation.Exception = e;

                    confirmCallback(confirmation);
                }
            }
        }

        /// <summary>
        /// Completes and asynchronous delivery operation started by a call to one of the
        /// BeginDeliver() methods.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by BeginDelivery().</param>
        public void EndDelivery(IAsyncResult ar)
        {
            var deliverAR = (AsyncResult)ar;

            deliverAR.Wait();
            try
            {
                if (deliverAR.Exception != null)
                    throw deliverAR.Exception;

                return;
            }
            finally
            {
                deliverAR.Dispose();
            }
        }
    }
}
