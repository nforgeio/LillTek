//-----------------------------------------------------------------------------
// FILE:        _ClusterMemberStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _ClusterStatus
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterStatus_Serialize_ClusterMemberStatus()
        {

            EnhancedMemoryStream ms = new EnhancedMemoryStream();
            ClusterMemberSettings settings = new ClusterMemberSettings((MsgEP)"logical://test");
            ClusterMemberStatus status;

            status = new ClusterMemberStatus("logical://test/foo", ClusterMemberState.Master, settings);
            status["setting1"] = "value1";
            status["setting2"] = "value2";
            status.ProtocolCaps = unchecked((ClusterMemberProtocolCaps)0xFFFFFFFF);

            status.Write(ms);
            ms.Position = 0;
            status = new ClusterMemberStatus(ms);

            Assert.AreEqual((MsgEP)"logical://test/foo", status.InstanceEP);
            Assert.AreEqual(Helper.MachineName, status.MachineName);
            Assert.AreEqual(unchecked((ClusterMemberProtocolCaps)0xFFFFFFFF), status.ProtocolCaps);
            Assert.AreEqual(ClusterMemberState.Master, status.State);
            Assert.AreEqual(settings, status.Settings);
            Assert.AreEqual("value1", status["setting1"]);
            Assert.AreEqual("value2", status["setting2"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterStatus_Serialize_ClusterStatus()
        {
            Dictionary<string, string> clusterProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            EnhancedMemoryStream ms = new EnhancedMemoryStream();
            ClusterMemberSettings settings = new ClusterMemberSettings((MsgEP)"logical://test");
            ClusterMemberStatus memberStatus;
            ClusterStatus status;

            status = new ClusterStatus((MsgEP)"logical://test/master");
            status.LoadProperties(clusterProps);
            status.MasterProtocolCaps = unchecked((ClusterMemberProtocolCaps)0xFFFFFFFF);

            memberStatus = new ClusterMemberStatus("logical://test/master", ClusterMemberState.Master, settings);
            memberStatus["setting1"] = "value1";
            memberStatus["setting2"] = "value2";
            status.Members.Add(memberStatus);

            memberStatus = new ClusterMemberStatus("logical://test/bar", ClusterMemberState.Slave, settings);
            memberStatus["setting3"] = "value3";
            memberStatus["setting4"] = "value4";
            status.Members.Add(memberStatus);

            status.Write(ms);
            ms.Position = 0;
            status = new ClusterStatus(ms);

            Assert.AreEqual((MsgEP)"logical://test/master", status.MasterEP);
            Assert.AreEqual(Helper.MachineName, status.MasterMachine);
            Assert.AreEqual(unchecked((ClusterMemberProtocolCaps)0xFFFFFFFF), status.MasterProtocolCaps);
            Assert.AreEqual(2, status.Members.Count);

            memberStatus = status.Members[0];
            Assert.AreEqual((MsgEP)"logical://test/master", memberStatus.InstanceEP);
            Assert.AreEqual(Helper.MachineName, memberStatus.MachineName);
            Assert.AreEqual(ClusterMemberState.Master, memberStatus.State);
            Assert.AreEqual(settings, memberStatus.Settings);
            Assert.AreEqual("value1", memberStatus["setting1"]);
            Assert.AreEqual("value2", memberStatus["setting2"]);

            memberStatus = status.Members[1];
            Assert.AreEqual((MsgEP)"logical://test/bar", memberStatus.InstanceEP);
            Assert.AreEqual(Helper.MachineName, memberStatus.MachineName);
            Assert.AreEqual(ClusterMemberState.Slave, memberStatus.State);
            Assert.AreEqual(settings, memberStatus.Settings);
            Assert.AreEqual("value3", memberStatus["setting3"]);
            Assert.AreEqual("value4", memberStatus["setting4"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterStatus_Clear_ClusterMemberStatus()
        {
            ClusterMemberStatus status = new ClusterMemberStatus((MsgEP)"logical://test/instance", ClusterMemberState.Stopped,
                                                                     new ClusterMemberSettings((MsgEP)"logical://test"));
            status["foo"] = "bar";
            status["_internal"] = "test";

            Assert.AreEqual("bar", status["foo"]);
            Assert.AreEqual("test", status["_internal"]);

            status.Clear();
            Assert.IsFalse(status.ContainsKey("foo"));
            Assert.AreEqual("test", status["_internal"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterStatus_Clone_ClusterMemberStatus()
        {
            ClusterMemberSettings settings = new ClusterMemberSettings((MsgEP)"logical://test");
            ClusterMemberStatus input, output;

            input = new ClusterMemberStatus("logical://test/foo", ClusterMemberState.Master, settings);
            input["setting1"] = "value1";
            input["setting2"] = "value2";
            input.ProtocolCaps = unchecked((ClusterMemberProtocolCaps)0xFFFFFFFF);

            output = input.Clone();
            Assert.AreNotSame(input, output);

            Assert.AreEqual((MsgEP)"logical://test/foo", output.InstanceEP);
            Assert.AreEqual(Helper.MachineName, output.MachineName);
            Assert.AreEqual(unchecked((ClusterMemberProtocolCaps)0xFFFFFFFF), output.ProtocolCaps);
            Assert.AreEqual(ClusterMemberState.Master, output.State);
            Assert.AreEqual(settings, output.Settings);
            Assert.AreEqual("value1", output["setting1"]);
            Assert.AreEqual("value2", output["setting2"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterStatus_Clone_ClusterStatus()
        {
            ClusterMemberSettings settings = new ClusterMemberSettings((MsgEP)"logical://test");
            ClusterMemberStatus memberStatus;
            ClusterStatus input, output;

            input = new ClusterStatus((MsgEP)"logical://test/master");
            input.ClusterTime = DateTime.UtcNow;
            input.MasterProtocolCaps = unchecked((ClusterMemberProtocolCaps)0xFFFFFFFF);

            memberStatus = new ClusterMemberStatus("logical://test/master", ClusterMemberState.Master, settings);
            memberStatus["setting1"] = "value1";
            memberStatus["setting2"] = "value2";
            input.Members.Add(memberStatus);

            memberStatus = new ClusterMemberStatus("logical://test/bar", ClusterMemberState.Slave, settings);
            memberStatus["setting3"] = "value3";
            memberStatus["setting4"] = "value4";
            input.Members.Add(memberStatus);

            output = input.Clone();
            Assert.AreNotSame(input, output);

            Assert.AreEqual((MsgEP)"logical://test/master", output.MasterEP);
            Assert.AreEqual(Helper.MachineName, output.MasterMachine);
            Assert.AreEqual(input.ClusterTime, output.ClusterTime);
            Assert.AreEqual(input.ReceiveTime, output.ReceiveTime);
            Assert.AreEqual(unchecked((ClusterMemberProtocolCaps)0xFFFFFFFF), output.MasterProtocolCaps);
            Assert.AreEqual(2, output.Members.Count);

            memberStatus = output.Members[0];
            Assert.AreEqual((MsgEP)"logical://test/master", memberStatus.InstanceEP);
            Assert.AreEqual(Helper.MachineName, memberStatus.MachineName);
            Assert.AreEqual(ClusterMemberState.Master, memberStatus.State);
            Assert.AreEqual(settings, memberStatus.Settings);
            Assert.AreEqual("value1", memberStatus["setting1"]);
            Assert.AreEqual("value2", memberStatus["setting2"]);

            memberStatus = output.Members[1];
            Assert.AreEqual((MsgEP)"logical://test/bar", memberStatus.InstanceEP);
            Assert.AreEqual(Helper.MachineName, memberStatus.MachineName);
            Assert.AreEqual(ClusterMemberState.Slave, memberStatus.State);
            Assert.AreEqual(settings, memberStatus.Settings);
            Assert.AreEqual("value3", memberStatus["setting3"]);
            Assert.AreEqual("value4", memberStatus["setting4"]);
        }
    }
}

