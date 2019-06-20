//-----------------------------------------------------------------------------
// FILE:        MsgRouter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Manages the discovery of other routers as well as the routing
//              of messages between them.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Messaging.Internal;
using LillTek.Net.Broadcast;
using LillTek.Net.Sockets;

// $todo(jeff.lill): 
//
// Enhance MsgRouterMetrics to support additional metrics such as
// message send/receive and byte counts and then have the class
// write this to Windows performance counters.

namespace LillTek.Messaging
{
    /// <summary>
    /// Defines the delegate that will be called by a <see cref="MsgRouter" />
    /// instance when a message is received by thr router.
    /// </summary>
    /// <param name="msg">The received message.</param>
    /// <returns><c>true</c> if the message was successfully dispatched.</returns>
    public delegate bool MsgReceiveDelegate(Msg msg);

    /// <summary>
    /// Manages the discovery of other routers as well as the routing
    /// of messages between them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class implements the lowest level of message routing, routing between
    /// known network endpoints (IP address/port).  Derived implementations may
    /// implement more advanced routing schemes by overriding the RouteXXX()
    /// methods.
    /// </para>
    /// <para>
    /// The <b>SyncRoot</b> property is provided as a convienent object for implementing
    /// thread synchronization.  This property defaults to the <see cref="MsgRouter" />
    /// instance but can be set to another object instance.  Note that since many
    /// classes cache the <see cref="MsgRouter.SyncRoot" /> property, this value
    /// cannot be changed after it has been referenced.  This means that applications
    /// that need to customize the syncroot will need to do this very early, probably
    /// just after the router is instantiated.
    /// </para>
    /// <para>
    /// Each router opens up to three sockets on startup: one listening TCP 
    /// socket, one UDP socket used for unicast transmissions and (optionally) one
    /// UDP socket for used multicasting.  The TCP endpoints for the listening and
    /// unicast sockets can be set to specific network interfaces and ports or these
    /// values can be selected by the operating system.  The multicast socket's
    /// endpoint should be set to a specific multicast group address and port.
    /// </para>
    /// <para>
    /// MsgRouter is designed so that multiple instances of the router on the same
    /// or different machines will use the multicast socket for discovery purposes
    /// and use the remaining two sockets for point-to-point communication.
    /// </para>
    /// <para><b><u>Message Framing and Encryption</u></b></para>
    /// <para>
    /// The MsgRouter, <see cref="TcpChannel" />, and <see cref="UdpChannel" />
    /// classes implement a simple shared key encryption scheme.  When each 
    /// MsgRouter instance is instantiated, its constructor must be passed the 
    /// name of the encryption algorithm along with the encryption key and 
    /// initialization vector.
    /// </para>
    /// <para>
    /// Messages sent via the router will be serialized into a byte array via the
    /// <see cref="Msg.Save" /> method and then the resulting bytes will be packed into an 
    /// encrypted frame for transmission.  This frame is lays out as shown below:
    /// </para>
    /// <code language="none">
    /// +----------------+
    /// |  Frame Length  |      4 bytes in the clear in network order
    /// +----------------+
    /// |                |
    /// |                |
    /// |   Serialized   |
    /// |   Message      |      Serialized message bytes
    /// |   Bytes        |
    /// |                |
    /// |                |
    /// +----------------+
    /// |      SALT      |      4 bytes of cryptographic salt
    /// +----------------+
    /// </code>
    /// <para>
    /// The Frame length specifies the number of bytes of encrypted message and salt 
    /// data to follow.  This is transmitted in the clear.  The salt and message bytes
    /// are encrypted together.
    /// </para>
    /// <para><b><u>TCP Channel Implementation Note</u></b></para>
    /// <para>
    /// The current implementation of the messaging library assumes that messaging node
    /// discovery will be occur via UDP multicast messages and that no messaging node
    /// will be behind a NAT.  During this discovery, nodes will be identified by their
    /// the IP address and port implicit in the UDP packet and by the listening TCP port
    /// specified in the body of the message itself.
    /// </para>
    /// <para>
    /// Although it is possible for nodes to transmit messages via UDP, the typical
    /// mode of application message transport will be via TCP.
    /// </para>
    /// <para>
    /// TCP connections between nodes are not established until a message is sent
    /// from one node to another.  At this time, the sending node will establish a
    /// TCP connection to the receiving node.  Before sending the message, each side
    /// of the connection will transmit an initialization message to the other side
    /// specifying the port each is listening on.  This information will be used by
    /// each node to implement a map that allows future messages to each node to be
    /// routed over this connection.  Finally, after all of this is accomplished,
    /// the message is delivered over the connection.
    /// </para>
    /// <para>
    /// TCP channels accepted on the listening socket are added to the pendingChannels
    /// list.  These channels are waiting for notification of the reception of the
    /// internal channel initialization message via a call to <see cref="MsgRouter.OnTcpInit" />.
    /// This method will add the channel to the hash table of open TCP channels and remove 
    /// the channel from the pendingChannels list.  Note that is is possible in rare
    /// circumstances for a TCP channel with the same IP address/port to already be
    /// present in the hash table.  This can happen if both nodes establish simultanious
    /// connections to the other.  In this case, the duplicate channel will remain in
    /// the pendingChannels list so that it can be tracked and tested for idleness.
    /// </para>
    /// <note>
    /// Only inbound channels are actually added to the pendingTcp list since
    /// connecting channels already know the listening port of the remote node.
    /// </note>
    /// <para><b><u>The Application Global Router</u></b></para>
    /// <para>
    /// The <see cref="MsgRouter" /> class exposes a few static members that
    /// are used to maintain a reference to a global router that can be used
    /// across the application.  The <see cref="Global" /> returns a reference
    /// to the global application router if one has been started, <c>null</c>
    /// otherwise.  This property will be automatically set to the first router
    /// explicitly created or started or <see cref="StartGlobal" /> can be called
    /// to create a <see cref="LeafRouter" />.
    /// </para>
    /// <para>
    /// <see cref="StopGlobal" /> is used to close the global router.  Calls to
    /// <see cref="StartGlobal()" /> and <see cref="StopGlobal" /> can be nested,
    /// with the open method creating a new router only if one doesn't already 
    /// exist.  A reference count is incremented by every call to <see cref="StartGlobal()" />
    /// and is decremented with every call to <see cref="StopGlobal" />.  The
    /// router isn't closed until the reference count goes to zero.  This means
    /// that every call to <see cref="StartGlobal()" /> must eventually be matched
    /// with a single call to <see cref="StopGlobal" />.
    /// </para>
    /// <para>
    /// The <see cref="SetGlobal(MsgRouter)" /> method can also be used to set
    /// an explicit global router.  This call must also eventually be matched with a call
    /// to <see cref="StopGlobal" />.
    /// </para>
    /// <para>
    /// The main purpose for all of this is so that code such as the LillTek
    /// WCF transport channels can be implemented without requiring the application
    /// consuming these transports to have to know anything about LillTek messaging.
    /// </para>
    /// </remarks>
    public class MsgRouter : ILockable
    {
        //---------------------------------------------------------------------
        // Constants

        /// <summary>
        /// Default socket buffer size.
        /// </summary>
        internal const int DefSockBufSize = 64 * 1024;

        /// <summary>
        /// <see cref="NetTrace" /> subsystem name.
        /// </summary>
        public const string TraceSubsystem = "LillTek.Messaging";

        /// <summary>
        /// Size of the message frame header in bytes. 
        /// </summary>
        internal const int FrameHeaderSize = 4;

        private const string msgAlreadyStarted      = "Router already started.";
        private const string msgNotStarted          = "Router not started.";
        private const string msgNoMulticast         = "Multicast disabled for this router.";
        private const string msgNullToEP            = "Target endpoint is null.";
        private const string msgReplyToNull         = "Reply endpoint is null.";
        private const string msgNonLogicalBroadcast = "Broadcast allowed only for logical endpoints.";

        //---------------------------------------------------------------------
        // Static members

        private static MsgRouter    globalRouter    = null;
        private static object       globalSyncRoot = new object();
        private static int          globalRefCount = 0;

        /// <summary>
        /// Returns the global application message router (or <c>null</c>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This provides a convenient way to access the application's global
        /// message router.  This is initialized whenever a message router is
        /// started and is reset to <c>null</c> when the router is stopped.
        /// </para>
        /// <note>
        /// If multiple routers are started by the application then this will
        /// reference the first router started.
        /// </note>
        /// </remarks>
        public static MsgRouter Global
        {
            get { return globalRouter; }
        }

        /// <summary>
        /// Used to make a specific router global.
        /// </summary>
        /// <param name="router">The router.</param>
        /// <remarks>
        /// <note>
        /// This method does nothing except increment the global reference count if
        /// a global router has already been set.
        /// </note>
        /// <note>
        /// This must be matched eventually with a call to <see cref="StopGlobal" />.
        /// </note>
        /// </remarks>
        public static void SetGlobal(MsgRouter router)
        {
            lock (globalSyncRoot)
            {
                if (globalRefCount == 0)
                    globalRouter = router;

                globalRefCount++;
            }
        }

        /// <summary>
        /// Starts a new global <see cref="LeafRouter" /> if one isn't already open, incrementing
        /// an internal reference count.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This must be matched eventually with a call to <see cref="StopGlobal" />.
        /// </note>
        /// </remarks>
        public static void StartGlobal()
        {
            lock (globalSyncRoot)
            {
                if (globalRefCount == 0)
                {
                    LeafRouter router;

                    router = new LeafRouter();
                    router.Start();

                    globalRouter = router;
                }

                globalRefCount++;
            }
        }

