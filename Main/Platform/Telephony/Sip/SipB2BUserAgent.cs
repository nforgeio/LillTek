//-----------------------------------------------------------------------------
// FILE:        SipB2BUserAgent.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a SIP Back-to-Back User Agent (B2BUA).

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using LillTek.Common;

// $todo(jeff.lill): 
//
// I've taken some shortcuts and implemented a few important things
// synchronously.  At some point, I need to come back and fix this
// to improve scalability.

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Implements a SIP Back-to-Back User Agent (B2BUA) as described
    /// here in <a href="http://en.wikipedia.org/wiki/Back-to-back_user_agent">Wikipedia</a>.
    /// </summary>
    /// <typeparam name="TState">The application session state type.</typeparam>
    /// <remarks>
    /// <para>
    /// Back-to-Back User Agent's (B2BUA) are a combination of a User Agent Server (UAS)
    /// and a User Agent Client (UAC).  The UAS receives requests from other SIP clients
    /// and processes them by using the UAC to resubmit them to other network elements.
    /// B2BUAs implicitly track the state of the operation, including that of any dialogs.
    /// </para>
    /// <para>
    /// B2BUAs in many was are similar to stateful proxies described in RFC 3261 on
    /// page 92, but the special behavors described for proxies are not required 
    /// for B2BUAs because an explicit UAC is used to resubmit and process requests
    /// rather than simply forwarding messages, as a stateful proxy does.  There
    /// is also no need for a B2BUA to modify message routing headers to keep a B2BUA 
    /// in the messaging path between to two SIP endpoints since the B2BUA is the
    /// endpoint seen by each of the two SIP endpoints.
    /// </para>
    /// <para>
    /// The <see cref="SipB2BUserAgent{TState}" /> class is designed to be used 
    /// within applications that act as Session Border Controllers and perform protocol bridging,
    /// where all SIP messages between two endpoints need to be inspected and potentially
    /// modfied and also where one side or the other of a session may not be able to 
    /// handle necessary operations such as authentication.  To use a B2BUA, you need
    /// to construct and start a <see cref="SipCore" /> instance, then construct a
    /// <see cref="SipB2BUserAgent{TState}" />, passing the core and then start the agent.
    /// Here's an example:
    /// </para>
    /// <code language="cs">
    /// SipBasicCore        core;
    /// SipB2BUserAgent     b2bua;
    /// 
    /// core = new SipBasicCore(SipCoreSettings.LoadConfig("CoreSettings"));
    /// core.Start();
    /// b2bua = new SipB2BUserAgent(core);
    /// b2bua.Start();
    /// </code>
    /// <para>
    /// A B2BUA session is the combination of the dialog established by a client with the
    /// B2BUA, a dialog with a server established by the B2BUA, and application state.
    /// The client dialog is referred to as the <i>client</i> dialog and the server dialog is 
    /// referred to as the <i>server</i> dialog.  B2BUA sessions are implemented by the
    /// <see cref="SipB2BUASession{TState}" /> generic class.
    /// </para>
    /// <para>
    /// Applications use <see cref="SipB2BUserAgent{TState}" /> by enlisting in
    /// the following events that are raised as SIP requests and responses are received or
    /// when a session is closed.  The following events are available:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><see cref="InviteRequestReceived" /></term>
    ///         <description>
    ///         Raised when the initial INVITE request is received from the client on the upstream
    ///         dialog.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="InviteResponseReceived" /></term>
    ///         <description>
    ///         Raised when the INVITE response is received from the server on the downstream
    ///         dialog.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ClientRequestReceived" /></term>
    ///         <description>
    ///         Raised when the B2BUA receives a request on the upstream dialog from
    ///         the client.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ClientResponseReceived" /></term>
    ///         <description>
    ///         Raised when the B2BUA receives a response on the upstream dialog from
    ///         the client.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ServerRequestReceived" /></term>
    ///         <description>
    ///         Raised when the B2BUA receives a request on the downstream dialog from
    ///         the server.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ServerResponseReceived" /></term>
    ///         <description>
    ///         Raised when the B2BUA receives a response on the downstream dialog from
    ///         the server.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="SessionConfirmed" /></term>
    ///         <description>
    ///         Raised when a session has been confirmed when the client on the downstream
    ///         dialog sends an ACK request to the server.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="SessionClosing" /></term>
    ///         <description>
    ///         Raised just before the B2BUA closes the session.
    ///         </description>
    ///     </item>
    /// </list>
    /// <para>
    /// The base class provides reasonable implementations for each of
    /// these events.  Applications need only enlist in and handle the 
    /// events that need special handling.
    /// </para>
    /// <para><b><u>Instantiating and Starting a B2BUA</u></b></para>
    /// <para>
    /// <see cref="SipB2BUserAgent{TState}" /> instances are designed to be associated with
    /// a <see cref="SipCore" /> with the core handling the management of the transports,
    /// the routing of messages between transports and client/server agents, as well
    /// as the management of dialogs.  You should instantiate the <see cref="SipCore" />
    /// first, then create initialize and start the <see cref="SipB2BUserAgent{TState}" />, and then
    /// finally, start the core.
    /// </para>
    /// <code language="cs">
    /// SipCoreSettings                     coreSettings = new SipCoreSettings&lt;SipDialog&gt;();
    /// SipCore                             core;
    /// SipB2BUserAgent&lt;object,SipDialog&gt;   b2bUA;
    /// 
    /// core = new SipCore(coreSettings);
    /// 
    /// b2bUA                         = new SipB2BUserAgent&lt;object,SipDialog&gt;();
    /// b2bUA.InviteRequestReceived  += new SipB2BUAEventDelegate&lt;object,SipDialog&gt;(OnInviteRequest);
    /// b2bUA.InviteResponseReceived += new SipB2BUAEventDelegate&lt;object,SipDialog&gt;(OnInviteResponse);
    /// b2bUA.Start();
    /// 
    /// core.Start();
    /// </code>
    /// <para>
    /// Note that <see cref="SipB2BUserAgent{TState}" /> is a generic class that requires
    /// the <typeparamref name="TState"/> that defines application specific state.
    /// </para>
    /// <note>
    /// <see cref="SipB2BUserAgent{TState}" />" /> instances enlist in some
    /// of the core's events and is implemented with the assumption that it is the
    /// only entity that is processing events raised by the core.  Any other
    /// event handlers <b>must</b> limit their activities to monitoring these
    /// events (e.g. for logging purposes) rather than acting on them.
    /// </note>
    /// <para><b><u>Handling B2BUA Events</u></b></para>
    /// <para>
    /// All B2BUA events call a <see cref="SipB2BUAEventDelegate{TState}" /> when raised.  A
    /// B2BUA agent reference will be passed as the <b>sender</b> parameter and a
    /// <see cref="SipB2BUAEventArgs{TState}" /> instance as the
    /// event arguments.  The argument class includes properties that reference the
    /// <see cref="SipCore" /> and the B2BUA as well as the session, request, and
    /// response associated with the event.  Note that the request and response will be
    /// <c>null</c> when appropriate for the particular event.
    /// </para>
    /// <para>
    /// Event handlers can indicate that they wish to session to be closed b
    /// setting the event argument's <see cref="SipB2BUAEventArgs{TState}.CloseSession" />
    /// property to <c>true</c>.
    /// </para>
    /// <para><see cref="InviteRequestReceived" /></para>
    /// <para>
    /// This event is raised when the initial INVITE <see cref="SipRequest" /> is received from the 
    /// client on the upstream dialog.  At this point, no downstream dialog has yet been established with
    /// the server.  The default action by the B2BUA is to forward the request onto the server via 
    /// the downstream dialog to establish an end-to-end dialog.  The event argument's
    /// <see cref="SipB2BUAEventArgs{TState}.ReceivedRequest" /> property will be set to the
    /// actual INVITE request received and the <see cref="SipB2BUAEventArgs{TState}.Request" />
    /// will be set to the INVITE request that will be forwarded on by the B2BUA.  The application's
    /// event handler can modify or replace the <see cref="SipB2BUAEventArgs{TState}.Request" /> 
    /// in the event arguments before returning and the B2BUA will send the modified 
    /// message.  The application can also set <see cref="SipB2BUAEventArgs{TState}.Request" /> to 
    /// <c>null</c> which indicates to the B2BUA that it should not forward the request.
    /// </para>
    /// <para>
    /// The event argument's <see cref="SipB2BUAEventArgs{TState}.Response" />
    /// property will be set to <c>null</c> when this event is raised.  Applications can
    /// set this to the <see cref="SipResponse" /> to be sent back to the client by
    /// the B2BUA.  This is useful in situations where the application does not want
    /// to pass a client request on the server.  In this case, the application will
    /// set the argument's <see cref="SipB2BUAEventArgs{TState}.Request" />
    /// property to <c>null</c> so the B2BUA won't forward it and the
    /// <see cref="SipB2BUAEventArgs{TState}.Response" /> property to
    /// the <see cref="SipResponse" /> message (typically indicating some kind of error).
    /// </para>
    /// <para>
    /// Note that the event argument's <see cref="SipB2BUASession{TState}.ServerDialog" />
    /// property will be <c>null</c> since the dialog has not been fully established.  In this
    /// case the request received will always be an INVITE.  The event handler can modify
    /// the request as desired, but anything but an INVITE will be ignored by the B2BUA
    /// when the handler returns.  The application can reject the client INVITE by 
    /// setting the event argument's <see cref="SipB2BUAEventArgs{TState}.Request" />
    /// property to <c>null</c> and setting <see cref="SipB2BUAEventArgs{TState}.Response" />
    /// to the a <see cref="SipResponse" /> indicating the desired error.
    /// </para>
    /// <para>
    /// The event handler can also set the event argument's <see cref="SipB2BUAEventArgs{TState}.MaxRedirects" /> 
    /// property to a positive value to override the default maximum number of INVITE redirects to be followed 
    /// by the B2BUA when attempting to establish a session with a server.
    /// </para>
    /// <para>
    /// The event argument's <see cref="SipB2BUAEventArgs{TState}.ServerLocalContact" /> and
    /// <see cref="SipB2BUAEventArgs{TState}.ClientLocalContact" /> properties may also
    /// be modified bu this event handler.  When set to a non-<c>null</c> value, these properties
    /// specify the <b>Contact</b> header value to be used in SIP messages to the server
    /// and client, respectively.
    /// </para>
    /// <para><see cref="InviteResponseReceived" /></para>
    /// <para>
    /// This event is raised when an INVITE <see cref="SipResponse" /> is received from the
    /// server on the downstream dialog.  The default action by the B2BUA is to forward the
    /// response onto the server via the downstream dialog.  The response message is available via the 
    /// event argument's <see cref="SipB2BUAEventArgs{TState}.Response" /> 
    /// property.  The event handler may modify or replace the response message as desired.
    /// Setting the <see cref="SipB2BUAEventArgs{TState}.Response" /> property
    /// to <c>null</c> indicates to the B2BUA that it should not forward the response to
    /// the server.
    /// </para>
    /// <para><see cref="ClientRequestReceived" /></para>
    /// <para>
    /// This event is raised when a <see cref="SipRequest" /> (other than the initial INVITE)
    /// is received from the client on the upstream dialog.  The default action by the B2BUA is to forward the
    /// request onto the server via the downstream dialog.  The application's
    /// event handler can modify or replace the <see cref="SipB2BUAEventArgs{TState}.Request" /> 
    /// in the event arguments before returning and the B2BUA will send the modified 
    /// message.  The application can also set the request to <c>null</c> which indicates
    /// to the B2BUA that it should not forward the request.
    /// </para>
    /// <para>
    /// The event argument's <see cref="SipB2BUAEventArgs{TState}.Response" />
    /// property will be set to <c>null</c> when this event is raised.  Applications can
    /// set this to the <see cref="SipResponse" /> to be sent back to the client by
    /// the B2BUA.  This is useful in situations where the application does not want
    /// to pass a client request on the server.  In this case, the application will
    /// set the argument's <see cref="SipB2BUAEventArgs{TState}.Request" />
    /// property to <c>null</c> so the B2BUA won't forward it and the
    /// <see cref="SipB2BUAEventArgs{TState}.Response" /> property to
    /// the <see cref="SipResponse" /> message (typically indicating some kind of error).
    /// </para>
    /// <para><see cref="ClientResponseReceived" /></para>
    /// <para>
    /// This event is raised when a <see cref="SipResponse" /> is received from the
    /// client on the upstream dialog.  The default action by the B2BUA is to forward the
    /// response onto the server via the downstream dialog.  The response message is available via the 
    /// event argument's <see cref="SipB2BUAEventArgs{TState}.Response" /> 
    /// property.  The event handler may modify or replace the response message as desired.
    /// Setting the <see cref="SipB2BUAEventArgs{TState}.Response" /> property
    /// to <c>null</c> indicates to the B2BUA that it should not forward the response to
    /// the server.
    /// </para>
    /// <para><see cref="ServerRequestReceived" /></para>
    /// <para>
    /// This event is raised when a <see cref="SipRequest" /> is received from the server on the
    /// downstream dialog.  The default action by the B2BUA is to forward the
    /// request onto the client via the upstream dialog.  The application's
    /// event handler can modify or replace the <see cref="SipB2BUAEventArgs{TState}.Request" /> 
    /// in the event arguments before returning and the B2BUA will send the modified 
    /// message.  The application can also set the request to <c>null</c> which indicates
    /// to the B2BUA that it should not forward the request.
    /// </para>
    /// <para><see cref="ServerResponseReceived" /></para>
    /// <para>
    /// This event is raised when a <see cref="SipResponse" /> is received from the
    /// server on the downstream dialog.  The default action by the B2BUA is to forward the
    /// response onto the client via the upstream dialog.  The response message is available via the 
    /// event argument's <see cref="SipB2BUAEventArgs{TState}.Response" /> 
    /// property.  The event handler may modify or replace the response message as desired.
    /// Setting the <see cref="SipB2BUAEventArgs{TState}.Response" /> property
    /// to <c>null</c> indicates to the B2BUA that it should not forward the response to
    /// the client.
    /// </para>
    /// <para><see cref="SessionConfirmed" /></para>
    /// <para>
    /// Raised when a session has been confirmed when the client on the downstream
    /// dialog sends an ACK request to the server.  The event argument's 
    /// <see cref="SipB2BUAEventArgs{TState}.Request" /> property will be set to
    /// the ACK request.  The event handler use this event to modify the ACK request
    /// before it is forwarded on to the server and also as a chance to perform
    /// an necessary application state initialization.
    /// </para>
    /// <para><see cref="SessionClosing" /></para>
    /// <para>
    /// This event is raised just before the session is closed.  The application can
    /// take this as an opportunity to perform any required cleanup.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class SipB2BUserAgent<TState> : ILockable
    {
        //---------------------------------------------------------------------
        // Static members

        private const string AppExceptionError = "Internal application exception";

        static private long nextSessionID = -1; // Next available session ID

        //---------------------------------------------------------------------
        // Instance members

        private SipCore             core;                   // The associated core
        private bool                isRunning;              // True if the B2BUA is running
        private bool                isUsed;                 // True if the B2BUA has been started at some point
        private int                 maxRedirects;           // Default maximum number of INVITE redirects
        private SipRequestDelegate  onClientRequest;        // Handles requests received on the client dialog
        private SipRequestDelegate  onServerRequest;        // Handles requests received on the server dialog

        // The table of SipB2BUASession sessions keyed by session ID.

        private Dictionary<long, SipB2BUASession<TState>> sessions;

        /// <summary>
        /// Raised when the initial INVITE request is received from the client on the upstream
        /// dialog.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This event is raised when the initial INVITE <see cref="SipRequest" /> is received from the 
        /// client on the upstream dialog.  At this point, no downstream dialog has yet been established with
        /// the server.  The default action by the B2BUA is to forward the request onto the server via 
        /// the downstream dialog to establish an end-to-end dialog.  The application's
        /// event handler can modify or replace the <see cref="SipB2BUAEventArgs{TState}.Request" /> 
        /// in the event arguments before returning and the B2BUA will send the modified 
        /// message.  The application can also set the request to <c>null</c> which indicates
        /// to the B2BUA that it should not forward the request.
        /// </para>
        /// <para>
        /// The event argument's <see cref="SipB2BUAEventArgs{TState}.Response" />
        /// property will be set to <c>null</c> when this event is raised.  Applications can
        /// set this to the <see cref="SipResponse" /> to be sent back to the client by
        /// the B2BUA.  This is useful in situations where the application does not want
        /// to pass a client request on the server.  In this case, the application will
        /// set the argument's <see cref="SipB2BUAEventArgs{TState}.Request" />
        /// property to <c>null</c> so the B2BUA won't forward it and the
        /// <see cref="SipB2BUAEventArgs{TState}.Response" /> property to
        /// the <see cref="SipResponse" /> message (typically indicating some kind of error).
        /// </para>
        /// <para>
        /// Note that the event argument's <see cref="SipB2BUASession{TState}.ServerDialog" />
        /// property will be <c>null</c> since the dialog has not been fully established.  In this
        /// case the request received will always be an INVITE.  The event handler can modify
        /// the request as desired, but anything but an INVITE will be ignored by the B2BUA
        /// when the handler returns.  The application can reject the client INVITE by 
        /// setting the event argument's <see cref="SipB2BUAEventArgs{TState}.Request" />
        /// property to <c>null</c> and setting <see cref="SipB2BUAEventArgs{TState}.Response" />
        /// to the a <see cref="SipResponse" /> indicating the desired error.
        /// </para>
        /// <para>
        /// The event handler can also set the event argument's <see cref="SipB2BUAEventArgs{TState}.MaxRedirects" /> 
        /// property to a positive value to override the default maximum number of INVITE redirects to be followed 
        /// by the B2BUA when attempting to establish a session with a server.
        /// </para>
        /// <para>
        /// The event argument's <see cref="SipB2BUAEventArgs{TState}.ServerLocalContact" /> and
        /// <see cref="SipB2BUAEventArgs{TState}.ClientLocalContact" /> properties may also
        /// be modified bu this event handler.  When set to a non-<c>null</c> value, these properties
        /// specify the <b>Contact</b> header value to be used in SIP messages to the server
        /// and client, respectively.
        /// </para>
        /// </remarks>
        public event SipB2BUAEventDelegate<TState> InviteRequestReceived;

        /// <summary>
        /// Raised when the B2BUA receives an INVITE response on the downstream dialog from
        /// the server.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This event is raised when an INVITE <see cref="SipResponse" /> is received from the
        /// server on the downstream dialog.  The default action by the B2BUA is to forward the
        /// response onto the server via the downstream dialog.  The response message is available via the 
        /// event argument's <see cref="SipB2BUAEventArgs{TState}.Response" /> 
        /// property.  The event handler may modify or replace the response message as desired.
        /// Setting the <see cref="SipB2BUAEventArgs{TState}.Response" /> property
        /// to <c>null</c> indicates to the B2BUA that it should not forward the response to
        /// the server.
        /// </para>
        /// </remarks>
        public event SipB2BUAEventDelegate<TState> InviteResponseReceived;

        /// <summary>
        /// Raised when the B2BUA receives a request on the upstream dialog from
        /// the client.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This event is raised when a <see cref="SipRequest" /> (other than the initial INVITE)
        /// is received from the client on the upstream dialog.  The default action by the B2BUA is to forward the
        /// request onto the server via the downstream dialog.  The application's
        /// event handler can modify or replace the <see cref="SipB2BUAEventArgs{TState}.Request" /> 
        /// in the event arguments before returning and the B2BUA will send the modified 
        /// message.  The application can also set the request to <c>null</c> which indicates
        /// to the B2BUA that it should not forward the request.
        /// </para>
        /// <para>
        /// The event argument's <see cref="SipB2BUAEventArgs{TState}.Response" />
        /// property will be set to <c>null</c> when this event is raised.  Applications can
        /// set this to the <see cref="SipResponse" /> to be sent back to the client by
        /// the B2BUA.  This is useful in situations where the application does not want
        /// to pass a client request on the server.  In this case, the application will
        /// set the argument's <see cref="SipB2BUAEventArgs{TState}.Request" />
        /// property to <c>null</c> so the B2BUA won't forward it and the
        /// <see cref="SipB2BUAEventArgs{TState}.Response" /> property to
        /// the <see cref="SipResponse" /> message (typically indicating some kind of error).
        /// </para>
        /// </remarks>
        public event SipB2BUAEventDelegate<TState> ClientRequestReceived;

        /// <summary>
        /// Raised when the B2BUA receives a response on the upstream dialog from
        /// the client.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This event is raised when a <see cref="SipResponse" /> is received from the
        /// client on the upstream dialog.  The default action by the B2BUA is to forward the
        /// response onto the server via the downstream dialog.  The response message is available via the 
        /// event argument's <see cref="SipB2BUAEventArgs{TState}.Response" /> 
        /// property.  The event handler may modify or replace the response message as desired.
        /// Setting the <see cref="SipB2BUAEventArgs{TState}.Response" /> property
        /// to <c>null</c> indicates to the B2BUA that it should not forward the response to
        /// the server.
        /// </para>
        /// </remarks>
        public event SipB2BUAEventDelegate<TState> ClientResponseReceived;

        /// <summary>
        /// Raised when the B2BUA receives a request on the downstream dialog from
        /// the server.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This event is raised when a <see cref="SipRequest" /> is received from the server on the
        /// downstream dialog.  The default action by the B2BUA is to forward the
        /// request onto the client via the upstream dialog.  The application's
        /// event handler can modify or replace the <see cref="SipB2BUAEventArgs{TState}.Request" /> 
        /// in the event arguments before returning and the B2BUA will send the modified 
        /// message.  The application can also set the request to <c>null</c> which indicates
        /// to the B2BUA that it should not forward the request.
        /// </para>
        /// </remarks>
        public event SipB2BUAEventDelegate<TState> ServerRequestReceived;

        /// <summary>
        /// Raised when the B2BUA receives a response on the downstream dialog from
        /// the server.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This event is raised when a <see cref="SipResponse" /> is received from the
        /// server on the downstream dialog.  The default action by the B2BUA is to forward the
        /// response onto the client via the upstream dialog.  The response message is available via the 
        /// event argument's <see cref="SipB2BUAEventArgs{TState}.Response" /> 
        /// property.  The event handler may modify or replace the response message as desired.
        /// Setting the <see cref="SipB2BUAEventArgs{TState}.Response" /> property
        /// to <c>null</c> indicates to the B2BUA that it should not forward the response to
        /// the client.
        /// </para>
        /// </remarks>
        public event SipB2BUAEventDelegate<TState> ServerResponseReceived;

        /// <summary>
        /// Raised when a session has been confirmed when the client on the downstream
        /// dialog sends an ACK request to the server.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Raised when a session has been confirmed when the client on the downstream
        /// dialog sends an ACK request to the server.  The event argument's 
        /// <see cref="SipB2BUAEventArgs{TState}.Request" /> property will be set to
        /// the ACK request.  The event handler use this event to modify the ACK request
        /// before it is forwarded on to the server and also as a chance to perform
        /// an necessary application state initialization.
        /// </para>
        /// </remarks>
        public event SipB2BUAEventDelegate<TState> SessionConfirmed;

        /// <summary>
        /// Raised just before the B2BUA closes the session.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This event is raised just before the session is closed.  The application can
        /// take this as an opportunity to perform any required cleanup.
        /// </para>
        /// </remarks>
        public event SipB2BUAEventDelegate<TState> SessionClosing;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="core">The associated <see cref="SipCore" />.</param>
        public SipB2BUserAgent(SipCore core)
        {
            this.core            = core;
            this.isRunning       = false;
            this.isUsed          = false;
            this.maxRedirects    = 5;
            this.sessions        = new Dictionary<long, SipB2BUASession<TState>>();
            this.onClientRequest = new SipRequestDelegate(OnClientRequest);
            this.onServerRequest = new SipRequestDelegate(OnServerRequest);

            if (core.IsRunning)
                OnCoreStarting(core, new EventArgs());
            else
            {
                // We want to be notified when the core is started to perform
                // some additional initialization.

                core.Starting += new EventHandler(OnCoreStarting);
            }
        }

        /// <summary>
        /// Starts the B2BUA.
        /// </summary>
        /// <remarks>
        /// <note><see cref="SipB2BUserAgent{TState}" /> instances cannot be restarted once they have been stopped.</note>
        /// </remarks>
        public virtual void Start()
        {
            using (TimedLock.Lock(this))
            {
                if (isRunning)
                    throw new InvalidOperationException("B2BUA is already running.");

                if (isUsed)
                    throw new InvalidOperationException("Cannot restart a B2BUA.");

                isRunning = true;
                isUsed    = true;

                core.CreateServerDialogEvent += new SipCreateDialogDelegate(OnCreateServerDialogEvent);
                core.CreateClientDialogEvent += new SipCreateDialogDelegate(OnCreateClientDialogEvent);
                core.RequestReceived         += new SipRequestDelegate(OnRequestReceived);
                core.ResponseReceived        += new SipResponseDelegate(OnResponseReceived);
                core.DialogCreated           += new SipDialogDelegate(OnDialogCreated);
                core.DialogConfirmed         += new SipDialogDelegate(OnDialogConfirmed);
                core.DialogClosed            += new SipDialogDelegate(OnDialogClosed);
            }
        }

        /// <summary>
        /// Stops the B2BUA if it is currently running.
        /// </summary>
        /// <remarks>
        /// <note><see cref="SipB2BUserAgent{TState}" /> instances cannot be restarted once they've been stopped.</note>
        /// </remarks>
        public virtual void Stop()
        {
            // Mark the instance as closed.

            isRunning = false;

            // Get a current list of the sessions and then raise
            // the application session closed event handler for
            // each session.

            if (SessionClosing != null)
            {
                List<SipB2BUASession<TState>> closeList;

                using (TimedLock.Lock(this))
                {
                    closeList = new List<SipB2BUASession<TState>>(sessions.Count);
                    foreach (SipB2BUASession<TState> session in sessions.Values)
                        closeList.Add(session);
                }

                // Raise the events outside of the lock

                for (int i = 0; i < closeList.Count; i++)
                    SessionClosing(this, new SipB2BUAEventArgs<TState>(core, this, closeList[i]));
            }
        }

        /// <summary>
        /// Returns the synchronization object to be used to protect internal
        /// member state, particularily the sessions table.
        /// </summary>
        public object SyncRoot
        {
            get { return this; }
        }

        /// <summary>
        /// Returns the collection of current <see cref="SipB2BUASession{TState}" />s.
        /// Be sure protect this by locking <see cref="SyncRoot" /> while modifying
        /// or enumerating this collection.
        /// </summary>
        public Dictionary<long, SipB2BUASession<TState>> Sessions
        {
            get { return sessions; }
        }

        /// <summary>
        /// Returns the associated <see cref="SipCore" />.
        /// </summary>
        public SipCore Core
        {
            get { return core; }
        }

        /// <summary>
        /// Returns the number of sessions currently managed by the B2BUA.
        /// </summary>
        public int SessionCount
        {
            get
            {
                using (TimedLock.Lock(this))
                {
                    if (!isRunning)
                        return 0;

                    return sessions.Count;
                }
            }
        }

        /// <summary>
        /// Logs a warning or error from an event handler.
        /// </summary>
        /// <param name="error">Pass <c>true</c> to log an error, <c>false</c> to lag a warning.</param>
        /// <param name="handlers">The event's delegates.</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message arguments.</param>
        private void LogEventHandlerProblem(bool error, Delegate[] handlers, string format, params object[] args)
        {
            var sb = new StringBuilder(512);
            var stack = new CallStack(1, true);

            sb.AppendFormat(format, args);
            sb.Append("\r\n\r\n");

            sb.AppendLine("Event Handlers:");
            foreach (SipB2BUAEventDelegate<TState> handler in handlers)
                sb.AppendFormat("    {0}.{1}()\r\n", handler.Method.ReflectedType.FullName, handler.Method.Name);

            sb.AppendLine();
            sb.AppendLine("Call Stack");
            stack.Dump(sb);

            if (error)
                SysLog.LogError("{0}", sb.ToString());
            else
                SysLog.LogWarning("{0}", sb.ToString());
        }

        /// <summary>
        /// Raises the <see cref="ServerResponseReceived" /> event with the response just received
        /// from the server, giving the application a chance to modify the response before it is actually
        /// transmitted back to the client.
        /// </summary>
        /// <param name="session">The B2BUA session.</param>
        /// <param name="receivedResponse">The SIP response from the server.</param>
        /// <param name="defaultResponse">The default generated response to be passed back to the client.</param>
        /// <param name="closeSession">Returns as <c>true</c> if the handler indicates that the session should be closed.</param>
        /// <returns>The response to be delivered to the client.</returns>
        private SipResponse FilterResponse(SipB2BUASession<TState> session, SipResponse receivedResponse, SipResponse defaultResponse, out bool closeSession)
        {
            closeSession = false;

            if (ServerResponseReceived != null)
            {
                var b2bArgs = new SipB2BUAEventArgs<TState>(core, this, session, receivedResponse, defaultResponse);

                ServerResponseReceived(this, b2bArgs);
                closeSession = b2bArgs.CloseSession;

                if (b2bArgs.Response == null)
                    LogEventHandlerProblem(false, ServerResponseReceived.GetInvocationList(),
                                           "ServerResponseHandler returned [Response=null]. " +
                                           "The original response will be delivered to the client.");
                else
                    defaultResponse = b2bArgs.Response;
            }

            return defaultResponse;
        }

        /// <summary>
        /// Raises the <see cref="InviteResponseReceived" /> event with the INVITE response just received
        /// from the server, giving the application a chance to modify the response before it is actually
        /// transmitted back to the client.
        /// </summary>
        /// <param name="session">The B2BUA session.</param>
        /// <param name="receivedResponse">The SIP response from the server.</param>
        /// <param name="defaultResponse">The default generated response to be passed back to the client.</param>
        /// <param name="closeSession">Returns as <c>true</c> if the handler indicates that the session should be closed.</param>
        /// <returns>The response to be delivered to the client.</returns>
        private SipResponse FilterInviteResponse(SipB2BUASession<TState> session, SipResponse receivedResponse, SipResponse defaultResponse, out bool closeSession)
        {
            closeSession = false;

            if (InviteResponseReceived != null)
            {
                var b2bArgs = new SipB2BUAEventArgs<TState>(core, this, session, receivedResponse, defaultResponse);

                InviteResponseReceived(this, b2bArgs);
                closeSession = b2bArgs.CloseSession;

                if (b2bArgs.Response == null)
                    LogEventHandlerProblem(false, InviteResponseReceived.GetInvocationList(),
                                           "InviteResponseReceived returned [Response=null]. " +
                                           "The original response will be delivered to the client.");
                else
                    defaultResponse = b2bArgs.Response;
            }

            return defaultResponse;
        }

        /// <summary>
        /// Establishes the dialog with the server.
        /// </summary>
        /// <param name="args">The dialog creation event arguments.</param>
        /// <param name="b2bArgs">The B2BUA event arguments.</param>
        private void InviteServer(SipDialogEventArgs args, SipB2BUAEventArgs<TState> b2bArgs)
        {
            // $todo(jeff.lill): 
            //
            // This is currently implemented synchronously.  This needs to be
            // modified to be completely asynchronous for scalability.

            SipServerTransaction    transaction = (SipServerTransaction)args.Transaction;
            SipB2BUASession<TState> session = b2bArgs.Session;
            SipRequest              orgInvite = b2bArgs.Request;
            SipContactValue         vTo;
            SipContactValue         vFrom;
            SipB2BUADialog<TState>  serverDialog;
            SipRequest              inviteRequest;
            SipUri                  serverUri;
            int                     maxRedirects;
            SipContactValue         defLocalContact;
            SipResponse             response;
            bool                    closeSession;

            Assertion.Test(b2bArgs.Request.Method == SipMethod.Invite);

            orgInvite       = b2bArgs.Request;
            serverUri       = (SipUri)orgInvite.Uri;
            maxRedirects    = b2bArgs.MaxRedirects > 0 ? b2bArgs.MaxRedirects : this.maxRedirects;
            defLocalContact = core.GetLocalContact(args.Transaction);

            session.ClientLocalContact            = b2bArgs.ClientLocalContact != null ? b2bArgs.ClientLocalContact : defLocalContact;
            session.ServerLocalContact            = b2bArgs.ServerLocalContact != null ? b2bArgs.ServerLocalContact : defLocalContact;
            session.ClientDialog.RequestReceived += onClientRequest;

            vTo          = orgInvite.GetHeader<SipContactValue>(SipHeader.To);
            vTo["tag"]   = null;

            vFrom        = orgInvite.GetHeader<SipContactValue>(SipHeader.From);
            vFrom["tag"] = null;

            orgInvite.SetHeader(SipHeader.Contact, session.ServerLocalContact);

            for (int i = 0; i < maxRedirects; i++)
            {
                inviteRequest     = orgInvite.Clone();
                inviteRequest.Uri = (string)serverUri;
                inviteRequest.SetHeader(SipHeader.To, vTo);
                inviteRequest.SetHeader(SipHeader.From, vFrom);

                serverDialog = (SipB2BUADialog<TState>)core.CreateDialog(inviteRequest, session.ServerLocalContact, session);
                if (SipHelper.IsSuccess(serverDialog.InviteStatus))
                {
                    session.ServerDialog          = serverDialog;
                    serverDialog.RequestReceived += onServerRequest;

                    response = GenerateDefaultResponse(args.ClientRequest, serverDialog.InviteResponse);
                    response.SetHeader(SipHeader.Contact, session.ClientLocalContact);

                    response = FilterInviteResponse(session, serverDialog.InviteResponse, response, out closeSession);
                    transaction.SendResponse(response);

                    if (closeSession)
                        CloseSession(session);

                    return;
                }
                else if (serverDialog.InviteStatus == SipStatus.MovedPermanently ||
                         serverDialog.InviteStatus == SipStatus.MovedTemporarily)
                {
                    var vContact = serverDialog.InviteResponse.GetHeader<SipContactValue>(SipHeader.Contact);

                    if (vContact == null)
                    {
                        response = args.ClientRequest.CreateResponse(SipStatus.ServerError, "Server moved but did not specify a [Contact] header.");
                        response = FilterInviteResponse(session, response, response, out closeSession);
                        transaction.SendResponse(response);
                        serverDialog.Close();
                        return;
                    }

                    SipUri toUri = (SipUri)vTo.Uri;

                    serverUri = (SipUri)vContact.Uri;
                    if (toUri.User != null)
                        serverUri.User = toUri.User;
                }
                else if (SipHelper.IsError(serverDialog.InviteStatus))
                {
                    if (serverDialog.InviteResponse != null)
                        response = serverDialog.InviteResponse;
                    else
                        response = orgInvite.CreateResponse(serverDialog.InviteStatus, null);

                    response = FilterInviteResponse(session, response, GenerateDefaultResponse(args.ClientRequest, response), out closeSession);
                    transaction.SendResponse(response);

                    CloseSession(session);
                    return;
                }
            }

            response = args.ClientRequest.CreateResponse(SipStatus.ServerError, "Too many server INVITE redirects.");
            response = FilterInviteResponse(session, response, GenerateDefaultResponse(args.ClientRequest, response), out closeSession);
            transaction.SendResponse(response);
            CloseSession(session);
        }

        /// <summary>
        /// Closes a B2BUA session.
        /// </summary>
        /// <param name="session">The <see cref="SipB2BUASession{TState}" /> to be closed.</param>
        public void CloseSession(SipB2BUASession<TState> session)
        {
            // $todo(jeff.lill): 
            //
            // I probably need to issue CANCEL requests here
            //in some situations.

            SipB2BUADialog<TState>  serverDialog;
            SipB2BUADialog<TState>  clientDialog;

            if (session == null)
                return;

            using (TimedLock.Lock(this))
            {
                if (sessions != null && sessions.ContainsKey(session.ID))
                    sessions.Remove(session.ID);

                serverDialog = session.ServerDialog;
                clientDialog = session.ClientDialog;
            }

            if (serverDialog != null)
                serverDialog.Close();

            if (clientDialog != null)
                clientDialog.Close();
        }

        /// <summary>
        /// Generates the default SIP request to be forwarded by the B2BUA from a
        /// request received by the B2BUA.
        /// </summary>
        /// <param name="receivedRequest">The received request.</param>
        /// <returns>The default request to be forwarded.</returns>
        private SipRequest GenerateDefaultRequest(SipRequest receivedRequest)
        {
            // The idea here is to clone the received request and then modify the 
            // headers as necessary so that the request can be forwarded to
            // the remote peer on the backing dialog.

            SipRequest  request;
            SipValue    v;

            request = receivedRequest.Clone();

            // Remove all dialog related headers and parameters so that
            // the proper values will be added back in when the message
            // is forwarded to the remote peer.

            request.RemoveHeader(SipHeader.Via);
            request.RemoveHeader(SipHeader.CallID);
            request.RemoveHeader(SipHeader.Contact);

            // Remove the "To" header's "tag" parameter if present.

            v = request.GetHeader<SipValue>(SipHeader.To);
            if (v != null && v.Parameters.ContainsKey("tag"))
                v.Parameters.Remove("tag");

            request.SetHeader(SipHeader.To, (string)v);

            // Remove the "From" header's "tag" parameter if present.

            v = request.GetHeader<SipValue>(SipHeader.From);
            if (v != null && v.Parameters.ContainsKey("tag"))
                v.Parameters.Remove("tag");

            request.SetHeader(SipHeader.From, (string)v);

            return request;
        }

        /// <summary>
        /// Generates the default SIP response to be forwarded by the B2BUA from a
        /// response received by the B2BUA.
        /// </summary>
        /// <param name="originalRequest">The original received <see cref="SipRequest" />.</param>
        /// <param name="receivedResponse">The received response.</param>
        /// <returns>The default response to be forwarded or <c>null</c> if the response should be ignored.</returns>
        private SipResponse GenerateDefaultResponse(SipRequest originalRequest, SipResponse receivedResponse)
        {
            var response = receivedResponse.Clone();

            response.SetHeader(SipHeader.CSeq, originalRequest.GetHeader(SipHeader.CSeq));
            response.SetHeader(SipHeader.Via, originalRequest.GetHeader(SipHeader.Via));
            response.SetHeader(SipHeader.CallID, originalRequest.GetHeader(SipHeader.CallID));
            response.SetHeader(SipHeader.To, originalRequest.GetHeader(SipHeader.To));
            response.SetHeader(SipHeader.From, originalRequest.GetHeader(SipHeader.From));

            return response;
        }

        //---------------------------------------------------------------------
        // Event handlers

        /// <summary>
        /// Handles <see cref="SipCore" />.<see cref="SipCore.Starting" /> events.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnCoreStarting(object sender, EventArgs args)
        {
            // NOP
        }

        /// <summary>
        /// Handles <see cref="SipCore" />.<see cref="SipCore.CreateServerDialogEvent" /> events.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnCreateServerDialogEvent(object sender, SipCreateDialogArgs args)
        {
            args.Dialog = new SipB2BUADialog<TState>();
        }

        /// <summary>
        /// Handles <see cref="SipCore" />.<see cref="SipCore.CreateClientDialogEvent" /> events.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnCreateClientDialogEvent(object sender, SipCreateDialogArgs args)
        {
            args.Dialog = new SipB2BUADialog<TState>();
        }

        /// <summary>
        /// Handles core <see cref="SipDialog.RequestReceived" /> events.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnRequestReceived(object sender, SipRequestEventArgs args)
        {
            if (args.Dialog != null)
                return;

            var request = args.Request;

            if (request.Method == SipMethod.Cancel)
            {
                // $todo(jeff.lill): 
                //
                // I'm going to hack CANCEL for now just to get this working
                // for a HQ demo.  I need to come back and do a complete
                // implementation of the RFC cancellation behaviors.

                args.Transaction.SendResponse(request.CreateResponse(SipStatus.OK, null));
                return;
            }
        }

        /// <summary>
        /// Handles core <see cref="SipDialog.ResponseReceived" /> events.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnResponseReceived(object sender, SipResponseEventArgs args)
        {
            // Pass provisional responses other than 100 (Trying) received from 
            // the server on the downstream dialog back to the client on the
            // upstream dialog if the downstream dialog is still in the early state.

            SipB2BUADialog<TState>      dialog           = (SipB2BUADialog<TState>)args.Dialog;
            SipResponse                 receivedResponse = args.Response;
            SipResponse                 response;
            SipB2BUASession<TState>     session;
            SipB2BUAEventArgs<TState>   b2bArgs;

            if (receivedResponse.Status == SipStatus.Trying || !receivedResponse.IsProvisional || dialog == null || !dialog.ToServer)
                return;

            // Munge the provisional response received from the server so that
            // it is suitable for transmission to the client.

            session  = dialog.Session;
            response = GenerateDefaultResponse(session.ClientDialog.InviteRequest, receivedResponse);

            // Raise the ServerResponseReceived event so that the application can
            // modify or act on the provisional response.

            if (ServerResponseReceived != null)
            {
                try
                {
                    b2bArgs = new SipB2BUAEventArgs<TState>(core, this, session, receivedResponse, response);
                    ServerResponseReceived(this, b2bArgs);

                    if (b2bArgs.CloseSession)
                    {
                        CloseSession(session);
                        return;
                    }

                    response = b2bArgs.Response;
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }

            // Forward the response to client, if the application's event handler
            // didn't set it to null.

            if (response != null)
                session.ClientDialog.AcceptingTransaction.SendResponse(response);
        }

        /// <summary>
        /// Handles requests received on the upstream dialog from the client.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnClientRequest(object sender, SipRequestEventArgs args)
        {
            // $todo(jeff.lill): At some point this shouldn't be handled synchronously.

            SipB2BUADialog<TState>      dialog          = (SipB2BUADialog<TState>)args.Dialog;
            SipB2BUASession<TState>     session         = dialog.Session;
            SipRequest                  requestReceived = args.Request.Clone();
            SipRequest                  request         = args.Request;
            SipB2BUAEventArgs<TState>   b2bArgs;

            if (ClientRequestReceived != null)
            {
                try
                {
                    b2bArgs = new SipB2BUAEventArgs<TState>(core, this, session, args.Request, GenerateDefaultRequest(requestReceived));
                    ClientRequestReceived(this, b2bArgs);

                    if (b2bArgs.CloseSession)
                    {
                        CloseSession(session);
                        return;
                    }

                    if (b2bArgs.Response != null)
                    {
                        args.Response = b2bArgs.Response;
                        return;
                    }

                    if (b2bArgs.Request != null)
                        request = b2bArgs.Request;
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }
            else
                request = GenerateDefaultRequest(requestReceived);

            SipResult       result;
            SipResponse     receivedResponse;

            result = session.ServerDialog.Request(request);
            if (result.Response == null)
                receivedResponse = request.CreateResponse(result.Status, null);
            else
                receivedResponse = result.Response;

            args.Response = GenerateDefaultResponse(requestReceived, receivedResponse);
            if (ServerResponseReceived != null)
            {
                try
                {
                    b2bArgs = new SipB2BUAEventArgs<TState>(core, this, session, receivedResponse, args.Response);
                    ServerResponseReceived(this, b2bArgs);

                    if (b2bArgs.CloseSession)
                    {
                        CloseSession(session);
                        return;
                    }

                    args.Response = b2bArgs.Response;
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }
        }

        /// <summary>
        /// Handles requests received on the downstream dialog from the server.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnServerRequest(object sender, SipRequestEventArgs args)
        {
            // $todo(jeff.lill): At some point this shouldn't be handled synchronously.

            SipB2BUADialog<TState>      dialog          = (SipB2BUADialog<TState>)args.Dialog;
            SipB2BUASession<TState>     session         = dialog.Session;
            SipRequest                  requestReceived = args.Request.Clone();
            SipRequest                  request         = args.Request;
            SipB2BUAEventArgs<TState>   b2bArgs;

            if (ServerRequestReceived != null)
            {
                try
                {
                    b2bArgs = new SipB2BUAEventArgs<TState>(core, this, session, args.Request, GenerateDefaultRequest(requestReceived));
                    ServerRequestReceived(this, b2bArgs);

                    if (b2bArgs.CloseSession)
                    {
                        CloseSession(session);
                        return;
                    }

                    if (b2bArgs.Response != null)
                    {
                        args.Response = b2bArgs.Response;
                        return;
                    }

                    if (b2bArgs.Request != null)
                        request = b2bArgs.Request;
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }
            else
                request = GenerateDefaultRequest(requestReceived);

            SipResult       result;
            SipResponse     receivedResponse;

            result = session.ClientDialog.Request(request);
            if (result.Response == null)
                receivedResponse = request.CreateResponse(result.Status, null);
            else
                receivedResponse = result.Response;

            args.Response = GenerateDefaultResponse(requestReceived, receivedResponse);
            if (ClientResponseReceived != null)
            {
                try
                {
                    b2bArgs = new SipB2BUAEventArgs<TState>(core, this, session, receivedResponse, args.Response);
                    ClientResponseReceived(this, b2bArgs);

                    if (b2bArgs.CloseSession)
                    {
                        CloseSession(session);
                        return;
                    }

                    args.Response = b2bArgs.Response;
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }
        }

        /// <summary>
        /// Handles core <see cref="SipCore.DialogCreated" /> events.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnDialogCreated(object sender, SipDialogEventArgs args)
        {
            var dialog = (SipB2BUADialog<TState>)args.Dialog;

            if (dialog.ToServer)
                return;

            try
            {
                SipB2BUAEventArgs<TState>   b2bArgs;
                SipB2BUASession<TState>     session;
                SipRequest                  inviteRequest = args.ClientRequest;
                SipServerTransaction        transaction   = (SipServerTransaction)args.Transaction;
                SipContactValue             vTo;
                SipContactValue             vFrom;

                // Validate the INVITE request

                vTo = inviteRequest.GetHeader(SipHeader.To);
                if (vTo == null)
                {
                    transaction.SendResponse(inviteRequest.CreateResponse(SipStatus.BadRequest, "Missing [To] header."));
                    return;
                }

                vFrom = inviteRequest.GetHeader(SipHeader.From);
                if (vFrom == null)
                {
                    transaction.SendResponse(inviteRequest.CreateResponse(SipStatus.BadRequest, "Missing [From] header."));
                    return;
                }

                // Create the session and fixup references between it and the client dialog.

                session = new SipB2BUASession<TState>(Interlocked.Increment(ref nextSessionID),
                                                      (SipB2BUADialog<TState>)args.Dialog,
                                                      core.GetLocalContact(args.Transaction));
                session.ClientDialog.Session = session;

                // Initialize the event handler arguments

                b2bArgs = new SipB2BUAEventArgs<TState>(core, this, session, inviteRequest, GenerateDefaultRequest(inviteRequest));

                // Give the handler a chance to modify the request and/or response.

                if (InviteRequestReceived != null)
                {
                    try
                    {
                        InviteRequestReceived(this, b2bArgs);

                        if (b2bArgs.CloseSession)
                        {
                            CloseSession(session);
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        // The handler threw an exception.  Log it and abort the dialog.

                        SysLog.LogException(e);

                        b2bArgs.Request = null;
                        b2bArgs.Response = inviteRequest.CreateResponse(SipStatus.ServerError, AppExceptionError);
                        return;
                    }
                }

                // Forward the INVITE or abort the dialog using with the response 
                // specified by the event handler.

                if (b2bArgs.Request == null && b2bArgs.Response == null)
                {
                    LogEventHandlerProblem(true, InviteRequestReceived.GetInvocationList(),
                                           "InviteRequest handler must specify a valid [Response] if [Request] returns as null.");

                    b2bArgs.Response = inviteRequest.CreateResponse(SipStatus.ServerError, AppExceptionError);
                    return;
                }

                if (b2bArgs.Request != null)
                {
                    if (b2bArgs.Request.Method != SipMethod.Invite)
                    {
                        LogEventHandlerProblem(false, InviteRequestReceived.GetInvocationList(),
                                               "InviteRequest handler return with [Request.Method={0}]. " +
                                               "Only INVITE requests are valid. The request returned will be " +
                                               "ignored and the orginal request is being forwarded to the server.", b2bArgs.Request.Method);

                        b2bArgs.Request = GenerateDefaultRequest(inviteRequest);
                    }

                    InviteServer(args, b2bArgs);
                }
                else
                {
                    // Return the response

                    transaction.SendResponse(b2bArgs.Response);
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Handles core <see cref="SipCore.DialogConfirmed" /> events.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnDialogConfirmed(object sender, SipDialogEventArgs args)
        {
            var dialog = (SipB2BUADialog<TState>)args.Dialog;

            if (dialog.ToServer)
                return;

            SipB2BUASession<TState>     session    = dialog.Session;
            SipRequest                  ackRequest = GenerateDefaultRequest(args.ClientRequest);
            SipB2BUAEventArgs<TState>   b2bArgs    = new SipB2BUAEventArgs<TState>(core, this, session, args.ClientRequest, ackRequest);

            if (SessionConfirmed != null)
            {
                SessionConfirmed(this, b2bArgs);
                if (b2bArgs.CloseSession)
                {
                    CloseSession(session);
                    return;
                }
            }

            if (b2bArgs.Request == null)
            {
                LogEventHandlerProblem(false, SessionConfirmed.GetInvocationList(),
                                       "SessionConfirmed handler return with [Request=null]. " +
                                       "The request returned will be ignored and the orginal " +
                                       "request is being forwarded to the server.");

                ackRequest = GenerateDefaultRequest(args.ClientRequest);
            }
            else if (b2bArgs.Request.Method != SipMethod.Ack)
            {
                LogEventHandlerProblem(false, SessionConfirmed.GetInvocationList(),
                                       "SessionConfirmed handler return with [Request.Method={0}]. " +
                                       "Only ACK requests are valid. The request returned will be " +
                                       "ignored and the orginal request is being forwarded to the server.", b2bArgs.Request.Method);

                ackRequest = GenerateDefaultRequest(args.ClientRequest);
            }
            else
                ackRequest = b2bArgs.Request;

            session.ServerDialog.SendAckRequest(ackRequest);
        }

        /// <summary>
        /// Handles core <see cref="SipCore.DialogClosed" /> events.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnDialogClosed(object sender, SipDialogEventArgs args)
        {
            SipB2BUADialog<TState>      dialog = (SipB2BUADialog<TState>)args.Dialog;
            SipB2BUASession<TState>     session = dialog.Session;
            SipB2BUAEventArgs<TState>   b2bArgs;

            if (session == null)
                return;

            b2bArgs = new SipB2BUAEventArgs<TState>(core, this, session);

            if (SessionClosing != null)
            {
                try
                {
                    SessionClosing(this, b2bArgs);
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }

            if (dialog.ToServer)
            {
                session.ClientDialog.Close();
            }
            else
            {
                if (session.ServerDialog != null)
                    session.ServerDialog.Close();
            }

            using (TimedLock.Lock(this))
            {
                if (sessions != null && sessions.ContainsKey(session.ID))
                    sessions.Remove(session.ID);
            }
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better locking diagnostics.
        /// </summary>
        /// <returns>A lock key to be used to identify this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
