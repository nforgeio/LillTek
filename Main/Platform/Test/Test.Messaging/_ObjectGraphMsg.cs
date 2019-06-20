//-----------------------------------------------------------------------------
// FILE:        _ObjectGraphMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests.

using System;
using System.IO;
using System.Net;
using System.Reflection;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Messaging.Test.Messages
{
    [TestClass]
    public class _ObjectGraphMsg
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ObjectGraphMsg_Basic()
        {
            ObjectGraphMsg msg;

            msg = new ObjectGraphMsg();
            Assert.IsNull(msg.Graph);
            Assert.AreEqual(Compress.Best, msg.Compress);

            msg = new ObjectGraphMsg(10);
            Assert.AreEqual(10, msg.Graph);
            Assert.AreEqual(Compress.Best, msg.Compress);

            msg = new ObjectGraphMsg("hello world", Compress.Always);
            Assert.AreEqual("hello world", msg.Graph);
            Assert.AreEqual(Compress.Always, msg.Compress);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ObjectGraphMsg_Clone()
        {
            ObjectGraphMsg clone;

            clone = (ObjectGraphMsg)new ObjectGraphMsg("hello world", Compress.Always).Clone();
            Assert.AreEqual("hello world", clone.Graph);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ObjectGraphMsg_Serialize()
        {
            ObjectGraphMsg msgOut, msgIn;
            EnhancedMemoryStream ms = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new ObjectGraphMsg("hello world", Compress.Always);

            Msg.Save(ms, msgOut);
            ms.Seek(0, SeekOrigin.Begin);
            msgIn = (ObjectGraphMsg)Msg.Load(ms);

            Assert.AreEqual("hello world", msgIn.Graph);

            //-----------------------------------

            ms.SetLength(0);

            msgOut = new ObjectGraphMsg(new string[] { "a", "b", "c" }, Compress.Always);

            Msg.Save(ms, msgOut);
            ms.Seek(0, SeekOrigin.Begin);
            msgIn = (ObjectGraphMsg)Msg.Load(ms);

            CollectionAssert.AreEqual(new string[] { "a", "b", "c" }, (string[])msgIn.Graph);

            //-----------------------------------

            ms.SetLength(0);

            msgOut = new ObjectGraphMsg(null, Compress.Always);

            Msg.Save(ms, msgOut);
            ms.Seek(0, SeekOrigin.Begin);
            msgIn = (ObjectGraphMsg)Msg.Load(ms);

            Assert.IsNull(msgIn.Graph);
        }
    }
}

