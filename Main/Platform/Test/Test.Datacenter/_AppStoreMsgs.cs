//-----------------------------------------------------------------------------
// FILE:        _AppStoreMsgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Net;
using System.Reflection;
using System.Threading;

using LillTek.Common;
using LillTek.Datacenter.Msgs.AppStore;
using LillTek.Messaging;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Test
{
    [TestClass]
    public class _AppStoreMsgs
    {
        [TestInitialize]
        public void Initialize()
        {
            Msg.LoadTypes(typeof(LillTek.Datacenter.Global).Assembly);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Msg.ClearTypes();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppStoreMsgs_Msg()
        {
            EnhancedStream es = new EnhancedMemoryStream();
            AppStoreMsg msgIn, msgOut;

            msgOut = new AppStoreMsg(AppStoreMsg.GetPrimaryCmd, AppRef.Parse("appref://myapps/theapp.zip?version=1.2.3.4"));

            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (AppStoreMsg)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            Assert.AreEqual(AppStoreMsg.GetPrimaryCmd, msgIn.Command);
            Assert.AreEqual(AppRef.Parse("appref://myapps/theapp.zip?version=1.2.3.4"), msgIn.AppRef);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppStoreMsgs_Query()
        {
            EnhancedStream es = new EnhancedMemoryStream();
            AppStoreQuery msgIn, msgOut;

            msgOut = new AppStoreQuery(AppStoreQuery.GetPrimaryCmd, AppRef.Parse("appref://myapps/theapp.zip?version=1.2.3.4"));

            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (AppStoreQuery)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            Assert.AreEqual(AppStoreQuery.GetPrimaryCmd, msgIn.Command);
            Assert.AreEqual(AppRef.Parse("appref://myapps/theapp.zip?version=1.2.3.4"), msgIn.AppRef);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AppStoreMsgs_Ack()
        {
            EnhancedStream es = new EnhancedMemoryStream();
            AppPackageInfo[] packages = new AppPackageInfo[] {
                
                new AppPackageInfo(AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4"),"app01.zip","",new byte[] {0,1,2},55,DateTime.MinValue),
                new AppPackageInfo(AppRef.Parse("appref://myapps/app02.zip?version=5.6.7.8"),"app02.zip","",new byte[] {3,4,5},66,DateTime.MinValue)
            };

            AppStoreAck msgIn, msgOut;

            msgOut = new AppStoreAck();
            msgOut.StoreEP = "logical://foo/bar";
            msgOut.Packages = packages;

            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (AppStoreAck)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            Assert.AreEqual("logical://foo/bar", msgIn.StoreEP);

            Assert.AreEqual(2, msgIn.Packages.Length);

            Assert.AreEqual(AppRef.Parse("appref://myapps/app01.zip?version=1.2.3.4"), msgIn.Packages[0].AppRef);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2 }, msgIn.Packages[0].MD5);
            Assert.AreEqual(55, msgIn.Packages[0].Size);

            Assert.AreEqual(AppRef.Parse("appref://myapps/app02.zip?version=5.6.7.8"), msgIn.Packages[1].AppRef);
            CollectionAssert.AreEqual(new byte[] { 3, 4, 5 }, msgIn.Packages[1].MD5);
            Assert.AreEqual(66, msgIn.Packages[1].Size);
        }
    }
}

