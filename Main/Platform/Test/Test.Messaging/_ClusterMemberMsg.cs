//-----------------------------------------------------------------------------
// FILE:        _ClusterMemberMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Net;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Testing;

namespace LillTek.Messaging.Test.Messages
{
    [TestClass]
    public class _ClusterMemberMsg
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ClusterMemberMsg_Basic()
        {
            EnhancedStream es = new EnhancedMemoryStream();
            ClusterMemberMsg msgIn, msgOut;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new ClusterMemberMsg("logical://test", "my command");
            msgOut.ProtocolCaps = unchecked((ClusterMemberProtocolCaps)0xFFFFFFFF);
            msgOut.Flags = (ClusterMemberMsgFlag)0x7FFFFFFF;
            msgOut._Set("hello", "world!");
            msgOut._Data = new byte[] { 0, 1, 2, 3, 4, 5 };

            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (ClusterMemberMsg)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            Assert.AreEqual("logical://test", (string)msgIn.SenderEP);
            Assert.AreEqual(unchecked((ClusterMemberProtocolCaps)0xFFFFFFFF), msgIn.ProtocolCaps);
            Assert.AreEqual((ClusterMemberMsgFlag)0x7FFFFFFF, msgIn.Flags);
            Assert.AreEqual("my command", msgIn.Command);
            Assert.AreEqual("world!", msgIn["hello"]);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5 }, msgOut._Data);
        }
    }
}

