//-----------------------------------------------------------------------------
// FILE:        WcfHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Misc WCF related utilities.

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
    /// Misc WCF related utilities.
    /// </summary>
    /// <remarks>
    /// <para><b><u>The Client Channel Open/Close Coding Pattern</u></b></para>
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
    /// This class provides the <see cref="Open" /> and <see cref="Close" /> methods to 
    /// help implement a better design pattern.  <see cref="Open" /> simply accepts an <see cref="object" />
    /// parameter, casts it to <see cref="IClientChannel" /> and calls <see cref="ICommunicationObject.Open()" />.
    /// This is essentially just a shortcut to avoid having to add a bunch of ugly explict
    /// type casts to your code.
    /// </para>
    /// <para>
    /// <see cref="Close" /> is designed to handle any exceptions by calling <see cref="ICommunicationObject.Abort()" />
    /// on the underlying channel and also to deal with the situation where the channel was not created
    /// properly.  Here's a simple coding pattern using these methods:
    /// </para>
    /// <code language="cs">
    /// IMyService  client = null;
    /// 
    /// try
    /// {
    ///     client = channelFactory.CreateChannel();
    ///     WcfHelper.Open(client);
    ///     
    ///     client.DoSomething();
    /// }
    /// finally 
    /// {
    ///     WcfHelper.Close(client);
    /// }
    /// </code>
    /// <para>
    /// An alternative to this design pattern is to use <see cref="WcfClientContext{TProxy}" />
    /// within a <c>using</c> statement.
    /// </para>
    /// </remarks>
    public static class WcfHelper
    {
        /// <summary>
        /// Opens a <see cref="IClientChannel" /> or client side proxy instance.
        /// </summary>
        /// <param name="clientChannel">The client channel or proxy.</param>
        public static void Open(object clientChannel)
        {
            ((IClientChannel)clientChannel).Open();
        }

        /// <summary>
        /// Closes or aborts a <see cref="IClientChannel" /> or client side proxy instance
        /// if it is applicated and open.
        /// </summary>
        /// <param name="clientChannel">The client channel or proxy.</param>
        public static void Close(object clientChannel)
        {
            var channel = (IClientChannel)clientChannel;

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
    }
}
