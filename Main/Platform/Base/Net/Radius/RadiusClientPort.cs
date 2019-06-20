//-----------------------------------------------------------------------------
// FILE:        RadiusClientPort.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the client side of the RADIUS protocol on a single UDP port.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Diagnostics;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Net.Sockets;

// $todo(jeff.lill): 
//
// I'm holding locks while I'm synchronously performing DNS
// resolutions.  This probably won't be too much of a problem
// if the host name is valid since the EnhancedDns class
// caches responses in-process.  This may be more of a problem
// we have host names that don't resolve (perhaps EnhancedDns
// should be modified to cache NAKs).

namespace LillTek.Net.Radius
{
    /// <summary>
    /// Implements the client side of the RADIUS protocol on a single UDP port.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is pretty easy to use.  Simply instantiate an instance, 
    /// call <see cref="Open" /> and then use <see cref="Authenticate" /> to
    /// initiate synchronous authentication operation or <see cref="BeginAuthenticate" />
    /// and <see cref="EndAuthenticate" /> to initiate an asynchronous operation.
    /// Then call <see cref="Close" /> when the client instance is no longer needed. 
    /// </para>
    /// <note>
    /// Due to the fact that the RADIUS protocol provides for only an
    /// 8-bit identifier to be used to correlate request and response packets,
    /// it is very possible for this identifier to wrap-around under high
    /// loads.  Use the <see cref="RadiusClient" /> class rather than this one,
    /// specifying a <see cref="RadiusClientSettings.PortCount" /> value large
    /// enough to avoid this problem.
    /// </note>
    /// <note>
    /// A single <see cref="RadiusClient" /> instance can be used to
    /// intiate multiple simultaneous authentication requests. 
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true" />
    internal sealed class RadiusClientPort : ILockable
    {
        //---------------------------------------------------------------------
        // Private types

        private sealed class AuthTransaction
        {
            public string           UserName;       // User name being authenticated
            public RadiusPacket     Packet;         // The RADIUS packet
            public DateTime         TTR;            // Time-to-retry for this transaction (SYS)
            public AsyncResult      AsyncResult;    // Async operation
            public int              SendCount;      // # of times a request has been sent
            public int              ServerPos;      // Index of the last server a packet was sent to

            public AuthTransaction(string userName, RadiusPacket packet, int serverPos, DateTime ttr, AsyncResult ar)
            {
                this.UserName    = userName;
                this.Packet      = packet;
                this.AsyncResult = ar;
                this.SendCount   = 1;
                this.ServerPos   = serverPos;
                this.TTR         = ttr;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private const string NotOpen = "RADIUS client is not open.";

        private EnhancedSocket      sock = null;            // The client's UDP socket
        private bool                isOpen = false;         // True if the client is open
        private NetworkBinding[]    servers = null;         // The RADIUS server network bindings
        private string              secret = null;          // The shared secret
        private int                 serverPos = 0;          // Index of the next round robin server
        private AuthTransaction[]   transactions = null;    // Outstanding authentication transactions indexed by ID
        private TimeSpan            retryInterval;          // Max time to wait for a response before retrying
        private NetworkBinding      networkBinding;         // IP address the socket is bound to
        private IPAddress           nasIPAddress;           // IP address to be used in the NAS-IP-Address attributes
        private int                 maxTransmissions;       // Max number of packet transmissions
        private int                 nextID;                 // The next packet identifier
        private AsyncCallback       onReceive;              // Delegate that handles received packets
        private byte[]              recvBuf;                // Packet receive buffer
        private EndPoint            sourceEP;               // Receives the source endpoint for received packets
        private RealmFormat         realmFormat;            // Specifies how realm and account strings are
                                                            // to be assembled into user names

        /// <summary>
        /// Constructor.
        /// </summary>
        public RadiusClientPort()
        {
        }

        /// <summary>
        /// Opens a RADIUS client port using the <see cref="RadiusClientSettings" /> passed.
        /// </summary>
        /// <param name="settings">The client settings.</param>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="Open" /> must eventually be matched
        /// with a call to <see cref="Close" /> so that system resources will be 
        /// released promptly.
        /// </note>
        /// </remarks>
        public void Open(RadiusClientSettings settings)
        {
            using (TimedLock.Lock(this))
            {
                if (isOpen)
                    throw new RadiusException("RADIUS client port is already open.");

                sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sock.Bind(settings.NetworkBinding);
                this.sock.SendBufferSize = settings.SocketBuffer;
                this.sock.ReceiveBufferSize = settings.SocketBuffer;

                isOpen           = true;
                networkBinding   = settings.NetworkBinding;
                servers          = settings.Servers;
                secret           = settings.Secret;
                retryInterval    = settings.RetryInterval;
                maxTransmissions = settings.MaxTransmissions;
                realmFormat      = settings.RealmFormat;

                nextID           = 0;
                serverPos        = 0;
                transactions     = new AuthTransaction[256];
                recvBuf          = new byte[TcpConst.MTU];
                sourceEP         = new IPEndPoint(IPAddress.Any, 0);
                onReceive        = new AsyncCallback(OnReceive);

                if (networkBinding.Address.Equals(IPAddress.Any))
                    nasIPAddress = NetHelper.GetActiveAdapter();
                else
                    nasIPAddress = networkBinding.Address;

                // Initiate reception of the first RADIUS packet.

                sock.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref sourceEP, onReceive, null);
            }
        }

