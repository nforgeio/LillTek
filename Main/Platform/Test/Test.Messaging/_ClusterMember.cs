//-----------------------------------------------------------------------------
// FILE:        _ClusterMember.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
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
    public class _ClusterMember
    {
        private const int LoopCount = 10;

        private object syncLock = new object();
        private TimeSpan runTime = TimeSpan.FromSeconds(10);
        private TimeSpan waitSlop = TimeSpan.FromSeconds(3);

        [TestInitialize]
        public void Initialize()
        {
            NetTrace.Start();
            NetTrace.Enable(ClusterMember.TraceSubsystem, 1);

            Helper.SetLocalGuidMode(GuidMode.CountUp);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Helper.SetLocalGuidMode(GuidMode.Normal);
            Config.SetConfig(null);

            NetTrace.Stop();
        }

        private void SetConfig(string keyPrefix, string config)
        {
            string cfg = @"

&section MsgRouter

    AppName                = LillTek.ClusterMember Unit Test
    AppDescription         = 
    RouterEP			   = physical://DETACHED/$(LillTek.DC.DefHubName)/$(Guid)
    CloudEP    			   = $(LillTek.DC.CloudEP)
    CloudAdapter    	   = ANY
    UdpEP				   = ANY:0
    TcpEP				   = ANY:0
    TcpBacklog			   = 100
    TcpDelay			   = off
    BkInterval			   = 1s
    MaxIdle				   = 5m
    EnableP2P              = yes
    AdvertiseTime		   = 1m
    DefMsgTTL			   = 5
    SharedKey		 	   = PLAINTEXT
    SessionCacheTime       = 2m
    SessionRetries         = 3
    SessionTimeout         = 10s
    MaxLogicalAdvertiseEPs = 256
    DeadRouterTTL          = 2s

&endsection

&section {0}
{1}
&endsection
";
            cfg = string.Format(cfg, keyPrefix, config);
            Config.SetConfig(cfg.Replace('&', '#'));
        }

        private void WaitForState(ClusterMember member, ClusterMemberState state, TimeSpan timeout)
        {
            DateTime exitTime = SysTime.Now + timeout + waitSlop;

            while (SysTime.Now < exitTime)
            {
                if (member.State == state)
                    return;

                Thread.Sleep(50);
            }

            throw new TimeoutException(string.Format("Timeout waiting for [state={0}]", state));
        }

        private void WaitForMasterOrSlave(ClusterMember member, TimeSpan timeout)
        {
            DateTime exitTime = SysTime.Now + timeout + waitSlop;

            while (SysTime.Now < exitTime)
            {
                if (member.State == ClusterMemberState.Master || member.State == ClusterMemberState.Slave)
                    return;

                Thread.Sleep(50);
            }

            throw new TimeoutException("Timeout waiting for [state=Master|Slave]");
        }

        private void WaitForOnline(ClusterMember member, TimeSpan timeout)
        {
            DateTime exitTime = SysTime.Now + timeout + waitSlop;

            while (SysTime.Now < exitTime)
            {
                if (member.IsOnline)
                    return;

                Thread.Sleep(50);
            }

            throw new TimeoutException("Timeout waiting for [IsOnline]");
        }

        private void WaitForClusterStatus(ClusterMember member, TimeSpan timeout)
        {
            DateTime exitTime = SysTime.Now + timeout + waitSlop;

            while (SysTime.Now < exitTime)
            {
                if (member.HasClusterStatus)
                    return;

                Thread.Sleep(50);
            }

            throw new TimeoutException("Timeout waiting for cluster status");
        }

        private ClusterMember WaitForMaster(ClusterMember[] members, TimeSpan timeout)
        {
            DateTime exitTime = SysTime.Now + timeout + waitSlop;

            while (SysTime.Now < exitTime)
            {
                foreach (ClusterMember member in members)
                    if (member.IsMaster)
                        return member;

                Thread.Sleep(50);
            }

            throw new TimeoutException("Timeout waiting for cluster status");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_LoneInstance()
        {
            // Verify that a lone cluster instance goes through the
            // required states and eventually elects itself to be the 
            // master.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember member = null;
            ClusterMemberSettings settings;

            SetConfig("Cluster", cfg);

            try
            {
                settings = ClusterMemberSettings.LoadConfig("Cluster");

                router = new LeafRouter();
                router.Start();

                member = new ClusterMember(router, "Cluster");
                member.Start();

                WaitForState(member, ClusterMemberState.Warmup, waitSlop);
                WaitForState(member, ClusterMemberState.Election, settings.MissingMasterInterval);
                WaitForState(member, ClusterMemberState.Master, settings.ElectionInterval);

                Assert.IsTrue(member.IsMaster);

                // Let the member run a while to verify that we don't
                // see any strange behavior.

                Thread.Sleep(runTime);
            }
            finally
            {
                if (member != null)
                    member.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_SimultaneousBoot()
        {
            // Launch four cluster members at the same time and verify that
            // they each go through the warmup and election sequence and
            // verify that the one instance was properly elected master.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[4];
            ClusterMember master = null;
            ClusterMemberSettings settings;

            SetConfig("Cluster", cfg);

            try
            {
                settings = ClusterMemberSettings.LoadConfig("Cluster");

                router = new LeafRouter();
                router.Start();

                // Start the members

                for (int i = 0; i < members.Length; i++)
                {
                    members[i] = new ClusterMember(router, "Cluster");
                    members[i].Start();
                }

                // Figure out which member should become the master.

                master = members[0];
                for (int i = 1; i < members.Length; i++)
                    if (MsgEP.Compare(members[i].InstanceEP, master.InstanceEP) > 0)
                        master = members[i];

                // Wait for all of the members to enter the warmup state

                foreach (ClusterMember member in members)
                    WaitForState(member, ClusterMemberState.Warmup, waitSlop);

                // Wait for the members to enter the election state

                foreach (ClusterMember member in members)
                    WaitForState(member, ClusterMemberState.Election, settings.MissingMasterInterval);

                // Wait for the members to enter the master or slave state.

                foreach (ClusterMember member in members)
                    WaitForMasterOrSlave(member, settings.ElectionInterval);

                // Wait a bit to allow the master to broadcast cluster
                // status with all members and then verify that all members
                // have cluster state.

                Thread.Sleep(settings.MissingMasterInterval);

                foreach (ClusterMember member in members)
                    WaitForClusterStatus(member, settings.MasterBroadcastInterval);

                // Confirm that the instance we expected to become the master
                // is indeed the master and that the other instances are slaves.

                Assert.IsTrue(master.IsMaster);
                foreach (ClusterMember member in members)
                {
                    if (!object.ReferenceEquals(master, member))
                        Assert.AreEqual(ClusterMemberState.Slave, member.State);
                    else
                        Assert.AreEqual(ClusterMemberState.Master, member.State);

                    Assert.AreEqual(master.MasterEP, member.ClusterStatus.MasterEP);
                }

                // Verify that each instance knows about the others.

                foreach (ClusterMember member in members)
                {
                    ClusterStatus status = member.ClusterStatus;

                    Assert.AreEqual(members.Length, status.Members.Count);
                    for (int i = 0; i < members.Length; i++)
                        Assert.IsNotNull(status.GetMemberStatus(members[i].InstanceEP));
                }

                // Let the member run a while to verify that we don't
                // see any strange behavior.

                Thread.Sleep(runTime);
            }
            finally
            {
                foreach (ClusterMember member in members)
                    member.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_SlaveStarts()
        {
            // Launch an instance and wait for it to become the master
            // and then launch two more instances and watch them become
            // slaves.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember master = null;
            ClusterMember slave1 = null;
            ClusterMember slave2 = null;
            ClusterMember[] members;
            ClusterMemberSettings settings;

            SetConfig("Cluster", cfg);

            try
            {
                settings = ClusterMemberSettings.LoadConfig("Cluster");

                router = new LeafRouter();
                router.Start();

                // Start the master

                master = new ClusterMember(router, "Cluster");
                master.Start();

                WaitForState(master, ClusterMemberState.Warmup, waitSlop);
                WaitForState(master, ClusterMemberState.Election, settings.MissingMasterInterval);
                WaitForState(master, ClusterMemberState.Master, settings.ElectionInterval);

                Assert.IsTrue(master.IsMaster);

                // Start the first slave

                slave1 = new ClusterMember(router, "Cluster");
                slave1.Start();

                WaitForState(slave1, ClusterMemberState.Warmup, waitSlop);
                WaitForState(slave1, ClusterMemberState.Slave, settings.MasterBroadcastInterval);

                // Start the second slave

                slave2 = new ClusterMember(router, "Cluster");
                slave2.Start();

                WaitForState(slave2, ClusterMemberState.Warmup, waitSlop);
                WaitForState(slave2, ClusterMemberState.Slave, settings.MasterBroadcastInterval);

                members = new ClusterMember[] { master, slave1, slave2 };

                // Wait a bit to allow the master to broadcast cluster
                // status with all members and then verify that all members
                // have cluster state.

                Thread.Sleep(settings.MissingMasterInterval);

                foreach (ClusterMember member in members)
                    WaitForClusterStatus(member, settings.MasterBroadcastInterval);

                // Confirm that the instance we expected to become the master
                // is indeed the master and that the other instances are slaves.

                Assert.IsTrue(master.IsMaster);
                foreach (ClusterMember member in members)
                {
                    if (!object.ReferenceEquals(master, member))
                        Assert.AreEqual(ClusterMemberState.Slave, member.State);
                    else
                        Assert.AreEqual(ClusterMemberState.Master, member.State);

                    Assert.AreEqual(master.MasterEP, member.ClusterStatus.MasterEP);
                }

                // Verify that each instance knows about the others.

                foreach (ClusterMember member in members)
                {
                    ClusterStatus status = member.ClusterStatus;

                    Assert.AreEqual(members.Length, status.Members.Count);
                    for (int i = 0; i < members.Length; i++)
                        Assert.IsNotNull(status.GetMemberStatus(members[i].InstanceEP));
                }

                // Let the member run a while to verify that we don't
                // see any strange behavior.

                Thread.Sleep(runTime);
            }
            finally
            {
                if (slave2 != null)
                    slave2.Stop();

                if (slave1 != null)
                    slave1.Stop();

                if (master != null)
                    master.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_SlaveStops()
        {
            // Start a master and a couple of slaves and then gracefully 
            // stop one of the slaves.  Verify that the remaining instances
            // remove the stopped slave status.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember master = null;
            ClusterMember slave1 = null;
            ClusterMember slave2 = null;
            ClusterMember[] members;
            ClusterMemberSettings settings;

            SetConfig("Cluster", cfg);

            try
            {
                settings = ClusterMemberSettings.LoadConfig("Cluster");

                router = new LeafRouter();
                router.Start();

                // Start the master

                master = new ClusterMember(router, "Cluster");
                master.Start();

                WaitForState(master, ClusterMemberState.Warmup, waitSlop);
                WaitForState(master, ClusterMemberState.Election, settings.MissingMasterInterval);
                WaitForState(master, ClusterMemberState.Master, settings.ElectionInterval);

                Assert.IsTrue(master.IsMaster);

                // Start the first slave

                slave1 = new ClusterMember(router, "Cluster");
                slave1.Start();

                WaitForState(slave1, ClusterMemberState.Warmup, waitSlop);
                WaitForState(slave1, ClusterMemberState.Slave, settings.MasterBroadcastInterval);

                // Start the second slave

                slave2 = new ClusterMember(router, "Cluster");
                slave2.Start();

                WaitForState(slave2, ClusterMemberState.Warmup, waitSlop);
                WaitForState(slave2, ClusterMemberState.Slave, settings.MasterBroadcastInterval);

                members = new ClusterMember[] { master, slave1, slave2 };

                // Wait a bit to allow the master to broadcast cluster
                // status with all members and then verify that all members
                // have cluster state.

                Thread.Sleep(settings.MissingMasterInterval);

                foreach (ClusterMember member in members)
                    WaitForClusterStatus(member, settings.MasterBroadcastInterval);

                // Confirm that the instance we expected to become the master
                // is indeed the master and that the other instances are slaves.

                Assert.IsTrue(master.IsMaster);
                foreach (ClusterMember member in members)
                    if (!object.ReferenceEquals(master, member))
                        Assert.AreEqual(ClusterMemberState.Slave, member.State);

                // Verify that each instance knows about the others.

                foreach (ClusterMember member in members)
                {
                    ClusterStatus status = member.ClusterStatus;

                    Assert.AreEqual(members.Length, status.Members.Count);
                    for (int i = 0; i < members.Length; i++)
                        Assert.IsNotNull(status.GetMemberStatus(members[i].InstanceEP));
                }

                // Let the member run a while to verify that we don't
                // see any strange behavior.

                Thread.Sleep(runTime);

                // Now stop slave1, wait for one master broadcast interval
                // and then verify that status for slave1 has been deleted.

                slave1.Stop();
                slave1 = null;

                Thread.Sleep(settings.MasterBroadcastInterval);

                Assert.IsTrue(master.IsMaster);

                members = new ClusterMember[] { master, slave2 };
                foreach (ClusterMember member in members)
                {
                    if (!object.ReferenceEquals(master, member))
                        Assert.AreEqual(ClusterMemberState.Slave, member.State);
                    else
                        Assert.AreEqual(ClusterMemberState.Master, member.State);

                    Assert.AreEqual(master.MasterEP, member.ClusterStatus.MasterEP);
                }
            }
            finally
            {
                if (slave2 != null)
                    slave2.Stop();

                if (slave1 != null)
                    slave1.Stop();

                if (master != null)
                    master.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        private ClusterMember masterStops_promotee;

        private void MasterStops_StatusChange(ClusterMember sender, ClusterMemberEventArgs args)
        {
            if (args.OriginalState == ClusterMemberState.Slave && args.NewState == ClusterMemberState.Master)
                masterStops_promotee = sender;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_MasterStops()
        {
            // Start a master and a couple of slaves and then gracefully 
            // stop the master.  Verify that the master promoted one of
            // the slaves and that it is now acting as master.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember master = null;
            ClusterMember slave1 = null;
            ClusterMember slave2 = null;
            ClusterMember expectedMaster;
            ClusterMember expectedSlave;
            ClusterMember[] members;
            ClusterMemberSettings settings;

            SetConfig("Cluster", cfg);

            try
            {
                settings = ClusterMemberSettings.LoadConfig("Cluster");

                router = new LeafRouter();
                router.Start();

                // Start the master

                master = new ClusterMember(router, "Cluster");
                master.Start();

                WaitForState(master, ClusterMemberState.Warmup, waitSlop);
                WaitForState(master, ClusterMemberState.Election, settings.MissingMasterInterval);
                WaitForState(master, ClusterMemberState.Master, settings.ElectionInterval);

                Assert.IsTrue(master.IsMaster);

                // Start the first slave

                slave1 = new ClusterMember(router, "Cluster");
                slave1.Start();

                WaitForState(slave1, ClusterMemberState.Warmup, waitSlop);
                WaitForState(slave1, ClusterMemberState.Slave, settings.MasterBroadcastInterval);

                // Start the second slave

                slave2 = new ClusterMember(router, "Cluster");
                slave2.Start();

                WaitForState(slave2, ClusterMemberState.Warmup, waitSlop);
                WaitForState(slave2, ClusterMemberState.Slave, settings.MasterBroadcastInterval);

                members = new ClusterMember[] { master, slave1, slave2 };

                // Wait a bit to allow the master to broadcast cluster
                // status with all members and then verify that all members
                // have cluster state.

                Thread.Sleep(settings.MissingMasterInterval);

                foreach (ClusterMember member in members)
                    WaitForClusterStatus(member, settings.MasterBroadcastInterval);

                // Confirm that the instance we expected to become the master
                // is indeed the master and that the other instances are slaves.

                Assert.IsTrue(master.IsMaster);
                foreach (ClusterMember member in members)
                    if (!object.ReferenceEquals(master, member))
                        Assert.AreEqual(ClusterMemberState.Slave, member.State);

                // Verify that each instance knows about the others.

                foreach (ClusterMember member in members)
                {
                    ClusterStatus status = member.ClusterStatus;

                    Assert.AreEqual(members.Length, status.Members.Count);
                    for (int i = 0; i < members.Length; i++)
                        Assert.IsNotNull(status.GetMemberStatus(members[i].InstanceEP));
                }

                // Let the member run a while to verify that we don't
                // see any strange behavior.

                Thread.Sleep(runTime);

                // Determine which slave will be promoted to master and then
                // hook its StateChange event to record the event.  Then stop
                // the master and a brief period (because the master should 
                // perform the promotion immediately) and then verify that 
                // slave was promoted.

                if (MsgEP.Compare(slave1.InstanceEP, slave2.InstanceEP) > 0)
                {
                    expectedMaster = slave1;
                    expectedSlave = slave2;
                }
                else
                {
                    expectedMaster = slave2;
                    expectedSlave = slave1;
                }

                masterStops_promotee = null;
                expectedMaster.StateChange += new ClusterMemberEventHandler(MasterStops_StatusChange);

                master.Stop();
                master = null;

                Thread.Sleep(100);

                // Due to fairly rare timing issues with this test, it's
                // possible for an election to have been called while we
                // we're sleeping above.

                if (expectedMaster.State == ClusterMemberState.Election)
                {
                    // Wait for the election to complete and the two 
                    // remaining instances to come back online.

                    foreach (ClusterMember member in members)
                    {
                        if (object.ReferenceEquals(expectedMaster, member))
                            continue;

                        WaitForMasterOrSlave(member, settings.ElectionInterval + settings.MasterBroadcastInterval);
                    }
                }
                else
                {
                    Assert.AreSame(expectedMaster, masterStops_promotee);
                    Assert.AreEqual(ClusterMemberState.Master, expectedMaster.State);
                    Assert.IsTrue(expectedMaster.IsMaster);

                    // Wait a bit and then verify that other slave recogizes the
                    // new master.

                    Thread.Sleep(settings.MasterBroadcastInterval);
                }

                // Verify that other slave recogizes the new master.

                members = new ClusterMember[] { expectedMaster, expectedSlave };
                foreach (ClusterMember member in members)
                {
                    if (!object.ReferenceEquals(expectedMaster, member))
                        Assert.AreEqual(ClusterMemberState.Slave, member.State);
                    else
                        Assert.AreEqual(ClusterMemberState.Master, member.State);

                    ClusterStatus clusterStatus = member.ClusterStatus;

                    Assert.AreEqual(expectedMaster.InstanceEP, clusterStatus.MasterEP);
                    Assert.AreEqual(2, clusterStatus.Members.Count);
                }
            }
            finally
            {
                masterStops_promotee = null;

                if (slave2 != null)
                    slave2.Stop();

                if (slave1 != null)
                    slave1.Stop();

                if (master != null)
                    master.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_SlaveFails()
        {
            // Start three instances and allow one to be elected as master.
            // Then simulate the failure of one of the slaves and verify
            // that the slave is removed from the cluster status.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[3];
            ClusterMember master = null;
            ClusterMember pausedSlave;
            ClusterMember remainingSlave;
            ClusterMemberSettings settings;
            ClusterStatus clusterStatus;

            SetConfig("Cluster", cfg);

            try
            {
                settings = ClusterMemberSettings.LoadConfig("Cluster");

                router = new LeafRouter();
                router.Start();

                // Start the members

                for (int i = 0; i < members.Length; i++)
                {
                    members[i] = new ClusterMember(router, "Cluster");
                    members[i].Start();
                }

                // Figure out which member should become the master.

                master = members[0];
                for (int i = 1; i < members.Length; i++)
                    if (MsgEP.Compare(members[i].InstanceEP, master.InstanceEP) > 0)
                        master = members[i];

                // Wait for all of the members to enter the warmup state

                foreach (ClusterMember member in members)
                    WaitForState(member, ClusterMemberState.Warmup, waitSlop);

                // Wait for the members to enter the election state

                foreach (ClusterMember member in members)
                    WaitForState(member, ClusterMemberState.Election, settings.MissingMasterInterval);

                // Wait for the members to enter the master or slave state.

                foreach (ClusterMember member in members)
                    WaitForMasterOrSlave(member, settings.ElectionInterval);

                // Wait a bit to allow the master to broadcast cluster
                // status with all members and then verify that all members
                // have cluster state.

                Thread.Sleep(settings.MissingMasterInterval);

                foreach (ClusterMember member in members)
                    WaitForClusterStatus(member, settings.MasterBroadcastInterval);

                // Confirm that the instance we expected to become the master
                // is indeed the master and that the other instances are slaves.

                Assert.IsTrue(master.IsMaster);
                foreach (ClusterMember member in members)
                {
                    if (!object.ReferenceEquals(master, member))
                        Assert.AreEqual(ClusterMemberState.Slave, member.State);
                    else
                        Assert.AreEqual(ClusterMemberState.Master, member.State);

                    Assert.AreEqual(master.MasterEP, member.ClusterStatus.MasterEP);
                }

                // Verify that each instance knows about the others.

                foreach (ClusterMember member in members)
                {
                    ClusterStatus status = member.ClusterStatus;

                    Assert.AreEqual(members.Length, status.Members.Count);
                    for (int i = 0; i < members.Length; i++)
                        Assert.IsNotNull(status.GetMemberStatus(members[i].InstanceEP));
                }

                // Let the member run a while to verify that we don't
                // see any strange behavior.

                Thread.Sleep(runTime);

                // Select one slave to remain alive and the other to
                // be paused, simulating failure.

                pausedSlave = null;
                remainingSlave = null;

                foreach (ClusterMember member in members)
                {
                    if (member.IsMaster)
                        continue;

                    if (pausedSlave == null)
                        pausedSlave = member;
                    else
                        remainingSlave = member;

                    if (pausedSlave != null && remainingSlave != null)
                        break;
                }

                // Pause the slave and then wait enough time it to be
                // discovered as missing and this fact to be broadcast
                // to the cluster.

                pausedSlave.Paused = true;
                Thread.Sleep(settings.MissingSlaveInterval + settings.MasterBroadcastInterval + waitSlop);

                // Verify that status information about the paused slave 
                // has been removed.

                clusterStatus = remainingSlave.ClusterStatus;
                Assert.AreEqual(2, clusterStatus.Members.Count);
                Assert.AreEqual(master.InstanceEP, clusterStatus.MasterEP);
                Assert.IsNotNull(clusterStatus.GetMemberStatus(master.InstanceEP));
                Assert.IsNotNull(clusterStatus.GetMemberStatus(remainingSlave.InstanceEP));

                clusterStatus = master.ClusterStatus;
                Assert.AreEqual(2, clusterStatus.Members.Count);
                Assert.AreEqual(master.InstanceEP, clusterStatus.MasterEP);
                Assert.IsNotNull(clusterStatus.GetMemberStatus(master.InstanceEP));
                Assert.IsNotNull(clusterStatus.GetMemberStatus(remainingSlave.InstanceEP));
            }
            finally
            {
                foreach (ClusterMember member in members)
                    member.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_MasterFails()
        {
            // Start three instances and wait for one to be elected as
            // master.  Then simulate a master failure and wait to see if
            // the remaining slaves elect a new master and update the 
            // the cluster status.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[3];
            ClusterMember master = null;
            ClusterMember newMaster;
            ClusterMember remainingSlave;
            ClusterMemberSettings settings;
            ClusterStatus clusterStatus;

            SetConfig("Cluster", cfg);

            try
            {
                settings = ClusterMemberSettings.LoadConfig("Cluster");

                router = new LeafRouter();
                router.Start();

                // Start the members

                for (int i = 0; i < members.Length; i++)
                {
                    members[i] = new ClusterMember(router, "Cluster");
                    members[i].Start();
                }

                // Figure out which member should become the master.

                master = members[0];
                for (int i = 1; i < members.Length; i++)
                    if (MsgEP.Compare(members[i].InstanceEP, master.InstanceEP) > 0)
                        master = members[i];

                // Wait for all of the members to enter the warmup state

                foreach (ClusterMember member in members)
                    WaitForState(member, ClusterMemberState.Warmup, waitSlop);

                // Wait for the members to enter the election state

                foreach (ClusterMember member in members)
                    WaitForState(member, ClusterMemberState.Election, settings.MissingMasterInterval);

                // Wait for the members to enter the master or slave state.

                foreach (ClusterMember member in members)
                    WaitForMasterOrSlave(member, settings.ElectionInterval);

                // Wait a bit to allow the master to broadcast cluster
                // status with all members and then verify that all members
                // have cluster state.

                Thread.Sleep(settings.MissingMasterInterval);

                foreach (ClusterMember member in members)
                    WaitForClusterStatus(member, settings.MasterBroadcastInterval);

                // Confirm that the instance we expected to become the master
                // is indeed the master and that the other instances are slaves.

                Assert.IsTrue(master.IsMaster);
                foreach (ClusterMember member in members)
                {
                    if (!object.ReferenceEquals(master, member))
                        Assert.AreEqual(ClusterMemberState.Slave, member.State);
                    else
                        Assert.AreEqual(ClusterMemberState.Master, member.State);

                    Assert.AreEqual(master.MasterEP, member.ClusterStatus.MasterEP);
                }

                // Verify that each instance knows about the others.

                foreach (ClusterMember member in members)
                {
                    ClusterStatus status = member.ClusterStatus;

                    Assert.AreEqual(members.Length, status.Members.Count);
                    for (int i = 0; i < members.Length; i++)
                        Assert.IsNotNull(status.GetMemberStatus(members[i].InstanceEP));
                }

                // Let the member run a while to verify that we don't
                // see any strange behavior.

                Thread.Sleep(runTime);

                // Pause the master and then wait enough time it to be
                // discovered as missing and a new master to be elected.

                master.Paused = true;
                Thread.Sleep(settings.MissingMasterInterval + settings.ElectionInterval + settings.MasterBroadcastInterval + waitSlop);

                // Verify that a new master has been elected.

                newMaster = null;
                remainingSlave = null;

                foreach (ClusterMember member in members)
                {
                    if (object.ReferenceEquals(member, master))
                        continue;

                    if (member.IsMaster)
                        newMaster = member;
                    else
                        remainingSlave = member;
                }

                // Verify that status information about the paused master 
                // has been removed.

                foreach (ClusterMember member in members)
                {
                    if (object.ReferenceEquals(member, master))
                        continue;

                    clusterStatus = member.ClusterStatus;
                    Assert.AreEqual(2, clusterStatus.Members.Count);
                    Assert.AreEqual(newMaster.InstanceEP, clusterStatus.MasterEP);
                    Assert.IsNotNull(clusterStatus.GetMemberStatus(newMaster.InstanceEP));
                    Assert.IsNotNull(clusterStatus.GetMemberStatus(remainingSlave.InstanceEP));
                }
            }
            finally
            {
                foreach (ClusterMember member in members)
                    member.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_MultipleMasters()
        {
            // Create three cluster members and allow them to elect a master.
            // The pause the master, simulating a network failure and wait for
            // the remaining cluster members to elect a new master.  The unpause
            // the original master.  This will result in multiple masters in
            // the cluster.  Verify that the cluster goes through the process
            // of electing a new master.

            // Start three instances and wait for one to be elected as
            // master.  Then simulate a master failure and wait to see if
            // the remaining slaves elect a new master and update the 
            // the cluster status.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[3];
            ClusterMember master = null;
            ClusterMember newMaster;
            ClusterMember remainingSlave;
            ClusterMemberSettings settings;
            ClusterStatus clusterStatus;

            SetConfig("Cluster", cfg);

            try
            {
                settings = ClusterMemberSettings.LoadConfig("Cluster");

                router = new LeafRouter();
                router.Start();

                // Start the members

                for (int i = 0; i < members.Length; i++)
                {
                    members[i] = new ClusterMember(router, "Cluster");
                    members[i].Start();
                }

                // Figure out which member should become the master.

                master = members[0];
                for (int i = 1; i < members.Length; i++)
                    if (MsgEP.Compare(members[i].InstanceEP, master.InstanceEP) > 0)
                        master = members[i];

                // Wait for all of the members to enter the warmup state

                foreach (ClusterMember member in members)
                    WaitForState(member, ClusterMemberState.Warmup, waitSlop);

                // Wait for the members to enter the election state

                foreach (ClusterMember member in members)
                    WaitForState(member, ClusterMemberState.Election, settings.MissingMasterInterval);

                // Wait for the members to enter the master or slave state.

                foreach (ClusterMember member in members)
                    WaitForMasterOrSlave(member, settings.ElectionInterval);

                // Wait a bit to allow the master to broadcast cluster
                // status with all members and then verify that all members
                // have cluster state.

                Thread.Sleep(settings.MissingMasterInterval);

                foreach (ClusterMember member in members)
                    WaitForClusterStatus(member, settings.MasterBroadcastInterval);

                // Confirm that the instance we expected to become the master
                // is indeed the master and that the other instances are slaves.

                Assert.IsTrue(master.IsMaster);
                foreach (ClusterMember member in members)
                {
                    if (!object.ReferenceEquals(master, member))
                        Assert.AreEqual(ClusterMemberState.Slave, member.State);
                    else
                        Assert.AreEqual(ClusterMemberState.Master, member.State);

                    Assert.AreEqual(master.MasterEP, member.ClusterStatus.MasterEP);
                }

                // Verify that each instance knows about the others.

                foreach (ClusterMember member in members)
                {
                    ClusterStatus status = member.ClusterStatus;

                    Assert.AreEqual(members.Length, status.Members.Count);
                    for (int i = 0; i < members.Length; i++)
                        Assert.IsNotNull(status.GetMemberStatus(members[i].InstanceEP));
                }

                // Let the member run a while to verify that we don't
                // see any strange behavior.

                Thread.Sleep(runTime);

                // Pause the master and then wait enough time it to be
                // discovered as missing and a new master to be elected.

                master.Paused = true;
                Thread.Sleep(settings.MissingMasterInterval + settings.ElectionInterval + settings.MasterBroadcastInterval + waitSlop);

                // Verify that a new master has been elected.

                newMaster = null;
                remainingSlave = null;

                foreach (ClusterMember member in members)
                {
                    if (object.ReferenceEquals(member, master))
                        continue;

                    if (member.IsMaster)
                        newMaster = member;
                    else
                        remainingSlave = member;
                }

                // Verify that status information about the paused master 
                // has been removed.

                foreach (ClusterMember member in members)
                {
                    if (object.ReferenceEquals(member, master))
                        continue;

                    clusterStatus = member.ClusterStatus;
                    Assert.AreEqual(2, clusterStatus.Members.Count);
                    Assert.AreEqual(newMaster.InstanceEP, clusterStatus.MasterEP);
                    Assert.IsNotNull(clusterStatus.GetMemberStatus(newMaster.InstanceEP));
                    Assert.IsNotNull(clusterStatus.GetMemberStatus(remainingSlave.InstanceEP));
                }

                // Bring the master back online.  It should broadcast an immediate
                // cluster-status update causing the other master to notice that
                // there are multiple master and to call for an election.  Wait
                // a bit an then verify that all instances are in the election state.
                // Note that the original master should be re-elected since it
                // still has the lexically greatest endpoint.

                // Wait for the members to enter the election state

                master.Paused = false;
                foreach (ClusterMember member in members)
                    WaitForState(member, ClusterMemberState.Election, settings.MissingMasterInterval);

                // Wait for the members to enter the master or slave state.

                foreach (ClusterMember member in members)
                    WaitForMasterOrSlave(member, settings.ElectionInterval);

                // Wait a bit to allow the master to broadcast cluster
                // status with all members and then verify that all members
                // have cluster state.

                Thread.Sleep(settings.MissingMasterInterval + waitSlop);

                foreach (ClusterMember member in members)
                    WaitForClusterStatus(member, settings.MasterBroadcastInterval);

                // Confirm that we have a new master and that the other instances are slaves.

                master = WaitForMaster(members, TimeSpan.FromSeconds(60));
                foreach (ClusterMember member in members)
                {
                    if (!object.ReferenceEquals(master, member))
                        Assert.AreEqual(ClusterMemberState.Slave, member.State);
                    else
                        Assert.AreEqual(ClusterMemberState.Master, member.State);

                    Assert.AreEqual(master.MasterEP, member.ClusterStatus.MasterEP);
                }

                // Verify that each instance knows about the others.

                foreach (ClusterMember member in members)
                {
                    ClusterStatus status = member.ClusterStatus;

                    Assert.AreEqual(members.Length, status.Members.Count);
                    for (int i = 0; i < members.Length; i++)
                        Assert.IsNotNull(status.GetMemberStatus(members[i].InstanceEP));
                }

                // Let the member run a while to verify that we don't
                // see any strange behavior.

                Thread.Sleep(runTime);
            }
            finally
            {
                foreach (ClusterMember member in members)
                    member.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_ClusterProperties()
        {
            // Launch four cluster members, wait for them to come online,
            // and then update a cluster property on the master and
            // verify that the change is replicated across the cluster.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[4];
            ClusterMember master = null;
            ClusterMemberSettings settings;

            SetConfig("Cluster", cfg);

            try
            {
                settings = ClusterMemberSettings.LoadConfig("Cluster");

                router = new LeafRouter();
                router.Start();

                // Start the members

                for (int i = 0; i < members.Length; i++)
                {
                    members[i] = new ClusterMember(router, "Cluster");
                    members[i].Start();
                }

                // Wait for the members to enter the master or slave state.

                foreach (ClusterMember member in members)
                    WaitForMasterOrSlave(member, TimeSpan.FromMinutes(1));

                // Wait long enough for a master to be elected

                master = WaitForMaster(members, TimeSpan.FromSeconds(60));

                // Test #1: Add a value at the master and verify that it
                //          replicates to all members.

                master.GlobalSet("hello", "world!");
                Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);

                for (int i = 0; i < members.Length; i++)
                {
                    Assert.AreEqual("world!", members[i].GlobalGet("hello"));
                    Assert.AreEqual("world!", members[i].GlobalGet("HELLO"));    // Verify case insensitivity
                }

                // Test #2: Modify a master value and verify that is 
                //          replicates to all members.

                master.GlobalSet("hello", "new world!");
                Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);

                for (int i = 0; i < members.Length; i++)
                    Assert.AreEqual("new world!", members[i].GlobalGet("hello"));

                // Test #3: Try adding a two more values.

                master.GlobalSet("foo", "bar");
                master.GlobalSet("foofoo", "barbar");
                Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);

                for (int i = 0; i < members.Length; i++)
                {
                    Assert.AreEqual("new world!", members[i].GlobalGet("hello"));
                    Assert.AreEqual("bar", members[i].GlobalGet("foo"));
                    Assert.AreEqual("barbar", members[i].GlobalGet("foofoo"));
                }

                // Test #4: Remove a value.

                master.GlobalRemove("foofoo");
                Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);

                for (int i = 0; i < members.Length; i++)
                {
                    Assert.AreEqual("new world!", members[i].GlobalGet("hello"));
                    Assert.AreEqual("bar", members[i].GlobalGet("foo"));
                    Assert.IsFalse(members[i].GlobalContainsKey("foofoo"));
                }

                // Test #5: Clear all values

                master.GlobalClear();
                Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);

                for (int i = 0; i < members.Length; i++)
                {
                    Assert.IsFalse(members[i].GlobalContainsKey("hello"));
                    Assert.IsFalse(members[i].GlobalContainsKey("foo"));
                    Assert.IsFalse(members[i].GlobalContainsKey("foofoo"));
                }
            }
            finally
            {
                foreach (ClusterMember member in members)
                    member.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_MemberProperties()
        {
            // Launch four cluster members, wait for them to come online,
            // and then modify properties on various members and confirm
            // that the changes are replicated across the cluster.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[4];
            ClusterMemberSettings settings;

            SetConfig("Cluster", cfg);

            try
            {
                settings = ClusterMemberSettings.LoadConfig("Cluster");

                router = new LeafRouter();
                router.Start();

                // Start the members

                for (int i = 0; i < members.Length; i++)
                {
                    members[i] = new ClusterMember(router, "Cluster");
                    members[i].Start();
                }

                // Wait for the members to enter the master or slave state.

                foreach (ClusterMember member in members)
                    WaitForMasterOrSlave(member, TimeSpan.FromMinutes(1));

                // Wait long enough for a master to be elected

                Thread.Sleep(settings.ElectionInterval + waitSlop);

                // Test #1: Add value to each member.

                foreach (ClusterMember member in members)
                    member["ID"] = member.InstanceEP.ToString();

                Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);

                foreach (ClusterMember member in members)
                {
                    Assert.AreEqual(member.InstanceEP, (MsgEP)member["ID"]);
                    Assert.AreEqual(member.InstanceEP, (MsgEP)member["id"]);    // Verify case insensitivity
                }

                // Test #2: Add a couple more values.

                foreach (ClusterMember member in members)
                {
                    member["Hello"] = "World!";
                    member["Foo"] = "bar";
                }

                Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);

                foreach (ClusterMember member in members)
                {
                    Assert.AreEqual(member.InstanceEP, (MsgEP)member["ID"]);
                    Assert.AreEqual("World!", member["hello"]);
                    Assert.AreEqual("bar", member["FOO"]);
                }

                // Test #3: Remove a value

                foreach (ClusterMember member in members)
                    member.Remove("foo");

                Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);

                foreach (ClusterMember member in members)
                {
                    Assert.AreEqual(member.InstanceEP, (MsgEP)member["ID"]);
                    Assert.AreEqual("World!", member["hello"]);
                    Assert.IsFalse(member.ContainsKey("FOO"));
                }

                // Test #4: Clear all values

                foreach (ClusterMember member in members)
                    member.Clear();

                Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);

                foreach (ClusterMember member in members)
                {
                    Assert.IsFalse(member.ContainsKey("ID"));
                    Assert.IsFalse(member.ContainsKey("hello"));
                    Assert.IsFalse(member.ContainsKey("FOO"));
                }
            }
            finally
            {
                foreach (ClusterMember member in members)
                    member.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        private class SceData
        {
            public ClusterMemberState OrgState;
            public ClusterMemberState NewState;

            public SceData(ClusterMemberState orgState, ClusterMemberState newState)
            {
                this.OrgState = orgState;
                this.NewState = newState;
            }
        }

        private List<SceData> sceStates = new List<SceData>();

        private void OnStateChange(ClusterMember sender, ClusterMemberEventArgs args)
        {
            lock (syncLock)
                sceStates.Add(new SceData(args.OriginalState, args.NewState));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_StateChangeEvent()
        {
            // Watch a single cluster instance go through the event sequenence:
            //
            //      Stopped --> Warmup --> Election --> Master --> Stop

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember member = null;
            ClusterMemberSettings settings;

            SetConfig("Cluster", cfg);

            try
            {
                settings = ClusterMemberSettings.LoadConfig("Cluster");

                router = new LeafRouter();
                router.Start();

                sceStates.Clear();

                member = new ClusterMember(router, "Cluster");
                member.StateChange += new ClusterMemberEventHandler(OnStateChange);
                member.Start();

                // Wait for the instance to enter the master state.

                WaitForMasterOrSlave(member, TimeSpan.FromMinutes(1));
                member.Stop();
                member = null;

                // Verify the state events:
                //
                //      Stopped --> Warmup --> Election --> Master --> Stop

                Assert.AreEqual(ClusterMemberState.Stopped, sceStates[0].OrgState);
                Assert.AreEqual(ClusterMemberState.Warmup, sceStates[0].NewState);

                Assert.AreEqual(ClusterMemberState.Warmup, sceStates[1].OrgState);
                Assert.AreEqual(ClusterMemberState.Election, sceStates[1].NewState);

                Assert.AreEqual(ClusterMemberState.Election, sceStates[2].OrgState);
                Assert.AreEqual(ClusterMemberState.Master, sceStates[2].NewState);

                Assert.AreEqual(ClusterMemberState.Master, sceStates[3].OrgState);
                Assert.AreEqual(ClusterMemberState.Stopped, sceStates[3].NewState);
            }
            finally
            {
                if (member != null)
                    member.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        private class SueData
        {
            public ClusterMember Member;

            public SueData(ClusterMember member)
            {
                this.Member = member;
            }
        }

        private List<SueData> sueEvents = new List<SueData>();

        private void OnStatusUpdate(ClusterMember sender, ClusterMemberEventArgs args)
        {
            lock (syncLock)
                sueEvents.Add(new SueData(sender));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_StatusUpdateEvent()
        {
            // Launch four cluster members with StatusUpdate event handlers, 
            // wait for them to come online record the status update events
            // received.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[4];
            ClusterMemberSettings settings;

            SetConfig("Cluster", cfg);

            try
            {
                settings = ClusterMemberSettings.LoadConfig("Cluster");

                sueEvents.Clear();

                router = new LeafRouter();
                router.Start();

                // Start the members

                for (int i = 0; i < members.Length; i++)
                {
                    members[i] = new ClusterMember(router, "Cluster");
                    members[i].ClusterStatusUpdate += new ClusterMemberEventHandler(OnStatusUpdate);
                    members[i].Start();
                }

                // Wait for the members to enter the master or slave state.

                foreach (ClusterMember member in members)
                    WaitForMasterOrSlave(member, TimeSpan.FromMinutes(1));

                // Wait long enough for a cluster status broadcast to be made by
                // the master and received by all instances.

                Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);

                // Verify that an update event was raised for each member.

                bool[] gotUpdate = new bool[members.Length];

                for (int i = 0; i < members.Length; i++)
                    foreach (SueData evt in sueEvents)
                        if (object.ReferenceEquals(evt.Member, members[i]))
                            gotUpdate[i] = true;

                for (int i = 0; i < gotUpdate.Length; i++)
                    Assert.IsTrue(gotUpdate[i]);
            }
            finally
            {
                foreach (ClusterMember member in members)
                    member.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        private void OnStatusTransmission(ClusterMember sender, ClusterMemberEventArgs args)
        {
            if (!sender.ContainsKey("ID"))
                sender["ID"] = sender.InstanceEP.ToString();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_StatusTransmissionEvent()
        {

            // Launch four cluster members with StatusTransmission event handlers, 
            // set a local member property within the handler and then verify that
            // property was set and replicated across the members.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[4];
            ClusterMemberSettings settings;

            SetConfig("Cluster", cfg);

            try
            {
                settings = ClusterMemberSettings.LoadConfig("Cluster");

                router = new LeafRouter();
                router.Start();

                // Start the members

                for (int i = 0; i < members.Length; i++)
                {
                    members[i] = new ClusterMember(router, "Cluster");
                    members[i].StatusTransmission += new ClusterMemberEventHandler(OnStatusTransmission);
                    members[i].Start();
                }

                // Wait for the members to enter the master or slave state.

                foreach (ClusterMember member in members)
                    WaitForMasterOrSlave(member, TimeSpan.FromMinutes(1));

                // Wait long enough for a master to be elected and for a status update
                // to be broadcast and received.

                Thread.Sleep(settings.ElectionInterval + settings.SlaveUpdateInterval + settings.MasterBroadcastInterval + waitSlop);

                // Verify that the StatusTransmission event handler was called and
                // by making sure that the "ID" property was set.

                foreach (ClusterMember member in members)
                    Assert.AreEqual(member.InstanceEP, (MsgEP)member["ID"]);
            }
            finally
            {
                foreach (ClusterMember member in members)
                    member.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        private ClusterMember msMaster = null;
        private ClusterMember msSlave = null;

        private void OnMasterTask(ClusterMember sender, ClusterMemberEventArgs args)
        {
            lock (syncLock)
            {
                if (msMaster == null)
                    msMaster = sender;
            }
        }

        private void OnSlaveTask(ClusterMember sender, ClusterMemberEventArgs args)
        {
            lock (syncLock)
            {
                if (msSlave == null)
                    msSlave = sender;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_MasterSlaveTask()
        {
            // Launch two cluster instances.  One will become the master and
            // the other a slave.  Wait a bit and verify that the SlaveTask
            // event was raised for the slave and the MasterTask event was
            // raised for the master.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[2];
            ClusterMemberSettings settings;

            SetConfig("Cluster", cfg);

            try
            {
                settings = ClusterMemberSettings.LoadConfig("Cluster");

                router = new LeafRouter();
                router.Start();

                // Start the members

                for (int i = 0; i < members.Length; i++)
                {
                    members[i] = new ClusterMember(router, "Cluster");
                    members[i].MasterTask += new ClusterMemberEventHandler(OnMasterTask);
                    members[i].SlaveTask += new ClusterMemberEventHandler(OnSlaveTask);
                    members[i].Start();
                }

                // Wait for the members to enter the master or slave state.

                foreach (ClusterMember member in members)
                    WaitForMasterOrSlave(member, TimeSpan.FromMinutes(1));

                // Wait long enough for a master to be elected and for a status update
                // to be broadcast and received and also for the master and slave task
                // events to have been raised at least once.

                msMaster = null;
                msSlave = null;

                Thread.Sleep(settings.ElectionInterval + settings.MasterBroadcastInterval + settings.SlaveBkInterval + settings.MasterBkInterval + waitSlop);

                // Verify that the MasterTask and SlaveTasks where raised properly.

                Assert.IsNotNull(msMaster);
                Assert.IsTrue(msMaster.IsMaster);

                Assert.IsNotNull(msSlave);
                Assert.IsFalse(msSlave.IsMaster);
            }
            finally
            {
                foreach (ClusterMember member in members)
                    member.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        private class MyClusterMember : ClusterMember
        {
            public int Value = -1;

            public MyClusterMember(MsgRouter router, string keyPrefix)
                : base(router, keyPrefix)
            {
            }

            [MsgHandler(LogicalEP = MsgEP.Null, DynamicScope = "Test")]
            public void OnMsg(PropertyMsg msg)
            {
                this.Value = msg._Get("value", -1);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_AddTarget()
        {
            // Launch two cluster instances, call AddTarget() to initialize a
            // custom message handler and then allow them to start
            // and establish a cluster.  Then send an application
            // to each unique instance and verify that the messages
            // were routed to the correct instances.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            MyClusterMember[] members = new MyClusterMember[2];
            ClusterMemberSettings settings;

            SetConfig("Cluster", cfg);

            try
            {
                settings = ClusterMemberSettings.LoadConfig("Cluster");

                router = new LeafRouter();
                router.Start();

                // Start the members

                for (int i = 0; i < members.Length; i++)
                {
                    members[i] = new MyClusterMember(router, "Cluster");
                    members[i].Start();
                    members[i].AddTarget(members[i], "Test");
                }

                // Wait for the members to enter the master or slave state.

                foreach (ClusterMember member in members)
                    WaitForMasterOrSlave(member, TimeSpan.FromMinutes(1));

                // Wait long enough for a master to be elected and for a status update
                // to be broadcast and received and for the master and slave task
                // events to have been raised at least once.

                Thread.Sleep(settings.ElectionInterval + settings.MasterBroadcastInterval + settings.SlaveBkInterval + settings.MasterBkInterval + waitSlop);

                // Now send a message to each cluster member and verify that it
                // was routed correctly.

                for (int i = 0; i < members.Length; i++)
                {
                    PropertyMsg msg;

                    msg = new PropertyMsg();
                    msg._Set("value", i);
                    members[i].Value = -1;
                    router.SendTo(members[i].InstanceEP, msg);
                }

                Thread.Sleep(1000);

                for (int i = 0; i < members.Length; i++)
                    Assert.AreEqual(i, members[i].Value);
            }
            finally
            {
                foreach (ClusterMember member in members)
                    member.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_ClusterTime()
        {
            // Launch four cluster instances.  Then verify that the
            // cluster time reported by each instance is within
            // 5 seconds of the current machine UTC time.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 15s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[4];
            ClusterMemberSettings settings;

            SetConfig("Cluster", cfg);

            try
            {
                settings = ClusterMemberSettings.LoadConfig("Cluster");

                router = new LeafRouter();
                router.Start();

                // Start the members

                for (int i = 0; i < members.Length; i++)
                {
                    members[i] = new ClusterMember(router, "Cluster");
                    members[i].Start();
                }

                // Verify that the times are remain accurate for the
                // next minute or so.

                for (int i = 0; i < 60; i++)
                {
                    foreach (ClusterMember member in members)
                        Assert.IsTrue(Helper.Within(DateTime.UtcNow, member.ClusterTime, TimeSpan.FromSeconds(5)));

                    Thread.Sleep(1000);
                }
            }
            finally
            {
                foreach (ClusterMember member in members)
                    member.Stop();

                if (router != null)
                    router.Stop();

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_Mode_Monitor()
        {
            // Launch a monitor mode instance and three other cluster
            // members and verify that the monitor sees the other instances
            // but the other instances don't see the monitor.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[4];
            ClusterMemberSettings settings;

            // Run the test several times

            for (int j = 0; j < LoopCount; j++)
            {

                try
                {

                    // Alternate the GUID generation mode to verify that we get good
                    // lexical distribution on the GUIDs

                    Helper.SetLocalGuidMode((j & 1) == 0 ? GuidMode.CountUp : GuidMode.CountDown);

                    SetConfig("Cluster", cfg);

                    router = new LeafRouter();
                    router.Start();

                    // Start the monitor member

                    settings = ClusterMemberSettings.LoadConfig("Cluster");
                    settings.Mode = ClusterMemberMode.Monitor;
                    members[0] = new ClusterMember(router, settings);
                    members[0].Start();

                    Assert.IsFalse(members[0].IsOnline);    // Observers are never online
                    Assert.AreEqual(ClusterMemberMode.Monitor, members[0].Mode);

                    // Start the normal members

                    for (int i = 1; i < members.Length; i++)
                    {
                        members[i] = new ClusterMember(router, "Cluster");
                        members[i].Start();
                        Assert.AreEqual(ClusterMemberMode.Normal, members[i].Mode);
                    }

                    // Wait for all of the normal members to come online

                    for (int i = 1; i < members.Length; i++)
                        WaitForOnline(members[i], TimeSpan.FromMinutes(1));

                    Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);

                    Assert.IsFalse(members[0].IsOnline);    // Observers are never online

                    // Verify that each instance knows about the normal members.

                    foreach (ClusterMember member in members)
                    {

                        ClusterStatus status = member.ClusterStatus;

                        Assert.AreEqual(members.Length - 1, status.Members.Count);
                        for (int i = 1; i < members.Length; i++)
                            Assert.IsNotNull(status.GetMemberStatus(members[i].InstanceEP));
                    }

                    // Verify that there's no status for the monitoring member

                    foreach (ClusterMember member in members)
                    {
                        ClusterStatus status = member.ClusterStatus;

                        Assert.IsNull(status.GetMemberStatus(members[0].InstanceEP));
                    }

                    // Let the member run a while to verify that we don't
                    // see any strange behavior.

                    Thread.Sleep(runTime);
                }
                finally
                {
                    foreach (ClusterMember member in members)
                        if (member != null)
                            member.Stop();

                    if (router != null)
                        router.Stop();

                    Config.SetConfig(null);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_Mode_Observer()
        {
            // Launch an observer mode instance and three other cluster
            // members and verify that the observer does not become the master
            // and that all instances see the others.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[4];
            ClusterMemberSettings settings;

            // Run the test serveral times

            for (int j = 0; j < LoopCount; j++)
            {
                try
                {
                    // Alternate the GUID generation mode to verify that we get good
                    // lexical distribution on the GUIDs

                    Helper.SetLocalGuidMode((j & 1) == 0 ? GuidMode.CountUp : GuidMode.CountDown);

                    SetConfig("Cluster", cfg);

                    router = new LeafRouter();
                    router.Start();

                    // Start the monitor member

                    settings = ClusterMemberSettings.LoadConfig("Cluster");
                    settings.Mode = ClusterMemberMode.Observer;
                    members[0] = new ClusterMember(router, settings);
                    members[0].Start();

                    Assert.IsTrue(members[0].IsOnline);     // Observers are online immediately
                    Assert.AreEqual(ClusterMemberMode.Observer, members[0].Mode);

                    // Start the normal members

                    for (int i = 1; i < members.Length; i++)
                    {
                        members[i] = new ClusterMember(router, "Cluster");
                        members[i].Start();
                        Assert.AreEqual(ClusterMemberMode.Normal, members[i].Mode);
                    }

                    // Wait for all of the normal members to come online

                    for (int i = 1; i < members.Length; i++)
                        WaitForOnline(members[i], TimeSpan.FromMinutes(1));

                    Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);

                    // Verify that each instance knows about the others members.

                    foreach (ClusterMember member in members)
                    {
                        ClusterStatus status = member.ClusterStatus;

                        Assert.AreEqual(members.Length, status.Members.Count);
                        for (int i = 0; i < members.Length; i++)
                        {

                            Assert.IsNotNull(status.GetMemberStatus(members[i].InstanceEP));
                            Assert.AreEqual(i == 0 ? ClusterMemberMode.Observer : ClusterMemberMode.Normal, status.GetMemberStatus(members[i].InstanceEP).Mode);
                        }
                    }

                    // Let the member run a while to verify that we don't
                    // see any strange behavior.

                    Thread.Sleep(runTime);
                }
                finally
                {
                    foreach (ClusterMember member in members)
                        if (member != null)
                            member.Stop();

                    if (router != null)
                        router.Stop();

                    Config.SetConfig(null);
                    Helper.SetLocalGuidMode(GuidMode.CountUp);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_Mode_PreferMaster()
        {
            // Launch a prefer master mode instance along with 3 other
            // normal mode instances and verify that the prefer master
            // instance is always elected to be master.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[4];
            ClusterMemberSettings settings;

            // Run the test several times.

            for (int j = 0; j < LoopCount; j++)
            {
                try
                {
                    // Alternate the GUID generation mode to verify that we get good
                    // lexical distribution on the GUIDs

                    Helper.SetLocalGuidMode((j & 1) != 0 ? GuidMode.CountUp : GuidMode.CountDown); ;

                    SetConfig("Cluster", cfg);

                    router = new LeafRouter();
                    router.Start();

                    // Start the prefer-slave mode member

                    settings = ClusterMemberSettings.LoadConfig("Cluster");
                    settings.Mode = ClusterMemberMode.PreferMaster;
                    members[0] = new ClusterMember(router, settings);
                    members[0].Start();

                    // Start the normal members

                    for (int i = 1; i < members.Length; i++)
                    {
                        members[i] = new ClusterMember(router, "Cluster");
                        members[i].Start();
                        Assert.AreEqual(ClusterMemberMode.Normal, members[i].Mode);
                    }

                    // Wait for all the members to enter the election state

                    for (int i = 0; i < members.Length; i++)
                        WaitForState(members[i], ClusterMemberState.Election, TimeSpan.FromMinutes(1));

                    // Wait for all of the members to come online

                    for (int i = 0; i < members.Length; i++)
                        WaitForOnline(members[i], TimeSpan.FromMinutes(1));

                    Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);

                    // Verify that the PreferMsster mode instance is the master

                    Assert.IsTrue(members[0].IsMaster);

                    // Verify that each instance knows about the others members.

                    foreach (ClusterMember member in members)
                    {
                        ClusterStatus status = member.ClusterStatus;

                        Assert.AreEqual(members.Length, status.Members.Count);
                        for (int i = 0; i < members.Length; i++)
                        {
                            Assert.IsNotNull(status.GetMemberStatus(members[i].InstanceEP));
                            Assert.AreEqual(i == 0 ? ClusterMemberMode.PreferMaster : ClusterMemberMode.Normal, status.GetMemberStatus(members[i].InstanceEP).Mode);
                        }
                    }
                }
                finally
                {
                    for (int i = 0; i < members.Length; i++)
                    {
                        if (members[i] != null)
                        {
                            members[i].Stop();
                            members[i] = null;
                        }
                    }

                    if (router != null)
                        router.Stop();

                    Config.SetConfig(null);
                    Helper.SetLocalGuidMode(GuidMode.CountUp);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_Mode_PreferMaster_Election()
        {
            // Launch three normal mode instances and allow them to go
            // through the election cycle.  Then start a prefer master
            // instance and verify that it calls an election and eventually
            // becomes the new master.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[4];
            ClusterMemberSettings settings;

            // Run the test several times.

            for (int j = 0; j < LoopCount; j++)
            {
                try
                {
                    // Alternate the GUID generation mode to verify that we get good
                    // lexical distribution on the GUIDs

                    Helper.SetLocalGuidMode((j & 1) == 0 ? GuidMode.CountUp : GuidMode.CountDown); ;

                    SetConfig("Cluster", cfg);
                    settings = ClusterMemberSettings.LoadConfig("Cluster");

                    router = new LeafRouter();
                    router.Start();

                    // Start the normal members and wait for them to go 
                    // go through the election cycle and elect a master.

                    for (int i = 1; i < members.Length; i++)
                    {
                        members[i] = new ClusterMember(router, "Cluster");
                        members[i].Start();
                        Assert.AreEqual(ClusterMemberMode.Normal, members[i].Mode);
                    }

                    for (int i = 1; i < members.Length; i++)
                        WaitForState(members[i], ClusterMemberState.Election, TimeSpan.FromMinutes(1));

                    for (int i = 1; i < members.Length; i++)
                        WaitForOnline(members[i], TimeSpan.FromMinutes(1));

                    Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);
                    Assert.IsNotNull(members[1].ClusterStatus.MasterEP);

                    // Start the prefer-master mode member.

                    settings = ClusterMemberSettings.LoadConfig("Cluster");
                    settings.Mode = ClusterMemberMode.PreferMaster;
                    members[0] = new ClusterMember(router, settings);
                    members[0].Start();

                    // Verify that an election is called and wait for all 
                    // instances to come back online and verify that the
                    // prefer-master instance is indeed the master.

                    WaitForOnline(members[0], TimeSpan.FromMinutes(1));
                    Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);
                    Assert.IsTrue(members[0].IsMaster);

                    // Verify that each instance knows about the others members.

                    foreach (ClusterMember member in members)
                    {
                        ClusterStatus status = member.ClusterStatus;

                        Assert.AreEqual(members.Length, status.Members.Count);
                        for (int i = 0; i < members.Length; i++)
                        {
                            Assert.IsNotNull(status.GetMemberStatus(members[i].InstanceEP));
                            Assert.AreEqual(i == 0 ? ClusterMemberMode.PreferMaster : ClusterMemberMode.Normal, status.GetMemberStatus(members[i].InstanceEP).Mode);
                        }
                    }
                }
                finally
                {
                    for (int i = 0; i < members.Length; i++)
                    {
                        if (members[i] != null)
                        {
                            members[i].Stop();
                            members[i] = null;
                        }
                    }

                    if (router != null)
                        router.Stop();

                    Config.SetConfig(null);
                    Helper.SetLocalGuidMode(GuidMode.CountUp);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_Mode_PreferSlave()
        {
            // Launch a prefer slave mode instance along with 3 other
            // normal mode instances and verify that the prefer slave
            // instance is never elected to be master.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[4];
            ClusterMemberSettings settings;

            // Run the test several times.

            for (int j = 0; j < LoopCount; j++)
            {
                try
                {
                    // Alternate the GUID generation mode to verify that we get good
                    // lexical distribution on the GUIDs

                    Helper.SetLocalGuidMode((j & 1) != 0 ? GuidMode.CountUp : GuidMode.CountDown); ;

                    SetConfig("Cluster", cfg);

                    router = new LeafRouter();
                    router.Start();

                    // Start the prefer-slave mode member

                    settings = ClusterMemberSettings.LoadConfig("Cluster");
                    settings.Mode = ClusterMemberMode.PreferSlave;
                    members[0] = new ClusterMember(router, settings);
                    members[0].Start();

                    // Start the normal members

                    for (int i = 1; i < members.Length; i++)
                    {
                        members[i] = new ClusterMember(router, "Cluster");
                        members[i].Start();
                        Assert.AreEqual(ClusterMemberMode.Normal, members[i].Mode);
                    }

                    // Wait for all the members to enter the election state

                    for (int i = 0; i < members.Length; i++)
                        WaitForState(members[i], ClusterMemberState.Election, TimeSpan.FromMinutes(1));

                    // Wait for all of the members to come online

                    for (int i = 0; i < members.Length; i++)
                        WaitForOnline(members[i], TimeSpan.FromMinutes(1));

                    Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);

                    // Verify that the PreferSlave mode instance is not the master

                    Assert.IsFalse(members[0].IsMaster);

                    // Verify that each instance knows about the others members.

                    foreach (ClusterMember member in members)
                    {
                        ClusterStatus status = member.ClusterStatus;

                        Assert.AreEqual(members.Length, status.Members.Count);
                        for (int i = 0; i < members.Length; i++)
                        {
                            Assert.IsNotNull(status.GetMemberStatus(members[i].InstanceEP));
                            Assert.AreEqual(i == 0 ? ClusterMemberMode.PreferSlave : ClusterMemberMode.Normal, status.GetMemberStatus(members[i].InstanceEP).Mode);
                        }
                    }
                }
                finally
                {
                    for (int i = 0; i < members.Length; i++)
                    {
                        if (members[i] != null)
                        {
                            members[i].Stop();
                            members[i] = null;
                        }
                    }

                    if (router != null)
                        router.Stop();

                    Config.SetConfig(null);
                    Helper.SetLocalGuidMode(GuidMode.CountUp);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_Mode_PreferSlave_Election()
        {
            // Launch a prefer slave mode instance and wait for it to 
            // grudgingly become the master (bacause there are no other
            // instances running).  Then start 3 other normal mode instances 
            // and verify that an election is called and one of the normal 
            // instances becomes the master.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = Normal
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[4];
            ClusterMemberSettings settings;

            // Run the test several times.

            for (int j = 0; j < LoopCount; j++)
            {
                try
                {
                    // Alternate the GUID generation mode to verify that we get good
                    // lexical distribution on the GUIDs

                    Helper.SetLocalGuidMode((j & 1) == 0 ? GuidMode.CountUp : GuidMode.CountDown); ;

                    SetConfig("Cluster", cfg);

                    router = new LeafRouter();
                    router.Start();

                    // Start the prefer-slave mode member and wait for
                    // it to become the master.

                    settings = ClusterMemberSettings.LoadConfig("Cluster");
                    settings.Mode = ClusterMemberMode.PreferSlave;
                    members[0] = new ClusterMember(router, settings);
                    members[0].Start();

                    WaitForOnline(members[0], TimeSpan.FromMinutes(1));
                    Assert.AreEqual(ClusterMemberState.Master, members[0].State);

                    // Start the normal members

                    for (int i = 1; i < members.Length; i++)
                    {
                        members[i] = new ClusterMember(router, "Cluster");
                        members[i].Start();
                        Assert.AreEqual(ClusterMemberMode.Normal, members[i].Mode);
                    }

                    // An election should be called

                    for (int i = 0; i < members.Length; i++)
                        WaitForState(members[i], ClusterMemberState.Election, TimeSpan.FromMinutes(1));

                    // Wait for all of the members to come back online

                    for (int i = 0; i < members.Length; i++)
                        WaitForOnline(members[i], TimeSpan.FromMinutes(1));

                    Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);

                    // Verify that the PreferSlave mode instance is not the master

                    Assert.IsFalse(members[0].IsMaster);

                    // Verify that each instance knows about the others members.

                    foreach (ClusterMember member in members)
                    {
                        ClusterStatus status = member.ClusterStatus;

                        Assert.AreEqual(members.Length, status.Members.Count);
                        for (int i = 0; i < members.Length; i++)
                        {
                            Assert.IsNotNull(status.GetMemberStatus(members[i].InstanceEP));
                            Assert.AreEqual(i == 0 ? ClusterMemberMode.PreferSlave : ClusterMemberMode.Normal, status.GetMemberStatus(members[i].InstanceEP).Mode);
                        }
                    }
                }
                finally
                {
                    for (int i = 0; i < members.Length; i++)
                    {
                        if (members[i] != null)
                        {
                            members[i].Stop();
                            members[i] = null;
                        }
                    }

                    if (router != null)
                        router.Stop();

                    Config.SetConfig(null);
                    Helper.SetLocalGuidMode(GuidMode.CountUp);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_Mode_PreferSlave_All()
        {
            // Launch 4 instances, all with the PreferSlave mode and verify
            // that one of them is elected as the master.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = PreferSlave
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[4];
            ClusterMemberSettings settings;

            // Run the test several times.

            for (int j = 0; j < LoopCount; j++)
            {
                try
                {
                    // Alternate the GUID generation mode to verify that we get good
                    // lexical distribution on the GUIDs

                    Helper.SetLocalGuidMode((j & 1) != 0 ? GuidMode.CountUp : GuidMode.CountDown); ;

                    SetConfig("Cluster", cfg);
                    settings = ClusterMemberSettings.LoadConfig("Cluster");

                    router = new LeafRouter();
                    router.Start();

                    // Start the members

                    for (int i = 0; i < members.Length; i++)
                    {
                        members[i] = new ClusterMember(router, "Cluster");
                        members[i].Start();
                        Assert.AreEqual(ClusterMemberMode.PreferSlave, members[i].Mode);
                    }

                    // Wait for all the members to enter the election state

                    for (int i = 0; i < members.Length; i++)
                        WaitForState(members[i], ClusterMemberState.Election, TimeSpan.FromMinutes(1));

                    // Wait for all of the members to come online

                    for (int i = 0; i < members.Length; i++)
                        WaitForOnline(members[i], TimeSpan.FromMinutes(1));

                    Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);

                    // Verify that each instance knows about the others members
                    // and that one of the members is the master.

                    foreach (ClusterMember member in members)
                    {
                        ClusterStatus status = member.ClusterStatus;

                        Assert.AreEqual(members.Length, status.Members.Count);
                        Assert.IsNotNull(member.ClusterStatus.MasterEP);

                        for (int i = 0; i < members.Length; i++)
                            Assert.IsNotNull(status.GetMemberStatus(members[i].InstanceEP));
                    }
                }
                finally
                {
                    for (int i = 0; i < members.Length; i++)
                    {
                        if (members[i] != null)
                        {
                            members[i].Stop();
                            members[i] = null;
                        }
                    }

                    if (router != null)
                        router.Stop();

                    Config.SetConfig(null);
                    Helper.SetLocalGuidMode(GuidMode.CountUp);
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMember_Mode_PreferMaster_All()
        {
            // Launch a prefer master mode instance and wait for it to 
            // the master.  Then start 3 other prefer master mode instances 
            // and verify that no election is called by the new instances
            // and the first instance remains the master.

            string cfg = @"

    ClusterBaseEP           = logical://LT/Test
    Mode                    = PreferMaster
    MasterBroadcastInterval = 1s
    SlaveUpdateInterval     = 1s
    MissingMasterCount      = 3
    MissingSlaveCount       = 3
    MasterBkInterval        = 1s
    SlaveBkInterval         = 1s
    BkInterval              = 250ms
    ElectionInterval        = 3s
";
            LeafRouter router = null;
            ClusterMember[] members = new ClusterMember[4];
            ClusterMemberSettings settings;

            // Run the test several times.

            for (int j = 0; j < LoopCount; j++)
            {
                try
                {
                    // Alternate the GUID generation mode to verify that we get good
                    // lexical distribution on the GUIDs

                    Helper.SetLocalGuidMode((j & 1) == 0 ? GuidMode.CountUp : GuidMode.CountDown); ;

                    SetConfig("Cluster", cfg);
                    settings = ClusterMemberSettings.LoadConfig("Cluster");

                    router = new LeafRouter();
                    router.Start();

                    // Start the prefer-master mode member and wait for
                    // it to become the master.

                    members[0] = new ClusterMember(router, settings);
                    members[0].Start();

                    WaitForOnline(members[0], TimeSpan.FromMinutes(1));
                    Assert.AreEqual(ClusterMemberState.Master, members[0].State);

                    // Start the other members

                    for (int i = 1; i < members.Length; i++)
                    {
                        members[i] = new ClusterMember(router, settings);
                        members[i].Start();
                        Assert.AreEqual(ClusterMemberMode.PreferMaster, members[i].Mode);
                    }

                    // Wait for all of the members to come online

                    for (int i = 0; i < members.Length; i++)
                        WaitForOnline(members[i], TimeSpan.FromMinutes(1));

                    Thread.Sleep(settings.MasterBroadcastInterval + waitSlop);

                    // Verify that the original instance is still the master.

                    Assert.IsTrue(members[0].IsMaster);

                    // Verify that each instance knows about the others members.

                    foreach (ClusterMember member in members)
                    {
                        ClusterStatus status = member.ClusterStatus;

                        Assert.AreEqual(members.Length, status.Members.Count);
                        for (int i = 0; i < members.Length; i++)
                        {
                            Assert.IsNotNull(status.GetMemberStatus(members[i].InstanceEP));
                            Assert.AreEqual(ClusterMemberMode.PreferMaster, status.GetMemberStatus(members[i].InstanceEP).Mode);
                        }
                    }
                }
                finally
                {
                    for (int i = 0; i < members.Length; i++)
                    {
                        if (members[i] != null)
                        {
                            members[i].Stop();
                            members[i] = null;
                        }
                    }

                    if (router != null)
                        router.Stop();

                    Config.SetConfig(null);
                    Helper.SetLocalGuidMode(GuidMode.CountUp);
                }
            }
        }
    }
}

