//-----------------------------------------------------------------------------
// FILE:        IMsgChannel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the basic behavior of a message channel.

using System;
using System.Net;
using System.Net.Sockets;

namespace LillTek.Messaging
{
    /// <summary>
    /// Describes the common behavior for all message channels.
    /// </summary>
    public interface IMsgChannel
    {
        /// <summary>
        /// Called occasionally by the associated router when the 
        /// channel's local endpoint is changed.
        /// </summary>
        /// <param name="localEP">The new endpoint.</param>
        /// <remarks>
        /// <para>
        /// The endpoint can change if the channel isn't bound to a
        /// specific IP address (aka IPAddress.Any), and the router
        /// detects an IP address change (due perhaps to a new network
        /// connection or a new IP address during a DHCP lease renewal).
        /// </para>
        /// <note>
        /// The endpoint passed will be normalized: the IP address will be valid.  
        /// If no adapter IP address association can be found, then the IP address 
        /// will be set to the loopback address (127.0.0.1).
        /// </note>
        /// </remarks>
        void OnNewEP(ChannelEP localEP);

        /// <summary>
        /// Closes the channel.
        /// </summary>
        void Close();

        /// <summary>
        /// Closes the channel if there's been no message activity within
        /// the specified timespan.
        /// </summary>
        /// <param name="maxIdle">Maximum idle time.</param>
        /// <returns><c>true</c> if the channel was closed.</returns>
        /// <remarks>
        /// Channels are not required to honor this call.  Specifically,
        /// the UDP channel implementation will always return false.
        /// </remarks>
        bool CloseIfIdle(TimeSpan maxIdle);

        /// <summary>
        /// Transmits the message to the specified channel endpoint.
        /// </summary>
        /// <param name="toEP">The target endpoint.</param>
        /// <param name="msg">The message.</param>
        void Transmit(ChannelEP toEP, Msg msg);
    }
}
