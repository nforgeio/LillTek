//-----------------------------------------------------------------------------
// FILE:        _WcfEnvelopeMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Net;
using System.Threading;
using System.Reflection;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.ServiceModel;
using LillTek.ServiceModel.Channels;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.ServiceModel.Channels.Test
{
    [TestClass]
    public class _WcfEnvelopeMsg
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void WcfEnvelopeMsg_Basic()
        {
            EnhancedStream es = new EnhancedMemoryStream();
            WcfEnvelopeMsg msgIn, msgOut;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetAssembly(typeof(WcfEnvelopeMsg)));

            msgOut = new WcfEnvelopeMsg();
            msgOut.Payload = new ArraySegment<byte>(new byte[] { 0, 1, 2, 3, 4 });

            es.Position = 0;
            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (WcfEnvelopeMsg)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, msgIn.Payload.Array);
        }
    }
}