        /// <summary>
        /// Decrements a the global router reference count, stopping the
        /// router if the count goes to zero.
        /// </summary>
        public static void StopGlobal()
        {
            lock (globalSyncRoot)
            {
                if (globalRefCount == 0)
                    throw new InvalidOperationException("Global router reference count underflow.  StopGlobal() call does not match a previous StartGlobal() call.");

                globalRefCount--;
                if (globalRefCount == 0)
                {

                    globalRouter.Stop();
                    globalRouter = null;
                }
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private object                              syncLock;               // Used to sync thread access
        private bool                                syncRootRef;            // Set to true the first time SyncRoot has been referenced
        private string                              appName;                // The application's name
        private string                              appDescription;         // The application description
        private DiscoveryMode                       discoveryMode;          // Specifies how router discovery is performed: multicast or UDP-Broadcast 
        private SocketConfig                        tcpSockConfig;          // TCP socket config info
        private SocketConfig                        udpUniSockConfig;       // UDP unicast socket config info
        private SocketConfig                        udpMultiSockConfig;     // UDP multicast socket config info
        private bool                                isOpen;                 // True if the router is open
        private bool                                tcpDelay;               // True if Nagel is to be enabled
        private IPAddress                           activeAdapter;          // IP address of the active network adapter
        private IPAddress                           cloudAdapter;           // IP address of the NIC to be used for multicasting router
                                                                            // discovery messages or ANY if the first active
                                                                            // NIC is to be used
        private MsgEP                               routerEP;               // Physical endpoint of this router (or null)
        private LocalEPMap                          localEPMap;             // Set of logical endpoints to be optimized for local routing
        private MsgRouterInfo                       routerInfo;             // Router capability information (or null)
        private IPEndPoint                          cloudEP;                // The discovery UDP multicast endpoint
        private UdpBroadcastClientSettings          broadcastSettings;      // UDP broadcast settings for DiscoveryMode=UDP-BROADCAST
        private IPEndPoint                          udpEP;                  // The UDP endpoint for this instance
        private IPEndPoint                          tcpEP;                  // The listening TCP endpoint
        private UdpChannel                          udpMulticast;           // The UDP multicast channel (or null)
        private UdpChannel                          udpUnicast;             // The UDP unicast channel
        private int                                 udpMsgQueueCountMax;    // Max queued outbound messages per UDP channel
        private int                                 udpMsgQueueSizeMax;     // Max bytes of serialized queued outbound messages per UDP channel
        private EnhancedSocket                      sockListen;             // The listening TCP socket
        private Dictionary<IPEndPoint, TcpChannel>  tcpChannels;            // TCP channels keyed by remote network endpoint
        private int                                 tcpMsgQueueCountMax;    // Max queued outbound messages per TCP channel
        private int                                 tcpMsgQueueSizeMax;     // Max bytes of serialized queued outbound messages per TCP channel
        private List<TcpChannel>                    tcpPending;             // See the TCP channel implemenation note below
        private AsyncCallback                       onAccept;               // Called when an async Accept() completes
        private WaitCallback                        onProcessMsg;           // Called to begin processing of a received message
        private BlockEncryptor                      encryptor;              // The message frame encryption
        private BlockDecryptor                      decryptor;              // and decryption objects
        private IMsgDispatcher                      dispatcher;             // The message dispatcher
        private LimitedThreadPool                   threadPool;             // Message handler thread pool
        private MsgReceiveDelegate                  onReceive;              // The message receive callback
        private TimeSpan                            maxIdle;                // Max time a TCP channel can be idle before being closed
        private TimeSpan                            bkInterval;             // Background timer interval
        private GatedTimer                          bkTimer;                // Background task timer
        private int                                 defMsgTTL;              // Default message TTL
        private ISessionManager                     sessionMgr;             // Associated session manager (or null)
        private TimeSpan                            sessionCacheTime;       // Default session cache time
        private int                                 sessionRetries;         // Maximum session initiation retries.
        private TimeSpan                            sessionTimeout;         // Default session timeout
        private bool                                enableP2P;              // True to enable peer-to-peer routing between leaves
        private TimeSpan                            physRouteTTL;           // Max physical route lifetime
        private PhysicalRouteTable                  physRoutes;             // Physical routing table
        private LogicalRouteTable                   logicalRoutes;          // Logical routing table
        private int                                 logicalChangeID;        // The last known logical route table change ID
        private WaitCallback                        onLogicalChange;        // Handles the firing of queued LogicalChange events
        private int                                 maxAdvertiseEPs;        // Maximum number of logical endpoints to include in LogicalAdvertiseMsg
        private TimeSpan                            deadRouterTTL;          // Maximum time to wait for a ReceiptMsg before signalling a dead router (0 to disable)
        private MsgTracker                          msgTracker;             // Implements message receipt tracking
        private AsyncCallback                       onQueryDone;            // Handles query completions
        private AsyncCallback                       onParallelQueryDone;    // Handles parallel query completions
        private MsgRouterMetrics                     metrics;                // Performance related metrics
        private bool                                idleCheck;              // Controls whether idled TCP channels will be closed
        private bool                                fragmentTcp;            // True to force 1 byte TCP buffers
        private bool                                isPaused;               // True if the router is currently paused

        //-------------------------------------------------
        // TCP Channel Implementation Notes
        //
        // The current implementation of the messaging library assumes that messaging node
        // discovery will be occur via UDP multicast messages and that no messaging node
        // will be behind a NAT.  During this discovery, nodes will be identified by their
        // the IP address and port implicit in the UDP packet and by the listening TCP port
        // specified in the body of the message itself.
        //
        // Although it is possible for nodes to transmit messages via UDP, the typical
        // mode of application message transport will be via TCP.
        //
        // TCP connections between nodes are not established until a message is sent
        // from one node to another.  At this time, the sending node will establish a
        // TCP connection to the receiving node.  Before sending the message, each side
        // of the connection will transmit an initialization message to the other side
        // specifying the port each is listening on.  This information will be used by
        // each node to implement a map that allows future messages to each node to be
        // routed over this connection.  Finally, after all of this is accomplished,
        // the message is delivered over the connection.
        //
        // TCP channels accepted on the listening socket are added to the pendingChannels
        // list.  These channels are waiting for notification of the reception of the
        // internal channel initialization message via a call to OnTcpInit().  This method
        // will add the channel to the hash table of open TCP channels and remove the
        // channel from the pendingChannels list.  Note that is is possible in rare
        // circumstances for a TCP channel with the same IP address/port to already be
        // present in the hash table.  This can happen if both nodes establish simultanious
        // connections to the other.  In this case, the duplicate channel will remain in
        // the pendingChannels list so that it can be tracked and tested for idleness.
        //
        // Note that only inbound channels are actually added to the pendingTcp list since
        // connecting channels already know the listening port of the remote node.

        /// <summary>
        /// This constructor initializes the router so that all received messages will be
        /// routed to the onReceive delegate passed.
        /// </summary>
        /// <param name="onReceive">The received message handler (or <c>null</c>).</param>
        public MsgRouter(MsgReceiveDelegate onReceive)
        {
            var defSettings = new RouterSettings("physical://null");

            this.syncLock            = this;
            this.syncRootRef         = false;
            this.appName             = Helper.EntryAssemblyFile;
            this.appDescription      = null;
            this.tcpSockConfig       = new SocketConfig(DefSockBufSize, DefSockBufSize);
            this.udpUniSockConfig    = new SocketConfig(DefSockBufSize, DefSockBufSize);
            this.udpMultiSockConfig  = new SocketConfig(DefSockBufSize, DefSockBufSize);
            this.udpMsgQueueCountMax = defSettings.UdpMsgQueueCountMax;
            this.udpMsgQueueSizeMax  = defSettings.UdpMsgQueueSizeMax;
            this.isOpen              = false;
            this.tcpDelay            = false;
            this.routerEP            = null;
            this.localEPMap          = new LocalEPMap();
            this.routerInfo          = null;
            this.udpMulticast        = null;
            this.udpUnicast          = null;
            this.sockListen          = null;
            this.tcpChannels         = null;
            this.tcpMsgQueueCountMax = defSettings.TcpMsgQueueCountMax;
            this.tcpMsgQueueSizeMax  = defSettings.TcpMsgQueueSizeMax;
            this.tcpPending          = null;
            this.onReceive           = onReceive;
            this.threadPool          = new LimitedThreadPool();
            this.encryptor           = null;
            this.decryptor           = null;
            this.bkInterval          = TimeSpan.FromSeconds(1.0);
            this.defMsgTTL           = 5;
            this.sessionMgr          = null;
            this.sessionCacheTime    = TimeSpan.FromMinutes(2.0);
            this.sessionRetries      = 3;
            this.SessionTimeout      = TimeSpan.FromSeconds(10.0);
            this.enableP2P           = true;
            this.physRoutes          = null;
            this.logicalRoutes       = null;
            this.logicalChangeID     = 0;
            this.onLogicalChange     = new WaitCallback(OnLogicalChange);
            this.maxAdvertiseEPs     = 256;
            this.deadRouterTTL       = TimeSpan.FromSeconds(2.0);
            this.msgTracker          = new MsgTracker(this);
            this.onQueryDone         = new AsyncCallback(OnQueryDone);
            this.onParallelQueryDone = new AsyncCallback(OnParallelQueryDone);
            this.metrics             = new MsgRouterMetrics();
            this.idleCheck           = true;
            this.fragmentTcp         = false;
            this.isPaused            = false;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public MsgRouter()
            : this((MsgReceiveDelegate)null)
        {
        }

        /// <summary>
        /// This constructor initializes the router so that all received messages will be
        /// routed through the MsgDispatcher object passed.
        /// </summary>
        /// <param name="dispatcher">The message dispatcher.</param>
        public MsgRouter(IMsgDispatcher dispatcher)
            : this(new MsgReceiveDelegate(dispatcher.Dispatch))
        {
            this.dispatcher = dispatcher;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~MsgRouter()
        {
            Stop();
        }

        /// <summary>
        /// This event is raised whenever a change to the router's logical route table
        /// has been detected.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This detection happens periodically during the router's background task
        /// processing.  This means that this event will not necessarily be raised for 
        /// every separate table modification.
        /// </para>
        /// <note>
        /// The event will be raised on a separate thread pool thread.
        /// </note>
        /// </remarks>
        public event MethodDelegate LogicalRouteChange;

        /// <summary>
        /// Handles the firing of queued LogicalRouteChange events.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void OnLogicalChange(object state)
        {
            if (LogicalRouteChange != null)
                LogicalRouteChange();
        }

        /// <summary>
        /// Associates a message dispatcher with the router if one is not
        /// already associated.
        /// </summary>
        /// <param name="dispatcher">The message dispatcher.</param>
        /// <remarks>
        /// Received messages are routed through this dispatcher to
        /// method instances marked with <c>[MsgHandler]</c>.  Derived router
        /// classes will typically establish a dispatcher that dispatches
        /// messages to themselves.
        /// </remarks>
        protected void SetDispatcher(IMsgDispatcher dispatcher)
        {
            using (TimedLock.Lock(this.syncLock))
            {
                if (this.dispatcher != null)
                    return;

                this.dispatcher = dispatcher;
                this.onReceive = new MsgReceiveDelegate(dispatcher.Dispatch);
            }
        }

        /// <summary>
        /// Returns the message dispatcher associated with this router, creating
        /// a default dispatcher if one hasn't yet been set.
        /// </summary>
        public IMsgDispatcher Dispatcher
        {
            get
            {
                using (TimedLock.Lock(this.syncLock))
                {
                    if (dispatcher == null)
                        SetDispatcher(new MsgDispatcher(this));

                    return dispatcher;
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="LimitedThreadPool" /> instance to be used for
        /// queuing messaging related tasks to the underlying .NET thread pool.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A <see cref="LimitedThreadPool" /> instance is used rather than using
        /// <see cref="Helper.UnsafeQueueUserWorkItem" /> due to the fact that its
        /// entirely possible for the .NET thread pool to be overwhelmed with queued
        /// tasks if messages are being received for processing faster than they
        /// can be executed.  This situation can result in application termination
        /// due to <see cref="OutOfMemoryException" />s.
        /// </para>
        /// <para>
        /// This queue enforces reasonable limits on the number of tasks that can
        /// be queued at any one time.  When this limit is exceeded, the tasks at
        /// the front of the queue will be discarded.  The idea here is that these
        /// tasks represent the oldest messages held by the system and that the
        /// operations associated with these message may have already been resubmitted
        /// due to being delayed long enough to initiate a timeout/retry.
        /// </para>
        /// </remarks>
        internal LimitedThreadPool ThreadPool
        {
            get { return threadPool; }
        }

        /// <summary>
        /// Returns the message router performance metrics related information.
        /// </summary>
        public MsgRouterMetrics Metrics
        {
            get { return metrics; }
        }

        /// <summary>
        /// The object to be used to synchronize thread access to the MsgRouter and
        /// its associated channels.
        /// </summary>
        /// <remarks>
        /// This defaults to the router object instance.  If a different object is desired,
        /// set this property before calling <see cref="Start" />.
        /// </remarks>
        public object SyncRoot
        {
            get
            {
                syncRootRef = true;
                return syncLock;
            }

            set
            {
#if DEBUG
                SysLog.LogWarning("Type [{0}] does not implement [ILockable].\r\nConsider implementing this this for better deadlock diagnostics.", value.GetType().FullName);
#endif
                if (syncRootRef && !object.ReferenceEquals(syncLock, value))
                    throw new InvalidOperationException("MsgRouter.SyncRoot cannot be modified after it has been referenced for the first time.");

                syncLock = value;
            }
        }

        /// <summary>
        /// The name of the application hosting the router.
        /// </summary>
        /// <remarks>
        /// This defaults to the name of the entry assembly file.  If another name is desired
        /// then set this property before calling <see cref="Start" />.
        /// </remarks>
        public string AppName
        {
            get { return appName; }
            set { appName = value == null ? "(unknown)" : value; }
        }

        /// <summary>
        /// A brief description of the application hosting the router.
        /// </summary>
        /// <remarks>
        /// This defaults to an empty string.  If another value is desired
        /// then set this property before calling <see cref="Start" />.
        /// </remarks>
        public string AppDescription
        {
            get { return appDescription; }
            set { appDescription = value == null ? string.Empty : value; }
        }

        /// <summary>
        /// Specifies how the router will go about discovering other routers on the
        /// network.  The possible values are <b>MULTICAST</b> (the default) and
        /// <b>UDPBROADCAST</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If <b>MULTICAST</b> is specified then the router will broadcast and listen
        /// for presence packets on the specified <see cref="RouterSettings.CloudAdapter"/> for the 
        /// <see cref="RouterSettings.CloudEP" /> multicast endpoint.
        /// </para>
        /// <para>
        /// If <b>UDPBROADCAST</b> is specified then the router will use the LillTek
        /// Broadcast Server to handle the transmission and reception presence packets
        /// on networks that don't support multicast.  The <see cref="BroadcastSettings" />
        /// property can be used to configure the internal <see cref="UdpBroadcastClient" />
        /// used to manage these broadcasts.
        /// </para>
        /// </remarks>
        public DiscoveryMode DiscoveryMode
        {
            get { return discoveryMode; }
            set { discoveryMode = value; }
        }

        /// <summary>
        /// Settings for the <see cref="UdpBroadcastClient" /> used to manage the precence
        /// packets used for router discovery when operating in <see cref="LillTek.Messaging.DiscoveryMode.UdpBroadcast "/>
        /// discovery mode.  This is initialized with reasonable default values.
        /// </summary>
        public UdpBroadcastClientSettings BroadcastSettings
        {
            get { return broadcastSettings; }
            set { broadcastSettings = value; }
        }

        /// <summary>
        /// This router's physical endpoint (or <c>null</c>).
        /// </summary>
        public MsgEP RouterEP
        {
            get { return routerEP; }

            set
            {
                if (value != null && !value.IsPhysical)
                    throw new ArgumentException("RouterEP must be a physical endpoint.");

                routerEP = value;
            }
        }

        /// <summary>
        /// Returns the table of logical endpoints where messages are to be
        /// routed automatically to the closest physical endpoint.
        /// </summary>
        public LocalEPMap LocalEPMap
        {
            get { return localEPMap; }
            set { localEPMap = value; }
        }

        /// <summary>
        /// Returns router's capability information.  This will be initialized after the router
        /// has been started.
        /// </summary>
        public MsgRouterInfo RouterInfo
        {
            get
            {
                if (routerInfo == null && isOpen)
                    routerInfo = new MsgRouterInfo(this);

                return routerInfo;
            }
        }

        /// <summary>
        /// Controls the Nagle packet coalescing algorithm on the TCP channels.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Setting this to true enables the Nagle algorithm on all subsequently 
        /// established TCP connections.  Changing this value will not impact
        /// the status of existing TCP connections.
        /// </para>
        /// <para>
        /// This value defaults to false.
        /// </para>
        /// </remarks>
        public bool TcpDelay
        {
            get
            {
                using (TimedLock.Lock(this.syncLock))
                    return tcpDelay;
            }

            set
            {
                using (TimedLock.Lock(this.syncLock))
                    tcpDelay = value;
            }
        }

        /// <summary>
        /// Sets the configuration information for the TCP socket
        /// connections established by the router.
        /// </summary>
        /// <remarks>
        /// This must be set before calling <see cref="Start" />.
        /// </remarks>
        public SocketConfig TcpSockConfig
        {
            get { return tcpSockConfig; }
            set { tcpSockConfig = value; }
        }

        /// <summary>
        /// Sets the configuration information for the UDP unicast
        /// sockets opened by this router.
        /// </summary>
        /// <remarks>
        /// This must be set before calling <see cref="Start" />.
        /// </remarks>
        public SocketConfig UdpUnicastSockConfig
        {
            get { return udpUniSockConfig; }
            set { udpUniSockConfig = value; }
        }

        /// <summary>
        /// Sets the configuration information for the UDP multicast
        /// sockets opened by this router.
        /// </summary>
        /// <remarks>
        /// This must be set before calling <see cref="Start" />.
        /// </remarks>
        public SocketConfig UdpMulticastSockConfig
        {
            get { return udpMultiSockConfig; }
            set { udpMultiSockConfig = value; }
        }

        /// <summary>
        /// Sets the maximum number of outbound normal priority messages that will be queued by
        /// UDP channels before messages will be discarded.
        /// </summary>
        public int UdpMsgQueueCountMax
        {
            get { return udpMsgQueueCountMax; }

            set
            {
                udpMsgQueueCountMax = value;
                using (TimedLock.Lock(syncLock))
                {
                    udpMsgQueueCountMax = value;

                    if (udpMulticast != null)
                        udpMulticast.SetQueueLimits(udpMsgQueueCountMax, udpMsgQueueSizeMax);

                    if (udpUnicast != null)
                        udpUnicast.SetQueueLimits(udpMsgQueueCountMax, udpMsgQueueSizeMax);
                }
            }
        }

        /// <summary>
        /// Sets the maximum bytes of serialized of outbound normal priority messages that will be queued by
        /// UDP channels before messages will be discarded.
        /// </summary>
        public int UdpMsgQueueSizeMax
        {
            get { return udpMsgQueueSizeMax; }

            set
            {
                using (TimedLock.Lock(syncLock))
                {
                    udpMsgQueueSizeMax = value;

                    if (udpMulticast != null)
                        udpMulticast.SetQueueLimits(udpMsgQueueCountMax, udpMsgQueueSizeMax);

                    if (udpUnicast != null)
                        udpUnicast.SetQueueLimits(udpMsgQueueCountMax, udpMsgQueueSizeMax);
                }
            }
        }

        /// <summary>
        /// Sets the maximum number of outbound normal priority messages that will be queued by
        /// TCP channels before messages will be discarded.
        /// </summary>
        public int TcpMsgQueueCountMax
        {
            get { return tcpMsgQueueCountMax; }

            set
            {
                using (TimedLock.Lock(syncLock))
                {
                    tcpMsgQueueCountMax = value;

                    if (tcpChannels != null)
                    {
                        foreach (TcpChannel channel in tcpChannels.Values)
                            channel.SetQueueLimits(tcpMsgQueueCountMax, tcpMsgQueueSizeMax);
                    }
                }
            }
        }

        /// <summary>
        /// Sets the maximum bytes of serialized of outbound normal priority messages that will be queued by
        /// TCP channels before messages will be discarded.
        /// </summary>
        public int TcpMsgQueueSizeMax
        {
            get { return tcpMsgQueueSizeMax; }

            set
            {
                using (TimedLock.Lock(syncLock))
                {
                    tcpMsgQueueSizeMax = value;

                    if (tcpChannels != null)
                    {
                        foreach (TcpChannel channel in tcpChannels.Values)
                            channel.SetQueueLimits(tcpMsgQueueCountMax, tcpMsgQueueSizeMax);
                    }
                }
            }
        }

        /// <summary>
        /// The interval at which the background timer is raised.
        /// </summary>
        /// <remarks>
        /// This defaults to 1 seconds.  This must be set before calling <see cref="Start" />.
        /// </remarks>
        public TimeSpan BkInterval
        {
            get { return bkInterval; }
            set { bkInterval = value; }
        }

        /// <summary>
        /// The default time the router's session manager will cache itempotent
        /// session replies.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This value can be overridden on a message handler basis by specifying
        /// a value for the [MsgSession(SessionCacheTime="3m"] attribute.
        /// </para>
        /// <para>
        /// This value defaults to 2 minutes and must be set before calling <see cref="Start" />.
        /// </para>
        /// </remarks>
        public TimeSpan SessionCacheTime
        {
            get { return sessionCacheTime; }
            set { sessionCacheTime = value; }
        }

        /// <summary>
        /// The default number of query/response session retries.
        /// </summary>
        /// <remarks>
        /// This defaults to 3 retries.  This must be set before calling <see cref="Start" />.
        /// </remarks>
        public int SessionRetries
        {
            get { return sessionRetries; }
            set { sessionRetries = value; }
        }

        /// <summary>
        /// The maximum time a server side session will be tracked without
        /// explicitly refreshing itself.
        /// </summary>
        /// <remarks>
        /// This defaults to 10 seconds.  This must be set before calling <see cref="Start" />.
        /// </remarks>
        public TimeSpan SessionTimeout
        {
            get { return sessionTimeout; }
            set { sessionTimeout = value; }
        }

        /// <summary>
        /// <c>true</c> if peer-to-peer routing is enabled between
        /// routers on the same subnet.
        /// </summary>
        /// <remarks>
        /// This defaults to true.
        /// </remarks>
        public bool EnableP2P
        {
            get { return enableP2P; }
            set { enableP2P = value; }
        }

        /// <summary>
        /// Returns <c>true</c> if the router is running.
        /// </summary>
        public bool IsOpen
        {
            get { return isOpen; }
        }

        /// <summary>
        /// The maximum number of logical endpoints to include in a single
        /// LogicalAdvertiseMsg.
        /// </summary>
        /// <remarks>
        /// Routers that need to advertise more routes than this will need 
        /// to send multiple messages.  Defaults to 256.
        /// </remarks>
        public int MaxLogicalAdvertiseEPs
        {
            get { return maxAdvertiseEPs; }
            set { maxAdvertiseEPs = value; }
        }

        /// <summary>
        /// The maximum time to wait for a <see cref="ReceiptMsg" /> before
        /// declaring a dead router.  Set TimeSpan.Zero to disable dead router
        /// detection.
        /// </summary>
        /// <remarks>
        /// Defaults to 0 seconds.
        /// </remarks>
        public TimeSpan DeadRouterTTL
        {
            get { return deadRouterTTL; }
            set { deadRouterTTL = value; }
        }

        /// <summary>
        /// Returns <c>true</c> if dead router detection is enabled.
        /// </summary>
        public bool DeadRouterDetection
        {
            get { return deadRouterTTL != TimeSpan.Zero; }
        }

        /// <summary>
        /// Used by Unit tests to pause the operation of a router.
        /// </summary>
        /// <remarks>
        /// Routers in a paused state ignore all messages routed to them
        /// and also stop performing background tasks.
        /// </remarks>
        public bool Paused
        {
            get { return isPaused; }
            set { isPaused = value; }
        }

        /// <summary>
        /// The maximum time that a physical route will be maintained by the
        /// router without being refreshed before being discarded.
        /// </summary>
        public TimeSpan PhysRouteTTL
        {
            get { return physRouteTTL; }

            internal set
            {
                using (TimedLock.Lock(this.SyncRoot))
                {

                    physRouteTTL = value;
                    if (physRoutes != null)
                        physRoutes.RouteTTL = value;
                }
            }
        }

        /// <summary>
        /// The router's physical route table (or <c>null</c>).
        /// </summary>
        public PhysicalRouteTable PhysicalRoutes
        {
            get { return physRoutes; }
            set { physRoutes = value; }
        }

        /// <summary>
        /// Adds or refreshes the physical route to a peer or child router.
        /// </summary>
        /// <param name="routerEP">The router's physical endpoint.</param>
        /// <param name="appName">Name of the application hosting the remote router.</param>
        /// <param name="appDescription">Description of the application hosting the remote router.</param>
        /// <param name="routerInfo">The router's capability information.</param>
        /// <param name="logicalEndpointSetID">The router's logical endpoint set ID.</param>
        /// <param name="udpEP">The router's UDP endpoint.</param>
        /// <param name="tcpEP">The router's TCP endpoint.</param>
        /// <remarks>
        /// <para>
        /// If the route already exists in the tabl but the logical endpoint set ID has
        /// changed, then this method will ensure that any logical routes associated
        /// with the old endpoint set will be flushed from the logical route table.
        /// </para>
        /// <note>
        /// The router's endpoint must be a direct descendant of 
        /// the associated router's endpoint or a peer in the physical 
        /// hierarchy.
        /// </note>
        /// </remarks>
        public void AddPhysicalRoute(MsgEP routerEP, string appName, string appDescription, MsgRouterInfo routerInfo,
                                     Guid logicalEndpointSetID, IPEndPoint udpEP, IPEndPoint tcpEP)
        {
            PhysicalRoute route;

            using (TimedLock.Lock(syncLock))
            {
                route = this.PhysicalRoutes[routerEP];
                if (route != null && route.LogicalEndpointSetID != logicalEndpointSetID)
                    this.LogicalRoutes.Flush(route.LogicalEndpointSetID);

                this.PhysicalRoutes.Add(routerEP, appName, appDescription, routerInfo, logicalEndpointSetID, udpEP, tcpEP);
            }
        }

        /// <summary>
        /// The router's logical routing table (or <c>null</c>).
        /// </summary>
        public LogicalRouteTable LogicalRoutes
        {
            get { return logicalRoutes; }
            set { logicalRoutes = value; }
        }

        /// <summary>
        /// Enables encryption for all router message traffic.
        /// </summary>
        /// <param name="sharedKey">The <see cref="SymmetricKey" /> to be used to perform the encryption.</param>
        /// <remarks>
        /// <para>
        /// This must be called before calling <see cref="Start" />.
        /// </para>
        /// </remarks>
        public void EnableEncryption(SymmetricKey sharedKey)
        {
            this.encryptor = new BlockEncryptor(sharedKey);
            this.decryptor = new BlockDecryptor(sharedKey);
        }

        /// <summary>
        /// The default TTL to assign to messages being transmitted and whose
        /// _TTL field is set to 0.
        /// </summary>
        /// <remarks>
        /// This must be initialized before calling <see cref="Start" />.  This defaults to 5.
        /// </remarks>
        public int DefMsgTTL
        {
            get { return defMsgTTL; }
            set { defMsgTTL = value; }
        }

        /// <summary>
        /// Sets/returns the session manager associated with the router.
        /// </summary>
        /// <remarks>
        /// <para>
        /// To associate a custom session manager with the router, assign
        /// the manager to this property before calling <see cref="Start" />.  The setter
        /// will throw an exception if the router has already been started.
        /// </para>
        /// <para>
        /// Before <see cref="Start" /> is called, the getter will return null or the 
        /// session manager set by calling the setter.  If a custom
        /// manager was not set and the router has been started then 
        /// the getter will associate a new instance of the default
        /// session manager <see cref="SessionManager" /> to the router
        /// (if one has not already been associated) and return that
        /// manager.
        /// </para>
        /// </remarks>
        public ISessionManager SessionManager
        {
            get
            {
                using (TimedLock.Lock(this.syncLock))
                {
                    if (!isOpen)
                        return sessionMgr;

                    if (sessionMgr != null)
                        return sessionMgr;

                    sessionMgr = new SessionManager();
                    sessionMgr.Init(this);
                    return sessionMgr;
                }
            }

            set
            {
                using (TimedLock.Lock(this.syncLock))
                {
                    if (isOpen)
                        throw new MsgException("Cannot assign a session manager after the router is started.");

                    sessionMgr = value;
                    sessionMgr.Init(this);
                    dispatcher.AddTarget(sessionMgr);
                }
            }
        }

        /// <summary>
        /// Starts the message router on the specified UDP and TCP endpoints.
        /// </summary>
        /// <param name="cloudAdapter">
        /// The network adapter to use for the discovery multicast channel or IPAddress.Any to bind all adapters.
        /// </param>
        /// <param name="cloudEP">The discovery UDP multicast endpoint (or <c>null</c>).</param>
        /// <param name="udpEP">The UDP network endpoint.</param>
        /// <param name="tcpEP">The TCP network (listening) endpoint.</param>
        /// <param name="tcpBacklog">Maximum number of pending inbound TCP sockets connections to be queued.</param>
        /// <param name="maxIdle">Maximum time a TCP channel can be idle before being closed.</param>
        /// <remarks>
        /// cloudEP may be passed as null to disable multicast functionality.
        /// </remarks>
        public void Start(IPAddress cloudAdapter, IPEndPoint cloudEP,
                          IPEndPoint udpEP, IPEndPoint tcpEP,
                          int tcpBacklog, TimeSpan maxIdle)
        {
            using (TimedLock.Lock(this.syncLock))
            {
                if (isOpen)
                    throw new MsgException(msgAlreadyStarted);

                try
                {
                    if (physRoutes == null)
                        physRoutes = new PhysicalRouteTable(this, physRouteTTL);

                    if (logicalRoutes == null)
                        logicalRoutes = new LogicalRouteTable(this);

                    logicalChangeID = logicalRoutes.ChangeID - 1;   // I'm explicitly setting this to a different value
                                                                    // so an LogicalRouteChange event will be raised the
                                                                    // next time the background task runs
                    if (this.encryptor == null)
                        this.encryptor = new BlockEncryptor(CryptoAlgorithm.PlainText, new byte[] { 0 }, new byte[] { 0 });

                    if (this.decryptor == null)
                        this.decryptor = new BlockDecryptor(CryptoAlgorithm.PlainText, new byte[] { 0 }, new byte[] { 0 });

                    this.cloudAdapter = cloudAdapter;

                    if (!cloudAdapter.Equals(IPAddress.Any))
                        this.activeAdapter = cloudAdapter;
                    else
                        this.activeAdapter = NetHelper.GetActiveAdapter();

                    this.onProcessMsg = new WaitCallback(OnProcessMsg);

                    udpUnicast = new UdpChannel(this);
                    udpUnicast.OpenUnicast(udpEP);
                    this.udpEP = new IPEndPoint(udpEP.Address, udpUnicast.Port);

                    this.cloudEP = cloudEP;
                    if (cloudEP != null)
                    {
                        udpMulticast = new UdpChannel(this);

                        switch (discoveryMode)
                        {
                            case DiscoveryMode.Multicast:

                                udpMulticast.OpenMulticast(cloudAdapter, cloudEP);
                                break;

                            case DiscoveryMode.UdpBroadcast:

                                udpMulticast.OpenUdpBroadcast(broadcastSettings);
                                break;

                            default:

                                throw new NotImplementedException("Unexpected Discovery Mode");
                        }
                    }

                    tcpChannels = new Dictionary<IPEndPoint, TcpChannel>();
                    tcpPending  = new List<TcpChannel>();

                    sockListen = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    sockListen.Bind(tcpEP);
                    sockListen.Listen(tcpBacklog);
                    this.tcpEP = new IPEndPoint(tcpEP.Address, ((IPEndPoint)sockListen.LocalEndPoint).Port);

                    onAccept = new AsyncCallback(OnAccept);
                    sockListen.BeginAccept(onAccept, null);

                    this.maxIdle = maxIdle;
                    this.bkTimer = new GatedTimer(new TimerCallback(OnBkTimer), null, TimeSpan.Zero, bkInterval);

                    this.isOpen = true;
                }
                catch
                {
                    Stop();
                    throw;
                }
            }
        }

        /// <summary>
        /// Stops the message router if it has been started.
        /// </summary>
        /// <remarks>
        /// It is not an error to stop a router that's not running.
        /// </remarks>
        public void Stop()
        {
            using (TimedLock.Lock(this.syncLock))
            {
                if (!isOpen)
                    return;

                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                isOpen             = false;
                routerEP           = null;
                routerInfo         = null;
                LogicalRouteChange = null;

                if (dispatcher != null)
                    dispatcher.Clear();

                if (udpUnicast != null)
                {
                    udpUnicast.Close();
                    udpUnicast = null;
                }

                if (udpMulticast != null)
                {
                    udpMulticast.Close();
                    udpMulticast = null;
                }

                if (sockListen != null)
                    sockListen.Close();

                if (tcpChannels != null)
                {
                    foreach (var channel in tcpChannels.Values)
                        channel.Close();

                    tcpChannels.Clear();
                    tcpChannels = null;
                }

                if (tcpPending != null)
                {
                    foreach (var channel in tcpPending)
                        channel.Close();

                    tcpPending = null;
                }

                msgTracker.Clear();
            }

            // Pause for a bit to allow any async socket operations to unwind.

            Thread.Sleep(1000);
        }

        /// <summary>
        /// Called by the message dispatcher if the set of logical endpoints
        /// implemented by the router changes.
        /// </summary>
        /// <param name="logicalEndpointSetID">The new logical endpoint set ID.</param>
        /// <remarks>
        /// The base class does nothing.  Derived classes should provide
        /// a reasonable implementation.
        /// </remarks>
        public virtual void OnLogicalEndpointSetChange(Guid logicalEndpointSetID)
        {
        }

        /// <summary>
        /// Forces the update of the message dispatcher's logical endpoint
        /// set ID and then readvertises the router with the new ID.
        /// </summary>
        /// <remarks>
        /// The base class implementation simply calls the dispatcher's
        /// <see cref="IMsgDispatcher.LogicalAdvertise"/> method.
        /// </remarks>
        public virtual void LogicalAdvertise()
        {
            dispatcher.LogicalAdvertise();
        }

        /// <summary>
        /// Called periodically on a background thread, giving derived
        /// classes a chance to preform any necessary background activities.
        /// </summary>
        protected virtual void OnBkTimer()
        {
            using (TimedLock.Lock(this.syncLock))
            {
                if (physRoutes != null)
                    physRoutes.Flush();

                if (logicalRoutes != null)
                    logicalRoutes.Flush();

                if (logicalRoutes != null)
                {
                    if (logicalChangeID != logicalRoutes.ChangeID)
                    {
                        logicalChangeID = logicalRoutes.ChangeID;
                        threadPool.QueuePriorityTask(onLogicalChange, null);
                    }
                }
            }
        }

        /// <summary>
        /// Called periodically by the background task timer.
        /// </summary>
        /// <param name="o">Not used.</param>
        /// <remarks>
        /// Manages some background tasks including the closing of idle TCP channels as
        /// well as giving the session manager time to perform its own background
        /// activities.  Also calls the virtual <see cref="OnBkTimer()" /> method with 
        /// can be implemented by derived classes to implement their own behaviors.
        /// </remarks>
        private void OnBkTimer(object o)
        {
            ISessionManager sessionMgr;

            if (isPaused)
                return;

            using (TimedLock.Lock(this.syncLock))
            {

                if (!isOpen)
                    return;

                sessionMgr = this.sessionMgr;

                if (!idleCheck)
                    CloseIdleTcp(this.maxIdle);
            }

            // $todo(jeff.lill): I need to rig up some kind of unit test for this.

            // Periodically re-poll for the active network adapter's IP address
            // in case a new network adapter has been plugged in to the net or
            // if a new address was obtained when a lease expired.
            //
            // Note that we're polling only if the cloud adapter is set to
            // ANY.  If a specific adapter is configured then we'll simply
            // keep using its IP address.

            if (cloudAdapter.Equals(IPAddress.Any))
            {
                var ipAddr = NetHelper.GetActiveAdapter();

                using (TimedLock.Lock(this.syncLock))
                {
                    if (isOpen && !activeAdapter.Equals(ipAddr))
                    {
                        // Notify the channels of the change if they're not explicitly 
                        // bound to an IP address

                        if (udpUnicast != null && udpEP.Address.Equals(IPAddress.Any))
                            udpUnicast.OnNewEP(new ChannelEP(Transport.Udp, this.UdpEP));

                        if (udpMulticast != null && cloudEP.Address.Equals(IPAddress.Any))
                            udpMulticast.OnNewEP(new ChannelEP(Transport.Multicast, this.CloudEP));

                        // I'm going to have to rebuild the TCP channels table

                        var tcpTemp = new List<TcpChannel>();

                        foreach (var channel in tcpChannels.Values)
                            tcpTemp.Add(channel);

                        tcpChannels.Clear();

                        foreach (var channel in tcpTemp)
                        {
                            var ep = new ChannelEP(Transport.Tcp, this.TcpEP);

                            channel.OnNewEP(ep);
                            tcpChannels[ep.NetEP] = channel;
                        }
                    }
                }
            }

            // Give the session manager (if any) a chance to perform any
            // background tasks.

            if (sessionMgr != null)
                sessionMgr.OnBkTimer();

            // Perform the dead router detection

            msgTracker.DetectDeadRouters();

            // Give derived classes a chance to perform background work.

            OnBkTimer();
        }

        /// <summary>
        /// Called when the router detects that it's been assigned a new network address.
        /// </summary>
        protected virtual void OnNewEP()
        {
        }

        /// <summary>
        /// Closes any TCP channels that have been idle for longer than 
        /// the timespan passed.
        /// </summary>
        /// <param name="maxIdle">The maximum idle time.</param>
        public void CloseIdleTcp(TimeSpan maxIdle)
        {
            using (TimedLock.Lock(this.syncLock))
            {
                if (!isOpen)
                    return;

                // Remove idle channels from the main hash table

                tcpChannels.Remove(entry => entry.Value.CloseIfIdle(maxIdle));

                // Remove idle channels from the pending connections

                List<TcpChannel> delList = null;

                foreach (TcpChannel channel in tcpPending)
                {
                    if (channel.CloseIfIdle(maxIdle))
                    {
                        if (delList == null)
                            delList = new List<TcpChannel>();

                        delList.Add(channel);
                    }
                }

                if (delList != null)
                {
                    foreach (var channel in delList)
                        tcpPending.Remove(channel);

                    // It is possible that a channel was removed from the main
                    // hash table that was blocking a channel in the tcpPending
                    // array from being added there.  This code will attempt to add
                    // all of the pending (and initialized) channels again to
                    // the main hash table.

                    delList.Clear();
                    foreach (var channel in tcpPending)
                    {
                        if (channel.RemoteEP.Port == 0)
                            continue;   // Not initialized

                        TcpChannel existingChannel;

                        tcpChannels.TryGetValue(channel.RemoteEP, out existingChannel);

                        if (existingChannel == null)
                        {
                            tcpChannels.Add(channel.RemoteEP, channel);
                            delList.Add(channel);
                        }
                    }

                    foreach (var channel in delList)
                        tcpPending.Remove(channel);
                }
            }
        }

        /// <summary>
        /// Handles asynchronous accept() on the listening TCP socket.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnAccept(IAsyncResult ar)
        {
            using (TimedLock.Lock(this.syncLock))
            {
                EnhancedSocket  sockAccept;
                TcpChannel      channel;

                try
                {
                    sockAccept = sockListen.EndAccept(ar);
                    channel    = new TcpChannel(this);
                    channel.Open(sockAccept);
                }
                catch
                {
                }

                try
                {
                    if (sockListen.IsOpen)
                        sockListen.BeginAccept(onAccept, null);
                }
                catch (Exception e)
                {
                    Trace("TCP Accept Failure", e);
                }
            }
        }

        /// <summary>
        /// Adds the channel to the main hash table
        /// </summary>
        /// <param name="channel">The newly initialized channel.</param>
        /// <remarks>
        /// This will be called by a Tcp channel when it receives the intialization
        /// message from the other endpoint.  At this point, the channel's RemoteEP
        /// property will be fully initialized.
        /// </remarks>
        internal virtual void OnTcpInit(TcpChannel channel)
        {
            using (TimedLock.Lock(this.syncLock))
            {
                TcpChannel tcpChannel;

                tcpChannels.TryGetValue(channel.RemoteEP, out tcpChannel);

                if (tcpChannel == null)
                    tcpChannels.Add(channel.RemoteEP, channel);
                else if (tcpChannel == channel)
                    return;
                else
                {
                    Assertion.Test(tcpPending.IndexOf(channel) == -1);
                    tcpPending.Add(channel);
                }
            }
        }

        /// <summary>
        /// Called when a TCP channel detects that the remote endpoint closes
        /// its connection.
        /// </summary>
        /// <param name="channel">The channel.</param>
        internal virtual void OnTcpClose(TcpChannel channel)
        {
            using (TimedLock.Lock(this.syncLock))
            {
                if (!isOpen)
                    return;

                TcpChannel existingChannel;

                tcpChannels.TryGetValue(channel.RemoteEP, out existingChannel);

                if (existingChannel != null)
                {
                    tcpChannels.Remove(channel.RemoteEP);
                    logicalRoutes.Flush();
                }
            }
        }

        /// <summary>
        /// Returns the router's discovery UDP multicast endpoint.
        /// </summary>
        public IPEndPoint CloudEP
        {
            get { return cloudEP; }
        }

        /// <summary>
        /// Returns the router's UDP unicast network endpoint.
        /// </summary>
        public IPEndPoint UdpEP
        {
            get { return NormalizeEP(udpEP); }
        }

        /// <summary>
        /// Returns the router's TCP network (listening) endpoint.
        /// </summary>
        public IPEndPoint TcpEP
        {
            get { return NormalizeEP(tcpEP); }

            internal set
            {
                tcpEP = value;
            }
        }

        /// <summary>
        /// Appends 4 bytes of salt to the end of the serialized message in the 
        /// buffer, encrypts it, and then prepends the frame length onto
        /// the buffer before returning the result.
        /// </summary>
        /// <param name="msgBuf">The serialized message.</param>
        /// <param name="cbMsg">The length of the data in the buffer.</param>
        /// <returns>The encrypted frame, ready for transmission.</returns>
        internal byte[] EncryptFrame(byte[] msgBuf, int cbMsg)
        {
            // $todo(jeff.lill): 
            //
            // There's an awful lot of buffer copying
            // going on here.  Something to take a look at
            // optimizing sometime.

            byte[]          encrypted;
            byte[]      frame;
            int         pos;

            cbMsg = Crypto.AppendSalt4(msgBuf, cbMsg);
            using (TimedLock.Lock(this.syncLock))
                encrypted = encryptor.Encrypt(msgBuf, 0, cbMsg);

            frame = new byte[FrameHeaderSize + encrypted.Length];
            pos   = 0;
            Helper.WriteInt32(frame, ref pos, encrypted.Length);
            Array.Copy(encrypted, 0, frame, FrameHeaderSize, encrypted.Length);

            return frame;
        }

        /// <summary>
        /// Decrypts the partial frame buffer passed and returns the serialized message
        /// bytes without salt.  A partial frame does not include the frame header
        /// bytes.
        /// </summary>
        /// <param name="encrypted">The encrypted message and salt bytes.</param>
        /// <param name="cbEncrypted">Bytes of valid encrypted message data.</param>
        /// <param name="cbMsg">Returns as the number of message bytes in the returned buffer.</param>
        /// <returns>The decryped message bytes.</returns>
        /// <remarks>
        /// Used by TCP channels where the frame header and body will be read from 
        /// the socket separately.
        /// </remarks>
        internal byte[] DecryptMessage(byte[] encrypted, int cbEncrypted, out int cbMsg)
        {
            byte[] decrypted;

            using (TimedLock.Lock(this.syncLock))
                decrypted = decryptor.Decrypt(encrypted, 0, cbEncrypted);

            cbMsg = decrypted.Length - 4;   // This effectively removes the appended salt
            return decrypted;
        }

        /// <summary>
        /// Decrypts the full frame buffer passed and returns the serialized message
        /// bytes without salt.  A full frame includes the frame header bytes.
        /// </summary>
        /// <param name="frame">The entire message frame.</param>
        /// <param name="cbFrame">Size of the frame in bytes.</param>
        /// <param name="cbMsg">Returns as the number of message bytes in the returned buffer.</param>
        /// <returns>The decryped message bytes.</returns>
        /// <remarks>
        /// Used by TCP channels where the frame header and body will be read from 
        /// the socket together.
        /// </remarks>
        internal byte[] DecryptFrame(byte[] frame, int cbFrame, out int cbMsg)
        {
            byte[] decrypted;

            using (TimedLock.Lock(this.syncLock))
                decrypted = decryptor.Decrypt(frame, FrameHeaderSize, cbFrame - FrameHeaderSize);

            cbMsg = decrypted.Length - 4;   // This effectively removes the appended salt
            return decrypted;
        }

        /// <summary>
        /// Determines whether a <see cref="ReceiptMsg" /> message should be sent confirming
        /// that the message pass was delivered successfully.
        /// </summary>
        /// <param name="msg">The message.</param>
        protected void SendMsgReceipt(Msg msg)
        {
            if ((msg._Flags & MsgFlag.ReceiptRequest) == 0)
                return;     // No receipt requested

            if ((msg._Flags & MsgFlag.Broadcast) != 0)
                return;     // We don't send receipts for broadcast messages

            if (msg._ReceiveChannel == null)
                return;     // It looks like the message originated from this
                            // router so no receipt is required.

            if (msg._ReceiptEP == null)
                return;     // There's no receipt destination

            if (msg._ToEP.IsPhysical && msg._ToEP.Equals(routerEP))
                return;     // Don't send a receipt if the message is targeted at physical
                            // endpoint other than this router.

            // $todo(jeff.lill): 
            //
            // I wonder if I should perhaps send the message via UDP
            // as well.  Under super high loads, it could be possible
            // for the receipt message top be queued behind a lot of
            // data on the outbound socket.  This could exceed the
            // DeadRouterTTL on the tracking router, essentially forcing
            // this router offline at exactly the wrong time (when loads
            // are high).
            //
            // Another potentially better solution is to have tracking
            // routers automatically adjust their DeadRouterTTLs upward if 
            // routers determine that they've been unjustly determined
            // to be dead.

            SendTo(msg._ReceiptEP, this.routerEP, new ReceiptMsg(msg._MsgID));
        }

        /// <summary>
        /// Initiates tracking for an expected <see cref="ReceiptMsg" /> for the message
        /// being forwarded along the specified physical route.
        /// </summary>
        /// <param name="route">The physical route.</param>
        /// <param name="msg">The message to potentally be tracked.</param>
        /// <remarks>
        /// This method should be called just before the message is routed
        /// out of this router instance.  The method will perform any necessary
        /// modifications to the message and also save the state necessary to 
        /// track receipt messages.
        /// </remarks>
        protected void TrackMsgReceipt(PhysicalRoute route, Msg msg)
        {
            if ((msg._Flags & MsgFlag.ReceiptRequest) == 0 || msg._ReceiptEP != null)
                return;         // No receipt is requested or another router is
                                // already tracking the message

            switch (routerEP.Segments.Length)
            {
                case 0:

                    // This is a root router.  Root routers never track receipts.

                    return;

                case 1:

                    // This is a hub router.  Hub routers only track receipts for
                    // messages being forwarded to leaf routers on the subnet.

                    if (!routerEP.IsPhysicalDescendant(route.RouterEP))
                        return;

                    break;

                case 2:

                    // This is a leaf router.  Leaf routers only track receipts
                    // if the router is peer-to-peer enabled and the message is
                    // being forwarded to a peer router.

                    if (!enableP2P || !routerEP.IsPhysicalPeer(route.RouterEP))
                        return;

                    break;

                default:

                    throw new NotImplementedException();
            }

            // If we get to this point we're going to track the message.

            msg._ReceiptEP = routerEP;
            if (msg._MsgID == Guid.Empty)
                msg._MsgID = Helper.NewGuid();

            msgTracker.Track(route, msg);
        }

        /// <summary>
        /// </summary>
        /// <param name="deadRouterEP">The dead router's physical endpoint.</param>
        /// <param name="logicalEndpointSetID">
        /// The dead router's logical endpoint set ID at the time the message was routed.
        /// </param>
        /// <remarks>
        /// <para>
        /// This is called by <see cref="MsgTracker.DetectDeadRouters" /> whenever it appears that
        /// we've found a dead router.  The method should take the appropriate action.
        /// </para>
        /// <para>
        /// This base implementation multicasts a <see cref="DeadRouterMsg" /> to the subnet.
        /// </para>
        /// </remarks>
        public virtual void OnDeadRouterDetected(MsgEP deadRouterEP, Guid logicalEndpointSetID)
        {
            Multicast(new DeadRouterMsg(deadRouterEP, logicalEndpointSetID));
        }

        /// <summary>
        /// Used to queue received message information from <see cref="OnReceive(LillTek.Messaging.IMsgChannel, LillTek.Messaging.Msg)" /> 
        /// to <see cref="OnProcessMsg" />.
        /// </summary>
        private sealed class RecvMsgInfo
        {
            public IMsgChannel  Channel;
            public Msg          Msg;

            public RecvMsgInfo(IMsgChannel channel, Msg msg)
            {
                this.Channel = channel;
                this.Msg     = msg;
            }
        }

        /// <summary>
        /// This method should be called whenever a message is received by a
        /// channel or from the application (via a <b>Send()</b>, <b>Broadcast()</b> or 
        /// <b>Reply()</b> call.  The method queues the message for routing and delivery.
        /// </summary>
        /// <param name="channel">The receiving channel (or <c>null</c>).</param>
        /// <param name="msg">The received message.</param>
        /// <remarks>
        /// <note>
        /// This method queues the information to a separate worker
        /// thread to be process asynchronously and will return almost
        /// immediately.
        /// </note>
        /// </remarks>
        internal void OnReceive(IMsgChannel channel, Msg msg)
        {
            if (msg._ToEP.IsNull)
                return;     // Discard messages sent to null endpoints

            threadPool.QueuePriorityTask(onProcessMsg, new RecvMsgInfo(channel, msg));
        }

        /// <summary>
        /// This method should be called whenever a message is received by a
        /// channel or from the application (via a Send(), Broadcast() or Reply()
        /// call.  The method queues the message for routing and delivery.
        /// </summary>
        /// <param name="state">A RecvMsgInfo instance.</param>
        /// <remarks>
        /// <para>
        /// This method compares the _ToEP of the message to the physical
        /// endpoint of this router.  If it's a match or if _ToEP is a channel
        /// endpoint, then the message will be passed to the router's receive 
        /// handler.  If there's no match then RouteXXX() will be called to route 
        /// the message.
        /// </para>
        /// <para>
        /// Pass channel as <c>null</c> if the message is being sourced from
        /// this router instance.
        /// </para>
        /// </remarks>
        private void OnProcessMsg(object state)
        {
            var info = (RecvMsgInfo)state;
            var channel = info.Channel;
            var msg = info.Msg;
            if (isPaused)
                return;

            msg._InUse          = true;
            msg._ReceiveChannel = channel;

            try
            {
                if (msg._ToEP == null || msg._ToEP.IsPhysical)
                {
                    bool match;

                    using (TimedLock.Lock(this.syncLock))
                    {
                        if (!isOpen)
                        {
                            OnDiscardMsg(msg, "Router is closed");
                            return;
                        }

                        match = routerEP != null && msg._ToEP != null && routerEP.IsPhysicalMatch(msg._ToEP);
                    }

                    if (match || msg._ToEP.IsChannel)
                    {
                        msg._Trace(this, 1, "Route Local", null);
                        SendMsgReceipt(msg);
                        OnReceive(msg);
                    }
                    else
                    {
                        // Clear the message's channelEPs, if present, before doing
                        // any routing.

                        msg._SetToChannel(null);
                        msg._SetFromChannel(null);

                        msg._Trace(this, 1, "Route Remote", null);
                        RoutePhysical(msg._ToEP, msg);
                    }
                }
                else
                {
                    // The message is targeted at a logical endpoint.  Call RouteLogical()
                    // so that derived classes can implement custom routing.

                    using (TimedLock.Lock(this.syncLock))
                    {
                        if (!isOpen)
                        {
                            OnDiscardMsg(msg, "Router is closed");
                            return;
                        }
                    }

                    msg._Trace(this, 1, "Route Logical", null);
                    RouteLogical(msg);
                }
            }
            catch (Exception e)
            {

                NetTrace.Write(MsgRouter.TraceSubsystem, 0, this.GetType().Name + ": Exception", e);
            }
        }

        /// <summary>
        /// Called when a message is received whose _ToEP matches the
        /// physical endpoint of this router instance.
        /// </summary>
        /// <param name="msg">The received message.</param>
        protected virtual void OnReceive(Msg msg)
        {
            if (onReceive == null)
            {
                msg._Trace(this, 0, "******* Message Discarded", ": No msg handlers");
                return;
            }

            if (!onReceive(msg))
                msg._Trace(this, 0, "******* Message Discarded", ": No msg handler");
        }

        /// <summary>
        /// Asynchronously transmits the message on the specified physical route's
        /// TCP channel endpoint, also initiating any necessary message receipt
        /// tracking.
        /// </summary>
        /// <param name="route">Specifies the target route.</param>
        /// <param name="msg">The message to be sent.</param>
        internal void TransmitTcp(PhysicalRoute route, Msg msg)
        {
            ChannelEP   toEP = route.TcpEP;
            TcpChannel  tcpChannel;

            TrackMsgReceipt(route, msg);

            tcpChannels.TryGetValue(toEP.NetEP, out tcpChannel);

            if (tcpChannel == null)
            {
                tcpChannel = new TcpChannel(this);
                tcpChannels.Add(toEP.NetEP, tcpChannel);
                tcpChannel.Connect(toEP.NetEP, msg);
            }
            else
                tcpChannel.Transmit(toEP, msg);
        }

        /// <summary>
        /// Asynchronously transmits the message to the specified channel endpoint.
        /// </summary>
        /// <param name="toEP">Specifies the target router.</param>
        /// <param name="msg">The message to be sent.</param>
        /// <remarks>
        /// <note>
        /// The message will be sent directly to the target router 
        /// without calling <see cref="RoutePhysical" /> to make any additional routing decisions.
        /// Note also this this method does not implement any message receipt
        /// tracking.
        /// </note>
        /// </remarks>
        internal void Transmit(ChannelEP toEP, Msg msg)
        {
            TcpChannel tcpChannel;

            using (TimedLock.Lock(this.syncLock))
            {
                if (!isOpen)
                    throw new MsgException(msgNotStarted);

                switch (toEP.Transport)
                {
                    case Transport.Tcp:

                        tcpChannels.TryGetValue(toEP.NetEP, out tcpChannel);

                        if (tcpChannel == null)
                        {
                            tcpChannel = new TcpChannel(this);
                            tcpChannels.Add(toEP.NetEP, tcpChannel);
                            tcpChannel.Connect(toEP.NetEP, msg);
                        }
                        else
                            tcpChannel.Transmit(toEP, msg);

                        break;

                    case Transport.Udp:

                        udpUnicast.Transmit(toEP, msg);
                        break;

                    case Transport.Multicast:

                        if (udpMulticast == null)
                            throw new MsgException(msgNoMulticast);
                        else
                            udpMulticast.Transmit(toEP, msg);

                        break;

                    default:

                        Assertion.Fail("Unexpected Transport type.");
                        break;
                }
            }
        }

        /// <summary>
        /// Normalizes the network endpoint passed by converting
        /// address=IPAddress.Any into the IP address of the active
        /// network adapter.
        /// </summary>
        /// <param name="ep">The endpoint to normalize.</param>
        /// <returns>The normalized endpoint.</returns>
        internal IPEndPoint NormalizeEP(IPEndPoint ep)
        {
            if (ep.Address.Equals(IPAddress.Any))
            {
                using (TimedLock.Lock(this.syncLock))
                    return new IPEndPoint(activeAdapter, ep.Port);
            }
            else
                return ep;
        }

        /// <summary>
        /// Normalizes the channel endpoint passed by converting
        /// address=IPAddress.Any into the IP address of the active
        /// network adapter.
        /// </summary>
        /// <param name="ep">The endpoint to normalize.</param>
        /// <returns>The normalized endpoint.</returns>
        internal ChannelEP NormalizeEP(ChannelEP ep)
        {
            if (ep.NetEP.Address.Equals(IPAddress.Any))
            {
                using (TimedLock.Lock(this.syncLock))
                    return new ChannelEP(ep.Transport, new IPEndPoint(activeAdapter, ep.NetEP.Port));
            }
            else
                return ep;
        }

        /// <summary>
        /// Returns <c>true</c> if UDP multicasting is enabled for this router.
        /// </summary>
        public bool MulticastEnabled
        {
            get { return udpMulticast != null; }
        }

        /// <summary>
        /// Multicasts the message to all the routers in this router's
        /// multicast group.
        /// </summary>
        /// <param name="msg">The message to be multicast.</param>
        /// <remarks>
        /// <note>
        /// The message will be sent directly to the target routers 
        /// without calling <see cref="RoutePhysical" /> to make any additional routing decisions.
        /// </note>
        /// </remarks>
        public void Multicast(Msg msg)
        {
            using (TimedLock.Lock(this.syncLock))
            {
                if (!isOpen)
                    throw new MsgException(msgNotStarted);

                if (udpMulticast == null)
                    throw new MsgException(msgNoMulticast);

                msg._Trace(this, 0, "Multicast", null);
                udpMulticast.Transmit(new ChannelEP(Transport.Multicast, cloudEP), msg);
            }
        }

        /// <summary>
        /// Handles the physical routing of the message passed.
        /// </summary>
        /// <param name="physicalEP">The physical target endpoint.</param>
        /// <param name="msg">The message.</param>
        /// <remarks>
        /// <para>
        /// This method is designed to be overriden by derived classes so
        /// that they can implement more advanced routing schemes.  This
        /// base implementation simply routes the message to the router 
        /// specified by the channel endpoint embedded in the message's
        /// ToEP, if present.
        /// </para>
        /// </remarks>
        protected virtual void RoutePhysical(MsgEP physicalEP, Msg msg)
        {
            Assertion.Test(physicalEP.IsPhysical, "Physical endpoint expected");
            Assertion.Test(physicalEP.ChannelEP == null, "ChannelEP must be null for routed messages.");

            if (msg._TTL == 0)
            {
                OnDiscardMsg(msg, "TTL=0");
                return;
            }

            msg._TTL--;

            using (TimedLock.Lock(this.syncLock))
            {
                if (!isOpen)
                    return;

                var route = this.PhysicalRoutes[physicalEP];

                if (route != null)
                    TransmitTcp(route, msg);

                return;
            }
        }

        /// <summary>
        /// Handles the logical routing of the message passed.
        /// </summary>
        /// <param name="msg">The message.</param>
        /// <remarks>
        /// <para>
        /// This method is designed to be overriden by derived classes so
        /// that they can implement more advanced routing schemes.  The base
        /// implementation simply attempts to route the message to a local
        /// application handler.  No routing to remote routers will be
        /// attempted.
        /// </para>
        /// </remarks>
        protected virtual void RouteLogical(Msg msg)
        {
            Assertion.Test(msg._ToEP.IsLogical);

            if (msg._TTL == 0)
            {
                OnDiscardMsg(msg, "TTL=0");
                return;
            }

            msg._TTL--;

            using (TimedLock.Lock(this.syncLock))
            {
                if (!isOpen)
                    return;

                if (dispatcher.Dispatch(msg))
                    SendMsgReceipt(msg);
            }
        }

        /// <summary>
        /// Called by the RouteXXX() methods when it discards a non-routable message.
        /// </summary>
        /// <param name="msg">The message.</param>
        /// <param name="reason">The reason.</param>
        /// <remarks>
        /// This is used for debugging purposes.
        /// </remarks>
        [Conditional("TRACE")]
        protected virtual void OnDiscardMsg(Msg msg, string reason)
        {
            msg._Trace(this, 0, "******* Message Discarded", ": " + reason);
        }

        /// <summary>
        /// Writes the string passed out to the NetTrace.
        /// </summary>
        /// <param name="detail">The detail level (0..255).</param>
        /// <param name="tEvent">The event text.</param>
        /// <param name="summary">The summary text.</param>
        /// <param name="details">The details text.</param>
        [Conditional("TRACE")]
        internal void Trace(int detail, string tEvent, string summary, string details)
        {
            if (summary == null)
                summary = this.GetType().Name;
            else
                summary = this.GetType().Name + ": " + summary;

            NetTrace.Write(MsgRouter.TraceSubsystem, detail, tEvent, summary, details);
        }

        /// <summary>
        /// Writes the exception passed out to the NetTrace.
        /// </summary>
        /// <param name="tEvent">The event text.</param>
        /// <param name="e">The exception.</param>
        [Conditional("TRACE")]
        internal void Trace(string tEvent, Exception e)
        {
            const string format =
@"Exception: {0}
Message:   {1}
Stack:

";
            StringBuilder sb = new StringBuilder();
            string summary;

            summary = this.GetType().Name + ": " + e.GetType().Name;

            sb.AppendFormat(null, format, e.GetType().ToString(), e.Message);
            sb.AppendFormat(e.StackTrace);

            NetTrace.Write(MsgRouter.TraceSubsystem, 0, tEvent, summary, sb.ToString());
        }

        /// <summary>
        /// Initiates the routing of the message passed.
        /// </summary>
        /// <param name="msg">The message.</param>
        /// <remarks>
        /// <note>
        /// Messages passed to this method should considered to be owned by the 
        /// messaging library and must not be accessed or reused by application code.
        /// </note>
        /// </remarks>
        public void Send(Msg msg)
        {
            Assertion.Test(!msg._InUse, "Illegal message reuse.");
            msg._InUse = true;

            if (msg._ToEP == null)
                throw new ArgumentNullException(msgNullToEP, "msg");

            if (msg._ToEP.IsLogical && localEPMap.Match(msg._ToEP))
                msg._Flags |= MsgFlag.ClosestRoute;

            if (msg._ToEP.Broadcast)
                msg._Flags |= MsgFlag.Broadcast;

            if (msg._TTL == 0)
                msg._TTL = defMsgTTL == 255 ? defMsgTTL : defMsgTTL + 1;

            msg._Trace(this, 0, "Send", null);
            OnReceive(null, msg);
        }

        /// <summary>
        /// Initiates the routing of the message passed
        /// to the specified endpoint.
        /// </summary>
        /// <param name="toEP">The target endpoint.</param>
        /// <param name="msg">The message.</param>
        /// <remarks>
        /// <note>
        /// Messages passed to this method should considered to be owned by the 
        /// messaging library and must not be accessed or reused by application code.
        /// </note>
        /// </remarks>
        public void SendTo(MsgEP toEP, Msg msg)
        {
            Assertion.Test(!msg._InUse, "Illegal message reuse.");
            msg._InUse = true;

            if (toEP == null)
                throw new ArgumentNullException(msgNullToEP, "toEP");

            if (toEP.Broadcast)
                msg._Flags |= MsgFlag.Broadcast;

            msg._ToEP = toEP.Clone(true);

            if (msg._ToEP.IsLogical && localEPMap.Match(msg._ToEP))
                msg._Flags |= MsgFlag.ClosestRoute;

            if (msg._TTL == 0)
                msg._TTL = defMsgTTL == 255 ? defMsgTTL : defMsgTTL + 1;

            msg._Trace(this, 0, "SendTo", null);
            OnReceive(null, msg);
        }

        /// <summary>
        /// Initiates the routing of the message passed
        /// to the specified endpoint.
        /// </summary>
        /// <param name="toEP">The target endpoint.</param>
        /// <param name="fromEP">The source endpoint (or <c>null</c>).</param>
        /// <param name="msg">The message.</param>
        /// <remarks>
        /// <note>
        /// Messages passed to this method should considered to be owned by the 
        /// messaging library and must not be accessed or reused by application code.
        /// </note>
        /// </remarks>
        public void SendTo(MsgEP toEP, MsgEP fromEP, Msg msg)
        {
            Assertion.Test(!msg._InUse, "Illegal message reuse.");
            msg._InUse = true;

            if (toEP == null)
                throw new ArgumentNullException(msgNullToEP, "toEP");

            if (fromEP != null)
                fromEP = fromEP.Clone(true);

            msg._ToEP = toEP.Clone(true);
            msg._FromEP = fromEP;

            if (msg._ToEP.IsLogical && localEPMap.Match(msg._ToEP))
                msg._Flags |= MsgFlag.ClosestRoute;

            if (msg._TTL == 0)
                msg._TTL = defMsgTTL == 255 ? defMsgTTL : defMsgTTL + 1;

            msg._Trace(this, 0, "SendTo", null);
            OnReceive(null, msg);
        }

        /// <summary>
        /// Initiates the delivery of a reply to the message passed.
        /// </summary>
        /// <param name="msg">The original request message.</param>
        /// <param name="reply">The reply.</param>
        /// <remarks>
        /// <para>
        /// This method works by routing the reply to the FromEP
        /// of the original message, after copying the original
        /// session ID into the reply.
        /// </para>
        /// <note>
        /// Messages passed to this method should considered to be owned by the 
        /// messaging library and must not be accessed or reused by application code.
        /// </note>
        /// </remarks>
        public void ReplyTo(Msg msg, Msg reply)
        {
            Assertion.Test(!reply._InUse, "Illegal message reuse.");
            reply._InUse = true;

            if (msg._FromEP == null)
                throw new ArgumentNullException(msgReplyToNull);

            reply._ToEP = msg._FromEP.Clone(true);
            reply._SessionID = msg._SessionID;

            if (reply._TTL == 0)
                reply._TTL = defMsgTTL == 255 ? defMsgTTL : defMsgTTL + 1;

            reply._Trace(this, 0, "ReplyTo: " + msg.GetType().Name, null);
            OnReceive(null, reply);

            // Give the session manager the chance to cache the reply.

            SessionManager.OnReply(reply);
        }

        /// <summary>
        /// Initiates the delivery of a reply to a request whose
        /// <see cref="MsgRequestContext" /> is passed.
        /// </summary>
        /// <param name="context">The <see cref="MsgRequestContext" /> holding state about the original request message.</param>
        /// <param name="reply">The reply.</param>
        /// <remarks>
        /// <note>
        /// This is not a general purpose method.  It is designed to be
        /// called only by the <see cref="MsgRequestContext" /> type.
        /// </note>
        /// <para>
        /// Use of this override helps to save memory since the entire
        /// request message doesn't need to be retained throughout the
        /// time that the request is being processed since <see cref="MsgRequestContext" />
        /// copies only the few message header fields necessary to submit
        /// the reply.
        /// </para>
        /// <para>
        /// This method works by routing the reply to the FromEP
        /// of the original message, after copying the original
        /// session ID into the reply.
        /// </para>
        /// <note>
        /// Messages passed to this method should considered to be owned by the 
        /// messaging library and must not be accessed or reused by application code.
        /// </note>
        /// </remarks>
        internal void ReplyTo(MsgRequestContext context, Msg reply)
        {
            Assertion.Test(!reply._InUse, "Illegal message reuse.");
            reply._InUse = true;

            if (context.FromEP == null)
                throw new ArgumentNullException(msgReplyToNull);

            reply._ToEP = context.FromEP;
            reply._SessionID = context.SessionID;

            if (reply._TTL == 0)
                reply._TTL = defMsgTTL == 255 ? defMsgTTL : defMsgTTL + 1;

            reply._Trace(this, 0, "ReplyTo: " + context.TraceName, null);
            OnReceive(null, reply);

            // Give the session manager the chance to cache the reply.

            SessionManager.OnReply(reply);
        }

        /// <summary>
        /// Initiates a broadcast of the message passed.
        /// </summary>
        /// <param name="msg">The message.</param>
        /// <remarks>
        /// <note>
        /// Broadcast has meaning only for logical endpoints.
        /// </note>
        /// <note>
        /// Messages passed to this method should considered to be owned by the 
        /// messaging library and must not be accessed or reused by application code.
        /// </note>
        /// </remarks>
        public void Broadcast(Msg msg)
        {
            Assertion.Test(!msg._InUse, "Illegal message reuse.");
            msg._InUse = true;

            msg._Flags |= MsgFlag.Broadcast;

            if (msg._ToEP == null)
                throw new ArgumentNullException(msgNullToEP, "msg");

            if (!msg._ToEP.IsLogical)
                throw new ArgumentException(msgNonLogicalBroadcast);

            if (msg._TTL == 0)
                msg._TTL = defMsgTTL == 255 ? defMsgTTL : defMsgTTL + 1;

            msg._Trace(this, 0, "Broadcast", null);
            OnReceive(null, msg);
        }

        /// <summary>
        /// Initiates a broadcast of the message passed.
        /// </summary>
        /// <param name="toEP">The target endpoint.</param>
        /// <param name="msg">The message.</param>
        /// <remarks>
        /// <note>
        /// Broadcast has meaning only for logical endpoints.
        /// </note>
        /// <note>
        /// Messages passed to this method should considered to be owned by the 
        /// messaging library and must not be accessed or reused by application code.
        /// </note>
        /// </remarks>
        public void BroadcastTo(MsgEP toEP, Msg msg)
        {
            Assertion.Test(!msg._InUse, "Illegal message reuse.");
            msg._InUse = true;

            msg._Flags |= MsgFlag.Broadcast;

            if (toEP == null)
                throw new ArgumentNullException(msgNullToEP, "toEP");

            if (!toEP.IsLogical)
                throw new ArgumentException(msgNonLogicalBroadcast);

            msg._ToEP = toEP.Clone(true);

            if (msg._TTL == 0)
                msg._TTL = defMsgTTL == 255 ? defMsgTTL : defMsgTTL + 1;

            msg._Trace(this, 0, "BroadcastTo", null);
            OnReceive(null, msg);
        }

        /// <summary>
        /// Initiates a broadcast of the message passed.
        /// </summary>
        /// <param name="toEP">The target endpoint.</param>
        /// <param name="fromEP">The source endpoint (or <c>null</c>).</param>
        /// <param name="msg">The message.</param>
        /// <remarks>
        /// <note>
        /// Broadcast has meaning only for logical endpoints.
        /// </note>
        /// <note>
        /// Messages passed to this method should considered to be owned by the 
        /// messaging library and must not be accessed or reused by application code.
        /// </note>
        /// </remarks>
        public void BroadcastTo(MsgEP toEP, MsgEP fromEP, Msg msg)
        {
            Assertion.Test(!msg._InUse, "Illegal message reuse.");
            msg._InUse = true;

            msg._Flags |= MsgFlag.Broadcast;

            if (toEP == null)
                throw new ArgumentNullException(msgNullToEP, "toEP");

            if (!toEP.IsLogical)
                throw new ArgumentException(msgNonLogicalBroadcast);

            if (fromEP != null)
                fromEP = fromEP.Clone(true);

            msg._ToEP = toEP.Clone(true);
            msg._FromEP = fromEP;

            if (msg._TTL == 0)
                msg._TTL = defMsgTTL == 255 ? defMsgTTL : defMsgTTL + 1;

            msg._Trace(this, 0, "BroadcastTo", null);
            OnReceive(null, msg);
        }

        /// <summary>
        /// Initiates an asynchronous query/response pattern against an endpoint, using a
        /// <see cref="QuerySession" /> instance to manage the operation.
        /// </summary>
        /// <param name="toEP">The target endpoint.</param>
        /// <param name="query">The query message.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The async result used to track the operation.</returns>
        /// <remarks>
        /// <para>
        /// The query will attempt to establish a session with the server up
        /// to <see cref="SessionRetries" /> times, with each attempt waiting
        /// timespan of up to <see cref="SessionTimeout" />.  Once a session
        /// is established, the query will wait up to <see cref="SessionTimeout" />
        /// for a query response or wait for <see cref="SessionKeepAliveMsg" />
        /// messages as specified by their <see cref="SessionKeepAliveMsg.SessionTTL" />
        /// properties.
        /// </para>
        /// <para>
        /// Message handlers implementing the server side of a Q/R session
        /// must use the <see cref="ReplyTo(LillTek.Messaging.Msg, LillTek.Messaging.Msg)" /> 
        /// method to send the reply message to enable session caching to work properly.
        /// </para>
        /// <note>
        /// Messages passed to this method should considered to be owned by the 
        /// messaging library and must not be accessed or reused by application code.
        /// </note>
        /// <note>
        /// Each call to <see cref="BeginQuery" /> must be matched with a call to <see cref="EndQuery" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginQuery(MsgEP toEP, Msg query, AsyncCallback callback, object state)
        {
            QuerySession session;
            AsyncQueryResult arQuery;

            using (TimedLock.Lock(this.syncLock))
            {

                // Create a default session manager if one doesn't already exist.

                if (sessionMgr == null)
                {

                    sessionMgr = new SessionManager();
                    sessionMgr.Init(this);
                }
            }

            session = new QuerySession();
            arQuery = new AsyncQueryResult(session, sessionMgr, callback, state);
           
            Guid    sessionID;

            if ((query._Flags & MsgFlag.KeepSessionID) != 0 && query._SessionID != Guid.Empty)
                sessionID = query._SessionID;
            else
                sessionID = Helper.NewGuid();

            session.InitClient(this,sessionMgr,TimeSpan.FromTicks(sessionTimeout.Ticks * SessionRetries),sessionID);
            session.BeginQuery(toEP, query, onQueryDone, arQuery);

            arQuery.Started();
            return arQuery;
        }

        /// <summary>
        /// Handles async query completions.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnQueryDone(IAsyncResult ar)
        {
            var arQuery = (AsyncQueryResult)ar.AsyncState;

            try
            {
                arQuery.Reply = arQuery.Session.EndQuery(ar);
                arQuery.Notify();
            }
            catch (Exception e)
            {
                arQuery.Notify(e);
            }
        }

        /// <summary>
        /// Completes the execution of an asynchronous query operation.
        /// </summary>
        /// <param name="ar">The async result returned by <see cref="BeginQuery" />.</param>
        /// <returns>The query response message.</returns>
        /// <remarks>
        /// <note>
        /// This method will automatically throw a <see cref="SessionException" />
        /// message when an acknowledgement message is received that implements
        /// <see cref="IAck" /> and has a non-empty <see cref="IAck.Exception" /> property.
        /// </note>
        /// <para>
        /// Message handlers implementing the server side of a Q/R session
        /// must use the <see cref="ReplyTo(LillTek.Messaging.Msg, LillTek.Messaging.Msg)" /> 
        /// method to send the reply message to enable session caching to work properly.
        /// </para>
        /// <note>
        /// Each call to <see cref="BeginQuery" /> must be matched with
        /// a call to <see cref="EndQuery" />.
        /// </note>
        /// </remarks>
        public Msg EndQuery(IAsyncResult ar)
        {
            var     arQuery = (AsyncQueryResult)ar;
            IAck    ack;

            arQuery.Wait();

            try
            {
                if (arQuery.Exception != null)
                    throw arQuery.Exception;

                // Throw a SessionException if the response implements IAck
                // and has a non-empty Exception property.

                ack = arQuery.Reply as IAck;
                if (ack != null && !string.IsNullOrWhiteSpace(ack.Exception))
                    throw SessionException.Create(ack.ExceptionTypeName, ack.Exception);

                return arQuery.Reply;
            }
            finally
            {
                arQuery.Dispose();
            }
        }

        /// <summary>
        /// Initiates a synchronous query/response pattern against an endpoint, using a
        /// <see cref="QuerySession" /> instance to manage the operation.
        /// </summary>
        /// <param name="toEP">The target endpoint.</param>
        /// <param name="query">The query message.</param>
        /// <returns>The query response message.</returns>
        /// <remarks>
        /// <para>
        /// The query will attempt to establish a session with the server up
        /// to <see cref="SessionRetries" /> times, with each attempt waiting
        /// timespan of up to <see cref="SessionTimeout" />.  Once a session
        /// is established, the query will wait up to <see cref="SessionTimeout" />
        /// for a query response or wait for <see cref="SessionKeepAliveMsg" />
        /// messages as specified by their <see cref="SessionKeepAliveMsg.SessionTTL" />
        /// properties.
        /// </para>
        /// <note>
        /// This method will automatically throw a <see cref="SessionException" />
        /// when an acknowledgement message is received that implements
        /// <see cref="IAck" /> and has a non-empty <see cref="IAck.Exception" /> property.
        /// </note>
        /// <para>
        /// Message handlers implementing the server side of a Q/R session
        /// must use the <see cref="ReplyTo(LillTek.Messaging.Msg, LillTek.Messaging.Msg)" /> 
        /// method to send the reply message to enable session caching to work properly.
        /// </para>
        /// <note>
        /// Messages passed to this method should considered to be owned by the 
        /// messaging library and must not be accessed or reused by application code.
        /// </note>
        /// </remarks>
        public Msg Query(MsgEP toEP, Msg query)
        {
            var ar = BeginQuery(toEP, query, null, null);

            return EndQuery(ar);
        }

        /// <summary>
        /// Initiates an asynchronous parallel query by executing the individual queries specified
        /// by a <see cref="LillTek.Messaging.ParallelQuery" /> instance.  This uses a <see cref="ParallelQuerySession" /> 
        /// to manage the operation.
        /// </summary>
        /// <param name="parallelQuery">The parallel query specifying the individual query operations.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The async result used to track the operation.</returns>
        /// <remarks>
        /// <para>
        /// This method initiates parallel queries for each of the query specified in
        /// in the <see cref="LillTek.Messaging.ParallelQuery" /> passed and then
        /// collects the responses.  The <see cref="LillTek.Messaging.ParallelQuery.WaitMode" />
        /// controls when the method returns.  The default value is <see cref="ParallelWait.ForAll" />
        /// which has the method wait for all of the queries to complete (either 
        /// successfully or due to an error).  <see cref="ParallelWait.ForAny" />
        /// indicates that the 
        /// </para>
        /// <note>
        /// Messages passed to this method should considered to be owned by the 
        /// messaging library and cannot be reused.
        /// </note>
        /// <note>
        /// Each call to <see cref="BeginParallelQuery" /> must be matched with a call to <see cref="EndParallelQuery" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginParallelQuery(ParallelQuery parallelQuery, AsyncCallback callback, object state)
        {
            ParallelQuerySession    session;
            AsyncResult             arParallel;

            using (TimedLock.Lock(this.syncLock))
            {
                // Create a default session manager if one doesn't already exist.

                if (sessionMgr == null)
                {
                    sessionMgr = new SessionManager();
                    sessionMgr.Init(this);
                }
            }

            session    = new ParallelQuerySession();
            arParallel = new AsyncParallelQueryResult(session, sessionMgr, callback, state);

            session.InitClient(this, sessionMgr, TimeSpan.FromTicks(sessionTimeout.Ticks * SessionRetries), Helper.NewGuid());
            session.BeginParallelQuery(parallelQuery, onParallelQueryDone, arParallel);

            arParallel.Started();
            return arParallel;
        }

        /// <summary>
        /// Handles async parallel query completions.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnParallelQueryDone(IAsyncResult ar)
        {
            var arParallel = (AsyncParallelQueryResult)ar.AsyncState;

            try
            {
                arParallel.Session.EndParallelQuery(ar);
                arParallel.Notify();
            }
            catch (Exception e)
            {
                arParallel.Notify(e);
            }
        }

        /// <summary>
        /// Completes the execution of an asynchronous parallel query operation.
        /// </summary>
        /// <param name="ar">The async result returned by <see cref="BeginParallelQuery" />.</param>
        /// <returns>The <see cref="ParallelQuery" /> instance holding the individual queries and results.</returns>
        /// <remarks>
        /// <note>
        /// This method <b>does not</b> automatically throw a <see cref="SessionException" />
        /// when an acknowledgement message is received that implements
        /// <see cref="IAck" /> and has a non-empty <see cref="IAck.Exception" /> property.
        /// Instead, the exception will be added to the appropriate <see cref="ParallelOperation" />
        /// instance within the query.
        /// </note>
        /// <note>
        /// Each call to <see cref="BeginParallelQuery" /> must be matched with
        /// a call to <see cref="EndParallelQuery" />.
        /// </note>
        /// </remarks>
        public ParallelQuery EndParallelQuery(IAsyncResult ar)
        {
            var arParallel    = (AsyncParallelQueryResult)ar;
            var parallelQuery = arParallel.Session.Query;

            arParallel.Wait();

            try
            {
                if (arParallel.Exception != null)
                    throw arParallel.Exception;

                return parallelQuery;
            }
            finally
            {
                arParallel.Dispose();
            }
        }

        /// <summary>
        /// Performs a synchronous <see cref="ParallelQuerySession" /> operation.
        /// </summary>
        /// <param name="parallelQuery">The parallel query specifying the individual query operations.</param>
        /// <returns>The parallel query with added responses.</returns>
        /// <remarks>
        /// <para>
        /// This method initiates parallel queries for each of the query messages
        /// passed in the <see cref="LillTek.Messaging.ParallelQuery" /> parameter passed and then
        /// collects the responses.  The <see cref="LillTek.Messaging.ParallelQuery"/>.<see cref="LillTek.Messaging.ParallelQuery.WaitMode" />
        /// controls when the method returns.  The default value is <see cref="ParallelWait.ForAll" />
        /// which has the method wait for all of the queries to complete (either 
        /// successfully or due to an error).  <see cref="ParallelWait.ForAny" />
        /// indicates that the 
        /// </para>
        /// <note>
        /// Messages passed to this method should considered to be owned by the 
        /// messaging library and must not be accessed or reused by application code.
        /// </note>
        /// </remarks>
        public ParallelQuery ParallelQuery(ParallelQuery parallelQuery)
        {
            var ar = BeginParallelQuery(parallelQuery, null, null);

            return EndParallelQuery(ar);
        }

        /// <summary>
        /// Creates a client side session of the specified type.
        /// </summary>
        /// <param name="sessionType">The session type (this must implement <see cref="ISession" />.</param>
        /// <returns>The new client session.</returns>
        public ISession CreateSession(System.Type sessionType)
        {
            ISession session;

            using (TimedLock.Lock(this.syncLock))
            {
                // Create a default session manager if one doesn't already exist.

                if (sessionMgr == null)
                {
                    sessionMgr = new SessionManager();
                    sessionMgr.Init(this);
                }
            }

            session = Helper.CreateInstance<ISession>(sessionType);
            session.InitClient(this, sessionMgr, sessionTimeout, Helper.NewGuid());

            return session;
        }

        /// <summary>
        /// Creates a <see cref="ReliableTransferSession" /> and associates it 
        /// with this message router.
        /// </summary>
        /// <returns>The new <see cref="ReliableTransferSession" /> instance.</returns>
        public ReliableTransferSession CreateReliableTransferSession()
        {
            return (ReliableTransferSession)CreateSession(typeof(ReliableTransferSession));
        }

        /// <summary>
        /// Creates a <see cref="ReliableTransferSession" /> and associates it 
        /// with this message router.
        /// </summary>
        /// <returns>The new <see cref="ReliableTransferSession" /> instance.</returns>
        public DuplexSession CreateDuplexSession()
        {
            return (DuplexSession)CreateSession(typeof(DuplexSession));
        }

        /// <summary>
        /// Generates the set of LogicalAdvertiseMsg instances that hold the
        /// logical endpoints consumed by the router.
        /// </summary>
        /// <param name="logicalEPs">A set of logical endpoints to add (or <c>null</c>).</param>
        /// <param name="addDispatch"><c>true</c> if the endpoints with application handlers should be added.</param>
        /// <returns>A list of zero or more LogicalAdvertiseMsgs.</returns>
        public LogicalAdvertiseMsg[] GenLogicalAdvertiseMsgs(List<LogicalRoute> logicalEPs, bool addDispatch)
        {
            Dictionary<string, MsgEP>   endpoints;
            LogicalAdvertiseMsg[]       result;
            LogicalAdvertiseMsg         msg;
            MsgEP                       ep;
            int                         pos;

            endpoints = new Dictionary<string, MsgEP>();

            if (logicalEPs != null)
            {
                for (int i = 0; i < logicalEPs.Count; i++)
                {
                    ep = logicalEPs[i].LogicalEP;
                    if (!endpoints.ContainsKey(ep.ToString()))
                        endpoints.Add(ep.ToString(), ep);
                }
            }

            if (addDispatch)
            {
                foreach (LogicalRoute route in dispatcher.LogicalRoutes)
                {
                    ep = route.LogicalEP;
                    if (!endpoints.ContainsKey(ep.ToString()))
                        endpoints.Add(ep.ToString(), ep);
                }
            }

            if (endpoints.Count == 0)
                return new LogicalAdvertiseMsg[0];

            result = new LogicalAdvertiseMsg[endpoints.Count % maxAdvertiseEPs == 0 ? endpoints.Count / maxAdvertiseEPs : endpoints.Count / maxAdvertiseEPs + 1];
            msg    = null;
            pos    = 0;

            foreach (MsgEP logicalEP in endpoints.Values)
            {
                if (msg == null)
                    msg = new LogicalAdvertiseMsg(routerEP, this.AppName, this.AppDescription, this.RouterInfo, udpEP.Port, tcpEP.Port, dispatcher.LogicalEndpointSetID);

                msg.AddLogicalEP(logicalEP);

                if (msg.EndpointCount >= maxAdvertiseEPs)
                {
                    result[pos++] = msg;
                    msg = null;
                }
            }

            if (pos < result.Length)
            {
                Assertion.Test(msg != null);
                result[pos++] = msg;
            }

            Assertion.Test(pos == result.Length);

            return result;
        }

        /// <summary>
        /// Handles received <see cref="DeadRouterMsg" /> messages.
        /// </summary>
        /// <param name="msg">The message.</param>
        /// <remarks>
        /// <para>
        /// If the message received indicates that this router instance is the dead router
        /// and the logical endpoint set ID matches this router's current set ID, then
        /// this method generates a new set ID and multicasts a RouterAdvertiseMsg to the
        /// subnet.
        /// </para>
        /// <para>
        /// If the message indicates that another router is dead, then the method will
        /// purge the router from the routing tables.
        /// </para>
        /// </remarks>
        [MsgHandler]
        public void OnMsg(DeadRouterMsg msg)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    return;

                if (msg.RouterEP.Equals(routerEP))
                {
                    if (msg.LogicalEndpointSetID == dispatcher.LogicalEndpointSetID)
                        dispatcher.RefreshLogicalEndpointSetID();
                }
                else
                {
                    physRoutes.Remove(msg.RouterEP);
                    logicalRoutes.Flush(msg.LogicalEndpointSetID);
                }
            }
        }

        /// <summary>
        /// Handles <see cref="ReceiptMsg" /> messages received from routers
        /// in an effort to quickly detect dead routers.
        /// </summary>
        /// <param name="msg">The receipt message.</param>
        /// <remarks>
        /// The method passes the message to the router's <see cref="MsgTracker" />
        /// instance's <see cref="MsgTracker.OnReceiptMsg" /> method so that it
        /// can close out tracking for the original message.
        /// </remarks>
        [MsgHandler]
        public void OnMsg(ReceiptMsg msg)
        {
            msgTracker.OnReceiptMsg(msg);
        }

        /// <summary>
        /// Used by Unit tests to control whether idled TCP channels will be 
        /// automatically closed.  Defaults to true.
        /// </summary>
        public bool IdleCheck 
        {
            get {return idleCheck;}
            set {idleCheck = value;}
        }

        /// <summary>
        /// Used by Unit tests to get the physical routes currently maintained 
        /// by the router.
        /// </summary>
        public PhysicalRoute[] GetPhysicalRoutes()
        {
            PhysicalRoute[]     routes;

            using (TimedLock.Lock(this.SyncRoot))
            {
                if (this.physRoutes == null)
                    return new PhysicalRoute[0];

                routes = this.physRoutes.ToArray();
            }

            Array.Sort(routes);
            return routes;
        }

        /// <summary>
        /// Used by Unit tests to queue the message passed for sending
        /// rather than sending it immediately.  A subsequent <see cref="Send" /> or
        /// <see cref="Multicast" /> call should send the message specified in the call
        /// and then transmit the queued messages.
        /// </summary>
        /// <param name="toEP">Channel endpoint of the target router.</param>
        /// <param name="msg">The message to queue.</param>
        internal void QueueTo(ChannelEP toEP, Msg msg) 
        {
            using (TimedLock.Lock(this.syncLock))
            {
                if (!isOpen)
                    throw new MsgException(msgNotStarted);

                if (msg._TTL == 0)
                    msg._TTL = defMsgTTL == 255 ? defMsgTTL : defMsgTTL+1;

                switch (toEP.Transport) {

                    case Transport.Tcp :

                        TcpChannel  tcpChannel;

                        tcpChannels.TryGetValue(toEP.NetEP, out tcpChannel);

                        if (tcpChannel == null)
                        {
                            tcpChannel = new TcpChannel(this);
                            tcpChannels.Add(toEP.NetEP, tcpChannel);
                            tcpChannel.Connect(toEP.NetEP, null);
                        }

                        tcpChannel.QueueTo(toEP,msg);
                        break;

                    case Transport.Udp :

                        udpUnicast.QueueTo(toEP, msg);
                        break;

                    case Transport.Multicast :

                        if (udpMulticast == null)
                            throw new MsgException(msgNoMulticast);
                        else 
                            udpMulticast.QueueTo(toEP, msg);

                        break;

                    default :

                        Assertion.Fail("Unexpected Transport type.");
                        break;
                }
            }
        }

        /// <summary>
        /// Used by Unit tests to determine whether an open TCP channel 
        /// exists to the router at the specfied remote endpoint.
        /// </summary>
        /// <param name="ep">The remote endpoint.</param>
        /// <returns><c>true</c> if the channel is open.</returns>
        internal bool IsTcpChannelOpen(IPEndPoint ep) 
        {
            using (TimedLock.Lock(this.syncLock)) 
            {
                TcpChannel  tcpChannel;

                tcpChannels.TryGetValue(ep, out tcpChannel);

                return tcpChannel != null;
            }
        }

        /// <summary>
        /// Used by Unit tests that need to track the number of
        /// mapped TCP channels.
        /// </summary>
        internal int MappedTcpChannelCount
        {
            get 
            {
                using (TimedLock.Lock(this.syncLock)) 
                    return tcpChannels.Count;
            }
        }

        /// <summary>
        /// Used by Unit tests that need to track the number of
        /// pending TCP channels.
        /// </summary>
        internal int PendingTcpChannelCount 
        {
            get 
            {
                using (TimedLock.Lock(this.syncLock))
                    return tcpPending.Count;
            }
        }

        /// <summary>
        /// Used by Unit tests to force TCP channels to limit themselves
        /// to reading/writing one byte at a time to exercise the send/receive
        /// buffer alignment handling.
        /// </summary>
        internal bool FragmentTcp 
        {
            get { return fragmentTcp; }
            set { fragmentTcp = value; }
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better deadlock
        /// diagnostic information.
        /// </summary>
        /// <returns>The process unique lock key for this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
