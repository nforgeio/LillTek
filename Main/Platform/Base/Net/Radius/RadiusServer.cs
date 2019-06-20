//-----------------------------------------------------------------------------
// FILE:        RadiusServer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a lightweight, extensible RADIUS server.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Radius
{
    /// <summary>
    /// Called by <see cref="RadiusServer" /> retreive the NAS credentials for the network access service device
    /// whose IP address is passed.
    /// </summary>
    /// <param name="address">IP address of the NAS device.</param>
    /// <returns>The <see cref="RadiusNasInfo" /> for the device if found, null otherwise.</returns>
    public delegate RadiusNasInfo RadiusNasInfoDelegate(IPAddress address);

    /// <summary>
    /// Called by <see cref="RadiusServer" /> authenticate user account credentials. 
    /// </summary>
    /// <param name="realm">The authentication scope.</param>
    /// <param name="account">The user account.</param>
    /// <param name="password">The password.</param>
    /// <returns><c>true</c> if the credentials are authentic.</returns>
    public delegate bool RadiusAuthenticateDelegate(string realm, string account, string password);

    /// <summary>
    /// Called by <see cref="RadiusServer" /> to log authentication events.
    /// </summary>
    /// <param name="logEntry">The log entry information.</param>
    /// <remarks>
    /// <para>
    /// Under the current implementation of <see cref="RadiusServer" /> this will be
    /// called for every successful and unsuccessful authentication attempt as well
    /// as situations where NAS device authentication failed.
    /// </para>
    /// </remarks>
    public delegate void RadiusLogDelegate(RadiusLogEntry logEntry);

    /// <summary>
    /// Called by <see cref="RadiusServer" /> in as a mechanism for unit tests
    /// to monitor the requests received.
    /// </summary>
    /// <param name="server">The server instance.</param>
    /// <param name="request">The received request.</param>
    /// <returns><c>true</c> if the packet is to be processed normally, <c>false</c> if it is to be ignored.</returns>
    internal delegate bool RadiusDiagnosticDelegate(RadiusServer server, RadiusPacket request);

    /// <summary>
    /// Implements a simple, lightweight, and extensible RADIUS server.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The current RADIUS Server implements processes only <b>Access-Request</b> packets
    /// sent to it holding a <b>NAS-IP-Address</b>, a <b>User-Name</b>, and a <b>User-Password</b>.
    /// All other packet types will be ignored.
    /// </para>
    /// <para>
    /// The server expects the authentication realm and account to be encoded in the <b>User-Name</b>.
    /// to be encoded the using one of the standard Windows notations:
    /// </para>
    /// <code language="none">
    ///     &lt;realm&gt; "\" &lt;account&gt;
    /// </code>
    /// <para>
    /// The server will respond with an <b>Access-Accept</b> or <b>Access-Reject</b> packet
    /// depending on whether the credentials can be authenticated.  At this point, the server
    /// does not attempt to implement the challenge/response authentication pattern.
    /// </para>
    /// <para>
    /// Network Access Services (NAS) devices such as a VPN router authenticate against 
    /// a RADIUS server by sending an <b>Access-Request</b> packet to the server.  This
    /// packet contains the credentials of the account to be authenticated including
    /// the <b>User-Name</b> and <b>User-Password</b>.  The RADIUS server and each NAS
    /// device share a secret (essentially a password).  The <b>User-Password</b> is
    /// encrypted by the NAS device using a randomly generated 16 byte authenticator,
    /// the shared secret and some bitwise XOR operations.  The RADIUS server can 
    /// reverse the operation to obtain the original password.
    /// </para>
    /// <para>
    /// Note that this shared secret must be configured on both the NAS device as
    /// well as the RADIUS server so that the password can be authenticated.  NAS
    /// devices are identified by the source IP address of the UDP packet received
    /// by the RADIUS server.  The server maps this IP address to the <see cref="RadiusNasInfo" />
    /// instance associated with the NAS device which holds the shared secret
    /// as well as other information about the device.
    /// </para>
    /// <para>
    /// The RADIUS server uses the <see cref="Devices" /> table to hold information 
    /// about each NAS device.  This table can be set before or after the server is 
    /// started.  Each <see cref="RadiusNasInfo" /> entry in this table holds the
    /// shared secret encoded as an ANSI password, the NAS device's IP address, and
    /// an optional DNS host name (and potentially other fields).
    /// </para>
    /// <para>
    /// If the NAS device IP address is known and fixed it can be specified explicitly
    /// in the <see cref="RadiusNasInfo.Address" /> field.  If a DNS (or WINS) host name is
    /// available for the device then the <see cref="RadiusNasInfo.Host" /> can be specified 
    /// and the IP address can be set to ANY.  The RADIUS server will perform periodic
    /// DNS lookups to resolve the host name into the IP address necessary to
    /// map inbound RADIUS packets to NAS device information.  The server is
    /// smart enough to handle multihomed DNS hosts.  The server constructors can
    /// also initialize the NAS sevices from a <see cref="RadiusServerSettings" />
    /// instance or from the application configuration.
    /// </para>
    /// <para>
    /// Applications that wish to override the default IP address to NAS device
    /// information mapping (perhaps by querying a database), can subscribe to the 
    /// <see cref="GetNasInfoEvent" />.
    /// </para>
    /// <para>
    /// A default NAS shared secret can be specified in the <see cref="RadiusServerSettings.DefaultSecret" />
    /// setting.  If a RADIUS packet is received from a NAS device in that is not 
    /// in the <see cref="Devices" /> list or was not found via the <see cref="GetNasInfoEvent" />
    /// then a non-<c>null</c> <see cref="RadiusServerSettings.DefaultSecret" /> will be used
    /// to decrypt the packet's password attribute.  This is a convienent feature
    /// for IT operations but its use will reduce the security of the service a bit.
    /// </para>
    /// <para>
    /// The <see cref="LogEvent" /> can be used by applications that wish to 
    /// log authentication related events.  This event will be called for each
    /// successful and unsuccessful authentication attempt.
    /// </para>
    /// <para>
    /// User credentials can be specified two ways.  One way is to use one of
    /// <see cref="LoadAccountsFromFile" /> or <see cref="LoadAccountsFromString" />
    /// methods.  These methods process the text line by line, adding any credentials
    /// found to an internal table.  Account credentials should be formatted as:
    /// </para>
    /// <code language="none">
    ///     &lt;realm&gt;;&lt;account&gt;;&lt;password&gt;;
    /// </code>
    /// <para>
    /// The server supports the user name parsing modes specified by the 
    /// <see cref="RealmFormat" /> enumeration and the <see cref="RadiusServerSettings.RealmFormat" />
    /// setting.  Currently, this includes formatting user names as:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term>
    ///         &lt;realm&gt;\&lt;account&gt;<br/>
    ///         &lt;realm&gt;/&lt;account&gt;
    ///         </term>
    ///         <description>
    ///         A forward or back slash is used to separate the
    ///         realm from the account.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>
    ///         &lt;account&gt;@&lt;realm&gt;
    ///         </term>
    ///         <description>
    ///         An @ sign is used to separate the account from
    ///         the realm using an email address like syntax.
    ///         </description>
    ///     </item>
    /// </list>
    /// <para>
    /// Blank lines and lines starting with "//" will be ignored as comments.  The
    /// realm and account are case insensitve but the password is case sensitive.
    /// </para>
    /// <para>
    /// The second method for authenticating user credentials is to subscribe to the
    /// <see cref="AuthenticateEvent" />.  Doing this will override the default
    /// behavior using the internal credentials table and give application code
    /// the chance to perform the authentication when the event is raised.
    /// </para>
    /// <para>
    /// At this point, the class implements only the SPAP authentication method.
    /// CHAP or any of the other more advanced methods are not currently supported.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class RadiusServer : ILockable
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// Used to hold a copy of the server state outside of a thread
        /// lock.  Note that the RadiusServer code below takes some care
        /// to ensure that these objects will remain invariant outside
        /// of locks.
        /// </summary>
        private sealed class ServerState
        {
            public Dictionary<IPAddress, RadiusNasInfo>     DeviceMap;
            public Dictionary<string, string>               Accounts;

            private event RadiusNasInfoDelegate             getNasInfoEvent;
            private event RadiusAuthenticateDelegate        authenticateEvent;
            private event RadiusLogDelegate                 logEvent;

            /// <summary>
            /// Initializes the instance.  This must be called within a lock.
            /// </summary>
            /// <param name="server"></param>
            public ServerState(RadiusServer server)
            {
                this.getNasInfoEvent   = server.GetNasInfoEvent;
                this.authenticateEvent = server.AuthenticateEvent;
                this.logEvent          = server.LogEvent;
                this.DeviceMap         = server.deviceMap;
                this.Accounts          = server.accounts;
            }

            public bool HasGetNasInfoEventHandler
            {
                get { return getNasInfoEvent != null; }
            }

            public RadiusNasInfo GetNasInfo(IPAddress address)
            {
                Assertion.Test(getNasInfoEvent != null);
                return getNasInfoEvent(address);
            }

            public bool HasAuthenticateEventHandler
            {
                get { return authenticateEvent != null; }
            }

            public bool Authenticate(string realm, string account, string password)
            {
                Assertion.Test(authenticateEvent != null);
                return authenticateEvent(realm, account, password);
            }

            public bool HasLogEventHandler
            {
                get { return logEvent != null; }
            }

            public void Log(bool success, RadiusLogEntryType entryType, string realm, string account, string message)
            {
                Assertion.Test(logEvent != null);
                logEvent(new RadiusLogEntry(success, entryType, realm, account, message));
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private const string NotRunning = "RADIUS server is not running.";

        private EnhancedSocket                          sock = null;            // The server's UDP socket
        private bool                                    isRunning = false;      // True if the server is running
        private GatedTimer                              bkTimer = null;         // Background task timer
        private byte[]                                  recvBuf = null;         // Packet receive buffer
        private EndPoint                                remoteEP = null;        // Remote endpoint for received packets
        private List<RadiusNasInfo>                     devices = null;         // The NAS device information
        private Dictionary<IPAddress, RadiusNasInfo>    deviceMap = null;       // Hash table mapping device IP address
                                                                                // to the device information
        private Dictionary<string, string>              accounts = null;        // Used to map realm + account to password
                                                                                // for local authentication
        private string                                  defSecret;              // The default NAS secret (or null)
        private NetworkBinding                          networkBinding;         // Network binding setting
        private IPEndPoint                              actualEndPoint;         // The server's actual endpoint
        private TimeSpan                                dnsRefreshInterval;     // Interval between DNS refresh
        private DateTime                                nextDnsRefresh;         // Next scheduled DNS refresh(SYS)
        private AsyncCallback                           onReceive;              // The packet receive callback
        private RealmFormat                             realmFormat;            // Specifies how realm and account
                                                                                // strings are to be parsed from user names

        /// <summary>
        /// Applications can subscribe to this event to customize the mapping between NAS devices
        /// and their associated information especially the shared secret.  Note that the
        /// event handler must be threadsafe.
        /// </summary>
        public event RadiusNasInfoDelegate GetNasInfoEvent;

        /// <summary>
        /// Applications can subscribe to this event to customize the credential validation.
        /// Note that the event handler must be threadsafe.
        /// </summary>
        public event RadiusAuthenticateDelegate AuthenticateEvent;

        /// <summary>
        /// Applications can subscribe to this event to be notified of authentication
        /// related log events.
        /// </summary>
        public event RadiusLogDelegate LogEvent;

        /// <summary>
        /// Available for use by unit tests for monitoring the packets received
        /// by the server.
        /// </summary>
        internal RadiusDiagnosticDelegate DiagnosticHook = null;

        /// <summary>
        /// Constructor.
        /// </summary>
        public RadiusServer()
        {
        }

        /// <summary>
        /// Returns the server's network endpoint when the server is running.
        /// </summary>
        public IPEndPoint EndPoint
        {
            get
            {
                using (TimedLock.Lock(this))
                {
                    if (!isRunning)
                        throw new RadiusException(NotRunning);

                    return actualEndPoint;
                }
            }
        }

        /// <summary>
        /// Starts the server using settings gathered from the application
        /// configuration.
        /// </summary>
        /// <param name="keyPrefix">
        /// The key prefix to use when loading server settings from the 
        /// application configuration.
        /// </param>
        /// <remarks>
        /// <para>
        /// The RADIUS server settings are loaded from the application
        /// configuration, using the specified key prefix.  See 
        /// <see cref="RadiusServerSettings.LoadConfig" /> for a description
        /// of the server application configuration settings.
        /// </para>
        /// <note>
        /// All successful calls to <b>Start()</b> must eventually be matched
        /// with a call to <see cref="Stop" /> so that system resources will be 
        /// released promptly.
        /// </note>
        /// </remarks>
        public void Start(string keyPrefix)
        {
            Start(RadiusServerSettings.LoadConfig(keyPrefix));
        }

        /// <summary>
        /// Starts the server using the settings passed.
        /// </summary>
        /// <param name="settings">The server settings.</param>
        /// <remarks>
        /// <note>
        /// All successful calls to <b>Start()</b> must eventually be matched
        /// with a call to <see cref="Stop" /> so that system resources will be 
        /// released promptly.
        /// </note>
        /// </remarks>
        public void Start(RadiusServerSettings settings)
        {
            using (TimedLock.Lock(this))
            {
                if (isRunning)
                    throw new RadiusException("RADIUS server has already started.");

                this.sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                this.sock.Bind(settings.NetworkBinding);
                this.sock.SendBufferSize    = settings.SocketBuffer;
                this.sock.ReceiveBufferSize = settings.SocketBuffer;

                this.isRunning              = true;
                this.defSecret              = settings.DefaultSecret;
                this.networkBinding         = settings.NetworkBinding;
                this.dnsRefreshInterval     = settings.DnsRefreshInterval;
                this.nextDnsRefresh         = SysTime.Now + dnsRefreshInterval;
                this.bkTimer                = new GatedTimer(new TimerCallback(OnBkTask), null, settings.BkTaskInterval, settings.BkTaskInterval);
                this.recvBuf                = new byte[TcpConst.MTU];
                this.Devices                = settings.Devices;
                this.onReceive              = new AsyncCallback(OnReceive);
                this.remoteEP               = new IPEndPoint(IPAddress.Any, 0);
                this.realmFormat            = settings.RealmFormat;

                if (networkBinding.Address.Equals(IPAddress.Any))
                    actualEndPoint = new IPEndPoint(NetHelper.GetActiveAdapter(), ((IPEndPoint)sock.LocalEndPoint).Port);
                else
                    actualEndPoint = new IPEndPoint(networkBinding.Address, ((IPEndPoint)sock.LocalEndPoint).Port);

                this.sock.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref remoteEP, onReceive, null);
            }
        }

        /// <summary>
        /// Stops the server if it's currently running.
        /// </summary>
        public void Stop()
        {
            using (TimedLock.Lock(this))
            {

                if (sock != null)
                    sock.Close();

                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                devices           = null;
                deviceMap         = null;
                accounts          = null;
                LogEvent          = null;
                AuthenticateEvent = null;
                GetNasInfoEvent   = null;
                isRunning         = false;
            }

            Thread.Sleep(1000);     // Give any pending authentication event calls a chance
                                    // to unwind.
        }

        /// <summary>
        /// Returns <c>true</c> if the server is running.
        /// </summary>
        public bool IsRunning
        {
            get { return isRunning; }
        }

        /// <summary>
        /// Information about the NAS devices known to the RADIUS server
        /// including the shared secret or <c>null</c> if the application is going
        /// to handle device management via <see cref="GetNasInfoEvent" />.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The items in this list should be considered to be read-only
        /// and should not be modified directly.  If you wish to update this list,
        /// create a new list with the new entries and then assign the new list
        /// to this property.
        /// </note>
        /// </remarks>
        public List<RadiusNasInfo> Devices
        {
            get
            {
                if (!isRunning)
                    throw new RadiusException(NotRunning);

                return devices;
            }

            set
            {
                using (TimedLock.Lock(this))
                {
                    if (!isRunning)
                        throw new RadiusException(NotRunning);

                    devices = value;
                }

                LoadDeviceMap(value);
            }
        }

        /// <summary>
        /// Loads the hash table mapping the NAS device IP address to the
        /// device information, resolving device host names as necessary.
        /// Note that this method should be called outside of a lock
        /// so as not to block the server while performing the DNS lookups.
        /// </summary>
        private void LoadDeviceMap(List<RadiusNasInfo> devices)
        {
            var newMap = new Dictionary<IPAddress, RadiusNasInfo>();

            if (devices != null)
            {
                foreach (RadiusNasInfo device in devices)
                {
                    if (device.Host == null)
                        newMap[device.Address] = device;
                    else
                    {
                        try
                        {
                            var hostEntry = Dns.GetHostEntry(device.Host);

                            foreach (IPAddress address in hostEntry.AddressList)
                                newMap[address] = device;
                        }
                        catch (Exception e)
                        {
                            SysLog.LogException(e);
                        }
                    }
                }
            }

            using (TimedLock.Lock(this))
            {
                if (!isRunning)
                    return;

                deviceMap = newMap;
            }
        }

        /// <summary>
        /// Loads the local account map from a <see cref="TextReader" />.
        /// </summary>
        /// <param name="reader">The reader.</param>
        private void LoadAccounts(TextReader reader)
        {
            var         newAccounts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string      line;
            string[]    fields;
            int         lineNum = 0;

            for (line = reader.ReadLine(); line != null; line = reader.ReadLine())
            {
                lineNum++;

                line = line.Trim();
                if (line.Length == 0 || line.StartsWith("//"))
                    continue;

                fields = line.Split(new char[] { ';' }, StringSplitOptions.None);
                if (fields.Length != 3)
                    throw new RadiusException("Invalid account credentials on line [{0}].", lineNum);

                newAccounts[fields[0].Trim() + ":" + fields[1].Trim()] = fields[2].Trim();
            }

            using (TimedLock.Lock(this))
            {
                if (!isRunning)
                    throw new RadiusException(NotRunning);

                accounts = newAccounts;
            }
        }

        /// <summary>
        /// Loads the local account map from an ANSI encoded text file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <remarks>
        /// <para>
        /// Account credentials should be formatted one to a text line as:
        /// </para>
        /// <code language="none">
        ///     &lt;realm&gt;;&lt;account&gt;;&lt;password&gt;;
        /// </code>
        /// <para>
        /// Blank lines and lines starting with "//" will be ignored as comments.  The
        /// realm and account are case insensitve but the password is case sensitive.
        /// </para>
        /// </remarks>
        public void LoadAccountsFromFile(string path)
        {
            var reader = new StreamReader(path, Helper.AnsiEncoding);

            try
            {
                LoadAccounts(reader);
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        /// Loads the local account map from a string.
        /// </summary>
        /// <param name="value">The accounts string.</param>
        /// <remarks>
        /// <para>
        /// Account credentials should be formatted one to a text line as:
        /// </para>
        /// <code language="none">
        ///     &lt;realm&gt;;&lt;account&gt;;&lt;password&gt;;
        /// </code>
        /// <para>
        /// Blank lines and lines starting with "//" will be ignored as comments.  The
        /// realm and account are case insensitve but the password is case sensitive.
        /// </para>
        /// </remarks>
        public void LoadAccountsFromString(string value)
        {
            var reader = new StringReader(value);

            try
            {
                LoadAccounts(reader);
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        /// Implements background task processing.
        /// </summary>
        /// <param name="o">Not used.</param>
        private void OnBkTask(object o)
        {
            if (!isRunning)
                return;

            try
            {
                if (SysTime.Now >= nextDnsRefresh)
                {
                    LoadDeviceMap(devices);
                    nextDnsRefresh = SysTime.Now + dnsRefreshInterval;
                }

                // $todo(jeff.lill): 
                //
                // If I really wanted to get fancy, I'd poll for
                // IP address changes if networkBinding.Address == IPAddress.Any
                // to transparently handle DHCP lease changes.
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Returns the information associated with the NAS device whose
        /// IP address is passed.  This method assumes that the lock is
        /// NOT held by the current thread.
        /// </summary>
        /// <param name="state">The cloned server state.</param>
        /// <param name="address">The NAS device IP address.</param>
        /// <returns>The <see cref="RadiusNasInfo" /> record associated with the NAS device or <c>null</c>.</returns>
        private RadiusNasInfo GetNasInfo(ServerState state, IPAddress address)
        {
            RadiusNasInfo nasInfo;

            if (state.HasGetNasInfoEventHandler)
                return state.GetNasInfo(address);

            if (state.DeviceMap == null)
                return null;

            if (state.DeviceMap.TryGetValue(address, out nasInfo))
                return nasInfo;

            return null;
        }

        /// <summary>
        /// Authenticates the account credentials passed.  This method
        /// assumes that the lock is NOT held by the current thread.
        /// </summary>
        /// <param name="state">The cloned server state.</param>
        /// <param name="realm">The authentication source.</param>
        /// <param name="account">The account name.</param>
        /// <param name="password">The password.</param>
        /// <returns><c>true</c> if the credential were authenticated.</returns>
        private bool Authenticate(ServerState state, string realm, string account, string password)
        {
            string foundPassword;

            if (state.HasAuthenticateEventHandler)
                return state.Authenticate(realm, account, password);

            if (state.Accounts == null)
                return false;

            if (!state.Accounts.TryGetValue(realm + ":" + account, out foundPassword))
                return false;

            return password == foundPassword;
        }

        /// <summary>
        /// Raises the <see cref="LogEvent" />.  Assumes that the lock is already held by the 
        /// current thread.
        /// </summary>
        /// <param name="state">The cloned server state.</param>
        /// <param name="success">True for successful authentication events, false for failures.</param>
        /// <param name="entryType">The log entry type.</param>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The user account.</param>
        /// <param name="format">A human readable message format.</param>
        /// <param name="args">The message arguments.</param>
        private void Log(ServerState state, bool success, RadiusLogEntryType entryType, string realm, string account, string format, params object[] args)
        {
            if (state.HasLogEventHandler)
                state.Log(success, entryType, realm, account, string.Format(format, args));
        }

        /// <summary>
        /// Handle asynchronous packet reception.
        /// </summary>
        /// <param name="ar">The operation's <see cref="IAsyncResult" /> instance.</param>
        private void OnReceive(IAsyncResult ar)
        {
            RadiusPacket    request = null;
            ServerState     state   = null;
            int             cbRecv;

            using (TimedLock.Lock(this))
            {
                // Finish receiving the next request packet

                try
                {
                    cbRecv = sock.EndReceiveFrom(ar, ref remoteEP);
                }
                catch (SocketClosedException)
                {
                    // We'll see this when the RADIUS server instance is closed.
                    // I'm not going to report this to the event log.

                    return;
                }
                catch (Exception e)
                {
                    // I'm going to assume that something really bad has
                    // happened to the socket, log the exception and then
                    // return without initiating another receive.  This
                    // effectively stops the server.

                    SysLog.LogException(e);
                    return;
                }

                // Parse the request.  We're going to initiate the
                // authentication below, outside of the lock.

                try
                {
                    request = new RadiusPacket((IPEndPoint)remoteEP, recvBuf, cbRecv);

                    // Unit tests can use this hook to monitor incoming packets
                    // as well cause them to be ignored.

                    if (DiagnosticHook != null && !DiagnosticHook(this, request))
                        request = null;

                    if (request != null && request.Code == RadiusCode.AccessRequest)
                    {
                        state = new ServerState(this);
                    }
                    else
                    {
                        // Ignore all RADIUS requests except for Access-Request

                        request = null;
                    }
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }

                // Initiate reception of the next request

                try
                {
                    sock.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref remoteEP, onReceive, null);
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }

            if (request == null)
                return; // We're ignoring the packet

            // Validate the packet and the NAS 

            RadiusNasInfo   nasInfo;
            IPAddress       nasIPAddress;
            byte[]          nasIdentifier;
            string          userName;
            string          realm;
            string          account;
            byte[]          encryptedPassword;
            string          password;

            if (!request.GetAttributeAsAddress(RadiusAttributeType.NasIpAddress, out nasIPAddress) &&
                !request.GetAttributeAsBinary(RadiusAttributeType.NasIdentifier, out nasIdentifier))
            {
                // Access-Request packets are required by RFC 2865 to have either a NAS-IP-Address 
                // or a NAS-IP-Identifier attribute.  Discard any packets that don't have one
                // of these.

                return;
            }

            if (!request.GetAttributeAsText(RadiusAttributeType.UserName, out userName) ||
                !request.GetAttributeAsBinary(RadiusAttributeType.UserPassword, out encryptedPassword))
            {
                // The User-Name attribute is required by RFC 2865 and this implementation
                // requires a User-Password attribute.  Ignore packets without these.

                return;
            }

            // Parse the realm and account from the user name

            Helper.ParseUserName(realmFormat, userName, out realm, out account);

            // Lookup the NAS shared secret and decrypt the password.

            nasInfo = GetNasInfo(state, request.SourceEP.Address);
            if (nasInfo == null)
            {
                if (defSecret == null)
                {
                    // Not being able to find information about a NAS device could
                    // represent a serious security problem or attack so I'm going
                    // to log this.

                    Log(state, false, RadiusLogEntryType.UnknownNas, realm, account, "RADIUS: Unknown NAS device NAS=[{0}].", request.SourceEP);
                    return;
                }

                nasInfo = new RadiusNasInfo(request.SourceEP.Address, defSecret);
            }

            password = request.DecryptUserPassword(encryptedPassword, nasInfo.Secret);

            // Perform the authentication, compute the response
            // authenticator and then send a response packet.

            RadiusPacket response;

            if (Authenticate(state, realm, account, password))
            {
                Log(state, true, RadiusLogEntryType.Authentication, realm, account,
                    "Authenticated: realm=[{0}] account=[{1}] NAS=[{2}]", realm, account, request.SourceEP);

                response = new RadiusPacket(RadiusCode.AccessAccept, request.Identifier, null,
                                            new RadiusAttribute(RadiusAttributeType.ServiceType, (int)RadiusServiceType.Login));
            }
            else
            {
                Log(state, false, RadiusLogEntryType.Authentication, realm, account,
                    "Authentication Fail: realm=[{0}] account=[{1}] NAS=[{2}]", realm, account, request.SourceEP);

                response = new RadiusPacket(RadiusCode.AccessReject, request.Identifier, null);
            }

            response.ComputeResponseAuthenticator(request, nasInfo.Secret);

            try
            {
                sock.SendTo(response.ToArray(), request.SourceEP);
            }
            catch (SocketClosedException)
            {
                // We'll see this when the RADIUS server instance is closed.
                // I'm not going to report this to the event log.
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
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
