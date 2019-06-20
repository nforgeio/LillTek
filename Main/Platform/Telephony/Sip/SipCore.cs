//-----------------------------------------------------------------------------
// FILE:        SipCore.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the common behavior for the SIP cores defined
//              within the LillTek SIP stack.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;

using LillTek.Common;

// $todo(jeff.lill): Implement a mechanism to purge orphaned early dialogs

// $todo(jeff.lill): 
//
// SipCore only handles a single registration URI right now.
// It needs to support multiple URIs so that SipMssGateway
// will work with multiple registrations.

// $todo(jeff.lill): 
//
// I need to figure out a way to detect orphaned dialogs
// and delete them.  This will probably be a combination
// of RFC 4028's session timers and a hard dialog lifetime
// limit.

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Defines the common behaviors of a SIP core.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SIP Cores are a higher level layer in the protocol stack than are
    /// SIP Agents.  The main concept implemented by SIP agents is that of a
    /// <i>transaction</i> (the request/response pattern for non-INVITE operations
    /// and the INVITE/response/ACK pattern for INVITE).  The LillTek SIP
    /// stack implements two agents: <see cref="SipClientAgent" /> and
    /// <see cref="SipServerAgent" />, each responsible for handling one
    /// side of a SIP transaction.
    /// </para>
    /// <para>
    /// SIP cores implement the concept of a <i>dialog</i>.  A dialog is a
    /// relationship between two or more SIP endpoints that encapsulates multiple
    /// transactions, issued in either direction.  For example, an instant
    /// messaging session between two end users is a dialog.  The dialog is
    /// established via an INVITE transaction and then users can send messages
    /// to each other in the context of this dialog.
    /// </para>
    /// <para>
    /// A SIP core is an entity that sends and receives <see cref="SipMessage" />s
    /// to <see cref="ISipTransport" />s, performing any processing necessary.
    /// This concept is defined on page 19 of RFC 3261.  Examples of SIP cores
    /// include end-user oriented SIP endpoints, server oriented SIP endpoints,
    /// as well as stateless and stateful proxies.
    /// </para>
    /// <para>
    /// This class implements functions useful to the typical SIP core, including
    /// initializing and starting the SIP transports, and managing background
    /// task timers.
    /// </para>
    /// <para>
    /// Use the constructor <see cref="SipCore(SipCoreSettings)" /> to create
    /// an agent using explict settings.  Note that this method initializes the 
    /// cores's transports but doesn't start them.  You'll need to call <see cref="Start" />
    /// to do this.
    /// </para>
    /// <note>
    /// The derived class must call the <see cref="SetRouter" /> method and add its
    /// <see cref="ISipAgent" />s to the <see cref="Agents" /> collection before 
    /// calling <see cref="Start" />.
    /// </note>
    /// <para>
    /// Call <see cref="Stop" /> to gracefully shut down the core and its transports.
    /// Note that a core should not be reused.
    /// </para>
    /// <para><b><u>Submitting Requests Outside of a Dialog</u></b></para>
    /// <para>
    /// To submit a SIP request synchronously, call <see cref="Request" />.  The asynchronous methods
    /// <see cref="BeginRequest(SipRequest,AsyncCallback,object)" /> and <see cref="EndRequest" />.
    /// are also available.  These operations ultimately return a <see cref="SipResult" />
    /// instance that includes the operation <see cref="SipStatus" /> and the <see cref="SipResponse" />.
    /// </para>
    /// <para>
    /// If the core's <see cref="SipCoreSettings.AutoAuthenticate" /> setting is set to <c>true</c> 
    /// then the request methods will transparently handle any <see cref="SipStatus.Unauthorized" />
    /// and <see cref="SipStatus.ProxyAuthenticationRequired" /> challenges by computing the authentication
    /// response from the user ID and password from the core's <see cref="SipCoreSettings" /> and
    /// resubmitting the request.
    /// </para>
    /// <para><b><u>Registration Support</u></b></para>
    /// <para>
    /// Many applications require that a registration be active with a SIP registrar so
    /// that the application can be discovered by other applications.  These registrations
    /// must be periodically refreshed and RFC 3261 describes rules for how to accomplish
    /// this.  <see cref="SipCore" /> includes built-in support for registration.
    /// </para>
    /// <para>
    /// A one time registration request can be submitted synchronously via 
    /// <see cref="Register(string,string,TimeSpan)" /> or asynchronously via
    /// <see cref="BeginRegister(string,string,TimeSpan,AsyncCallback,object)" /> and 
    /// <see cref="EndRegister" />.  These methods will handle authentication internally
    /// if <see cref="SipCoreSettings.AutoAuthenticate" /> is <c>true</c>.
    /// </para>
    /// <para>
    /// <see cref="SipCore" /> also implements persistent registrations by resubmitting
    /// REGISTER requests on an internal thread.  Call <see cref="Register(string,string,TimeSpan)" />
    /// to establish the 
    /// </para>
    /// <para><b><u>Implementing a Custom Derived <see cref="SipCore" /></u></b></para>
    /// <para>
    /// The core receives and processes <see cref="SipRequest" />s sent by clients by  
    /// implementing the <see cref="OnRequestReceived" /> method.  This method is called
    /// a transaction is initiated to handle a received request.  The arguments include a reference
    /// to the request and the <see cref="SipServerTransaction" /> and is passed in
    /// a <see cref="SipRequestEventArgs" /> parameter.  The core can choose the handle
    /// the request immediately be sending one or more responses via the transaction's
    /// <see cref="SipServerTransaction.SendResponse" /> method or it can save a reference
    /// to the transaction, return and then complete the operation on another thread.
    /// </para>
    /// <note>
    /// A final response must eventually be passed to the transaction to avoid memory leaks.
    /// </note>
    /// <para>
    /// <see cref="SipCore" /> defines serveral virtual methods that will be called 
    /// by other components as certain events happen, including: receiving SIP
    /// requests or responses, dialog creation and distruction, etc.  <see cref="SipCore" />
    /// provides base implementations that will well for most applications, typically
    /// resulting in the firing of a <see cref="RequestReceived" />, <see cref="ResponseReceived" />,
    /// <see cref="DialogCreated" />, <see cref="DialogConfirmed" />, or <see cref="DialogClosed" /> 
    /// event.
    /// </para>
    /// <para>
    /// The base implementation of these handlers will be appropriate for SIP application
    /// endpoints.  Certain SIP applications such as proxies will need to override
    /// <see cref="OnResponseReceived" />, <see cref="OnInviteReceived" />, <see cref="OnInviteConfirmed(SipRequestEventArgs)" />,
    /// <see cref="OnInviteConfirmed(SipResponseEventArgs)" /> <see cref="OnInviteFailed(SipRequestEventArgs,SipStatus)" />, 
    /// <see cref="OnInviteFailed(SipResponseEventArgs,SipStatus)" />, and <see cref="OnUncorrelatedResponse" /> with 
    /// custom implementations.
    /// </para>
    /// <para>
    /// The <see cref="OnInviteReceived" />, <see cref="OnInviteConfirmed(SipRequestEventArgs)" /> and 
    /// <see cref="OnInviteFailed(SipRequestEventArgs,SipStatus)" /> methods will be called indicate the progress 
    /// of the creation of a dialog with a client. Dialogs are initiated when a client initiates a 
    /// transaction by sending an INVITE  to the server.  The server agent creates a <see cref="SipServerTransaction" /> 
    /// to handle the INVITE.  The transaction immediately sends a Trying provisonal response
    /// back to the client and then signals the server agent to call the <see cref="OnInviteReceived" />
    /// method.
    /// </para>
    /// <para>
    /// The <see cref="OnInviteReceived" /> method may send additional provisional 
    /// responses back to the client on the transaction and then must send a final 2xx 
    /// or 3xx-6xx response with any necessary session descriptions (like SDP) in the 
    /// payload.  At this point, the client will respond by sending an ACK request on
    /// the same transaction to confirm the session.  At this point, the transaction will 
    /// signal the server agent to call the <see cref="OnInviteConfirmed(SipRequestEventArgs)" /> method.  
    /// The method should then begin streaming the media as negotiated.
    /// </para>
    /// <para>
    /// If for some reason, the client does not send the ACK or a nework problem
    /// prevents it from being delivered, then the transaction will eventually
    /// timeout and signal the server agent to call <see cref="OnInviteFailed(SipResponseEventArgs,SipStatus)" />
    /// The method must remove an state its maintaining relating to the dialog.
    /// </para>
    /// <para><b><u>SipCore Events</u></b></para>
    /// <para>
    /// Applications can enlist in several events exposed by the <see cref="SipCore" /> class:
    /// </para>
    /// <table type="table">
    ///     <item>
    ///         <term><see cref="Starting" /></term>
    ///         <description>
    ///         Raised when the core is started.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="Stopping" /></term>
    ///         <description>
    ///         Raised when the core is stopped.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="RequestReceived" /></term>
    ///         <description>
    ///         Raised when the core receives a <see cref="SipRequest" /> other than a dialog creation or termination
    ///         related request such as INVITE or BYE from a UAC.  Server applications will typically enlist 
    ///         in this event to filter and potentially handle client requests.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ResponseReceived" /></term>
    ///         <description>
    ///         Raised when the core receives a non-dialog creation or termination related
    ///         <see cref="SipResponse" /> from a UAS.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="DialogCreated" /></term>
    ///         <description>
    ///         Raised when the core first initiates a <see cref="SipDialog" /> with a SIP peer.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="DialogConfirmed" /></term>
    ///         <description>
    ///         Raised when a <see cref="SipDialog" /> has been fully established between
    ///         the SIP peers.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="DialogClosed" /></term>
    ///         <description>
    ///         Raised when a <see cref="SipDialog" /> has been terminated.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="RegistrationChanged" /></term>
    ///         <description>
    ///         Raised whenever the automatic registration state of the core
    ///         with a SIP registrar changes.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="CreateServerDialogEvent" /></term>
    ///         <description>
    ///         Raised when a server side <see cref="SipDialog" /> needs to be instantiated.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="CreateClientDialogEvent" /></term>
    ///         <description>
    ///         Raised when a client side <see cref="SipDialog" /> needs to be instantiated.
    ///         </description>
    ///     </item>
    /// </table>
    /// </remarks>
    /// <threadsafety instance="true" />
    public abstract class SipCore : ILockable
    {
        //---------------------------------------------------------------------
        // Instance members

        private object                  syncLock = new object();
        private SipCoreSettings         settings;               // The core settings
        private SipUri                  outboundProxyUri;       // The outbound proxy URI (or null)
        private ISipTransport[]         transports;             // The core's transports
        private SipTransportSettings[]  transportSettings;      // The corresponding transport settings
        private ISipMessageRouter       router;                 // The SIP message to agent router
        private bool                    isRunning;              // True if the core is running
        private bool                    isStopPending;          // True if we're in the process of stopping the core
        private GatedTimer              bkTimer;                // Background task timer
        private PolledTimer             transportTimer;         // Schedules transport background activities
        private List<ISipAgent>         agents;                 // The core's agents
        private AsyncCallback           onRequest;              // Handles async client request completions
        private AsyncCallback           onFireAndForget;        // Handles fire-and-forget Request transaction completions
        private SipTraceMode            traceMode;              // Diagnostic tracing flags

        // Registration related state

        private string                  regCallID;              // The Call-ID when issuing REGISTER requests
                                                                // for the lifetime of the core
        private int                     regCSeq;                // REGISTER sequence number
        private string                  registrarUri;           // The SIP registrar URI
        private string                  accountUri;             // The registered entity's account URI
        private PolledTimer             regTimer;               // Fires for the next registration
        private bool                    isRegistered;           // True if we're registered
        private bool                    autoRegistration;       // True if the core is maintaining a
                                                                // persistent registration
        // The unconfirmed server-side dialogs.
        //
        // The server-side early dialogs are keyed by a combination of the dialog's Call-ID and
        // the From tag from the client.

        private Dictionary<string, SipDialog> earlyDialogs = new Dictionary<string, SipDialog>(StringComparer.OrdinalIgnoreCase);

        // The active dialogs keyed by dialog ID.

        private Dictionary<string, SipDialog> dialogs = new Dictionary<string, SipDialog>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Raised when the core is started.
        /// </summary>
        public event EventHandler Starting;

        /// <summary>
        /// Raised when the core is stopped.
        /// </summary>
        public event EventHandler Stopping;

        /// <summary>
        /// Raised when the core receives a <see cref="SipRequest" /> other than a dialog creation or termination
        /// related request such as INVITE, or BYE from a UAC.  Server applications will typically enlist 
        /// in this event to filter and potentially handle client requests.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The event handler can determine if the request is associated with
        /// a <see cref="SipDialog" /> by examining the <see cref="SipRequestEventArgs.Dialog" />
        /// property of the <see cref="SipRequestEventArgs" /> argument.
        /// </para>
        /// <para>
        /// The event handler can choose to process the request immediately and
        /// send one or more responses immediately to the request by referencing the
        /// transaction from the <see cref="SipRequestEventArgs" /> and calling its
        /// <see cref="SipServerTransaction.SendResponse" /> method, before the handler
        /// returns.
        /// </para>
        /// <para>
        /// Alternatively, the event handler may choose to save a reference to the transaction,
        /// begin an asynchronous operation to handle the request, setting the event argument's
        /// <see cref="SipRequestEventArgs.WillRespondAsynchronously" /> property to <c>true</c> returning, 
        /// and then ultimately passing the response to the transaction's <see cref="SipServerTransaction.SendResponse" />
        /// method.
        /// </para>
        /// <para>
        /// Finally, the event handler may construct an appropriate <see cref="SipResponse" />
        /// and assign this to the <b>args</b> parameter's <see cref="SipRequestEventArgs.Response" />
        /// property to have the <see cref="SipCore" /> handle the transmission of the response.
        /// </para>
        /// <para>
        /// For dialog related requests, the core will pass the request on to the dialog
        /// if <see cref="SipRequestEventArgs.Response" /> or <see cref="SipRequestEventArgs.WillRespondAsynchronously" /> 
        /// were not set by the event handler.
        /// </para>
        /// </remarks>
        public event SipRequestDelegate RequestReceived;

        /// <summary>
        /// Raised when the core receives a non-dialog creation or termination related
        /// <see cref="SipResponse" /> from a UAS.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The event handler can determine if the response is associated with
        /// a <see cref="SipDialog" /> by examining the <see cref="SipResponseEventArgs.Dialog" />
        /// property of the <see cref="SipResponseEventArgs" /> argument.
        /// </para>
        /// <note>
        /// This event won't typically see much use except perhaps for logging 
        /// purposes.  Most applications will use the <see cref="Request" /> or
        /// <see cref="BeginRequest(SipRequest,AsyncCallback,object)" />/<see cref="EndRequest" /> methods to
        /// process the correlated response to a SIP request.
        /// </note>
        /// </remarks>
        public event SipResponseDelegate ResponseReceived;

        /// <summary>
        /// Raised when the core first initiates a <see cref="SipDialog" /> with a SIP peer.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This event is raised for dialogs initiated by this core as well as dialogs
        /// initiated from a remote peer.
        /// </para>
        /// <note>
        /// <para>
        /// This handler called immediately after the core has processed the first
        /// dialog related message from the remote peer.  For the core initiating
        /// the dialog, this will happen when the core sees the first provisional
        /// or successful final response encoding a full dialog ID from the server.
        /// Applications will typically transmit an ACK <see cref="SipRequest" />
        /// back to the server to complete the dialog creation handshake when handling
        /// this event.
        /// </para>
        /// <para>
        /// For server side cores, this will be raised when the core receives the
        /// initial INVITE from the remote peer.  Applications will typically
        /// send a <see cref="SipResponse" /> back to the remote peer that 
        /// includes any necessary session related information (for success
        /// responses).
        /// </para>
        /// </note>
        /// </remarks>
        public event SipDialogDelegate DialogCreated;

        /// <summary>
        /// Raised when a <see cref="SipDialog" /> has been fully established between
        /// the SIP peers.
        /// </summary>
        /// <remarks>
        /// For the core initiating the dialog, this will be raised after a
        /// final 2xx response has been received from the server.  For server
        /// side cores, this will be raised when the confirming ACK message
        /// is received from the remote peer.
        /// </remarks>
        public event SipDialogDelegate DialogConfirmed;

        /// <summary>
        /// Raised when a <see cref="SipDialog" /> has been terminated.
        /// </summary>
        /// <remarks>
        /// Fully confirmed dialogs are terminated when one of the peers
        /// initiate a BYE transaction.  Unconfirmed dialogs can be terminated
        /// via a CANCEL transaction, a timeout or another error responses.
        /// </remarks>
        public event SipDialogDelegate DialogClosed;

        /// <summary>
        /// Raised whenever the automatic registration state of the core
        /// with a SIP registrar changes.
        /// </summary>
        public event SipRegistrationStateDelegate RegistrationChanged;

        /// <summary>
        /// Raised when a server side <see cref="SipDialog" /> needs to be instantiated.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This event is used to give the application an opportunity to
        /// create a custom dialog derived from <see cref="SipDialog" />.
        /// The event handler should set the <see cref="SipCreateDialogArgs" />.<see cref="SipCreateDialogArgs.Dialog" />
        /// property to the new dialog.
        /// </para>
        /// <para>
        /// If no handler is enlisted in this event or if no dialog is
        /// created, then the base class will create a <see cref="SipDialog" />
        /// instance instead.
        /// </para>
        /// </remarks>
        public event SipCreateDialogDelegate CreateServerDialogEvent;

        /// <summary>
        /// Raised when a client side <see cref="SipDialog" /> needs to be instantiated.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This event is used to give the application an opportunity to
        /// create a custom dialog derived from <see cref="SipDialog" />.
        /// The event handler should set the <see cref="SipCreateDialogArgs" />.<see cref="SipCreateDialogArgs.Dialog" />
        /// property to the new dialog.
        /// </para>
        /// <para>
        /// If no handler is enlisted in this event or if no dialog is
        /// created, then the base class will create a <see cref="SipDialog" />
        /// instance instead.
        /// </para>
        /// </remarks>
        public event SipCreateDialogDelegate CreateClientDialogEvent;

        /// <summary>
        /// Constructs an core, creating the transports but does not not start them.
        /// </summary>
        /// <param name="settings">The <see cref="SipCoreSettings" />.</param>
        public SipCore(SipCoreSettings settings)
        {
            this.isRunning         = false;
            this.isStopPending     = false;
            this.settings          = settings;
            this.transportSettings = settings.TransportSettings;
            this.outboundProxyUri  = settings.OutboundProxyUri;
            this.transports        = new ISipTransport[settings.TransportSettings.Length];
            this.router            = null;
            this.bkTimer           = null;
            this.transportTimer    = new PolledTimer(settings.TransportBkInterval);
            this.agents            = new List<ISipAgent>();
            this.onRequest         = new AsyncCallback(OnRequest);
            this.onFireAndForget   = new AsyncCallback(OnFireAndForget);
            this.traceMode         = settings.TraceMode;

            for (int i = 0; i < transports.Length; i++)
            {
                ISipTransport transport;

                switch (transportSettings[i].TransportType)
                {
                    case SipTransportType.Unspecified:

                        throw new SipException("Unspecified transport type.");

                    case SipTransportType.UDP:

                        transport = new SipUdpTransport();
                        break;

                    case SipTransportType.TCP:

                        transport = new SipTcpTransport();
                        break;

                    case SipTransportType.TLS:

                        throw new NotImplementedException("SIP-TLS transport is not implemented.");

                    default:

                        throw new NotImplementedException("Unexpected SIP transport.");
                }

                transports[i] = transport;
            }

            // Registration related state

            this.regCallID        = SipHelper.GenerateCallID();
            this.regCSeq          = 0;
            this.isRegistered     = false;
            this.autoRegistration = false;
            this.regTimer         = new PolledTimer(TimeSpan.FromMinutes(1), true);
            this.regTimer.Disable();
        }

        /// <summary>
        /// Sets the <see cref="ISipMessageRouter" /> to be used by the core to
        /// determine which of the core's agents should process the request.
        /// This must be called before calling <see cref="Start" />.
        /// </summary>
        /// <param name="router">The <see cref="ISipMessageRouter" /></param>
        protected void SetRouter(ISipMessageRouter router)
        {
            this.router = router;
        }

        /// <summary>
        /// Starts the core's transports so it can start processing SIP messages.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The derived class must call <see cref="SetRouter" /> and added 
        /// its <see cref="ISipAgent" />s to the <see cref="Agents" /> collection
        /// before calling this method.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the derived class has not yet called<see cref="SetRouter" /> or added
        /// its <see cref="ISipAgent" />s to the <see cref="Agents" /> collection.
        /// </exception>
        public virtual void Start()
        {
            using (TimedLock.Lock(this))
            {
                if (router == null)
                    throw new InvalidOperationException("SetRouter() must be called before calling Start().");

                if (agents.Count == 0)
                    throw new InvalidOperationException("The core's agents must be assigned to the Agents property before calling Start().");

                try
                {
                    if (isRunning)
                        throw new InvalidOperationException("Core is already running.");

                    if (isStopPending)
                        throw new InvalidOperationException("Cannot restart a SipCore.");

                    for (int i = 0; i < transports.Length; i++)
                    {
                        var transport = transports[i];

                        transport.Start(transportSettings[i], router);
                    }

                    SetTraceMode(traceMode);

                    this.bkTimer   = new GatedTimer(new TimerCallback(OnBkTask), null, settings.BkInterval);
                    this.isRunning = true;

                    this.transportTimer.Reset();
                }
                catch
                {
                    if (bkTimer != null)
                    {
                        bkTimer.Dispose();
                        bkTimer = null;
                    }

                    // Close any transports that were opened successfully.

                    foreach (ISipTransport transport in transports)
                        transport.Stop();

                    throw;
                }
            }

            if (Starting != null)
                Starting(this, new EventArgs());
        }

        /// <summary>
        /// Stops the core if it is currently running, terminating all transactions and dialogs.
        /// </summary>
        public virtual void Stop()
        {
            var dialogList = new List<SipDialog>();

            if (Stopping != null)
                Stopping(this, new EventArgs());

            using (TimedLock.Lock(this))
            {
                if (!isRunning || isStopPending)
                    return;

                isStopPending = true;

                foreach (SipDialog dialog in dialogs.Values)
                    dialogList.Add(dialog);
            }

            StopAutoRegistration();

            // Terminate any dialogs

            foreach (SipDialog dialog in dialogList)
                dialog.Close();

            bkTimer.Dispose();
            bkTimer = null;

            using (TimedLock.Lock(this))
            {
                isRunning = false;

                // Stop the agents

                foreach (ISipAgent agent in this.Agents)
                    agent.Stop();

                // Stop the transports

                foreach (ISipTransport transport in transports)
                    transport.Stop();

                // Misc cleanup

                dialogs.Clear();
            }
        }

        /// <summary>
        /// Stops the core's the core's transports without closing any transactions
        /// or dialogs.  This available for internal unit testing purposes for
        /// simulating network or hardware failures.
        /// </summary>
        internal void DisableTransports()
        {
            using (TimedLock.Lock(this)) {

                if (transports == null)
                    return;

                foreach (ISipTransport transport in transports)
                    transport.Disable();
            }
        }

        /// <summary>
        /// Sets the diagnostic tracing mode.
        /// </summary>
        /// <param name="traceMode">The <see cref="SipTraceMode" /> flags.</param>
        /// <remarks>
        /// <note>
        /// The SIP stack uses <see cref="NetTrace" /> for trace output.
        /// <see cref="NetTrace" />.<see cref="NetTrace.Start()" /> must
        /// be called before calling this method to enable tracing.
        /// </note>
        /// </remarks>
        public void SetTraceMode(SipTraceMode traceMode)
        {
            if (traceMode != SipTraceMode.None)
                NetTrace.Enable(SipHelper.TraceSubsystem, 0);

            this.traceMode = traceMode;

            foreach (ISipTransport transport in transports)
                transport.SetTraceMode(traceMode);
        }

        /// <summary>
        /// The <see cref="SipUri" /> for the outbound proxy or <c>null</c> if no
        /// proxy is configured.
        /// </summary>
        /// <remarks>
        /// This property is initialized from the <see cref="SipCoreSettings" /> when
        /// the core is created.  The proxy URI can be changed while the core is
        /// running.
        /// </remarks>
        public SipUri OutboundProxyUri
        {
            get { return outboundProxyUri; }
            set { outboundProxyUri = value; }
        }

        /// <summary>
        /// Returns the list of <see cref="ISipAgent" />s managed by the core.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Derived classes must add their agents to this collection before
        /// calling <see cref="Start" />.
        /// </note>
        /// </remarks>
        public List<ISipAgent> Agents
        {
            get { return agents; }
        }

        /// <summary>
        /// Returns <c>true</c> if the core is currently running.
        /// </summary>
        public bool IsRunning
        {
            get { return isRunning; }
        }

        /// <summary>
        /// Returns the array of transports available to the agent.
        /// </summary>
        public ISipTransport[] Transports
        {
            get { return transports; }
        }

        /// <summary>
        /// Returns the <see cref="SipCoreSettings" />.
        /// </summary>
        public SipCoreSettings Settings
        {
            get { return settings; }
        }

        /// <summary>
        /// Returns the number of SIP dialogs currently managed by the core.
        /// </summary>
        public int DialogCount
        {
            get
            {
                using (TimedLock.Lock(this))
                {
                    if (!isRunning)
                        return 0;

                    return dialogs.Count + earlyDialogs.Count;
                }
            }
        }

        /// <summary>
        /// Creates a server-side <see cref="SipDialog" /> from a received INVITE <see cref="SipRequest" />.
        /// </summary>
        /// <param name="inviteRequest">The INVITE request.</param>
        /// <param name="localContact">The <see cref="SipContactValue" /> for the local side of the dialog.</param>
        /// <returns>The new dialog instance.</returns>
        /// <remarks>
        /// <para>
        /// This method provides an opportunity to create a custom dialog derived from
        /// <see cref="SipDialog" />, where application state can be convienently located.
        /// To accomplish this, the derived clas must override this method and then call
        /// the <see cref="SipDialog" />'s static generic <see cref="SipDialog.CreateAcceptingDialog" />
        /// method to construct and initialize the dialog.
        /// </para>
        /// <para>
        /// This base implementation raises the <see cref="CreateServerDialogEvent" /> event
        /// to return a custom a dialog class if the delegate is not <c>null</c>, otherwise the base
        /// class will return a new <see cref="SipDialog" /> instance.
        /// </para>
        /// </remarks>
        protected virtual SipDialog CreateServerDialog(SipRequest inviteRequest, SipContactValue localContact)
        {
            SipDialog dialog = null;

            try
            {
                if (CreateServerDialogEvent != null)
                {
                    var args = new SipCreateDialogArgs();

                    CreateServerDialogEvent(this, args);
                    if (args.Dialog != null)
                    {
                        dialog = args.Dialog;
                        return dialog;
                    }
                }

                dialog = SipDialog.CreateAcceptingDialog<SipDialog>(this, (SipServerTransaction)inviteRequest.SourceTransaction, inviteRequest, localContact, null);
                return dialog;
            }
            finally
            {
                dialog.Initialize(this, inviteRequest, localContact, (SipServerTransaction)inviteRequest.SourceTransaction, null);
            }
        }

        /// <summary>
        /// Creates a client side <see cref="SipDialog" /> from the INVITE <see cref="SipRequest" /> to
        /// be sent to the server.
        /// </summary>
        /// <param name="inviteRequest">The INVITE request.</param>
        /// <param name="localContact">The <see cref="SipContactValue" /> for the local side of the dialog.</param>
        /// <param name="state">State to be passed to the derived class <see cref="SipDialog.Initialize" /> method (or <c>null</c>).</param>
        /// <returns>The new dialog instance.</returns>
        /// <remarks>
        /// <para>
        /// This method provides an opportunity to create a custom dialog derived from
        /// <see cref="SipDialog" />, where application state can be convienently located.
        /// To accomplish this, the derived clas must override this method and then call
        /// the <see cref="SipDialog" />'s static generic <see cref="SipDialog.CreateInitiatingDialog" />
        /// method to construct and initialize the dialog.
        /// </para>
        /// <para>
        /// This base implementation raises the <see cref="CreateClientDialogEvent" /> event
        /// to return a custom a dialog class if the delegate is not <c>null</c>, otherwise the base
        /// class will return a new <see cref="SipDialog" /> instance.
        /// </para>
        /// </remarks>
        protected virtual SipDialog CreateClientDialog(SipRequest inviteRequest, SipContactValue localContact, object state)
        {
            SipDialog dialog = null;

            try
            {
                if (CreateClientDialogEvent != null)
                {
                    var args = new SipCreateDialogArgs();

                    CreateClientDialogEvent(this, args);
                    if (args.Dialog != null)
                    {
                        dialog = args.Dialog;
                        return args.Dialog;
                    }
                }

                dialog = SipDialog.CreateInitiatingDialog<SipDialog>(this, inviteRequest, localContact, state);
                return dialog;
            }
            finally
            {
                dialog.Initialize(this, inviteRequest, localContact, null, state);
            }
        }

        /// <summary>
        /// Returns the <see cref="SipDialog" /> from its early dialog ID.
        /// </summary>
        /// <param name="earlyID">The early ID.</param>
        /// <returns>The dialog if found, <c>null</c> otherwise.</returns>
        /// <remarks>
        /// <note>
        /// This method does not work for confirmed dialogs.  Use <see cref="GetDialog" /> instead.
        /// </note>
        /// </remarks>
        public SipDialog GetEarlyDialog(string earlyID)
        {
            SipDialog dialog;

            using (TimedLock.Lock(this))
            {
                if (!isRunning)
                    return null;

                if (earlyDialogs.TryGetValue(earlyID, out dialog))
                    return dialog;
                else
                    return null;
            }
        }

        /// <summary>
        /// Adds a <see cref="SipDialog" /> to the collection of early dialogs.
        /// </summary>
        /// <param name="dialog">The new dialog.</param>
        public void AddEarlyDialog(SipDialog dialog)
        {
            if (dialog == null)
                throw new ArgumentNullException("dialog");

            using (TimedLock.Lock(this))
            {
                if (!isRunning)
                    return;

                string earlyID = dialog.EarlyID;

                if (earlyID != null && !earlyDialogs.ContainsKey(earlyID))
                {
                    dialog.EarlyTTD = SysTime.Now + settings.EarlyDialogTTL;
                    earlyDialogs.Add(earlyID, dialog);
                }
            }
        }

        /// <summary>
        /// Removes a <see cref="SipDialog" /> from the collection of early dialogs
        /// if it is present.
        /// </summary>
        /// <param name="dialog">The dialog to be removed.</param>
        public void RemoveEarlyDialog(SipDialog dialog)
        {
            if (dialog == null)
                throw new ArgumentNullException("dialog");

            using (TimedLock.Lock(this))
            {
                if (!IsRunning)
                    return;

                if (dialog.EarlyID != null && earlyDialogs.ContainsKey(dialog.EarlyID))
                    earlyDialogs.Remove(dialog.EarlyID);
            }
        }
        /// <summary>
        /// Returns the <see cref="SipDialog" /> from its globally unique dialog ID.
        /// </summary>
        /// <param name="dialogID">The confirmed dialog ID.</param>
        /// <returns>The dialog if found, <c>null</c> otherwise.</returns>
        /// <remarks>
        /// <note>
        /// This method does not work for early dialogs.  Use <see cref="GetEarlyDialog" /> instead.
        /// </note>
        /// </remarks>
        public SipDialog GetDialog(string dialogID)
        {
            SipDialog dialog;

            using (TimedLock.Lock(this))
            {
                if (!isRunning)
                    return null;

                if (dialogs.TryGetValue(dialogID, out dialog))
                    return dialog;
                else
                    return null;
            }
        }

        /// <summary>
        /// Adds a <see cref="SipDialog" /> to the collection of active dialogs
        /// and removes it from the early dialog collection if present there.
        /// </summary>
        /// <param name="dialog">The new dialog.</param>
        public void AddDialog(SipDialog dialog)
        {
            if (dialog == null)
                throw new ArgumentNullException("dialog");

            using (TimedLock.Lock(this))
            {
                if (!isRunning)
                    return;

                // Remove the dialog from the early dialogs collection
                // if it exists there.

                string earlyDialogID = dialog.EarlyID;

                if (earlyDialogID != null && earlyDialogs.ContainsKey(earlyDialogID))
                    earlyDialogs.Remove(earlyDialogID);

                // Add the dialog to the active dialogs collection
                // if it's not already present.

                string dialogID = dialog.ID;

                if (dialogID != null && !dialogs.ContainsKey(dialog.ID))
                    dialogs.Add(dialog.ID, dialog);
            }
        }

        /// <summary>
        /// Removes a <see cref="SipDialog" /> from the collection of active dialogs
        /// and/or early dialogs if it is present.
        /// </summary>
        /// <param name="dialog">The dialog to be removed.</param>
        public void RemoveDialog(SipDialog dialog)
        {
            if (dialog == null)
                throw new ArgumentNullException("dialog");

            using (TimedLock.Lock(this))
            {
                if (!IsRunning)
                    return;

                if (dialog.EarlyID != null && earlyDialogs.ContainsKey(dialog.EarlyID))
                    earlyDialogs.Remove(dialog.EarlyID);

                if (dialog.ID != null && dialogs.ContainsKey(dialog.ID))
                    dialogs.Remove(dialog.ID);
            }
        }

        /// <summary>
        /// Returns the <see cref="SipContactValue" /> to be used in the as the
        /// local contact for dialogs created by the core.
        /// </summary>
        /// <param name="transaction">The <see cref="SipTransaction" /> that received the request.</param>
        /// <returns>A <see cref="SipContactValue" /> instance.</returns>
        /// <remarks>
        /// The base implementation generates a SIP URI from the <see cref="SipCoreSettings" />'s
        /// <see cref="SipCoreSettings.LocalContact" /> value.  If this is a full SIP URI, then
        /// the setting will be returned <i>as is</i>.  Otherwise the method will assume that
        /// the value is an IP address or host name and generate a SIP URI by adding the
        /// scheme, port, and transport parameter based on the transport the request was
        /// received on.
        /// </remarks>
        public virtual SipContactValue GetLocalContact(SipTransaction transaction)
        {
            ISipTransport   transport;
            SipUri          uri;

            if (SipUri.TryParse(settings.LocalContact, out uri))
                return new SipContactValue(null, (string)uri);

            transport = transaction.Transport;
            uri       = new SipUri(transport.TransportType, transport.Settings.ExternalBinding);

            return new SipContactValue(null, (string)uri);
        }

        /// <summary>
        /// Returns the <see cref="SipClientAgent" /> to be used for submitting the
        /// <see cref="SipRequest" /> passed.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The <see cref="SipClientAgent" />.</returns>
        /// <remarks>
        /// The base implemention of this method returns the first <see cref="SipClientAgent" />
        /// it finds in the <see cref="Agents" /> list.  Derived classes can override this
        /// method to implement their own selection algorithm, perhaps based on the
        /// request itself.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if no appropriate <see cref="SipClientAgent" /> can be found</exception>
        protected virtual SipClientAgent GetClientAgent(SipRequest request)
        {
            foreach (var agent in agents)
            {
                var clientAgent = agent as SipClientAgent;

                if (clientAgent != null)
                    return clientAgent;
            }

            throw new InvalidOperationException("No [SipClientAgent] is available.");
        }

        /// <summary>
        /// Selects the appropriate transport and endpoint for the <see cref="SipRequest" /> passed and
        /// then submits it to the transport for transmission.
        /// </summary>
        /// <param name="agent">The source <see cref="ISipAgent" />.</param>
        /// <param name="request">The <see cref="SipRequest" /> to be transmitted.</param>
        /// <remarks>
        /// <note>
        /// The message will be discarded if no appropriate transport or endpoint
        /// could be found for the request.
        /// </note>
        /// </remarks>
        public virtual void Send(ISipAgent agent, SipRequest request)
        {
            ISipTransport   transport;
            NetworkBinding  binding;

            transport = router.SelectTransport(agent, request, out binding);
            if (transport != null)
                transport.Send(binding, request);
        }

        /// <summary>
        /// Creates and transmits a <see cref="SipResponse" /> for the request
        /// referenced by the <paramref name="args" /> parameter.
        /// </summary>
        /// <param name="args">A <see cref="SipRequestEventArgs" /> object holding necessary information about the request.</param>
        /// <param name="status">The <see cref="SipStatus" /> to be used when constructing the response.</param>
        /// <param name="reasonPhrase">The reason phrase (or <c>null</c>).</param>
        /// <exception cref="InvalidOperationException">Thrown if a <see cref="SipResponse" /> has already been sent for this request.</exception>
        public virtual void Reply(SipRequestEventArgs args, SipStatus status, string reasonPhrase)
        {
            if (args.ResponseSent)
                throw new InvalidOperationException("Response has already already been sent for this request.");

            args.ResponseSent = true;
            args.Transaction.SendResponse(args.Request.CreateResponse(status, reasonPhrase));
        }

        /// <summary>
        /// Transmits a <see cref="SipResponse" /> for the request
        /// referenced by the <paramref name="args" /> parameter.
        /// </summary>
        /// <param name="args">A <see cref="SipRequestEventArgs" /> object holding necessary information about the request.</param>
        /// <param name="response">The response to be delivered.</param>
        /// <exception cref="InvalidOperationException">Thrown if a <see cref="SipResponse" /> has already been sent for this request.</exception>
        public virtual void Reply(SipRequestEventArgs args, SipResponse response)
        {
            if (args.ResponseSent)
                throw new InvalidOperationException("Response has already already been sent for this request.");

            args.ResponseSent = true;
            args.Transaction.SendResponse(response);
        }

        //---------------------------------------------------------------------
        // ISipAgent's use these methods to communicate state changes and
        // messages to the core.

        /// <summary>
        /// Called when a <see cref="SipRequest" /> is received by one of the core's
        /// <see cref="SipServerAgent" />s.
        /// </summary>
        /// <param name="args">
        /// A <see cref="SipRequestEventArgs" /> instance that includes the request 
        /// along with other useful information.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method is called for all received requests before any attempt
        /// is made to map the request to a dialog.  The base implementation
        /// performs this mapping and sets the <see cref="SipRequestEventArgs.Dialog" />
        /// property to the dialog, if one is found.
        /// </para>
        /// <para>
        /// The base implementation also handles BYE requests
        /// targeted at dialogs.  The <see cref="RequestReceived" /> <b>will not</b>
        /// be raised for these messages, <see cref="DialogClosed" /> will be
        /// raised instead.
        /// </para>
        /// </remarks>
        public virtual void OnRequestReceived(SipRequestEventArgs args)
        {
            string      dialogID;
            SipDialog   dialog;

            dialogID = SipDialog.GetDialogID(args.Request);
            if (dialogID != null)
                dialog = GetDialog(dialogID);
            else
                dialog = null;

            try
            {
                if (dialogID == null)
                {
                    // The request is not part of a dialog.

                    if (RequestReceived == null)
                    {
                        Reply(args, SipStatus.NotImplemented, null);
                        return;
                    }

                    // Handle the request

                    RequestReceived(this, args);

                    if (args.Response != null)
                        Reply(args, args.Response);
                    else if (!args.WillRespondAsynchronously && !args.ResponseSent)
                        Reply(args, SipStatus.NotImplemented, null);

                    return;
                }

                // The request indicates that it's part of the dialog.

                if (args.Request.Method == SipMethod.Ack)
                    return;     // We don't process ACKs outside of the
                // original INVITE transaction

                if (dialog == null)
                {
                    // Dialog doesn't exist

                    Reply(args, SipStatus.TransactionDoesNotExist, null);
                    return;
                }

                if (!dialog.IsValid(args.Request))
                {
                    // Request appears to be out-of-order

                    Reply(args, SipStatus.ServerError, null);
                    return;
                }

                args.Dialog = dialog;

                // Handle BYE requests here

                if (args.Request.Method == SipMethod.Bye)
                {
                    RemoveDialog(dialog);
                    Reply(args, SipStatus.OK, null);
                    dialog.State = SipDialogState.CloseEventPending;

                    if (DialogClosed != null)
                        DialogClosed(this, new SipDialogEventArgs(dialog, args.Transaction, this, null, null));

                    dialog.OnClose();
                    return;
                }

                // Handle other requests by passing firing RequestReceived

                if (RequestReceived != null)
                {
                    RequestReceived(this, args);

                    if (args.Response != null)
                    {
                        Reply(args, args.Response);
                        return;
                    }
                    else if (args.WillRespondAsynchronously || args.ResponseSent)
                        return;
                }

                // Route the request to the dialog if it wasn't already handled by
                // the RequestReceived event handler.

                dialog.OnRequestReceived(this, args);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
                Reply(args, SipStatus.ServerError, null);
            }
        }

        /// <summary>
        /// Called when a non-INVITE related <see cref="SipResponse" /> is received by one of the core's
        /// <see cref="SipClientAgent" />s.
        /// </summary>
        /// <param name="args">
        /// A <see cref="SipResponseEventArgs" /> instance that includes the response 
        /// along with other useful information.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method is called for all received response before any attempt
        /// is made to map the response to a dialog.
        /// </para>
        /// <note>
        /// This base implementation attempts to map the response to a 
        /// dialog, setting the <paramref name="args"/> parameter's <see cref="SipResponseEventArgs.Dialog" />
        /// property if a dialog was found.
        /// </note>
        /// </remarks>
        public virtual void OnResponseReceived(SipResponseEventArgs args)
        {
            string earlyID = null;
            string dialogID = null;

            if (args.Response != null)
            {
                earlyID = SipDialog.GetEarlyDialogID(args.Response);
                dialogID = SipDialog.GetDialogID(args.Response);
            }

            if (earlyID == null && dialogID == null)
            {
                // The response is not part of a dialog.

                if (ResponseReceived != null)
                    ResponseReceived(this, args);

                return;
            }

            // The response indicates that it's part of a dialog.  Raise the
            // ResponseReceived event handler and then route the message 
            // to the dialog (if there is one).

            args.Dialog = GetEarlyDialog(earlyID);
            if (args.Dialog == null)
                args.Dialog = GetDialog(dialogID);

            if (ResponseReceived != null)
                ResponseReceived(this, args);

            if (args.Dialog != null)
                args.Dialog.OnResponseReceived(this, args);
        }

        /// <summary>
        /// Called when an INVITE request is received by one of the core's <see cref="SipServerAgent" />s.
        /// </summary>
        /// <param name="args">
        /// A <see cref="SipRequestEventArgs" /> instance that includes the request 
        /// along with other useful information.
        /// </param>
        /// <remarks>
        /// This base implementation will verify that the dialog doesn't already exist
        /// before firing the <see cref="DialogCreated" /> event.
        /// </remarks>
        public virtual void OnInviteReceived(SipRequestEventArgs args)
        {
            string      dialogID = SipDialog.GetDialogID(args.Request);
            SipDialog   dialog;

            if (dialogID == null)
                return;     // Shouldn't ever see this

            lock (syncLock)
            {
                dialog = GetDialog(dialogID);
                if (dialog == null)
                {
                    dialog = CreateServerDialog(args.Request, GetLocalContact(args.Transaction));
                    AddDialog(dialog);
                }

                args.Dialog = dialog;
            }

            if (DialogCreated != null)
                DialogCreated(this, new SipDialogEventArgs(dialog, args.Transaction, this, args.Request, null));
        }

        /// <summary>
        /// Called when a dialog with a server has been partially established.
        /// with a provisional response from the server that included the
        /// server side dialog tag on the <b>To</b> header of the response.
        /// </summary>
        /// <param name="args">
        /// A <see cref="SipResponseEventArgs" /> instance that includes the response 
        /// along with other useful information.
        /// </param>
        /// <remarks>
        /// This base implementation will verify that the dialog already exists
        /// before firing the <see cref="DialogCreated" /> event.
        /// </remarks>
        public virtual void OnInviteProvisional(SipResponseEventArgs args)
        {
            string      dialogID = SipDialog.GetDialogID(args.Response);
            bool        raiseCreated = false;
            SipDialog   dialog;

            lock (syncLock)
            {
                dialog = GetDialog(dialogID);
                if (dialog == null)
                {
                    dialog = args.Dialog;
                    raiseCreated = true;

                    dialogs.Add(dialogID, dialog);
                }

                dialog.State = SipDialogState.Early;
            }

            if (raiseCreated && DialogCreated != null)
                DialogCreated(this, new SipDialogEventArgs(dialog, args.Transaction, this, null, null));

            // Route the response to the dialog.

            dialog.OnResponseReceived(this, args);
        }

        /// <summary>
        /// Called when a dialog with a client has been established.
        /// </summary>
        /// <param name="args">
        /// A <see cref="SipRequestEventArgs" /> instance that includes the 
        /// ACK request from the client along with other useful information.
        /// </param>
        /// <remarks>
        /// This base implementation will verify that the dialog already exists
        /// before firing the <see cref="DialogConfirmed" /> event.
        /// </remarks>
        public virtual void OnInviteConfirmed(SipRequestEventArgs args)
        {
            string              dialogID = SipDialog.GetDialogID(args.Request);
            string              earlyID  = SipDialog.GetEarlyDialogID(args.Request);
            bool                raiseCreated = false;
            SipDialogEventArgs  dialogArgs;
            SipDialog           dialog;

            Assertion.Test(args.Request.Method == SipMethod.Ack);

            lock (syncLock)
            {
                dialog = GetDialog(dialogID);
                if (dialog == null)
                {
                    dialog = GetEarlyDialog(earlyID);
                    if (dialog != null)
                        RemoveEarlyDialog(dialog);
                    else
                        raiseCreated = true;

                    AddDialog(dialog);
                }

                args.Dialog = dialog;
                dialog.State = SipDialogState.Confirmed;
            }

            dialogArgs = new SipDialogEventArgs(dialog, args.Transaction, this, args.Request, null);

            if (raiseCreated && DialogCreated != null)
                DialogCreated(this, dialogArgs);

            if (DialogConfirmed != null)
                DialogConfirmed(this, dialogArgs);

            // Route the ACK to the dialog

            dialog.OnRequestReceived(this, args);
        }

        /// <summary>
        /// Called when a dialog with a server has been established.
        /// </summary>
        /// <param name="args">
        /// A <see cref="SipResponseEventArgs" /> instance that includes the 
        /// response along with other useful information.
        /// </param>
        /// <remarks>
        /// This base implementation will verify that the dialog already exists
        /// before firing the <see cref="DialogConfirmed" /> event.
        /// </remarks>
        public virtual void OnInviteConfirmed(SipResponseEventArgs args)
        {
            string              dialogID     = SipDialog.GetDialogID(args.Response);
            bool                raiseCreated = false;
            SipDialogEventArgs  dialogArgs;
            SipDialog           dialog;

            lock (syncLock)
            {
                dialog = GetDialog(dialogID);
                if (dialog == null)
                {
                    dialog       = args.Dialog;
                    raiseCreated = true;

                    dialogs.Add(dialogID, dialog);
                }

                args.Dialog = dialog;
                dialog.SetDisposition(args.Response.Status, args.Response);
                dialog.OnConfirmed(args);
            }

            dialogArgs = new SipDialogEventArgs(dialog, args.Transaction, this, null, new SipResult(dialog.InviteRequest, dialog, args.Agent, args.Response));

            if (raiseCreated && DialogCreated != null)
                DialogCreated(this, dialogArgs);

            if (DialogConfirmed != null)
                DialogConfirmed(this, dialogArgs);
        }

        /// <summary>
        /// Called when a dialog with a client could not be established due to 
        /// a timeout or transport related error.
        /// </summary>
        /// <param name="args">
        /// A <see cref="SipRequestEventArgs" /> instance that includes the 
        /// original INVITE request along with other useful information.
        /// </param>
        /// /// <param name="status">A <see cref="SipStatus" /> code describing the error.</param>
        /// <remarks>
        /// This base implementation will verify that the dialog exists
        /// before firing the <see cref="DialogClosed" /> event.
        /// </remarks>
        public virtual void OnInviteFailed(SipRequestEventArgs args, SipStatus status)
        {
            string      dialogID = SipDialog.GetDialogID(args.InviteRequest);
            string      earlyID  = SipDialog.GetEarlyDialogID(args.InviteRequest);
            SipDialog   dialog   = null;

            using (TimedLock.Lock(this))
            {
                dialog = GetDialog(dialogID);
                if (dialog == null)
                    dialog = GetEarlyDialog(earlyID);

                if (dialog == null)
                    return;

                RemoveDialog(dialog);
            }

            if (args.Dialog != null)
            {
                args.Dialog = dialog;

                dialog.SetDisposition(status, null);
                dialog.Close();
            }
        }

        /// <summary>
        /// Called when a dialog with a server could not be established due to a 
        /// timeout or transport related error.
        /// </summary>
        /// <param name="args">
        /// A <see cref="SipResponseEventArgs" /> instance that includes the 
        /// original INVITE request along with other useful information.
        /// </param>
        /// <param name="status">A <see cref="SipStatus" /> code describing the error.</param>
        /// <remarks>
        /// This base implementation will verify that the dialog exists
        /// before firing the <see cref="DialogClosed" /> event.
        /// </remarks>
        public virtual void OnInviteFailed(SipResponseEventArgs args, SipStatus status)
        {
            string      dialogID = SipDialog.GetDialogID(args.Response);
            SipDialog   dialog   = null;

            using (TimedLock.Lock(this))
            {
                dialog = GetDialog(dialogID);
                if (dialog == null)
                    return;

                RemoveDialog(dialog);
            }

            if (args.Dialog != null)
            {
                args.Dialog = dialog;

                dialog.SetDisposition(status, null);
                dialog.Close();
            }
        }

        /// <summary>
        /// Called by a core's <see cref="SipServerAgent" /> when it receives an ACK
        /// <see cref="SipRequest" /> that does not correlate to an existing transaction.
        /// </summary>
        /// <param name="agent">The <see cref="ISipAgent" /> where the response was routed.</param>
        /// <param name="ackRequest">The ACK <see cref="SipRequest" /> received.</param>
        /// <remarks>
        /// This method will be called when an ACK is received confirming the client's
        /// participation in the dialog.  This method handles this by calling 
        /// <see cref="OnInviteConfirmed(SipRequestEventArgs)" /> which will raise the <see cref="DialogConfirmed" /> 
        /// event.
        /// </remarks>
        public virtual void OnConfirmingAck(ISipAgent agent, SipRequest ackRequest)
        {
            OnInviteConfirmed(new SipRequestEventArgs(ackRequest, null, null, (SipServerAgent)agent, this, null));
        }

        /// <summary>
        /// Called by a core's <see cref="SipClientAgent" /> when it receives a <see cref="SipResponse" />
        /// that does not correlate to an existing transaction.
        /// </summary>
        /// <param name="agent">The <see cref="ISipAgent" /> where the response was routed.</param>
        /// <param name="response">The uncorrelated <see cref="SipResponse" />.</param>
        /// <remarks>
        /// <para>
        /// The main purpose for this method is to give the core a chance to
        /// handle the generation of ACK responses for INVITE transactions.
        /// Exactly how this is handled depends on whether or not the core
        /// is a proxy.
        /// </para>
        /// <para>
        /// This base implementation implements the non-proxy UAC case.  Proxies
        /// will have to override this and implement the correct proxy behavior.
        /// </para>
        /// <para>
        /// See pages 128-129 of RFC 3261 for more information.
        /// </para>
        /// </remarks>
        public virtual void OnUncorrelatedResponse(ISipAgent agent, SipResponse response)
        {
            SipDialog   dialog;
            string      dialogID;

            using (TimedLock.Lock(this))
            {
                if (!isRunning)
                    return;

                dialogID = SipDialog.GetDialogID(response);
                if (dialogID == null || !dialogs.TryGetValue(dialogID, out dialog))
                    return;

                dialog.SendAckRequest(null);
            }
        }

        /// <summary>
        /// Called internally by <see cref="SipDialog" /> when it is closed locally
        /// by a call to its <see cref="SipDialog.Close" /> method.
        /// </summary>
        /// <param name="dialog">The <see cref="SipDialog" /> being closed.</param>
        /// <remarks>
        /// The base implementation raises the <see cref="DialogClosed" /> event.
        /// </remarks>
        internal virtual void OnDialogClosed(SipDialog dialog)
        {
            if (DialogClosed != null && !dialog.CoreDialogClosedRaised)
            {
                dialog.CoreDialogClosedRaised = true;
                DialogClosed(this, new SipDialogEventArgs(dialog, null, this, null, null));
            }
        }

        /// <summary>
        /// Called periodically on a background thread to handle background activities.
        /// </summary>
        /// <param name="state">Not used.</param>
        /// <remarks>
        /// This base class implementation calls each transport's <see cref="ISipTransport.OnBkTask" />
        /// method periodically.
        /// </remarks>
        public virtual void OnBkTask(object state)
        {
            using (TimedLock.Lock(this))
            {
                if (!isRunning)
                    return;

                // Give the agents a chance to handle their background activities.

                foreach (var agent in agents)
                    agent.OnBkTask();

                // Give the transports a chance to handle their background activities.

                if (transportTimer.HasFired)
                {
                    foreach (var transport in transports)
                        transport.OnBkTask();

                    transportTimer.Reset();
                }

                // Remove any terminated dialogs

                List<SipDialog> delList = new List<SipDialog>();

                foreach (var dialog in dialogs.Values)
                    if (dialog.State == SipDialogState.Closed)
                        delList.Add(dialog);

                foreach (SipDialog dialog in delList)
                    dialogs.Remove(dialog.ID);

                // Remove any early dialogs that have exceeded their time-to-die.

                var now = SysTime.Now;

                delList.Clear();
                foreach (var dialog in earlyDialogs.Values)
                    if (dialog.EarlyTTD <= now)
                        delList.Add(dialog);

                foreach (SipDialog dialog in delList)
                    earlyDialogs.Remove(dialog.EarlyID);

                // Handle automatic registration

                AutoRegister();
            }
        }

        //---------------------------------------------------------------------
        // Non-dialog related request submission methods

        // Authentication Implementation Note
        // 
        // The SipCore implicitly supports MD5 digest authentication.  A UAS
        // will indicate that it requires authentication via a Unauthorized (401)
        // or ProxyAuthenticationRequired (407) response with the challenge
        // specified in the "WWW-Autheticate" or "Proxy-Authenticate" header.
        // The SipCore will calculate the challenge response using the user ID
        // and password from the SipCoreSettings, add an "Authorization" or
        // "Proxy-Authorization" header with the response and then resubmit
        // the request.
        //
        // Note that there are four authorization scenarios:
        //
        //      1. No authentication is required
        //      2. The proxy requires authentication
        //      3. The server requires authentication
        //      4. Both the proxy and server require authentication
        //
        // SipCore supports all of these.  #4 is particularily interesting.
        // Here's how this would play out:
        //
        //      1. Initial rfequest is submitted
        //      2. Proxy responds with ProxyAuthenticationRequired
        //      3. Request is resubmitted with Proxy-Authorization
        //      4. Proxy passed the request onto the server
        //      5. Server response with Unauthenticated
        //      6. Core adds an Authorization and resubmits the request

        // $todo(jeff.lill): 
        //
        // This code assumes that the account and password
        // are the same for both endpoint and proxy authentication
        // challenges.  I need to extend SipCore settings
        // to differentiate between these two cases.

        /// <summary>
        /// Used internal by the async request methods to hold operation state.
        /// </summary>
        private sealed class RequestAsyncResult : AsyncResult
        {
            public SipRequest       Request;            // The request being submitted
            public SipClientAgent   Agent;              // The client agent used
            public int              AuthCount;          // # of times the target has asked for authentication
            public int              ProxyAuthCount;     // # of times the proxy has asked for authentication
            public SipResult        SipResult;          // The ultimate request result
            public SipDialog        Dialog;             // The dialog associated with the request (if any)

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="request">The request being submitted.</param>
            /// <param name="dialog">The request dialog (or <c>null</c>).</param>
            /// <param name="agent">The client agent being used.</param>
            /// <param name="callback">The application callback (or <c>null</c>).</param>
            /// <param name="state">The application state (or <c>null</c>).</param>
            public RequestAsyncResult(SipRequest request, SipDialog dialog, SipClientAgent agent, AsyncCallback callback, object state)
                : base(null, callback, state)
            {
                this.Request        = request;
                this.Dialog         = dialog;
                this.Agent          = agent;
                this.AuthCount      = 0;
                this.ProxyAuthCount = 0;
                this.SipResult      = null;
            }
        }

        /// <summary>
        /// Initiates an asynchronous SIP request.
        /// </summary>
        /// <param name="request">The <see cref="SipRequest" /> to be submitted.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the operation's progress.</returns>
        /// <remarks>
        /// <para>
        /// This method transparently handles any authentication required by the
        /// remote endpoint if the <see cref="SipCoreSettings.UserName" /> and <see cref="SipCoreSettings.Password" />
        /// fields are valid in the <see cref="SipCoreSettings" /> value passed to the
        /// class constructor.
        /// </para>
        /// <para>
        /// All requests to <see cref="BeginRequest(SipRequest,AsyncCallback,object)" /> must be matched with a 
        /// call to <see cref="EndRequest" />.
        /// </para>
        /// <note>
        /// This method adds the <b>Call-ID</b>header if it's not already present.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginRequest(SipRequest request, AsyncCallback callback, object state)
        {
            return BeginRequest(request, null, callback, state);
        }

        /// <summary>
        /// Initiates an asynchronous SIP request.
        /// </summary>
        /// <param name="request">The <see cref="SipRequest" /> to be submitted.</param>
        /// <param name="dialog">The <see cref="SipDialog" /> for requests that initiate a dialog (or <c>null</c>).</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the operation's progress.</returns>
        /// <remarks>
        /// <para>
        /// This method transparently handles any authentication required by the
        /// remote endpoint if the <see cref="SipCoreSettings.UserName" /> and <see cref="SipCoreSettings.Password" />
        /// fields are valid in the <see cref="SipCoreSettings" /> value passed to the
        /// class constructor.
        /// </para>
        /// <para>
        /// All requests to <see cref="BeginRequest(SipRequest,AsyncCallback,object)" /> must be matched with a 
        /// call to <see cref="EndRequest" />.
        /// </para>
        /// <note>
        /// This method adds the <b>Call-ID</b> header if its not already present.
        /// </note>
        /// </remarks>
        private IAsyncResult BeginRequest(SipRequest request, SipDialog dialog, AsyncCallback callback, object state)
        {
            SipClientAgent      agent;
            IAsyncResult        ar;
            RequestAsyncResult  arRequest;

            agent     = GetClientAgent(request);
            arRequest = new RequestAsyncResult(request, dialog, agent, callback, state);

            // Initalize common headers if they're not already present

            if (!request.ContainsHeader(SipHeader.CallID))
                request.AddHeader(SipHeader.CallID, SipHelper.GenerateCallID());

            ar = agent.BeginRequest(request, dialog, onRequest, arRequest);
            arRequest.Started();

            return arRequest;
        }

        /// <summary>
        /// Handles request completions.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnRequest(IAsyncResult ar)
        {
            RequestAsyncResult      arRequest = (RequestAsyncResult)ar.AsyncState;
            SipDialog               dialog    = arRequest.Dialog;
            SipRequest              request;
            SipResult               sipResult;
            SipResponse             response;
            string                  authenticateHeaderName = null;
            string                  authorizeHeaderName    = null;
            SipAuthenticateValue    authValue;
            SipAuthorizationValue   authorizationValue;
            SipHeader               viaHeader;
            bool                    authRequired;
            string                  userName;
            string                  password;

            try
            {
                sipResult = arRequest.Agent.EndRequest(ar);
                response  = sipResult.Response;

                using (TimedLock.Lock(this))
                {
                    request = arRequest.Request.Clone();
                }

                // Handle request resubmission for authentication

                authRequired = false;
                if (response != null && settings.AutoAuthenticate)
                {
                    // $todo(jeff.lill): 
                    //
                    // I'm currently supporting one of each type
                    // of authentication per request.  I'm not
                    // sure this will work.  The proxy might end up 
                    // requiring two authentications, one for each 
                    // request made to the server (the initial 
                    // request and the follow-up one with the
                    // authorization header).

                    if (response.Status == SipStatus.Unauthorized)
                    {
                        authRequired           = arRequest.AuthCount++ == 0;
                        authenticateHeaderName = SipHeader.WWWAuthenticate;
                        authorizeHeaderName    = SipHeader.Authorization;
                    }
                    else if (response.Status == SipStatus.ProxyAuthenticationRequired)
                    {
                        authRequired           = arRequest.ProxyAuthCount++ == 0;
                        authenticateHeaderName = SipHeader.ProxyAuthenticate;
                        authorizeHeaderName    = SipHeader.ProxyAuthorization;
                    }

                    if (authRequired)
                    {
                        // Gather the authentication challenge and the user credentials

                        authValue = response.GetHeader<SipAuthenticateValue>(authenticateHeaderName);
                        userName  = string.IsNullOrWhiteSpace(settings.UserName) ? null : settings.UserName;
                        password  = string.IsNullOrWhiteSpace(settings.UserName) ? null : settings.Password;

                        if (authValue != null && userName != null && password != null)
                        {
                            // Compute the challenge response and add it to the request.

                            authorizationValue = new SipAuthorizationValue(authValue, userName, password, request.MethodText, request.Uri);
                            request.AddHeader(authorizeHeaderName, authorizationValue);

                            // Pop the top "Via" header and initiate another request.

                            viaHeader = request[SipHeader.Via];
                            if (viaHeader != null)
                                viaHeader.RemoveFirst();

                            if (dialog != null)
                            {
                                // We need to increment the CSeq number if the request is
                                // happening in the context of a dialog.

                                request.SetHeader(SipHeader.CSeq, new SipCSeqValue(dialog.GetNextCSeq(), request.MethodText));

                                // The dialog needs to know any authorization headers generated
                                // so it'll be able to add them to the ACK.

                                switch (response.Status)
                                {
                                    case SipStatus.Unauthorized:

                                        dialog.SetAckAuthHeader(authorizationValue);
                                        break;

                                    case SipStatus.ProxyAuthenticationRequired:

                                        dialog.SetAckProxyAuthHeader(authorizationValue);
                                        break;
                                }
                            }

                            arRequest.Agent.BeginRequest(request, arRequest.Dialog, onRequest, arRequest);
                            return;
                        }
                    }
                }

                // No authentication required or not enough information available
                // to perform it.

                arRequest.SipResult = sipResult;
                arRequest.Notify();
            }
            catch (Exception e)
            {
                arRequest.Notify(e);
            }
        }

        /// <summary>
        /// Completes the asynchronous operation begun by a call to <see cref="BeginRequest(SipRequest,AsyncCallback,object)" />.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> returned by <see cref="BeginRequest(SipRequest,AsyncCallback,object)" />.</param>
        /// <returns>The <see cref="SipResult" /> detailing the result of the operation.</returns>
        public SipResult EndRequest(IAsyncResult ar)
        {
            var arRequest = (RequestAsyncResult)ar;

            arRequest.Wait();
            try
            {
                if (arRequest.Exception != null)
                    throw arRequest.Exception;

                return arRequest.SipResult;
            }
            finally
            {
                arRequest.Dispose();
            }
        }

        /// <summary>
        /// Executes a synchronous SIP request.
        /// </summary>
        /// <param name="request">The <see cref="SipRequest" /> to be submitted.</param>
        /// <returns>The <see cref="SipResult" /> detailing the result of the operation.</returns>
        /// <remarks>
        /// This method will block the current thread until the operation completes.
        /// </remarks>
        public SipResult Request(SipRequest request)
        {
            var ar = BeginRequest(request, null, null);

            return EndRequest(ar);
        }

        /// <summary>
        /// Submits a <see cref="SipRequest" /> transaction for processing without providing
        /// any completion notifications.
        /// </summary>
        /// <param name="request">The <see cref="SipRequest" />.</param>
        /// <remarks>
        /// This method is useful for situations like submitting a BYE or CANCEL
        /// transaction, where the transaction result isn't really that important 
        /// to track.
        /// </remarks>
        public void FireAndForget(SipRequest request)
        {
            BeginRequest(request, onFireAndForget, null);
        }

        /// <summary>
        /// Handle's fire-and-forget completions.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" />.</param>
        private void OnFireAndForget(IAsyncResult ar)
        {
            EndRegister(ar);
        }

        //---------------------------------------------------------------------
        // Dialog related methods

        /// <summary>
        /// Initiates an asynchronous operation to establish a dialog with a SIP peer.
        /// </summary>
        /// <param name="request">The <see cref="SipRequest" /> to be used to establish the dialog.</param>
        /// <param name="localContact">The local <b>Contact</b> header value.</param>
        /// <param name="dialogState">Application defined dialog state to be passed to derived dialog classes (or <c>null</c>).</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the operation's progress.</returns>
        /// <remarks>
        /// <para>
        /// This method accepts only <see cref="SipMethod.Invite" /> requests and will initialize the
        /// request's <b>Call-ID</b>, <b>CSeq</b>, and <b>Allow</b> headers to reasonable values if these
        /// are not already set.  The <b>Contact</b> header is set to the <paramref name="localContact" /> parameter 
        /// passed.  The <b>To</b> and <b>From</b> headers must already  be set to reasonable values.  The 
        /// method generates the unique dialog tag and adds it to the <b>From</b> header.
        /// </para>
        /// </remarks>
        public IAsyncResult BeginCreateDialog(SipRequest request, SipContactValue localContact,
                                              object dialogState, AsyncCallback callback, object state)
        {
            SipDialog       dialog;
            SipContactValue vTo;
            SipContactValue vFrom;

            if (isStopPending)
                throw new InvalidOperationException("Cannot create a dialog when the SipCore is in the process of stopping.");

            if (request.Method != SipMethod.Invite)
                throw new ArgumentException("Only INVITE dialogs are currently supported.");

            vTo = request.GetHeader<SipContactValue>(SipHeader.To);
            if (vTo == null)
                throw new ArgumentException("Invalid INVITE: Missing [To] header.");

            vFrom = request.GetHeader<SipContactValue>(SipHeader.From);
            if (vFrom == null)
                throw new ArgumentException("Invalid INVITE: Missing [From] header.");

            if (!request.ContainsHeader(SipHeader.CallID))
                request.AddHeader(SipHeader.CallID, SipHelper.GenerateCallID());

            if (!request.ContainsHeader(SipHeader.CSeq))
                request.AddHeader(SipHeader.CSeq, new SipCSeqValue(SipHelper.GenCSeq(), request.MethodText));

            if (!request.ContainsHeader("Allow"))
                request.SetHeader(SipHelper.AllowDefault);

            // Create the dialog, add the dialog's local tag to the "From" header, and
            // add the dialog to the "early" dialogs table.

            dialog = this.CreateClientDialog(request, localContact, dialogState);

            vFrom["tag"] = dialog.LocalTag;
            request.SetHeader(SipHeader.From, vFrom);

            if (!request.ContainsHeader(SipHeader.Contact))
                request.SetHeader(SipHeader.Contact, localContact);

            // Start the request and save the dialog instance in the async result

            return BeginRequest(request, dialog, callback, state);
        }

        /// <summary>
        /// Completes the asynchronous dialog creation operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginCreateDialog" />.</param>
        /// <returns>The new <see cref="SipDialog" /> instance.</returns>
        /// <remarks>
        /// <note>
        /// This method does not return until the dialog is fully confirmed with the
        /// server or some sort of error is detected.  This method will not return when
        /// the dialog is still in the early state.
        /// </note>
        /// </remarks>
        public SipDialog EndCreateDialog(IAsyncResult ar)
        {
            RequestAsyncResult  arRequest = (RequestAsyncResult)ar;
            SipResult           sipResult;
            SipDialog           dialog;
            string              dialogID;

            sipResult = EndRequest(ar);

            if (sipResult.Dialog == null)
                throw new InvalidOperationException("Dialog expected.");

            using (TimedLock.Lock(this))
            {
                if (!isRunning)
                    throw new InvalidOperationException("Core is no longer running.");

                dialog   = sipResult.Dialog;
                dialogID = dialog.ID;

                dialog.SetDisposition(sipResult.Status, sipResult.Response);
                RemoveEarlyDialog(dialog);

                if (SipHelper.IsProvisional(sipResult.Status))
                {
                    // We should never see this because the dialog should either
                    // be rejected or accepted at this point.

                    Assertion.Fail("Invalid provisional dialog response.");
                }
                else if (SipHelper.IsSuccess(sipResult.Status))
                {
                    // Verify that the dialog has been added to the table.
                    // SipClientAgent is supposed to call OnInviteConfirmed()
                    // which is supposed to take care of this.

                    if (dialogID == null)
                        throw new InvalidOperationException("SipClientAgent didn't compute a dialog ID.");

                    if (!dialogs.ContainsKey(dialogID))
                        throw new InvalidOperationException("Dialog should be present in table.");
                }
                else
                {
                    // Verify that the dialog isn't in the table.  SipClientAgent is 
                    // supposed to call OnInviteFailed() which is supposed to take 
                    // care of this.

                    if (dialogID != null && dialogs.ContainsKey(dialogID))
                        throw new InvalidOperationException("Dialog should not present in table.");
                }
            }

            return dialog;
        }

        /// <summary>
        /// Initiates a synchronous transaction to create a dialog with a remote peer.
        /// </summary>
        /// <param name="request">The <see cref="SipRequest" /> to be used to establish the dialog.</param>
        /// <param name="localContact">The local <b>Contact</b> header value.</param>
        /// <param name="dialogState">Application defined dialog state to be passed to derived dialog classes (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the operation's progress.</returns>
        /// <remarks>
        /// <para>
        /// This method accepts only <see cref="SipMethod.Invite" /> requests and will initialize the
        /// dialog's <b>Call-ID</b> and <b>CSeq</b> headers to reasonable values and <b>Contact</b> to the
        /// <paramref name="localContact" /> parameter passed.  The <b>To</b> and <b>From</b> headers must already 
        /// be set to reasonable values.  The method generates the unique dialog tag and adds it to the 
        /// <b>From</b> header.
        /// </para>
        /// <note>
        /// This method does not return until the dialog is fully confirmed with the
        /// server or some sort of error is detected.  This method will not return when
        /// the dialog is in an early state.
        /// </note>
        /// </remarks>
        public SipDialog CreateDialog(SipRequest request, SipContactValue localContact, object dialogState)
        {
            var ar = BeginCreateDialog(request, localContact, dialogState, null, null);

            return EndCreateDialog(ar);
        }

        //---------------------------------------------------------------------
        // Registration related methods

        /// <summary>
        /// Initiates an asynchronous one-time registration with a SIP registrar.
        /// </summary>
        /// <param name="serviceUri">URI of the SIP registrar service.</param>
        /// <param name="accountUri">The SIP account URI of the entity being registered.</param>
        /// <param name="desiredTTL">The requested lifetime of the registration.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the operation's progress.</returns>
        /// <remarks>
        /// All requests to <see cref="BeginRegister" /> must be matched with a call to <see cref="EndRegister" />.
        /// </remarks>
        public IAsyncResult BeginRegister(string serviceUri, string accountUri, TimeSpan desiredTTL, AsyncCallback callback, object state)
        {
            SipRegisterRequest  request;
            SipUri              sipUri;

            if (!SipUri.TryParse(accountUri, out sipUri))
                throw new ArgumentException("Invalid SIP URI.", "accountUri");

            request = new SipRegisterRequest(serviceUri, accountUri, accountUri, desiredTTL);

            // Initialize the CSeq, Call-ID, and Contact headers for REGISTER requests
            // as necessary.

            request.SetHeader(SipHeader.CSeq, string.Format("{0} {1}", Interlocked.Increment(ref regCSeq), request.MethodText));
            request.SetHeader(SipHeader.CallID, regCallID);
            request.SetHeader(SipHeader.Contact, settings.LocalContact);

            return BeginRequest(request, callback, state);
        }

        /// <summary>
        /// Completes the asynchronous operation begin by a call to <see cref="BeginRegister" />.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> returned by <see cref="BeginRegister" />.</param>
        /// <returns>The <see cref="SipResult" /> detailing the result of the operation.</returns>
        public SipResult EndRegister(IAsyncResult ar)
        {
            return EndRequest(ar);
        }

        /// <summary>
        /// Executes a synchronous one-time SIP REGISTER request.
        /// </summary>
        /// <param name="serviceUri">URI of the SIP registrar service.</param>
        /// <param name="accountUri">The SIP account URI of the entity being registered.</param>
        /// <param name="desiredTTL">The requested lifetime of the registration.</param>
        /// <returns>The <see cref="SipResult" /> detailing the result of the operation.</returns>
        /// <remarks>
        /// This method will block the current thread until the operation completes.
        /// </remarks>
        public SipResult Register(string serviceUri, string accountUri, TimeSpan desiredTTL)
        {
            var ar = BeginRegister(serviceUri, accountUri, desiredTTL, null, null);

            return EndRegister(ar);
        }

        /// <summary>
        /// Updates the internal registration state, firing <see cref="RegistrationChanged" />
        /// as necessary.
        /// </summary>
        /// <param name="autoRegistration">The new automatic registration state.</param>
        /// <param name="isRegistered">The current registration state.</param>
        private void SetRegState(bool autoRegistration, bool isRegistered)
        {
            bool raiseEvent = false;

            if (this.autoRegistration != autoRegistration)
                raiseEvent = true;

            if (this.isRegistered != autoRegistration && isRegistered)
                raiseEvent = true;

            this.autoRegistration = autoRegistration;
            this.isRegistered     = autoRegistration && isRegistered;

            if (raiseEvent && RegistrationChanged != null)
                RegistrationChanged(this, new SipRegistrationStateArgs(isRegistered, autoRegistration, registrarUri, accountUri, regTimer.Interval));
        }

        /// <summary>
        /// Initiates a persistent registration with a SIP registrar.
        /// </summary>
        /// <param name="registrarUri">The registrar URI.</param>
        /// <param name="accountUri">The SIP account URI of the entity being registered.</param>
        /// <remarks>
        /// <note>
        /// This method requires that <see cref="SipCoreSettings.AutoAuthenticate" /> be set to <c>true</c>.
        /// </note>
        /// <para>
        /// The core will maintain the registration by submitting periodic 
        /// REGISTER requests to the registrar on a background thread.
        /// Call <see cref="StopAutoRegistration" /> to stop this.  The
        /// <see cref="AutoRegistration" /> property returns <c>true</c>
        /// whenever automatic registration is enabled.
        /// </para>
        /// <para>
        /// This method does does not return an error code or throw an exception
        /// if the registration could not be completed immediately.  Instead,
        /// the core will continue to peridically retry the operation on a
        /// background thread.  You can use the <see cref="IsRegistered" />
        /// property to determine if the core is currently registered
        /// (the last registration attempt succeeded).  You can also enlist
        /// in the <see cref="RegistrationChanged" /> event which will be
        /// raised whenever the registration status changes.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="SipCoreSettings.AutoAuthenticate" /> is <c>false</c>.</exception>
        public void StartAutoRegistration(string registrarUri, string accountUri)
        {
            SipResult   result;

            if (autoRegistration)
                StopAutoRegistration();

            this.registrarUri = registrarUri;
            this.accountUri   = accountUri;

            result = Register(registrarUri, accountUri, TimeSpan.FromMinutes(1));
            if (result.Status != SipStatus.OK)
            {

                regTimer.Interval = TimeSpan.FromMinutes(1);
                SetRegState(true, false);
                return;
            }

            // Set the refresh interval to 90% of the Expires time returned
            // by the registrar or 1 minute.

            TimeSpan    interval = TimeSpan.FromMinutes(1);
            SipHeader   expires;
            int         seconds;

            expires = result.Response[SipHeader.Expires];
            if (expires != null && int.TryParse(expires.Text, out seconds))
                interval = TimeSpan.FromSeconds((double)seconds * 0.90);

            regTimer.Interval = interval;
            SetRegState(true, true);
        }

        /// <summary>
        /// Stops automatic registration.
        /// </summary>
        public void StopAutoRegistration()
        {
            if (!autoRegistration)
                return;

            SetRegState(false, false);
            Register(registrarUri, accountUri, TimeSpan.Zero);
        }

        /// <summary>
        /// Returns <c>true</c> if the core is currently registered with a SIP registrar.
        /// </summary>
        public bool IsRegistered
        {
            get { return isRegistered && autoRegistration; }
        }

        /// <summary>
        /// Returns <c>true</c> is automatic registration is currently enabled.
        /// </summary>
        public bool AutoRegistration
        {
            get { return autoRegistration; }
        }

        /// <summary>
        /// This will be called by <see cref="OnBkTask" /> to handle the automatic registration
        /// activities.
        /// </summary>
        private void AutoRegister()
        {
            // Handle registration renewals by kicking off another thread.

            if (autoRegistration && regTimer.HasFired)
            {
                Thread thread;

                regTimer.Disable();
                thread = new Thread(new ThreadStart(delegate()
                {
                    TimeSpan    interval = TimeSpan.FromMinutes(1);
                    bool        success  = false;
                    SipResult   result;

                    try
                    {
                        result = Register(registrarUri, accountUri, TimeSpan.FromMinutes(1));
                        if (result.Status != SipStatus.OK)
                        {
                            regTimer.Interval = TimeSpan.FromMinutes(1);
                            return;
                        }

                        // Set the refresh interval to 90% of the Expires time returned
                        // by the registrar or 1 minute.

                        SipHeader expires;
                        int seconds;

                        expires = result.Response[SipHeader.Expires];
                        if (expires != null && int.TryParse(expires.Text, out seconds))
                            interval = TimeSpan.FromSeconds((double)seconds * 0.90);

                        success = true;
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e);
                    }
                    finally
                    {
                        regTimer.Interval = interval;
                        SetRegState(autoRegistration, success);
                    }
                }));

                thread.Start();
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
