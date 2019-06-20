//-----------------------------------------------------------------------------
// FILE:        WcfClientContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Maps WCF channel proxy behavior into a form suitable for the
//              C# using statement coding pattern.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

using LillTek.Common;

namespace LillTek.Net.Wcf
{
    /// <summary>
    /// Maps WCF channel proxy behavior into a form suitable for the
    /// C# <c>using</c> statement coding pattern.
    /// </summary>
    /// <typeparam name="TProxy">The client proxy type.</typeparam>
    /// <remarks>
    /// <para>
    /// One of the subtle issues with client side WCF programming is that
    /// a call to a client side proxy's <see cref="ICommunicationObject.Close()" /> or
    /// <see cref="IDisposable.Dispose()" /> methods may throw a
    /// <see cref="TimeoutException" /> or a <see cref="CommunicationException" />.
    /// The proper way of handling these exceptions is to call the channel's
    /// <see cref="ICommunicationObject.Abort()" /> method.  This means that code
    /// such as:
    /// </para>
    /// <code language="cs">
    /// IMyService  client;
    /// 
    /// client = channelFactory.CreateChannel();
    /// using (client as IDisposabe)
    /// {
    ///     client.DoSomething();
    /// }
    /// </code>
    /// <para>
    /// is not a good coding pattern, since the implicit call to <see cref="IDisposable.Dispose()" />
    /// may throw one of these exceptions potentially leaving the client proxy in a semi-open
    /// state.
    /// </para>
    /// <para>
    /// Another subtle issue revolves around how WCF implements thread blocking when
    /// implicitly opening a client proxy.  This can cause <see cref="TimeoutException" />s
    /// to be thrown in situations where you don't expect them.  It's better to explicitly
    /// open the client channel, rather than having WCF do this.
    /// </para>
    /// <para>
    /// This class provides an easy way to code WCF client applications with the <c>using</c> 
    /// design pattern, by ensuring that the client channel is closed before exiting the
    /// <c>using</c> block.  Here's how to do this:
    /// </para>
    /// <code language="cs">
    /// using (WcfClientContext&lt;IMyService&gt; client = new WcfClientContext&lt;IMyService&gt;(channelFactory.CreateChannel()))
    /// {
    ///     client.Open();
    ///     client.Proxy.DoSomething();
    /// }
    /// </code>
    /// </remarks>
    /// <threadsatefy instance="false" />
    public sealed class WcfClientContext<TProxy> : IDisposable
    {
        private TProxy          proxy;
        private IClientChannel  channel;

        /// <summary>
        /// Constructs a <see cref="WcfClientContext{TProxy}" /> and associates it with
        /// an existing proxy instance.
        /// </summary>
        /// <param name="proxy">The client proxy instance.</param>
        public WcfClientContext(TProxy proxy)
        {
            this.proxy   = proxy;
            this.channel = (IClientChannel)proxy;
        }

        /// <summary>
        /// Returns the associated client proxy.
        /// </summary>
        public TProxy Proxy
        {
            get { return proxy; }
        }

        /// <summary>
        /// Opens the associated client proxy instance.
        /// </summary>
        public void Open()
        {
            channel.Open();
        }

        /// <summary>
        /// Closes the client proxy instance if it is currently open, ensuring that
        /// it transitions to the closed state by aborting the underlying channel
        /// if necessary.
        /// </summary>
        public void Close()
        {
            if (channel == null || channel.State == CommunicationState.Closed)
                return;

            try
            {
                channel.Close();
            }
            catch
            {
                channel.Abort();
            }
        }

        /// <summary>
        /// Ensures that the associated proxy is closed.
        /// </summary>
        public void Dispose()
        {
            Close();
        }
    }
}
