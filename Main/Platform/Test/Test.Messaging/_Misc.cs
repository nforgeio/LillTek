//-----------------------------------------------------------------------------
// FILE:        _Misc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Misc messaging unit tests.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;
using LillTek.Testing;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _Misc : ILockable
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Misc_RouterInfo()
        {
            MsgRouterInfo caps;

            caps = MsgRouterInfo.Default;
            Assert.AreEqual(MsgRouterInfo.CurrentProtocolVersion, caps.ProtocolVersion);
            Assert.AreEqual(new Version(Build.Version), caps.BuildVersion);
            Assert.IsTrue(caps.IsP2P);
            Assert.IsTrue(caps.ReceiptSend);
            Assert.IsTrue(caps.DeadRouterDetect);
            Assert.AreEqual(Helper.MachineName, caps.MachineName);

            caps = new MsgRouterInfo(caps.ToString());
            Assert.AreEqual(MsgRouterInfo.CurrentProtocolVersion, caps.ProtocolVersion);
            Assert.AreEqual(new Version(Build.Version), caps.BuildVersion);
            Assert.IsTrue(caps.IsP2P);
            Assert.IsTrue(caps.ReceiptSend);
            Assert.IsTrue(caps.DeadRouterDetect);
            Assert.AreEqual(Helper.MachineName, caps.MachineName);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Misc_RouterSyncRoot()
        {
            // Verify that an exception is thrown if an application attempts to
            // modify a router's syncroot after it has been referenced.

            MsgRouter router;
            object syncLock;

            router = new MsgRouter();
            syncLock = router.SyncRoot;
            Assert.AreSame(router, syncLock);

            syncLock = router.SyncRoot;
            Assert.AreSame(router, syncLock);

            try
            {
                router.SyncRoot = this;
                Assert.Fail("InvalidOperationException expected");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(InvalidOperationException));
            }

            syncLock = router.SyncRoot;
            Assert.AreSame(router, syncLock);

            // Verify that we can set a custom router syncroot

            router = new MsgRouter();
            router.SyncRoot = this;

            syncLock = router.SyncRoot;
            Assert.AreSame(this, syncLock);

            try
            {
                router.SyncRoot = router;
                Assert.Fail("InvalidOperationException expected");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(InvalidOperationException));
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

