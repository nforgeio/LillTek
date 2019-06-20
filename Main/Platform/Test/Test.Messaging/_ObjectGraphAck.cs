//-----------------------------------------------------------------------------
// FILE:        _ObjectGraphAck.cs
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
    public class _ObjectGraphAck
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ObjectGraphAck_Basic()
        {
            ObjectGraphAck msg;

            msg = new ObjectGraphAck();
            Assert.IsNull(msg.Graph);
            Assert.AreEqual(Compress.Best, msg.Compress);

            msg = new ObjectGraphAck(10);
            Assert.AreEqual(10, msg.Graph);
            Assert.AreEqual(Compress.Best, msg.Compress);

            msg = new ObjectGraphAck("hello world", Compress.Always);
            Assert.AreEqual("hello world", msg.Graph);
            Assert.AreEqual(Compress.Always, msg.Compress);

            msg = new ObjectGraphAck(new TimeoutException("timeout"));
            Assert.AreEqual("timeout", msg.Exception);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ObjectGraphAck_Clone()
        {
            ObjectGraphAck clone;

            clone = (ObjectGraphAck)new ObjectGraphAck("hello world", Compress.Always).Clone();
            Assert.AreEqual("hello world", clone.Graph);

            clone = (ObjectGraphAck)new ObjectGraphAck(new TimeoutException("timeout")).Clone();
            Assert.AreEqual("timeout", clone.Exception);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ObjectGraphAck_Serialize()
        {
            ObjectGraphAck msgOut, msgIn;
            EnhancedMemoryStream ms = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new ObjectGraphAck("hello world", Compress.Always);

            Msg.Save(ms, msgOut);
            ms.Seek(0, SeekOrigin.Begin);
            msgIn = (ObjectGraphAck)Msg.Load(ms);

            Assert.AreEqual("hello world", msgIn.Graph);

            //-----------------------------------

            ms.SetLength(0);

            msgOut = new ObjectGraphAck(new string[] { "a", "b", "c" }, Compress.Always);

            Msg.Save(ms, msgOut);
            ms.Seek(0, SeekOrigin.Begin);
            msgIn = (ObjectGraphAck)Msg.Load(ms);

            CollectionAssert.AreEqual(new string[] { "a", "b", "c" }, (string[])msgIn.Graph);

            //-----------------------------------

            ms.SetLength(0);

            msgOut = new ObjectGraphAck(new TimeoutException("timeout"));

            Msg.Save(ms, msgOut);
            ms.Seek(0, SeekOrigin.Begin);
            msgIn = (ObjectGraphAck)Msg.Load(ms);

            Assert.AreEqual("timeout", msgIn.Exception);

            //-----------------------------------

            ms.SetLength(0);

            msgOut = new ObjectGraphAck(null, Compress.Always);

            Msg.Save(ms, msgOut);
            ms.Seek(0, SeekOrigin.Begin);
            msgIn = (ObjectGraphAck)Msg.Load(ms);

            Assert.IsNull(msgIn.Graph);
        }
    }
}

