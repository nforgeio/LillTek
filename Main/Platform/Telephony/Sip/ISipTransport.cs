//-----------------------------------------------------------------------------
// FILE:        ISipTransport.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the behavior of a SIP transport implementation.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

using LillTek.Common;

// $todo(jeff.lill): 
//
// I might want to rethink the design of this class a bit and instead
// of simply having a single asynchronous Send() method, perhaps
// I should have BeginSend()/EndSend().  There are two problems with
// the current design for streaming transports:
//
//      * The connection operation is currently synchronously happening
//        as necessary in Send() so that connection errors can be 
//        reported to and handled by the transaction layer.  This will
//        hold working threads longer than really necessary and reduce
//        performance.
//
//      * There's no way to report transport errors encountered while
//        asynchronously deliverying the message.

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Defines the behavior of a SIP transport implementation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ISipTransport" />s are responsible for sending and receiving <see cref="SipMessage" />s
    /// to remote endpoints on the network.  Both streaming and non-streaming protocols (such as UDP)
    /// can be implemented on this model.
    /// </para>
    /// <para>
    /// The <see cref="Start(SipTransportSettings,ISipMessageRouter)" /> method is responsible for 
    /// establishing the transport's network bindings specified by the <see cref="SipTransportSettings" /> and 
    /// initiating message reception.  The <see cref="ISipMessageRouter" /> parameter specifies the agent router
    /// to be called when messages are received by the transport.  <see cref="Stop" /> stops the transport and
    /// release all network bindings.
    /// </para>
    /// <para>
    /// The <see cref="Send" /> method initiates an asynchronous operation to deliver a <see cref="SipMessage" />
    /// to a remote endpoint.
    /// </para>
    /// <para>
    /// The <see cref="TransportType" />, <see cref="Name" />, <see cref="IsStreaming" />, <see cref="Settings" />,
    /// and <see cref="LocalEndpoint" /> properties returns useful information about the transport.
    /// </para>
    /// <para>
    /// Some transports need to perform backgound tasks such as closing network connections that have
    /// been idle too long.  This functionality can be implemented in the <see cref="OnBkTask" /> method
    /// which will be called periodically from higher layers in the protocol stack (typically a <see cref="ISipMessageRouter" />
    /// implementation).
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public interface ISipTransport
    {
        /// <summary>
        /// Starts the transport.
        /// </summary>
        /// <param name="settings">The <see cref="SipTransportSettings" />.</param>
        /// <param name="router">The <see cref="ISipMessageRouter" /> instance that will handle the routing of received messages.</param>
        /// <exception cref="SocketException">Thrown if there's a conflict with the requested and existing socket bindings.</exception>
        void Start(SipTransportSettings settings, ISipMessageRouter router);

        /// <summary>
        /// Stops the transport if it's running.
        /// </summary>
        void Stop();

        /// <summary>
        /// Sets the diagnostic tracing mode.
        /// </summary>
        /// <param name="traceMode">The <see cref="SipTraceMode" /> flags.</param>
        void SetTraceMode(SipTraceMode traceMode);

        /// <summary>
        /// Disables the transport such that it will no longer send or receive SIP messages.  Used for
        /// unit testing to simulate network and hardware failures.
        /// </summary>
        void Disable();

        /// <summary>
        /// Asynchronously transmits the message passed to the destination
        /// indicated by the <see paramref="remoteEP" /> parameter.
        /// </summary>
        /// <param name="remoteEP">The destination SIP endpoint's <see cref="NetworkBinding" />.</param>
        /// <param name="message">The <see cref="SipMessage" /> to be transmitted.</param>
        /// <exception cref="SipTransportException">Thrown if the remote endpoint rejected the message or timed out.</exception>
        void Send(NetworkBinding remoteEP, SipMessage message);

        /// <summary>
        /// Returns the transport's <see cref="SipTransportType" />.
        /// </summary>
        SipTransportType TransportType { get; }

        /// <summary>
        /// Returns the transport's name, one of <b>UDP</b>, <b>TCP</b>, <b>TLS</b>.  This
        /// value is suitable for including in a SIP message's <b>Via</b> header.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Returns <c>true</c> for streaming transports (TCP or TLS), <c>false</c> for packet transports (UDP).
        /// </summary>
        bool IsStreaming { get; }

        /// <summary>
        /// The <see cref="SipTransportSettings" /> associated with this transport instance.
        /// </summary>
        SipTransportSettings Settings { get; set; }

        /// <summary>
        /// Returns the transport's local network binding.
        /// </summary>
        NetworkBinding LocalEndpoint { get; }

        /// <summary>
        /// This method must be called periodically on a background thread
        /// by the application so that the transport can implement any necessary
        /// background activities.
        /// </summary>
        void OnBkTask();
    }
}
