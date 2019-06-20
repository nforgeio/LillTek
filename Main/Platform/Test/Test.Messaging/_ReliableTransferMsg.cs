//-----------------------------------------------------------------------------
// FILE:        _ReliableTransferMsg.cs
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
    public class _ReliableTransferMsg
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void ReliableTransferMsg_Basic()
        {
            EnhancedStream es = new EnhancedMemoryStream();
            ReliableTransferMsg msgIn, msgOut;
            Guid id = Helper.NewGuid();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new ReliableTransferMsg(ReliableTransferMsg.ErrorCmd);
            msgOut.Direction = TransferDirection.Download;
            msgOut.TransferID = id;
            msgOut.Args = "Hello";
            msgOut.BlockData = new byte[] { 0, 1, 2, 3, 4 };
            msgOut.BlockIndex = 10;
            msgOut.BlockSize = 1024;
            msgOut.Exception = "Error";

            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (ReliableTransferMsg)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            Assert.AreEqual(TransferDirection.Download, msgIn.Direction);
            Assert.AreEqual(id, msgIn.TransferID);
            Assert.AreEqual("Hello", msgIn.Args);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, msgIn.BlockData);
            Assert.AreEqual(10, msgIn.BlockIndex);
            Assert.AreEqual(1024, msgIn.BlockSize);
            Assert.AreEqual("Error", msgIn.Exception);
        }
    }
}

