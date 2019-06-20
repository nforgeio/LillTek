//-----------------------------------------------------------------------------
// FILE:        _ServiceMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: ServiceMsg unit tests.

using System;
using System.Collections;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;
using LillTek.Service;

namespace LillTek.Service.Test
{
    [TestClass]
    public class _ServiceMsg
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Service")]
        public void ServiceMsg_Basic()
        {
            ServiceMsg msg;

            msg = new ServiceMsg();
            Assert.IsNull(msg["foo"]);

            msg["foo"] = "bar";
            Assert.AreEqual("bar", msg["foo"]);

            msg["FOO"] = "BAR";
            Assert.AreEqual("BAR", msg["FOO"]);

            msg["foo"] = "foobar";
            Assert.AreEqual("foobar", msg["foo"]);

            Assert.IsNull(msg.Command);

            msg.Command = "Hello";
            Assert.AreEqual("Hello", msg.Command);

            Assert.AreEqual(Guid.Empty, msg.RefID);

            Guid id = Helper.NewGuid();

            msg.RefID = id;
            Assert.AreEqual(id, msg.RefID);

            msg = new ServiceMsg("MyCommand");
            Assert.AreEqual("MyCommand", msg.Command);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Service")]
        public void ServiceMsg_Serialize()
        {
            byte[] buf;
            ServiceMsg msg1, msg2;

            msg1 = new ServiceMsg();
            msg1["foo"] = "bar";
            msg1["bar"] = "foobar";
            buf = msg1.ToBytes();

            msg2 = new ServiceMsg(buf);
            Assert.AreEqual("bar", msg2["foo"]);
            Assert.AreEqual("foobar", msg2["bar"]);
        }
    }
}