        /// <summary>
        /// Closes the client, releasing all resources.
        /// </summary>
        public void Close()
        {
            using (TimedLock.Lock(this))
            {
                if (sock != null)
                    sock.Close();

                if (transactions != null)
                {
                    // Terminate any outstanding authentication requests.

                    foreach (AuthTransaction transaction in transactions)
                        if (transaction != null)
                            transaction.AsyncResult.Notify(new RadiusException("RADIUS client is closed."));

                    transactions = null;
                }

                isOpen = false;
                servers = null;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the client is currently open.
        /// </summary>
        public bool IsOpen
        {
            get { return isOpen; }
        }

        /// <summary>
        /// Searches for an available transaction ID and returns it or -1
        /// if no slot is available.    This method assumes that
        /// a lock is already held on the current instance.
        /// </summary>
        private int GetTransactionID()
        {
            int slotStart = nextID;
            int slot;

            do
            {
                if (transactions[nextID] == null)
                {
                    slot   = nextID;
                    nextID = (++nextID) % transactions.Length;
                    return slot;
                }

                nextID = (++nextID) % transactions.Length;

            } while (slotStart != nextID);

            return -1;
        }

        /// <summary>
        /// Returns the <see cref="IPEndPoint" /> of the next server in the round robin rotation
        /// or <see cref="NetworkBinding.Any" /> if no server host resolves.  This method assumes that
        /// a lock is already held on the current instance.
        /// </summary>
        /// <param name="serverPos">The current round robin position in the servers list.</param>
        private IPEndPoint GetServerBinding(ref int serverPos)
        {
            NetworkBinding  curBinding = servers[serverPos];
            IPHostEntry     entry;
            IPAddress[]     addresses;

            if (servers.Length == 1)
            {
                try
                {
                    if (curBinding.IsHost)
                    {
                        entry = EnhancedDns.GetHostByName(curBinding.Host);
                        if (entry == null)
                            return NetworkBinding.Any;

                        addresses = entry.AddressList.IPv4Only();

                        if (addresses.Length == 0)
                            return NetworkBinding.Any;

                        return new IPEndPoint(addresses[0], curBinding.Port);
                    }
                    else
                        return new IPEndPoint(curBinding.Address, curBinding.Port);
                }
                catch
                {
                    return NetworkBinding.Any;
                }
            }
            else
            {
                int serverStart = serverPos;

                do
                {
                    if (curBinding.IsHost)
                    {
                        try
                        {
                            entry = EnhancedDns.GetHostByName(curBinding.Host);
                        }
                        catch
                        {
                            entry = null;
                        }

                        serverPos = (++serverPos) % servers.Length;

                        if (entry != null)
                        {
                            addresses = entry.AddressList.IPv4Only();
                            if (addresses.Length > 0)
                                return new IPEndPoint(addresses[0], curBinding.Port);
                        }
                    }
                    else
                    {
                        serverPos = (++serverPos) % servers.Length;
                        return curBinding;
                    }

                    curBinding = servers[serverPos];

                } while (serverPos != serverStart);

                return NetworkBinding.Any;
            }
        }

        /// <summary>
        /// Initiates an asynchronous operation to authenticate user credentials. 
        /// </summary>
        /// <param name="realm">Specifies the authentication scope.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">The password.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation.</returns>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginAuthenticate" /> must eventually be
        /// matched by a call to <see cref="EndAuthenticate" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginAuthenticate(string realm, string account, string password, AsyncCallback callback, object state)
        {
            AsyncResult         arAuth = new AsyncResult(null, callback, state);
            AuthTransaction     transaction;
            RadiusPacket        packet;
            string              userName;
            int                 ID;
            IPEndPoint          binding;

            using (TimedLock.Lock(this))
            {
                userName = Helper.GetUserName(realmFormat, realm, account);

                ID = GetTransactionID();
                if (ID == -1)
                    throw new RadiusException("RADIUS client cannot track more than 256 simultaneous authentication requests.");

                binding = GetServerBinding(ref serverPos);
                if (binding == NetworkBinding.Any)
                    throw new RadiusException("None of the RADIUS server hosts resolve to an IP address.");

                packet = new RadiusPacket(RadiusCode.AccessRequest, ID, Crypto.Rand(16));
                packet.Attributes.Add(new RadiusAttribute(RadiusAttributeType.UserName, userName));
                packet.Attributes.Add(new RadiusAttribute(RadiusAttributeType.UserPassword, packet.EncryptUserPassword(password, secret)));
                packet.Attributes.Add(new RadiusAttribute(RadiusAttributeType.NasIpAddress, nasIPAddress));

                transaction      = new AuthTransaction(userName, packet, serverPos, SysTime.Now + retryInterval, arAuth); ;
                transactions[ID] = transaction;
                sock.SendTo(transaction.Packet.ToArray(), binding);

                arAuth.Result = null;
                arAuth.Started();
            }

            return arAuth;
        }

        /// <summary>
        /// Completes an asynchronous authentication operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginAuthenticate" />.</param>
        /// <returns><c>true</c> if the credentials were authenticated.</returns>
        /// <exception cref="TimeoutException">Thrown if the retry interval and maximum transmissions have been reached.</exception>
        /// <exception cref="RadiusException">Thrown for RADIUS related problems.</exception>
        public bool EndAuthenticate(IAsyncResult ar)
        {
            var arAuth = (AsyncResult)ar;

            arAuth.Wait();
            try
            {
                if (arAuth.Exception != null)
                    throw arAuth.Exception;

                if (arAuth.Result == null)
                    throw new InvalidOperationException("RADIUS client: Unitialized authentication result.");

                return (bool)arAuth.Result;
            }
            finally
            {
                arAuth.Dispose();
            }
        }

        /// <summary>
        /// Performs a synchronous operation to authenticate user credentials.
        /// </summary>
        /// <param name="realm"></param>
        /// <param name="account"></param>
        /// <param name="password"></param>
        /// <returns><c>true</c> if the credentials were authenticated.</returns>
        /// <exception cref="TimeoutException">Thrown if the retry interval and maximum transmissions have been reached.</exception>
        /// <exception cref="RadiusException">Thrown for RADIUS related problems.</exception>
        public bool Authenticate(string realm, string account, string password)
        {
            var ar = BeginAuthenticate(realm, account, password, null, null);

            return EndAuthenticate(ar);
        }

        /// <summary>
        /// Handles the asynchronous reception of UDP packets on the socket.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance.</param>
        private void OnReceive(IAsyncResult ar)
        {
            AuthTransaction     transaction;
            RadiusPacket        response;
            byte[]              rawPacket;
            int                 cbPacket;

            // Complete receiving the packet and then initiate reception
            // of the next packet.  Note that I don't need a lock here
            // because there's never more than one async packet receive
            // outstanding at a given time.

            try
            {
                cbPacket  = sock.EndReceiveFrom(ar, ref sourceEP);
                rawPacket = (byte[])recvBuf.Clone();
                sock.BeginReceiveFrom(recvBuf, 0, recvBuf.Length, SocketFlags.None, ref sourceEP, onReceive, null);

                response = new RadiusPacket((IPEndPoint)sourceEP, rawPacket, cbPacket);
            }
            catch (SocketClosedException)
            {
                // We'll see this when the RADIUS server instance is closed.
                // I'm not going to report this to the event log.

                return;
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
                return;
            }

            // Map the packet to a transaction and signal that the
            // authentication transaction is complete.

            using (TimedLock.Lock(this))
            {
                if (!isOpen)
                    return;

                transaction = transactions[response.Identifier];
                if (transaction == null)
                    return;     // The transaction must have been aborted

                // Validate the response authenticator, discarding the packet
                // if it's not valid.

                if (!response.VerifyResponseAuthenticator(transaction.Packet, secret))
                    transaction.AsyncResult.Result = false;
                else
                {
                    // Complete the outstanding transaction if the packet is a
                    // valid answer.

                    switch (response.Code)
                    {
                        case RadiusCode.AccessAccept:

                            transaction.AsyncResult.Result = true;
                            break;

                        case RadiusCode.AccessReject:

                            transaction.AsyncResult.Result = false;
                            break;

                        default:

                            return;     // Ignore all other answers
                    }
                }

                transaction.AsyncResult.Notify();
                transactions[response.Identifier] = null;
            }
        }

        /// <summary>
        /// Implements background task processing.  This must be called periodically
        /// by the parent <see cref="RadiusClient" /> instance.
        /// </summary>
        public void OnBkTask()
        {
            var now = SysTime.Now;

            try
            {

                // $todo(jeff.lill): 
                //
                // I'm seeing an occasional NullReferenceException being 
                // thrown by NetworkInterface.GetAllNetworkInterfaces()
                // for no apparent reason so I'm going to comment this
                // code out for now.

#if TODO
                // It's possible that the machine's IP address has changed due to
                // a DHCP lease renewal or waking up on a different network.
                // I'm going to periodically look for the actual IP address 
                // if the binding is set to IPAddress.Any.

                if (networkBinding.Address.Equals(IPAddress.Any))
                    nasIPAddress = NetHelper.GetActiveAdapter();
#endif

                // Handle pending transaction retries and timeouts

                using (TimedLock.Lock(this))
                {
                    if (!isOpen)
                        return;

                    // Look for transactions that haven't seen a response for retryInterval.
                    // Abort those transactions where we've already retried sending the
                    // packet for the maximum number of times, send a retry packet for
                    // the other transactions.

                    for (int i = 0; i < transactions.Length; i++)
                    {
                        var transaction = transactions[i];

                        if (transaction == null || transaction.TTR >= now)
                            continue;

                        if (transaction.SendCount >= maxTransmissions)
                        {
                            // Abort the request with a TimeoutException.

                            transaction.AsyncResult.Notify(new TimeoutException());
                            transactions[i] = null;
                        }
                        else
                        {
                            // Retry sending the request packet

                            var binding = GetServerBinding(ref transaction.ServerPos);

                            if (binding == NetworkBinding.Any)
                            {
                                // We didn't get an IP address so abort the transaction.

                                transaction.AsyncResult.Notify(new RadiusException("None of the RADIUS server hosts resolve to an IP address."));
                                transactions[i] = null;
                                return;
                            }

                            transaction.SendCount++;
                            sock.SendTo(transaction.Packet.ToArray(), binding);
                        }
                    }
                }
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
