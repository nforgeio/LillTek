//-----------------------------------------------------------------------------
// FILE:        BaseTransportBindingElement.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an internal base TransportBindingElement capable of creating 
//              channel factories and listeners as appropriate for all LillTek 
//              Messaging based channel implementations.

using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Channels;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.ServiceModel;

namespace LillTek.ServiceModel.Channels
{
    /// <summary>
    /// Implements an internal <see cref="TransportBindingElement" /> capable of creating
    /// channel factories and listeners as appropriate for all LillTek 
    /// Messaging based channel implementations.
    /// </summary>
    public abstract class BaseTransportBindingElement : System.ServiceModel.Channels.TransportBindingElement
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public BaseTransportBindingElement()
            : base()
        {
        }

        /// <summary>
        /// Returns the element with the specified type from the binding element stack.
        /// </summary>
        /// <typeparam name="T">The requested element type.</typeparam>
        /// <param name="context">The <see cref="BindingContext" />.</param>
        /// <returns>The requested element if present, <c>null</c> otherwise.</returns>
        public override T GetProperty<T>(BindingContext context)
        {
            if (typeof(T) == this.GetType())
                return this as T;

            return null;
        }

        /// <summary>
        /// Returns a channel factory capable of creating channels of the specified type
        /// from a binding context.
        /// </summary>
        /// <typeparam name="TChannel">The type of channel to be constructed.</typeparam>
        /// <param name="context">The <see cref="BindingContext" /> holding the information necessary to construct the channel stack.</param>
        /// <returns>The channel factory.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="context"/> is <c>null</c>.</exception>
        /// <exception cref="NotImplementedException">Thrown if the binding element is unable to construct a channel factory for the specified type.</exception>
        public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            if (typeof(IDuplexChannel) == typeof(TChannel))
                return (IChannelFactory<TChannel>)(object)new DuplexChannelFactory(context);
            else if (typeof(IOutputChannel) == typeof(TChannel))
                return (IChannelFactory<TChannel>)(object)new OutputChannelFactory(context);
            else if (typeof(IOutputSessionChannel) == typeof(TChannel))
                return (IChannelFactory<TChannel>)(object)new OutputSessionChannelFactory(context);
            else if (typeof(IRequestChannel) == typeof(TChannel))
                return (IChannelFactory<TChannel>)(object)new RequestChannelFactory(context);

            throw new NotImplementedException(string.Format("Cannot construct channel factory for type: [{0}]", typeof(TChannel).FullName));
        }

        /// <summary>
        /// Determines whether it is possible to create a channel factory capable
        /// of creating channels of the specified type from a binding context.
        /// </summary>
        /// <typeparam name="TChannel">The type of channel to be constructed.</typeparam>
        /// <param name="context">The <see cref="BindingContext" /> holding the information necessary to construct the channel stack.</param>
        /// <returns><c>true</c> if the channel factory can be constructed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="context"/> is <c>null</c>.</exception>
        public override bool CanBuildChannelFactory<TChannel>(BindingContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            if (typeof(IDuplexChannel) == typeof(TChannel) ||
                typeof(IOutputChannel) == typeof(TChannel) ||
                typeof(IOutputSessionChannel) == typeof(TChannel) ||
                typeof(IRequestChannel) == typeof(TChannel))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns a channel listener capable of accepting channels of the specified type
        /// from a binding context.
        /// </summary>
        /// <typeparam name="TChannel">The type of channel to be constructed.</typeparam>
        /// <param name="context">The <see cref="BindingContext" /> holding the information necessary to construct the channel stack.</param>
        /// <returns>The channel listener.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="context"/> is <c>null</c>.</exception>
        /// <exception cref="NotImplementedException">Thrown if the binding element is unable to construct a channel listener for the specified type.</exception>
        public override IChannelListener<TChannel> BuildChannelListener<TChannel>(BindingContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            if (typeof(IDuplexChannel) == typeof(TChannel))
                return (IChannelListener<TChannel>)(object)new DuplexChannelListener(context);
            else if (typeof(IInputChannel) == typeof(TChannel))
                return (IChannelListener<TChannel>)(object)new InputChannelListener(context);
            else if (typeof(IInputSessionChannel) == typeof(TChannel))
                return (IChannelListener<TChannel>)(object)new InputSessionChannelListener(context);
            else if (typeof(IReplyChannel) == typeof(TChannel))
                return (IChannelListener<TChannel>)(object)new ReplyChannelListener(context);

            throw new NotImplementedException(string.Format("Cannot construct channel listener for type: [{0}]", typeof(TChannel).FullName));
        }

        /// <summary>
        /// Determines whether it is possible to create a channel listener capable
        /// of accepting channels of the specified type from a binding context.
        /// </summary>
        /// <typeparam name="TChannel">The type of channel to be constructed.</typeparam>
        /// <param name="context">The <see cref="BindingContext" /> holding the information necessary to construct the channel stack.</param>
        /// <returns><c>true</c> if the channel listener can be constructed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="context"/> is <c>null</c>.</exception>
        public override bool CanBuildChannelListener<TChannel>(BindingContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            if (typeof(IDuplexChannel) == typeof(TChannel) ||
                typeof(IInputChannel) == typeof(TChannel) ||
                typeof(IInputSessionChannel) == typeof(TChannel) ||
                typeof(IReplyChannel) == typeof(TChannel))
            {
                return true;
            }

            return false;
        }
    }
}
