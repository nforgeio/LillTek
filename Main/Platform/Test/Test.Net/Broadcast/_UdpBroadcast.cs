//-----------------------------------------------------------------------------
// FILE:        _UdpBroadcast.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Net.Sockets;
using LillTek.Testing;

namespace LillTek.Net.Broadcast
{
    [TestClass]
    public class _UdpBroadcast
    {

        private object syncLock = new object();

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_ServerCluster_DiscoverImmediate()
        {
            // Launch three server instances and verify that they quickly discover each other.

            UdpBroadcastServer server1 = null;
            UdpBroadcastServer server2 = null;
            UdpBroadcastServer server3 = null;

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server2
    NetworkBinding           = 127.0.0.1:10222
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server3
    NetworkBinding           = 127.0.0.1:10223
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                server2 = new UdpBroadcastServer("Server2");
                server3 = new UdpBroadcastServer("Server3");

                Thread.Sleep(3000);     // Wait a few seconds for the servers to discover each other.

                Assert.IsTrue(server1.IsMaster);
                Assert.IsFalse(server2.IsMaster);
                Assert.IsFalse(server3.IsMaster);

                var server1State = server1.GetServers();
                var server2State = server2.GetServers();
                var server3State = server3.GetServers();

                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (server2 != null)
                    server2.Close();

                if (server3 != null)
                    server3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_ServerCluster_DiscoverDelayed()
        {
            // Launch two server instances and verify that they quickly discover each other.
            // Then wait for a bit before starting the third instance and then verify that all
            // three instances discover the others.

            UdpBroadcastServer server1 = null;
            UdpBroadcastServer server2 = null;
            UdpBroadcastServer server3 = null;

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server2
    NetworkBinding           = 127.0.0.1:10222
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server3
    NetworkBinding           = 127.0.0.1:10223
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                server2 = new UdpBroadcastServer("Server2");

                Thread.Sleep(3000);     // Wait a few seconds for the two servers to discover each other.

                Assert.IsTrue(server1.IsMaster);
                Assert.IsFalse(server2.IsMaster);

                var server1State = server1.GetServers();
                var server2State = server2.GetServers();

                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());

                // Now start the third server and verify discovery

                server3 = new UdpBroadcastServer("Server3");

                Thread.Sleep(3000);     // Wait a few seconds for the two servers to discover each other.

                Assert.IsTrue(server1.IsMaster);
                Assert.IsFalse(server2.IsMaster);
                Assert.IsFalse(server3.IsMaster);

                server1State = server1.GetServers();
                server2State = server2.GetServers();

                var server3State = server3.GetServers();

                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (server2 != null)
                    server2.Close();

                if (server3 != null)
                    server3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_ServerCluster_NetFailAndRejoin()
        {
            // Launch two server instances and verify that they quickly discover each other.
            // Then wait for a bit before starting the third instance and then verify that all
            // three instances discover the others.  Then simulate a network failure of one of
            // the servers for a while, verify that the server is purged from the remaining
            // instances, then simulate a network fix, and verify that the server is discovered.

            UdpBroadcastServer server1 = null;
            UdpBroadcastServer server2 = null;
            UdpBroadcastServer server3 = null;

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15M
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server2
    NetworkBinding           = 127.0.0.1:10222
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server3
    NetworkBinding           = 127.0.0.1:10223
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                server2 = new UdpBroadcastServer("Server2");

                Thread.Sleep(3000);     // Wait a few seconds for the two servers to discover each other.

                Assert.IsTrue(server1.IsMaster);
                Assert.IsFalse(server2.IsMaster);

                var server1State = server1.GetServers();
                var server2State = server2.GetServers();

                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());

                // Now start the third server and verify discovery

                server3 = new UdpBroadcastServer("Server3");

                Thread.Sleep(3000);     // Wait a few seconds for the two servers to discover each other.

                Assert.IsTrue(server1.IsMaster);
                Assert.IsFalse(server2.IsMaster);
                Assert.IsFalse(server3.IsMaster);

                server1State = server1.GetServers();
                server2State = server2.GetServers();

                var server3State = server3.GetServers();

                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                // Simulate the failure of server1, verify that the other servers recognize that
                // it goes offline and that server2 becomes the cluster master.

                server1.PauseNetwork = true;

                Thread.Sleep(6000);

                Assert.IsTrue(server2.IsMaster);
                Assert.IsFalse(server3.IsMaster);

                server2State = server2.GetServers();
                server3State = server3.GetServers();

                Assert.AreEqual(0, server2State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreEqual(0, server3State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                // Simulate a network fix and verify that the other servers rediscover server1 and
                // that server1 resumes its role as the cluster master.

                server1.PauseNetwork = false;

                Thread.Sleep(3000);

                Assert.IsTrue(server1.IsMaster);
                Assert.IsFalse(server2.IsMaster);
                Assert.IsFalse(server3.IsMaster);

                server1State = server1.GetServers();
                server2State = server2.GetServers();
                server3State = server3.GetServers();

                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (server2 != null)
                    server2.Close();

                if (server3 != null)
                    server3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_ServerCluster_Close()
        {
            // Launch three server instances and verify that they quickly discover each other.
            // Then perform a graceful close on server1 and verify that the other servers 
            // quickly determine that server1 is gone and that server2 becomes the cluster master.      

            UdpBroadcastServer server1 = null;
            UdpBroadcastServer server2 = null;
            UdpBroadcastServer server3 = null;

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server2
    NetworkBinding           = 127.0.0.1:10222
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server3
    NetworkBinding           = 127.0.0.1:10223
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                server2 = new UdpBroadcastServer("Server2");
                server3 = new UdpBroadcastServer("Server3");

                Thread.Sleep(3000);     // Wait a few seconds for the three servers to discover each other.

                Assert.IsTrue(server1.IsMaster);
                Assert.IsFalse(server2.IsMaster);
                Assert.IsFalse(server3.IsMaster);

                var server1State = server1.GetServers();
                var server2State = server2.GetServers();
                var server3State = server3.GetServers();

                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                // Now, gracefully close server1 and wait long enough for the ServerUnregister messages
                // to be delivered to the other servers, but not long enough for the the TTL to expire
                // for server1.  Then verify that server1 has been removed from the other server tables
                // and server2 is now the cluster master.

                IPEndPoint server1Endpoint = server1.EndPoint;

                server1.Close();
                Thread.Sleep(1000);

                server1State = server1.GetServers();
                server2State = server2.GetServers();
                server3State = server3.GetServers();

                Assert.IsTrue(server2.IsMaster);
                Assert.IsFalse(server3.IsMaster);

                Assert.AreEqual(0, server2State.Where(s => s.EndPoint.Equals(server1Endpoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreEqual(0, server3State.Where(s => s.EndPoint.Equals(server1Endpoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (server2 != null)
                    server2.Close();

                if (server3 != null)
                    server3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Server_SecurityFailKey()
        {
            // Start three servers with server1 and server2 having the same shared key and server3
            // having a different key.  Then verify that server1 and server2 discover each other
            // and server3 is isolated.

            UdpBroadcastServer server1 = null;
            UdpBroadcastServer server2 = null;
            UdpBroadcastServer server3 = null;

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server2
    NetworkBinding           = 127.0.0.1:10222
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server3
    NetworkBinding           = 127.0.0.1:10223
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:ilvsQmaUXZHsNqHn3hoCLqaMu23bpUlQE06uCqjDEiw=:AnXHmk8ta+zjXGfcvsKdWw==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                server2 = new UdpBroadcastServer("Server2");
                server3 = new UdpBroadcastServer("Server3");

                Thread.Sleep(3000);     // Wait a few seconds for the three servers to discover each other.

                Assert.IsTrue(server1.IsMaster);
                Assert.IsFalse(server2.IsMaster);
                Assert.IsTrue(server3.IsMaster);    // Server3 will also be a master because it's isolated

                var server1State = server1.GetServers();
                var server2State = server2.GetServers();
                var server3State = server3.GetServers();

                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreEqual(0, server1State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreEqual(0, server2State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreEqual(0, server3State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreEqual(0, server3State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (server2 != null)
                    server2.Close();

                if (server3 != null)
                    server3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Server_SecurityFailTimestamp()
        {
            // Start three servers (with the same shared key and verify the cluster.  Then configure server3 to 
            // issue messages with a a timestamp far in the past and verify that server1 and server2 do not discover
            // server.  Perform the same test with a timestamp far in the future.  Then reset server3 to issue proper
            // timestamps and verify that it joins the cluster.

            UdpBroadcastServer server1 = null;
            UdpBroadcastServer server2 = null;
            UdpBroadcastServer server3 = null;

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server2
    NetworkBinding           = 127.0.0.1:10222
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server3
    NetworkBinding           = 127.0.0.1:10223
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    Server[2]                = 127.0.0.1:10223
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                server2 = new UdpBroadcastServer("Server2");
                server3 = new UdpBroadcastServer("Server3");

                Thread.Sleep(3000);     // Wait a few seconds for the three servers to discover each other.

                Assert.IsTrue(server1.IsMaster);
                Assert.IsFalse(server2.IsMaster);
                Assert.IsFalse(server3.IsMaster);

                var server1State = server1.GetServers();
                var server2State = server2.GetServers();
                var server3State = server3.GetServers();

                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                // Simulate timestamps far in the past for server3.

                server3.FixedTimestampUtc = DateTime.UtcNow - TimeSpan.FromDays(356);

                Thread.Sleep(7000);

                server1State = server1.GetServers();
                server2State = server2.GetServers();
                server3State = server3.GetServers();

                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreEqual(0, server1State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreEqual(0, server2State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreEqual(0, server3State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                // Simulate timestamps far in the future for server3.

                server3.FixedTimestampUtc = DateTime.UtcNow + TimeSpan.FromDays(356);

                Thread.Sleep(7000);

                server1State = server1.GetServers();
                server2State = server2.GetServers();
                server3State = server3.GetServers();

                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreEqual(0, server1State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreEqual(0, server2State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreEqual(0, server3State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                // Reset timestamps to normal

                server3.FixedTimestampUtc = DateTime.MinValue;

                Thread.Sleep(3000);

                server1State = server1.GetServers();
                server2State = server2.GetServers();
                server3State = server3.GetServers();

                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server3State.Where(s => s.EndPoint.Equals(server3.EndPoint)).ToArray().Count());
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (server2 != null)
                    server2.Close();

                if (server3 != null)
                    server3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Client_Discover()
        {
            // Start one server and three clients, verify that the server discovers the clients.

            UdpBroadcastServer server1 = null;
            UdpBroadcastServer server2 = null;
            UdpBroadcastClient client1 = null;
            UdpBroadcastClient client2 = null;
            UdpBroadcastClient client3 = null;

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server2
    NetworkBinding           = 127.0.0.1:10222
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Client1
    NetworkBinding           = 127.0.0.1:11221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client2
    NetworkBinding           = 127.0.0.1:11222
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client3
    NetworkBinding           = 127.0.0.1:11223
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                server2 = new UdpBroadcastServer("Server2");
                client1 = new UdpBroadcastClient("Client1");
                client2 = new UdpBroadcastClient("Client2");
                client3 = new UdpBroadcastClient("Client3");

                Thread.Sleep(3000);     // Wait a few seconds for server to discover the clients.

                var server1State = server1.GetClients();
                var server2State = server2.GetClients();

                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(client1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(client2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(client3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(client1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(client2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(client3.EndPoint)).ToArray().Count());
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (server2 != null)
                    server2.Close();

                if (client1 != null)
                    client1.Close();

                if (client2 != null)
                    client2.Close();

                if (client3 != null)
                    client3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Client_NetFailAndRejoin()
        {
            // Start two servers and three clients, verify that the servers discover the clients.
            // Then simulate a network failure of client3 and verify that it gets purged on the servers.  
            // Then simulate a network fix for client3 and verify that it is rediscovered.

            UdpBroadcastServer server1 = null;
            UdpBroadcastServer server2 = null;
            UdpBroadcastClient client1 = null;
            UdpBroadcastClient client2 = null;
            UdpBroadcastClient client3 = null;

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server2
    NetworkBinding           = 127.0.0.1:10222
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Client1
    NetworkBinding           = 127.0.0.1:11221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client2
    NetworkBinding           = 127.0.0.1:11222
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client3
    NetworkBinding           = 127.0.0.1:11223
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                server2 = new UdpBroadcastServer("Server2");
                client1 = new UdpBroadcastClient("Client1");
                client2 = new UdpBroadcastClient("Client2");
                client3 = new UdpBroadcastClient("Client3");

                Thread.Sleep(3000);     // Wait a few seconds for server to discover the clients.

                var server1State = server1.GetClients();
                var server2State = server2.GetClients();

                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(client1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(client2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(client3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(client1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(client2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(client3.EndPoint)).ToArray().Count());

                // Simulate a network failure for client3, wait a bit and verify that 
                // client3 is purged from the servers.

                client3.PauseNetwork = true;
                Thread.Sleep(7000);

                server1State = server1.GetClients();
                server2State = server2.GetClients();

                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(client1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(client2.EndPoint)).ToArray().Count());
                Assert.AreEqual(0, server1State.Where(s => s.EndPoint.Equals(client3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(client1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(client2.EndPoint)).ToArray().Count());
                Assert.AreEqual(0, server2State.Where(s => s.EndPoint.Equals(client3.EndPoint)).ToArray().Count());

                // Simulate a network fix and verify that client3 is rediscovered.

                client3.PauseNetwork = false;
                Thread.Sleep(3000);

                server1State = server1.GetClients();
                server2State = server2.GetClients();

                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(client1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(client2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(client3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(client1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(client2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(client3.EndPoint)).ToArray().Count());
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (server2 != null)
                    server2.Close();

                if (client1 != null)
                    client1.Close();

                if (client2 != null)
                    client2.Close();

                if (client3 != null)
                    client3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Client_Close()
        {
            // Start two servers and three clients, verify that the servers discover the clients.
            // Then close the clients one by one, verifying that the clients are quickly removed
            // from the server state.

            UdpBroadcastServer server1 = null;
            UdpBroadcastServer server2 = null;
            UdpBroadcastClient client1 = null;
            UdpBroadcastClient client2 = null;
            UdpBroadcastClient client3 = null;

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server2
    NetworkBinding           = 127.0.0.1:10222
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Client1
    NetworkBinding           = 127.0.0.1:11221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client2
    NetworkBinding           = 127.0.0.1:11222
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client3
    NetworkBinding           = 127.0.0.1:11223
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                server2 = new UdpBroadcastServer("Server2");
                client1 = new UdpBroadcastClient("Client1");
                client2 = new UdpBroadcastClient("Client2");
                client3 = new UdpBroadcastClient("Client3");

                Thread.Sleep(3000);     // Wait a few seconds for server to discover the clients.

                var server1State = server1.GetClients();
                var server2State = server2.GetClients();

                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(client1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(client2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(client3.EndPoint)).ToArray().Count());

                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(client1.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(client2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(client3.EndPoint)).ToArray().Count());

                // Close client1 and verify.

                var client1EP = client1.EndPoint;

                client1.Close();
                Thread.Sleep(500);

                server1State = server1.GetClients();
                server2State = server2.GetClients();

                Assert.AreEqual(0, server1State.Where(s => s.EndPoint.Equals(client1EP)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(client2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(client3.EndPoint)).ToArray().Count());

                Assert.AreEqual(0, server2State.Where(s => s.EndPoint.Equals(client1EP)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(client2.EndPoint)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(client3.EndPoint)).ToArray().Count());

                // Close client2 and verify.

                var client2EP = client2.EndPoint;

                client2.Close();
                Thread.Sleep(500);

                server1State = server1.GetClients();
                server2State = server2.GetClients();

                Assert.AreEqual(0, server1State.Where(s => s.EndPoint.Equals(client1EP)).ToArray().Count());
                Assert.AreEqual(0, server1State.Where(s => s.EndPoint.Equals(client2EP)).ToArray().Count());
                Assert.AreNotEqual(0, server1State.Where(s => s.EndPoint.Equals(client3.EndPoint)).ToArray().Count());

                Assert.AreEqual(0, server2State.Where(s => s.EndPoint.Equals(client1EP)).ToArray().Count());
                Assert.AreEqual(0, server2State.Where(s => s.EndPoint.Equals(client2EP)).ToArray().Count());
                Assert.AreNotEqual(0, server2State.Where(s => s.EndPoint.Equals(client3.EndPoint)).ToArray().Count());

                // Close client3 and verify.

                var client3EP = client3.EndPoint;

                client3.Close();
                Thread.Sleep(500);

                server1State = server1.GetClients();
                server2State = server2.GetClients();

                Assert.AreEqual(0, server1State.Where(s => s.EndPoint.Equals(client1EP)).ToArray().Count());
                Assert.AreEqual(0, server1State.Where(s => s.EndPoint.Equals(client2EP)).ToArray().Count());
                Assert.AreEqual(0, server1State.Where(s => s.EndPoint.Equals(client3EP)).ToArray().Count());

                Assert.AreEqual(0, server2State.Where(s => s.EndPoint.Equals(client1EP)).ToArray().Count());
                Assert.AreEqual(0, server2State.Where(s => s.EndPoint.Equals(client2EP)).ToArray().Count());
                Assert.AreEqual(0, server2State.Where(s => s.EndPoint.Equals(client3EP)).ToArray().Count());
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (server2 != null)
                    server2.Close();

                if (client1 != null)
                    client1.Close();

                if (client2 != null)
                    client2.Close();

                if (client3 != null)
                    client3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Client_Broadcast_Simple()
        {
            // Start one server and three clients, have each client broadcast a message, and then verify
            // that the messages got through.

            UdpBroadcastServer server1 = null;
            UdpBroadcastClient client1 = null;
            UdpBroadcastClient client2 = null;
            UdpBroadcastClient client3 = null;
            Dictionary<string, bool> client1Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client2Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client3Msgs = new Dictionary<string, bool>();

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Client1
    NetworkBinding           = 127.0.0.1:11221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client2
    NetworkBinding           = 127.0.0.1:11222
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client3
    NetworkBinding           = 127.0.0.1:11223
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                client1 = new UdpBroadcastClient("Client1");
                client2 = new UdpBroadcastClient("Client2");
                client3 = new UdpBroadcastClient("Client3");

                Thread.Sleep(3000);     // Wait a few seconds for server to discover the clients.

                // Configure message receive handlers.

                client1.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client1Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client2.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client2Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client3.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client3Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                // Broadcast a message from each of the clients and then verify 
                // that they were received by all clients.

                client1.Broadcast(Helper.ToUTF8("client1"));
                client2.Broadcast(Helper.ToUTF8("client2"));
                client3.Broadcast(Helper.ToUTF8("client3"));

                Thread.Sleep(2000);

                Assert.IsTrue(client1Msgs.ContainsKey("client1"));
                Assert.IsTrue(client1Msgs.ContainsKey("client2"));
                Assert.IsTrue(client1Msgs.ContainsKey("client3"));

                Assert.IsTrue(client2Msgs.ContainsKey("client1"));
                Assert.IsTrue(client2Msgs.ContainsKey("client2"));
                Assert.IsTrue(client2Msgs.ContainsKey("client3"));

                Assert.IsTrue(client3Msgs.ContainsKey("client1"));
                Assert.IsTrue(client3Msgs.ContainsKey("client2"));
                Assert.IsTrue(client3Msgs.ContainsKey("client3"));
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (client1 != null)
                    client1.Close();

                if (client2 != null)
                    client2.Close();

                if (client3 != null)
                    client3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Server_Broadcast_Defaults()
        {
            // Start one server and three clients with the minimal default settings and 
            // verify that the clients are discovered and they can broadcast.

            UdpBroadcastServer server1 = null;
            UdpBroadcastClient client1 = null;
            UdpBroadcastClient client2 = null;
            UdpBroadcastClient client3 = null;
            Dictionary<string, bool> client1Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client2Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client3Msgs = new Dictionary<string, bool>();

            var cfg = @"

&section Client1
    Server[0] = 127.0.0.1:UDP-BROADCAST
&endsection

&section Client2
    Server[0] = 127.0.0.1:UDP-BROADCAST
&endsection

&section Client3
    Server[0] = 127.0.0.1:UDP-BROADCAST
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer(new UdpBroadcastServerSettings(), null, null);
                client1 = new UdpBroadcastClient("Client1");
                client2 = new UdpBroadcastClient("Client2");
                client3 = new UdpBroadcastClient("Client3");

                Thread.Sleep(3000);     // Wait a few seconds for server to discover the clients.

                // Configure message receive handlers.

                client1.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client1Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client2.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client2Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client3.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client3Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                // Broadcast a message from each of the clients and then verify 
                // that they were received by all clients.

                client1.Broadcast(Helper.ToUTF8("client1"));
                client2.Broadcast(Helper.ToUTF8("client2"));
                client3.Broadcast(Helper.ToUTF8("client3"));

                Thread.Sleep(2000);

                Assert.IsTrue(client1Msgs.ContainsKey("client1"));
                Assert.IsTrue(client1Msgs.ContainsKey("client2"));
                Assert.IsTrue(client1Msgs.ContainsKey("client3"));

                Assert.IsTrue(client2Msgs.ContainsKey("client1"));
                Assert.IsTrue(client2Msgs.ContainsKey("client2"));
                Assert.IsTrue(client2Msgs.ContainsKey("client3"));

                Assert.IsTrue(client3Msgs.ContainsKey("client1"));
                Assert.IsTrue(client3Msgs.ContainsKey("client2"));
                Assert.IsTrue(client3Msgs.ContainsKey("client3"));
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (client1 != null)
                    client1.Close();

                if (client2 != null)
                    client2.Close();

                if (client3 != null)
                    client3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Client_Broadcast_Group()
        {
            // Start one server and three clients, but with client3 in a different broadcast group
            // from the other two, have each client broadcast a message and then verify that
            // the messages from client1 and client2 were broadcast to each other, but that
            // messages from client3 were delivered only to itself.

            UdpBroadcastServer server1 = null;
            UdpBroadcastClient client1 = null;
            UdpBroadcastClient client2 = null;
            UdpBroadcastClient client3 = null;
            Dictionary<string, bool> client1Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client2Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client3Msgs = new Dictionary<string, bool>();

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Client1
    NetworkBinding           = 127.0.0.1:11221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client2
    NetworkBinding           = 127.0.0.1:11222
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client3
    NetworkBinding           = 127.0.0.1:11223
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 100
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                client1 = new UdpBroadcastClient("Client1");
                client2 = new UdpBroadcastClient("Client2");
                client3 = new UdpBroadcastClient("Client3");

                Thread.Sleep(3000);     // Wait a few seconds for server to discover the clients.

                // Configure message receive handlers.

                client1.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client1Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client2.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client2Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client3.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client3Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                // Broadcast a message from each of the clients and then verify 
                // that they were received by all clients.

                client1.Broadcast(Helper.ToUTF8("client1"));
                client2.Broadcast(Helper.ToUTF8("client2"));
                client3.Broadcast(Helper.ToUTF8("client3"));

                Thread.Sleep(2000);

                Assert.IsTrue(client1Msgs.ContainsKey("client1"));
                Assert.IsTrue(client1Msgs.ContainsKey("client2"));
                Assert.IsFalse(client1Msgs.ContainsKey("client3"));

                Assert.IsTrue(client2Msgs.ContainsKey("client1"));
                Assert.IsTrue(client2Msgs.ContainsKey("client2"));
                Assert.IsFalse(client2Msgs.ContainsKey("client3"));

                Assert.IsFalse(client3Msgs.ContainsKey("client1"));
                Assert.IsFalse(client3Msgs.ContainsKey("client2"));
                Assert.IsTrue(client3Msgs.ContainsKey("client3"));
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (client1 != null)
                    client1.Close();

                if (client2 != null)
                    client2.Close();

                if (client3 != null)
                    client3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Client_BroadcastBlast_OneServer()
        {
            // Start one server and three clients, have each client broadcast 10K
            // messages and then verify that all of the messages got through.

            UdpBroadcastServer server1 = null;
            UdpBroadcastClient client1 = null;
            UdpBroadcastClient client2 = null;
            UdpBroadcastClient client3 = null;
            Dictionary<string, bool> client1Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client2Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client3Msgs = new Dictionary<string, bool>();

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Client1
    NetworkBinding           = 127.0.0.1:11221
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client2
    NetworkBinding           = 127.0.0.1:11222
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client3
    NetworkBinding           = 127.0.0.1:11223
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                client1 = new UdpBroadcastClient("Client1");
                client2 = new UdpBroadcastClient("Client2");
                client3 = new UdpBroadcastClient("Client3");

                Thread.Sleep(3000);     // Wait a few seconds for server to discover the clients.

                // Configure message receive handlers.

                client1.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client1Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client2.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client2Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client3.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client3Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                // Broadcast messages from each of the clients and then verify 
                // that they were received by all clients.

                const int count = 10000;

                for (int i = 0; i < count; i++)
                {
                    client1.Broadcast(Helper.ToUTF8(string.Format("client1-{0}", i)));
                    client2.Broadcast(Helper.ToUTF8(string.Format("client2-{0}", i)));
                    client3.Broadcast(Helper.ToUTF8(string.Format("client3-{0}", i)));

                    // Yield a bit longer after every 100th cycle to keep the
                    // socket buffers from overflowing.

                    if (i % 100 == 0)
                        Thread.Sleep(100);
                    else
                        Thread.Sleep(10);
                }

                Thread.Sleep(3000);

                for (int i = 0; i < count; i++)
                {
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client3-{0}", i)));

                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client3-{0}", i)));

                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client3-{0}", i)));
                }
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (client1 != null)
                    client1.Close();

                if (client2 != null)
                    client2.Close();

                if (client3 != null)
                    client3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Client_Broadcast_SingleMaster()
        {
            // Start two servers and three clients and broadcast a few messages from each.
            // Then verify that only one copy of each message was received, confirming
            // that only one of the servers was selected to be the master and performed
            // the retransmissions back to the clients.

            UdpBroadcastServer server1 = null;
            UdpBroadcastServer server2 = null;
            UdpBroadcastClient client1 = null;
            UdpBroadcastClient client2 = null;
            UdpBroadcastClient client3 = null;
            Dictionary<string, int> client1Msgs = new Dictionary<string, int>();
            Dictionary<string, int> client2Msgs = new Dictionary<string, int>();
            Dictionary<string, int> client3Msgs = new Dictionary<string, int>();

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server2
    NetworkBinding           = 127.0.0.1:10222
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Client1
    NetworkBinding           = 127.0.0.1:11221
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client2
    NetworkBinding           = 127.0.0.1:11222
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client3
    NetworkBinding           = 127.0.0.1:11223
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                server2 = new UdpBroadcastServer("Server2");
                client1 = new UdpBroadcastClient("Client1");
                client2 = new UdpBroadcastClient("Client2");
                client3 = new UdpBroadcastClient("Client3");

                Thread.Sleep(3000);     // Wait a few seconds for server to discover the clients.

                // Configure message receive handlers.

                client1.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                        {
                            if (!client1Msgs.ContainsKey(Helper.FromUTF8(a.Payload)))
                                client1Msgs[Helper.FromUTF8(a.Payload)] = 1;
                            else
                                client1Msgs[Helper.FromUTF8(a.Payload)] = client1Msgs[Helper.FromUTF8(a.Payload)] + 1;
                        }
                    };

                client2.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                        {
                            if (!client2Msgs.ContainsKey(Helper.FromUTF8(a.Payload)))
                                client2Msgs[Helper.FromUTF8(a.Payload)] = 1;
                            else
                                client2Msgs[Helper.FromUTF8(a.Payload)] = client2Msgs[Helper.FromUTF8(a.Payload)] + 1;
                        }
                    };

                client3.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                        {
                            if (!client3Msgs.ContainsKey(Helper.FromUTF8(a.Payload)))
                                client3Msgs[Helper.FromUTF8(a.Payload)] = 1;
                            else
                                client3Msgs[Helper.FromUTF8(a.Payload)] = client3Msgs[Helper.FromUTF8(a.Payload)] + 1;
                        }
                    };

                // Broadcast messages from each of the clients and then verify 
                // that they were received by all clients.

                const int count = 100;

                for (int i = 0; i < count; i++)
                {
                    client1.Broadcast(Helper.ToUTF8(string.Format("client1-{0}", i)));
                    client2.Broadcast(Helper.ToUTF8(string.Format("client2-{0}", i)));
                    client3.Broadcast(Helper.ToUTF8(string.Format("client3-{0}", i)));

                    // Yield a bit longer after every 100th cycle to keep the
                    // socket buffers from overflowing.

                    if (i % 10 == 0)
                        Thread.Sleep(100);
                    else
                        Thread.Sleep(10);
                }

                Thread.Sleep(3000);

                for (int i = 0; i < count; i++)
                {
                    Assert.AreEqual(1, client1Msgs[string.Format("client1-{0}", i)]);
                    Assert.AreEqual(1, client1Msgs[string.Format("client2-{0}", i)]);
                    Assert.AreEqual(1, client1Msgs[string.Format("client3-{0}", i)]);

                    Assert.AreEqual(1, client2Msgs[string.Format("client1-{0}", i)]);
                    Assert.AreEqual(1, client2Msgs[string.Format("client2-{0}", i)]);
                    Assert.AreEqual(1, client2Msgs[string.Format("client3-{0}", i)]);

                    Assert.AreEqual(1, client3Msgs[string.Format("client1-{0}", i)]);
                    Assert.AreEqual(1, client3Msgs[string.Format("client2-{0}", i)]);
                    Assert.AreEqual(1, client3Msgs[string.Format("client3-{0}", i)]);
                }
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (server2 != null)
                    server2.Close();

                if (client1 != null)
                    client1.Close();

                if (client2 != null)
                    client2.Close();

                if (client3 != null)
                    client3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Client_BroadcastBlast_TwoServers()
        {
            // Start two servers and three clients, have each client broadcast 10K
            // messages and then verify that all of the messages got through.

            UdpBroadcastServer server1 = null;
            UdpBroadcastServer server2 = null;
            UdpBroadcastClient client1 = null;
            UdpBroadcastClient client2 = null;
            UdpBroadcastClient client3 = null;
            Dictionary<string, bool> client1Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client2Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client3Msgs = new Dictionary<string, bool>();

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server2
    NetworkBinding           = 127.0.0.1:10222
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Client1
    NetworkBinding           = 127.0.0.1:11221
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client2
    NetworkBinding           = 127.0.0.1:11222
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client3
    NetworkBinding           = 127.0.0.1:11223
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                server2 = new UdpBroadcastServer("Server2");
                client1 = new UdpBroadcastClient("Client1");
                client2 = new UdpBroadcastClient("Client2");
                client3 = new UdpBroadcastClient("Client3");

                Thread.Sleep(3000);     // Wait a few seconds for server to discover the clients.

                // Configure message receive handlers.

                client1.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client1Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client2.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client2Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client3.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client3Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                // Broadcast messages from each of the clients and then verify 
                // that they were received by all clients.

                const int count = 10000;

                for (int i = 0; i < count; i++)
                {
                    client1.Broadcast(Helper.ToUTF8(string.Format("client1-{0}", i)));
                    client2.Broadcast(Helper.ToUTF8(string.Format("client2-{0}", i)));
                    client3.Broadcast(Helper.ToUTF8(string.Format("client3-{0}", i)));

                    // Yield a bit longer after every 100th cycle to keep the
                    // socket buffers from overflowing.

                    if (i % 100 == 0)
                        Thread.Sleep(100);
                    else
                        Thread.Sleep(10);
                }

                Thread.Sleep(3000);

                for (int i = 0; i < count; i++)
                {
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client3-{0}", i)));

                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client3-{0}", i)));

                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client3-{0}", i)));
                }
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (server2 != null)
                    server2.Close();

                if (client1 != null)
                    client1.Close();

                if (client2 != null)
                    client2.Close();

                if (client3 != null)
                    client3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Client_Broadcast_CloseFailover()
        {
            // Start two servers and three clients, verifying that server1 becomes the master. 
            // Then broadcast some messages, verifying reception.  Then close server1, verify
            // server2 becomes the master, then broadcast some more messages and verify.

            UdpBroadcastServer server1 = null;
            UdpBroadcastServer server2 = null;
            UdpBroadcastClient client1 = null;
            UdpBroadcastClient client2 = null;
            UdpBroadcastClient client3 = null;
            Dictionary<string, bool> client1Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client2Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client3Msgs = new Dictionary<string, bool>();

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server2
    NetworkBinding           = 127.0.0.1:10222
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Client1
    NetworkBinding           = 127.0.0.1:11221
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client2
    NetworkBinding           = 127.0.0.1:11222
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client3
    NetworkBinding           = 127.0.0.1:11223
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                server2 = new UdpBroadcastServer("Server2");
                client1 = new UdpBroadcastClient("Client1");
                client2 = new UdpBroadcastClient("Client2");
                client3 = new UdpBroadcastClient("Client3");

                Thread.Sleep(3000);     // Wait a few seconds for server to discover the clients.

                Assert.IsTrue(server1.IsMaster);

                // Configure message receive handlers.

                client1.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client1Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client2.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client2Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client3.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client3Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                // Broadcast a few messages from each of the clients and then verify 
                // that they were received by all clients.

                const int count = 1;

                for (int i = 0; i < count; i++)
                {
                    client1.Broadcast(Helper.ToUTF8(string.Format("client1-{0}", i)));
                    client2.Broadcast(Helper.ToUTF8(string.Format("client2-{0}", i)));
                    client3.Broadcast(Helper.ToUTF8(string.Format("client3-{0}", i)));

                    // Yield a bit longer after every 100th cycle to keep the
                    // socket buffers from overflowing.

                    if (i % 100 == 0)
                        Thread.Sleep(100);
                    else
                        Thread.Sleep(10);
                }

                Thread.Sleep(3000);

                for (int i = 0; i < count; i++)
                {
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client3-{0}", i)));

                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client3-{0}", i)));

                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client3-{0}", i)));
                }

                // Close server1, verify that server2 quickly becomes the master, and
                // then verify that we can still broadcast messages.

                server1.Close();
                Thread.Sleep(1000);

                Assert.IsTrue(server2.IsMaster);

                client1Msgs.Clear();
                client2Msgs.Clear();
                client3Msgs.Clear();

                for (int i = 0; i < count; i++)
                {
                    client1.Broadcast(Helper.ToUTF8(string.Format("client1-{0}", i)));
                    client2.Broadcast(Helper.ToUTF8(string.Format("client2-{0}", i)));
                    client3.Broadcast(Helper.ToUTF8(string.Format("client3-{0}", i)));

                    // Yield a bit longer after every 100th cycle to keep the
                    // socket buffers from overflowing.

                    if (i % 100 == 0)
                        Thread.Sleep(100);
                    else
                        Thread.Sleep(10);
                }

                Thread.Sleep(3000);

                for (int i = 0; i < count; i++)
                {
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client3-{0}", i)));

                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client3-{0}", i)));

                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client3-{0}", i)));
                }
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (server2 != null)
                    server2.Close();

                if (client1 != null)
                    client1.Close();

                if (client2 != null)
                    client2.Close();

                if (client3 != null)
                    client3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Client_Broadcast_NetFailover()
        {
            // Start two servers and three clients, verifying that server1 becomes the master. 
            // Then broadcast some messages, verifying reception.  Then simulate a network
            // failure for server1, wait long enough for server2 to purge server1 and
            // become the master, then broadcast some more messages and verify.

            UdpBroadcastServer server1 = null;
            UdpBroadcastServer server2 = null;
            UdpBroadcastClient client1 = null;
            UdpBroadcastClient client2 = null;
            UdpBroadcastClient client3 = null;
            Dictionary<string, bool> client1Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client2Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client3Msgs = new Dictionary<string, bool>();

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server2
    NetworkBinding           = 127.0.0.1:10222
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Client1
    NetworkBinding           = 127.0.0.1:11221
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client2
    NetworkBinding           = 127.0.0.1:11222
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client3
    NetworkBinding           = 127.0.0.1:11223
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                server2 = new UdpBroadcastServer("Server2");
                client1 = new UdpBroadcastClient("Client1");
                client2 = new UdpBroadcastClient("Client2");
                client3 = new UdpBroadcastClient("Client3");

                Thread.Sleep(3000);     // Wait a few seconds for server to discover the clients.

                Assert.IsTrue(server1.IsMaster);

                // Configure message receive handlers.

                client1.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client1Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client2.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client2Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client3.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client3Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                // Broadcast a few messages from each of the clients and then verify 
                // that they were received by all clients.

                const int count = 1;

                for (int i = 0; i < count; i++)
                {
                    client1.Broadcast(Helper.ToUTF8(string.Format("client1-{0}", i)));
                    client2.Broadcast(Helper.ToUTF8(string.Format("client2-{0}", i)));
                    client3.Broadcast(Helper.ToUTF8(string.Format("client3-{0}", i)));

                    // Yield a bit longer after every 100th cycle to keep the
                    // socket buffers from overflowing.

                    if (i % 100 == 0)
                        Thread.Sleep(100);
                    else
                        Thread.Sleep(10);
                }

                Thread.Sleep(3000);

                for (int i = 0; i < count; i++)
                {
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client3-{0}", i)));

                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client3-{0}", i)));

                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client3-{0}", i)));
                }

                // Sumilate a network failure for server1, verify that server2 eventually
                // becomes the master, and then verify that we can still broadcast messages.

                server1.PauseNetwork = true;
                Thread.Sleep(7000);

                Assert.IsTrue(server2.IsMaster);

                client1Msgs.Clear();
                client2Msgs.Clear();
                client3Msgs.Clear();

                for (int i = 0; i < count; i++)
                {
                    client1.Broadcast(Helper.ToUTF8(string.Format("client1-{0}", i)));
                    client2.Broadcast(Helper.ToUTF8(string.Format("client2-{0}", i)));
                    client3.Broadcast(Helper.ToUTF8(string.Format("client3-{0}", i)));

                    // Yield a bit longer after every 100th cycle to keep the
                    // socket buffers from overflowing.

                    if (i % 100 == 0)
                        Thread.Sleep(100);
                    else
                        Thread.Sleep(10);
                }

                Thread.Sleep(3000);

                for (int i = 0; i < count; i++)
                {
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client3-{0}", i)));

                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client3-{0}", i)));

                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client3-{0}", i)));
                }
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (server2 != null)
                    server2.Close();

                if (client1 != null)
                    client1.Close();

                if (client2 != null)
                    client2.Close();

                if (client3 != null)
                    client3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Client_BroadcastBlast_Failover()
        {
            // Start two servers and three clients, verifying that server1 becomes the master.  Then 
            // start a thread that will close server1 while in parallel, we're doing a broadcast
            // blast.  When, we're done verify that all of the messages were delivered, indicating
            // that the cluster successfully failed over to server2.

            UdpBroadcastServer server1 = null;
            UdpBroadcastServer server2 = null;
            UdpBroadcastClient client1 = null;
            UdpBroadcastClient client2 = null;
            UdpBroadcastClient client3 = null;
            Dictionary<string, bool> client1Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client2Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client3Msgs = new Dictionary<string, bool>();

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Server2
    NetworkBinding           = 127.0.0.1:10222
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Client1
    NetworkBinding           = 127.0.0.1:11221
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client2
    NetworkBinding           = 127.0.0.1:11222
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client3
    NetworkBinding           = 127.0.0.1:11223
    SocketBufferSize         = 1M
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                server2 = new UdpBroadcastServer("Server2");
                client1 = new UdpBroadcastClient("Client1");
                client2 = new UdpBroadcastClient("Client2");
                client3 = new UdpBroadcastClient("Client3");

                Thread.Sleep(3000);     // Wait a few seconds for server to discover the clients.

                Assert.IsTrue(server1.IsMaster);

                // Configure message receive handlers.

                client1.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client1Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client2.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client2Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client3.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client3Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                // Start a thread that will close server1 in 5 seconds.

                Thread thread;

                thread = new Thread(
                    () =>
                    {
                        Thread.Sleep(5000);
                        server1.Close();
                    });

                thread.Start();

                // Broadcast messages from each of the clients and then verify 
                // that they were received by all clients.

                const int count = 10000;

                for (int i = 0; i < count; i++)
                {
                    client1.Broadcast(Helper.ToUTF8(string.Format("client1-{0}", i)));
                    client2.Broadcast(Helper.ToUTF8(string.Format("client2-{0}", i)));
                    client3.Broadcast(Helper.ToUTF8(string.Format("client3-{0}", i)));

                    // Yield a bit longer after every 100th cycle to keep the
                    // socket buffers from overflowing.

                    if (i % 100 == 0)
                        Thread.Sleep(100);
                    else
                        Thread.Sleep(10);
                }

                Thread.Sleep(3000);

                for (int i = 0; i < count; i++)
                {
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client1Msgs.ContainsKey(string.Format("client3-{0}", i)));

                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client2Msgs.ContainsKey(string.Format("client3-{0}", i)));

                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client1-{0}", i)));
                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client2-{0}", i)));
                    Assert.IsTrue(client3Msgs.ContainsKey(string.Format("client3-{0}", i)));
                }
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (server2 != null)
                    server2.Close();

                if (client1 != null)
                    client1.Close();

                if (client2 != null)
                    client2.Close();

                if (client3 != null)
                    client3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Client_SecurityFailKey()
        {
            // Start one server and three clients, but with client3 with a different shared key
            // from the rest of the cluster, have each client broadcast a message and then verify that
            // the messages from client1 and client2 were broadcast to each other, and that
            // client3 received no messages (even from itself).

            UdpBroadcastServer server1 = null;
            UdpBroadcastClient client1 = null;
            UdpBroadcastClient client2 = null;
            UdpBroadcastClient client3 = null;
            Dictionary<string, bool> client1Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client2Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client3Msgs = new Dictionary<string, bool>();

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Client1
    NetworkBinding           = 127.0.0.1:11221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client2
    NetworkBinding           = 127.0.0.1:11222
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client3
    NetworkBinding           = 127.0.0.1:11223
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:ilvsQmaUXZHsNqHn3hoCLqaMu23bpUlQE06uCqjDEiw=:AnXHmk8ta+zjXGfcvsKdWw==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                client1 = new UdpBroadcastClient("Client1");
                client2 = new UdpBroadcastClient("Client2");
                client3 = new UdpBroadcastClient("Client3");

                Thread.Sleep(3000);     // Wait a few seconds for server to discover the clients.

                // Configure message receive handlers.

                client1.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client1Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client2.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client2Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client3.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client3Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                // Broadcast a message from each of the clients and then verify 
                // that they were received by all clients.

                client1.Broadcast(Helper.ToUTF8("client1"));
                client2.Broadcast(Helper.ToUTF8("client2"));
                client3.Broadcast(Helper.ToUTF8("client3"));

                Thread.Sleep(2000);

                Assert.IsTrue(client1Msgs.ContainsKey("client1"));
                Assert.IsTrue(client1Msgs.ContainsKey("client2"));
                Assert.IsFalse(client1Msgs.ContainsKey("client3"));

                Assert.IsTrue(client2Msgs.ContainsKey("client1"));
                Assert.IsTrue(client2Msgs.ContainsKey("client2"));
                Assert.IsFalse(client2Msgs.ContainsKey("client3"));

                Assert.IsFalse(client3Msgs.ContainsKey("client1"));
                Assert.IsFalse(client3Msgs.ContainsKey("client2"));
                Assert.IsFalse(client3Msgs.ContainsKey("client3"));
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (client1 != null)
                    client1.Close();

                if (client2 != null)
                    client2.Close();

                if (client3 != null)
                    client3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Client_SecurityFailTimestamp()
        {
            // Start one server and three clients and then have each client broadcast a 
            // message but simulating timestamp failures from client3.  Then verify that 
            // the messages from client1 and client2 were broadcast to each other, and
            // that client3 received no messages (even from itself).

            UdpBroadcastServer server1 = null;
            UdpBroadcastClient client1 = null;
            UdpBroadcastClient client2 = null;
            UdpBroadcastClient client3 = null;
            Dictionary<string, bool> client1Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client2Msgs = new Dictionary<string, bool>();
            Dictionary<string, bool> client3Msgs = new Dictionary<string, bool>();

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    Server[1]                = 127.0.0.1:10222
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

&section Client1
    NetworkBinding           = 127.0.0.1:11221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client2
    NetworkBinding           = 127.0.0.1:11222
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client3
    NetworkBinding           = 127.0.0.1:11223
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                client1 = new UdpBroadcastClient("Client1");
                client2 = new UdpBroadcastClient("Client2");
                client3 = new UdpBroadcastClient("Client3");

                Thread.Sleep(3000);     // Wait a few seconds for server to discover the clients.

                // Configure message receive handlers.

                client1.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client1Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client2.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client2Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                client3.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client3Msgs[Helper.FromUTF8(a.Payload)] = true;
                    };

                // Broadcast a message from each of the clients and then verify 
                // that they were received by all clients.

                client1.Broadcast(Helper.ToUTF8("client1"));
                client2.Broadcast(Helper.ToUTF8("client2"));

                client3.FixedTimestampUtc = DateTime.UtcNow - TimeSpan.FromDays(365);   // Time far in the past
                client3.Broadcast(Helper.ToUTF8("client3-past"));

                client3.FixedTimestampUtc = DateTime.UtcNow + TimeSpan.FromDays(365);   // Time far in the future
                client3.Broadcast(Helper.ToUTF8("client3-future"));

                client3.FixedTimestampUtc = DateTime.MinValue;
                client3.Broadcast(Helper.ToUTF8("client3-now"));

                Thread.Sleep(2000);

                Assert.IsTrue(client1Msgs.ContainsKey("client1"));
                Assert.IsTrue(client1Msgs.ContainsKey("client2"));
                Assert.IsFalse(client1Msgs.ContainsKey("client3-past"));
                Assert.IsFalse(client1Msgs.ContainsKey("client3-future"));
                Assert.IsTrue(client1Msgs.ContainsKey("client3-now"));

                Assert.IsTrue(client2Msgs.ContainsKey("client1"));
                Assert.IsTrue(client2Msgs.ContainsKey("client2"));
                Assert.IsFalse(client1Msgs.ContainsKey("client3-past"));
                Assert.IsFalse(client1Msgs.ContainsKey("client3-future"));
                Assert.IsTrue(client1Msgs.ContainsKey("client3-now"));

                Assert.IsTrue(client3Msgs.ContainsKey("client1"));
                Assert.IsTrue(client3Msgs.ContainsKey("client2"));
                Assert.IsFalse(client1Msgs.ContainsKey("client3-past"));
                Assert.IsFalse(client1Msgs.ContainsKey("client3-future"));
                Assert.IsTrue(client1Msgs.ContainsKey("client3-now"));
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (client1 != null)
                    client1.Close();

                if (client2 != null)
                    client2.Close();

                if (client3 != null)
                    client3.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Client_ResolveServerDns()
        {
            // This test requires that the machine's DNS server be manually configured as 127.0.0.1.

            Assert.Inconclusive("This test requires that the machine's DNS be manually configured as 127.0.0.1.");

            // Verify that the UDP broadcast client actually performs periodic server host
            // name DNS lookups by creating a DNS server instance with a static host/ip address
            // mappings, starting a UDP client that specifies these hosts as the UDP broadcast
            // server.  Then verify that the client resolves these initial mappings correctly,
            // before changing the DNS server mappings, and then verify that the client picks
            // up the changes.

            DnsServer dnsServer = null;
            UdpBroadcastClient client = null;
            IPAddress dnsResultAddr;

            var cfg = @"

&section Client
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 128K
    Server[0]                = test.lilltek.net:DYNAMIC-DNS
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5s
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                // Start the DNS server and configure it to return 127.0.0.10 to start.

                dnsResultAddr = Helper.ParseIPAddress("127.0.0.10");

                dnsServer = new DnsServer();
                dnsServer.Start(new DnsServerSettings());

                dnsServer.RequestEvent +=
                    (s, a) =>
                    {
                        DnsResponse response;

                        response = new DnsResponse(a.Request);
                        response.Answers.Add(new A_RR(a.Request.QName, dnsResultAddr, 0));

                        a.Response = response;
                    };

                // Start the broadcast client, wait a bit and then verify that the client 
                // has resolved the server DNS address.

                client = new UdpBroadcastClient("Client");

                Thread.Sleep(2000);
                Assert.AreEqual(new IPEndPoint(dnsResultAddr, NetworkPort.DynamicDns), client.GetServerEndpoints()[0]);

                // Reconfigure the DNS server to return 127.0.0.20, wait a bit longer
                // than the configured ServerResolveInterval and then verify that the client 
                // has resolved the new server DNS address.

                dnsResultAddr = Helper.ParseIPAddress("127.0.0.20");
                Thread.Sleep(6000);
                Assert.AreEqual(new IPEndPoint(dnsResultAddr, NetworkPort.DynamicDns), client.GetServerEndpoints()[0]);
            }
            finally
            {
                Config.SetConfig(null);

                if (dnsServer != null)
                    dnsServer.Stop();

                if (client != null)
                    client.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Broadcast")]
        public void UdpBroadcast_Client_VerifySourceAddress()
        {
            // Start one server and two clients, the first client configured with a the
            // loopback address for its binding and the second using the network
            // adapter's public IP address.  Then broadcast packets from each client
            // and verify that the source address delivered with the packets are correct.

            UdpBroadcastServer server1 = null;
            UdpBroadcastClient client1 = null;
            UdpBroadcastClient client2 = null;
            Dictionary<string, IPAddress> client1Msgs = new Dictionary<string, IPAddress>();
            Dictionary<string, IPAddress> client2Msgs = new Dictionary<string, IPAddress>();

            var cfg = @"

&section Server1
    NetworkBinding           = 127.0.0.1:10221
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BkTaskInterval           = 1s
    ClusterKeepAliveInterval = 1s
    ServerTTL                = 5s
    ClientTTL                = 5s
&endsection

// $todo(jeff.lill):
//
// For some strange reason I'm getting a SocketException: The request address is not valid in its context
// then sending a UDP packet to the physical adapters IP address for the machine, so I'm going to 
// configure both clients with loopback addresses.  This issue does not appear to be Windows Firewall
// or Microsoft Loopback driver related and is somewhat troubling because this might very well
// break communication for two LillTek MKessaging applications running on the same machine.

&section Client1
    // NetworkBinding           = $(ip-address):11222
    NetworkBinding           = 127.0.0.1:11222
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection

&section Client2
    NetworkBinding           = 127.0.0.1:11223
    SocketBufferSize         = 128K
    Server[0]                = 127.0.0.1:10221
    SharedKey                = aes:NtSkj76eyCAsJE4TnTqmPOuKd5hDDWwSS7ccTfeKEL8=:S9Xc6skGFWtxoxBaoTxJlQ==
    MessageTTL               = 15m
    BroadcastGroup           = 0
    BkTaskInterval           = 1s
    KeepAliveInterval        = 1s
    ServerResolveInterval    = 5m
&endsection
";

            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                server1 = new UdpBroadcastServer("Server1");
                client1 = new UdpBroadcastClient("Client1");
                client2 = new UdpBroadcastClient("Client2");

                Thread.Sleep(3000);     // Wait a few seconds for server to discover the clients.

                // Configure message receive handlers.

                client1.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client1Msgs[Helper.FromUTF8(a.Payload)] = a.SourceAddress;
                    };

                client2.PacketReceived +=
                    (s, a) =>
                    {
                        lock (syncLock)
                            client2Msgs[Helper.FromUTF8(a.Payload)] = a.SourceAddress;
                    };

                // Broadcast a message from each of the clients and then verify 
                // that they were received by all clients.

                client1.Broadcast(Helper.ToUTF8("client1"));
                client2.Broadcast(Helper.ToUTF8("client2"));
                Thread.Sleep(2000);

                Assert.IsTrue(client1Msgs.ContainsKey("client1"));
                Assert.AreEqual(client1.Settings.NetworkBinding.Address, client1Msgs["client1"]);
                Assert.IsTrue(client1Msgs.ContainsKey("client2"));
                Assert.AreEqual(client2.Settings.NetworkBinding.Address, client2Msgs["client2"]);

                Assert.IsTrue(client2Msgs.ContainsKey("client1"));
                Assert.AreEqual(client1.Settings.NetworkBinding.Address, client2Msgs["client1"]);
                Assert.IsTrue(client2Msgs.ContainsKey("client2"));
                Assert.AreEqual(client2.Settings.NetworkBinding.Address, client2Msgs["client2"]);
            }
            finally
            {
                Config.SetConfig(null);

                if (server1 != null)
                    server1.Close();

                if (client1 != null)
                    client1.Close();

                if (client2 != null)
                    client2.Close();
            }
        }
    }
}

