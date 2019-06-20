//-----------------------------------------------------------------------------
// FILE:        WcfChannelFactory.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a thin wrapper over ChannelFactory that eases
//              the creation of channels and service proxies based on a 
//              WcfEndpoint instance.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

using LillTek.Common;

namespace LillTek.Net.Wcf
{
    /// <summary>
    /// Implements a thin wrapper over <see cref="ChannelFactory" /> that eases
    /// the creation of channels and service proxies based on a <see cref="WcfEndpoint" /> 
    /// instance.
    /// </summary>
    /// <typeparam name="TChannel">The type of channel produced by the factory.</typeparam>
    public class WcfChannelFactory<TChannel> : IDisposable
    {
        private object                      syncLock = new object();
        private ChannelFactory<TChannel>    factory = null;

        /// <summary>
        /// Constructs a channel factory from a <see cref="WcfEndpoint" />.
        /// </summary>
        /// <param name="endpoint">The <see cref="WcfEndpoint" /> describing the service endpoint.</param>
        public WcfChannelFactory(WcfEndpoint endpoint)
        {
            Binding             binding;
            EndpointAddress     address;

            binding = endpoint.Binding;
            address = new EndpointAddress(endpoint.Uri);
            factory = new ChannelFactory<TChannel>(binding, address);
            factory.Open();
        }

        /// <summary>
        /// Constructs a channel factory from the textual description of a <see cref="WcfEndpoint" />.
        /// </summary>
        /// <param name="endpointSettings">The textual description of a <see cref="WcfEndpoint" />.</param>
        public WcfChannelFactory(string endpointSettings)
            : this(WcfEndpoint.Parse(endpointSettings))
        {
        }

        /// <summary>
        /// Creates a WCF channel proxy.
        /// </summary>
        /// <returns>The channel proxy instance.</returns>
        public TChannel CreateChannel()
        {
            return factory.CreateChannel();
        }

        /// <summary>
        /// Closes the factory if it is open.
        /// </summary>
        public void Close()
        {
            lock (syncLock)
            {
                if (factory != null)
                {
                    factory.Close();
                    factory = null;
                }
            }
        }

        /// <summary>
        /// Releases all resources accociated with the factory.
        /// </summary>
        public void Dispose()
        {
            Close();
        }
    }
}
