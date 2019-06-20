//-----------------------------------------------------------------------------
// FILE:        SipMssGateway.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a SIP gateway for Microsoft Speech Server that
//              allows Speech Server to establish connectivity with
//              low-cost SIP trunking providers.

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Diagnostics;

using LillTek.Common;
using LillTek.Net.Sockets;

// $todo(jeff.lill): 
//
// SipCore only handles a single registration URI right now
// so I'm only going to register the first URI from the
// configuration settings.

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Implements a SIP gateway for Microsoft Speech Server that
    /// allows Speech Server to establish connectivity with
    /// low-cost SIP trunking providers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Microsoft Speech Server 2007 has three basic limtations that
    /// prevent it from integrating with a traditional low-cost SIP
    /// trunking provider out-of-the-box:
    /// </para>
    /// <list type="bullet">
    ///     <item>
    ///     Speech Server 2007 supports SIP over stream based
    ///     protocols such as TCP or TLS.  Speech server does not support
    ///     SIP over UDP.  Asterisk and most SIP trunking services do not 
    ///     support SIP over streaming protocols.
    ///     </item>
    ///     <item>
    ///     Some SIP trunking providers do not implement static
    ///     routing of in-bound calls.  Instead, they require periodic
    ///     registration by the client to mantain a route.  Speech
    ///     Server is not capable of performing this registration.
    ///     </item>
    ///     <item>
    ///     Many SIP trunking providers will require authentication
    ///     for submitted requests.  Speech Server has no concept of 
    ///     credentials and thus cannot respond to authentication
    ///     challenges.
    ///     </item>
    /// </list>
    /// <para>
    /// This class can be used to solve these problems by acting as
    /// a bridge between Speech Server and the SIP Trunking provider,
    /// by implementing a <see cref="SipB2BUserAgent{TDialog}" /> that handles
    /// the protocol conversion and authentication as well as by
    /// optionally performing periodic registration updates with
    /// the provider.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class SipMssGateway
    {
        //---------------------------------------------------------------------
        // Implementation

        private SipCore                 core;           // The associated core
        private SipB2BUserAgent<object> b2bua;          // The back-to-back user agent
        private SipMssGatewaySettings   settings;       // The configuration settings
        private bool                    isRunning;      // True if the gateway is running
        private bool                    isUsed;         // True if the gateway has been started at some point
        private IPAddress               localAddress;   // IP address for the active network adapter
        private IPAddress               localSubnet;    // IP subnet mask for the active network adapter

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="core">The associated <see cref="SipCore" />.</param>
        /// <param name="settings">The gateway's <see cref="SipMssGatewaySettings" />.</param>
        public SipMssGateway(SipCore core, SipMssGatewaySettings settings)
        {
            this.core      = core;
            this.settings  = settings;
            this.isRunning = false;
            this.isUsed    = false;
        }

        /// <summary>
        /// Starts the gateway and the associated <see cref="SipCore" /> if it has not
        /// already been started.
        /// </summary>
        /// <remarks>
        /// <note><see cref="SipMssGateway" /> instances cannot be restarted.</note>
        /// </remarks>
        public void Start()
        {
            using (TimedLock.Lock(core))
            {
                if (isRunning)
                    throw new InvalidOperationException("MSS Gateway is already running.");

                if (isUsed)
                    throw new InvalidOperationException("Cannot restart a MSS Gateway.");

                isRunning = true;
                isUsed    = true;
            }

            Helper.GetNetworkInfo(out localAddress, out localSubnet);

            b2bua                         = new SipB2BUserAgent<object>(core);
            b2bua.InviteRequestReceived  += new SipB2BUAEventDelegate<object>(OnInviteRequestReceived);
            b2bua.InviteResponseReceived += new SipB2BUAEventDelegate<object>(OnInviteResponseReceived);
            b2bua.SessionConfirmed       += new SipB2BUAEventDelegate<object>(OnSessionConfirmed);
            b2bua.SessionClosing         += new SipB2BUAEventDelegate<object>(OnSessionClosing);
            b2bua.ClientRequestReceived  += new SipB2BUAEventDelegate<object>(OnClientRequestReceived);
            b2bua.ServerRequestReceived  += new SipB2BUAEventDelegate<object>(OnServerRequestReceived);
            b2bua.ClientResponseReceived += new SipB2BUAEventDelegate<object>(OnClientResponseReceived);
            b2bua.ServerResponseReceived += new SipB2BUAEventDelegate<object>(OnServerResponseReceived);

            if (!core.IsRunning)
                core.Start();

            b2bua.Start();

            if (settings.Register.Length > 1)
                SysLog.LogWarning("MSS Gateway currently supports only one registration URI.  Only the first URI will be registered.");

            if (settings.Register.Length > 0)
                core.StartAutoRegistration((string)settings.TrunkUri, (string)settings.Register[0]);
        }

        /// <summary>
        /// Stops the gateway if it's running.
        /// </summary>
        /// <remarks>
        /// <note><see cref="SipMssGateway" /> instances cannot be restarted.</note>
        /// </remarks>
        public void Stop()
        {
            using (TimedLock.Lock(core))
            {
                if (isRunning)
                    return;

                isRunning = false;
            }

            b2bua.Stop();
        }

        /// <summary>
        /// Returns <c>true</c> if the gateway is running.
        /// </summary>
        public bool IsRunning
        {
            get { return isRunning; }
        }

        //---------------------------------------------------------------------
        // B2BUA event handlers

        // Implementation Note
        // -------------------
        // 
        // The basic idea here is to intercept and modify the initial dialog INVITE
        // request and response messages for a dialog and modify these messages
        // before the B2BUA passes them on to the other side of the dialog.
        //
        // At this point, this simply means determining whether the request is
        // coming from Microsoft Speech Server (MSS) or from the SIP trunk.
        // I'm hacking this decision now by assuming that any SIP traffic from 
        // the current subnet is from MSS and should be routed outwards up the 
        // SIP trunk and that any traffic sourced outside the subnet is from the 
        // SIP trunk and should be routed to MSS.
        //
        // The current implementation updates the INVITE's URI to reflect the
        // updated routing.  The B2BUA takes care of modifying the dialog related
        // headers including the Call-ID and the To/From tags as well as munging
        // the Contact headers for request and response messages.

        private void OnInviteRequestReceived(object sender, SipB2BUAEventArgs<object> args)
        {
            // Called when the B2BUA receives a dialog initiating INVITE request
            // from the initiating side of the dialog.

            bool fromMSS;

            fromMSS = Helper.InSameSubnet(localAddress, args.Request.RemoteEndpoint.Address, localSubnet);
            fromMSS = localAddress.Equals(args.Request.RemoteEndpoint.Address);     // $todo(jeff.lill) delete this

            if (fromMSS)
                args.Request.Uri = (string)settings.TrunkUri;
            else
                args.Request.Uri = (string)settings.SpeechServerUri;
        }

        private void OnInviteResponseReceived(object sender, SipB2BUAEventArgs<object> args)
        {
            // Called when the B2BUA receives a dialog initiating INVITE response
            // from the accepting side of the dialog.
        }

        private void OnSessionConfirmed(object sender, SipB2BUAEventArgs<object> args)
        {
            // Called when the B2BUA receives the confirming ACK request from
            // the initiating side of the dialog.
        }

        private void OnSessionClosing(object sender, SipB2BUAEventArgs<object> args)
        {
            // Called when the B2BUA is in the process of closing a dialog/session.
        }

        private void OnClientRequestReceived(object sender, SipB2BUAEventArgs<object> args)
        {
            // Called when the B2BUA receives a request from the initiating side
            // of the bridged dialog.
        }

        private void OnServerRequestReceived(object sender, SipB2BUAEventArgs<object> args)
        {

            // Called when the B2BUA receives a request from the accepting side
            // of the bridged dialog.
        }

        private void OnClientResponseReceived(object sender, SipB2BUAEventArgs<object> args)
        {
            // Called when the B2BUA receives a response from the initiating side
            // of the bridged dialog.
        }

        private void OnServerResponseReceived(object sender, SipB2BUAEventArgs<object> args)
        {
            // Called when the B2BUA receives a response from the accepting side
            // of the bridged dialog.
        }
    }
}
