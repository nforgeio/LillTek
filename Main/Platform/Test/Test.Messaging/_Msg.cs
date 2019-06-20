//-----------------------------------------------------------------------------
// FILE:        _Msg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Unit tests for the Msg and derived classes.

using System;
using System.IO;
using System.Net;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;
using LillTek.Testing;

namespace LillTek.Messaging.Test.Messages
{
    public class _TestMsg1 : Msg
    {
        public _TestMsg1()
        {
        }
    }

    public class _TestMsg2 : Msg
    {
        public bool BoolValue;
        public byte ByteValue;
        public byte[] BytesValue;
        public int Int16Value;
        public int Int32Value;
        public long Int64Value;
        public float FloatValue;
        public string StringValue;

        public static string GetTypeID()
        {
            return "_TestMsg2";
        }

        public _TestMsg2()
        {
        }

        protected override void WritePayload(EnhancedStream es)
        {
            es.WriteBool(BoolValue);
            es.WriteByte(ByteValue);
            es.WriteBytes16(BytesValue);
            es.WriteInt16(Int16Value);
            es.WriteInt32(Int32Value);
            es.WriteInt64(Int64Value);
            es.WriteFloat(FloatValue);
            es.WriteString16(StringValue);
        }

        protected override void ReadPayload(EnhancedStream es, int cbPayload)
        {
            BoolValue = es.ReadBool();
            ByteValue = (byte)es.ReadByte();
            BytesValue = es.ReadBytes16();
            Int16Value = es.ReadInt16();
            Int32Value = es.ReadInt32();
            Int64Value = es.ReadInt64();
            FloatValue = es.ReadFloat();
            StringValue = es.ReadString16();
        }
    }

    public class BaseMsg : Msg
    {
        public string BaseValue;

        public BaseMsg()
        {
            this.BaseValue = "";
        }

        protected override void WriteBase(EnhancedStream es)
        {
            base.WriteBase(es);
            es.WriteString16(BaseValue);
        }

        protected override void ReadFrom(EnhancedStream es)
        {
            base.ReadFrom(es);
            BaseValue = es.ReadString16();
        }
    }

    public class DerivedMsg : BaseMsg
    {
        public string DerivedValue;

        public DerivedMsg()
        {
            this.DerivedValue = "";
        }

        protected override void WritePayload(EnhancedStream es)
        {
            es.WriteString16(DerivedValue);
        }

        protected override void ReadPayload(EnhancedStream es, int cbPayload)
        {
            DerivedValue = es.ReadString16();
        }
    }

    [MsgIgnore]
    public class EnvelopeTestMsg : Msg
    {
        public static string GetTypeID()
        {
            return ".EnvelopeTestMsg";
        }

        public string Value;

        public EnvelopeTestMsg()
        {
            this.Value = "";
        }

        protected override void WritePayload(EnhancedStream es)
        {
            es.WriteString16(Value);
        }

        protected override void ReadPayload(EnhancedStream es, int cbPayload)
        {
            Value = es.ReadString16();
        }
    }

    [TestClass]
    public class _Msg
    {
        private void TestBaseCloning(Msg msg1)
        {
            PropertyMsg msg2;

            msg1._Version = 66;
            msg1._ToEP = "logical://to";
            msg1._FromEP = "logical://from";
            msg1._TTL = 77;
            msg1._ReceiptEP = "logical://receipt";
            msg1._SessionID = Helper.NewGuid();
            msg1._SecurityToken = new byte[] { 0, 1, 2, 3, 4 };
            msg1._Flags |= MsgFlag.Broadcast;
            msg1._MsgID = Guid.Empty;

            msg2 = (PropertyMsg)msg1.Clone();
            Assert.AreEqual(msg1._Version, msg2._Version);
            Assert.AreEqual(msg1._ToEP, msg2._ToEP);
            Assert.AreEqual(msg1._FromEP, msg2._FromEP);
            Assert.AreEqual(msg1._TTL, msg2._TTL);
            Assert.AreEqual(msg1._ReceiptEP, msg2._ReceiptEP);
            Assert.AreEqual(msg1._SessionID, msg2._SessionID);
            Assert.AreEqual(msg1._SecurityToken, msg2._SecurityToken);
            Assert.AreEqual(msg1._Flags, msg2._Flags);
            Assert.AreEqual(msg1._MsgID, msg2._MsgID);

            msg1._MsgID = Helper.NewGuid();
            msg2 = (PropertyMsg)msg1.Clone();
            Assert.AreNotEqual(msg2._MsgID, msg1._MsgID);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Map_BaseMsg()
        {
            System.Type type;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            type = Msg.MapTypeID(typeof(Msg).FullName);
            Assert.AreEqual(typeof(Msg), type);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Map_NoType()
        {
            System.Type type;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            type = Msg.MapTypeID("___foo___");
            Assert.IsNull(type);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Map_NoGetTypeID()
        {
            System.Type type;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            type = Msg.MapTypeID("LillTek.Messaging.Test.Messages._TestMsg1");
            Assert.AreEqual(typeof(_TestMsg1), type);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Map_Normal()
        {
            System.Type type;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            type = Msg.MapTypeID(_TestMsg2.GetTypeID());
            Assert.AreEqual(typeof(_TestMsg2), type);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_BaseMsg()
        {
            Msg msg;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msg = new Msg();
            msg._Version = 77;
            msg._Flags |= MsgFlag.Broadcast;
            es.SetLength(0);
            Msg.Save(es, msg);

            es.Seek(0, SeekOrigin.Begin);
            msg = (Msg)Msg.Load(es);
            Assert.IsNotNull(msg);
            Assert.AreEqual(77, msg._Version);
            Assert.AreEqual(MsgFlag.Broadcast, msg._Flags);

            msg = new Msg();
            msg._Version = 77;
            msg._Flags |= MsgFlag.Broadcast | MsgFlag.OpenSession;
            es.SetLength(0);
            Msg.Save(es, msg);

            es.Seek(0, SeekOrigin.Begin);
            msg = (Msg)Msg.Load(es);
            Assert.IsNotNull(msg);
            Assert.AreEqual(77, msg._Version);
            Assert.AreEqual(MsgFlag.Broadcast | MsgFlag.OpenSession, msg._Flags);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_TcpInitMsg()
        {
            TcpInitMsg msg;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msg = new TcpInitMsg("physical://root/hub/leaf", new MsgRouterInfo(new Version(1000, 0)), true, 77);
            Msg.Save(es, msg);

            es.Seek(0, SeekOrigin.Begin);
            msg = (TcpInitMsg)Msg.Load(es);
            Assert.AreEqual(new MsgEP("physical://root/hub/leaf"), msg.RouterEP);
            Assert.AreEqual(new Version(1000, 0), msg.RouterInfo.ProtocolVersion);
            Assert.IsTrue(msg.IsUplink);
            Assert.IsTrue(msg.RouterInfo.IsP2P);
            Assert.AreEqual(77, msg.ListenPort);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Clone_TcpInitMsg()
        {
            TcpInitMsg msg;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msg = new TcpInitMsg("physical://root/hub/leaf", new MsgRouterInfo(new Version(1000, 0)), true, 77);
            msg = (TcpInitMsg)msg.Clone();
            Assert.AreEqual(new MsgEP("physical://root/hub/leaf"), msg.RouterEP);
            Assert.AreEqual(new Version(1000, 0), msg.RouterInfo.ProtocolVersion);
            Assert.IsTrue(msg.IsUplink);
            Assert.IsTrue(msg.RouterInfo.IsP2P);
            Assert.AreEqual(77, msg.ListenPort);

            TestBaseCloning(msg);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_TestMsg()
        {
            _TestMsg2 msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new _TestMsg2();
            msgOut.BoolValue = false;
            msgOut.ByteValue = 0xAA;
            msgOut.BytesValue = new byte[] { 0, 1, 2, 3 };
            msgOut.FloatValue = 10.77F;
            msgOut.Int16Value = 32000;
            msgOut.Int32Value = 100000;
            msgOut.Int64Value = 8000000000;
            msgOut.StringValue = "Hello World!";
            Msg.Save(es, msgOut);

            es.Seek(0, SeekOrigin.Begin);
            msgIn = (_TestMsg2)Msg.Load(es);
            Assert.AreEqual(msgOut.BoolValue, msgIn.BoolValue);
            Assert.AreEqual(msgOut.ByteValue, msgIn.ByteValue);
            CollectionAssert.AreEqual(msgOut.BytesValue, msgIn.BytesValue);
            Assert.AreEqual(msgOut.FloatValue, msgIn.FloatValue);
            Assert.AreEqual(msgOut.Int16Value, msgIn.Int16Value);
            Assert.AreEqual(msgOut.Int32Value, msgIn.Int32Value);
            Assert.AreEqual(msgOut.Int64Value, msgIn.Int64Value);
            Assert.AreEqual(msgOut.StringValue, msgIn.StringValue);

            Assert.AreEqual(0, msgIn._Version);
            Assert.IsNull(msgIn._ToEP);
            Assert.IsNull(msgIn._FromEP);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_DerivedMsg()
        {
            DerivedMsg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new DerivedMsg();
            msgOut.BaseValue = "Foo";
            msgOut.DerivedValue = "Bar";
            msgOut._ToEP = new MsgEP("physical://foo");
            msgOut._FromEP = new MsgEP("physical://bar");
            Msg.Save(es, msgOut);

            es.Seek(0, SeekOrigin.Begin);
            msgIn = (DerivedMsg)Msg.Load(es);
            Assert.AreEqual(msgOut.BaseValue, msgIn.BaseValue);
            Assert.AreEqual(msgOut.DerivedValue, msgIn.DerivedValue);
            Assert.AreEqual(msgOut._ToEP, msgIn._ToEP);
            Assert.AreEqual(msgOut._FromEP, msgIn._FromEP);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_ExtensionHeaders()
        {
            Msg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();
            MsgHeader header;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new Msg();
            msgOut._ExtensionHeaders.Set(new MsgHeader(MsgHeaderID.Comment, new byte[] { 0, 1, 2, 3, 4, 5 }));

            Msg.Save(es, msgOut);

            es.Seek(0, SeekOrigin.Begin);
            msgIn = Msg.Load(es);

            Assert.AreEqual(1, msgIn._ExtensionHeaders.Count);
            header = msgIn._ExtensionHeaders[MsgHeaderID.Comment];
            Assert.IsNotNull(header);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5 }, header.Contents);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_TTL()
        {
            Msg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new Msg();
            msgOut._TTL = 50;
            Msg.Save(es, msgOut);

            es.Seek(0, SeekOrigin.Begin);
            msgIn = Msg.Load(es);
            Assert.AreEqual(50, msgIn._TTL);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_ReceiptEP()
        {
            Msg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new Msg();
            msgOut._Flags |= MsgFlag.ReceiptRequest;
            msgOut._ReceiptEP = "physical://Foo";
            Msg.Save(es, msgOut);

            es.Seek(0, SeekOrigin.Begin);
            msgIn = Msg.Load(es);
            Assert.AreEqual(MsgFlag.ReceiptRequest, msgIn._Flags);
            Assert.AreEqual(msgOut._ReceiptEP, msgIn._ReceiptEP);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_Guids()
        {
            Msg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new Msg();
            Assert.AreEqual(Guid.Empty, msgOut._MsgID);
            Assert.AreEqual(Guid.Empty, msgOut._SessionID);

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = Msg.Load(es);

            Assert.AreEqual(Guid.Empty, msgIn._MsgID);
            Assert.AreEqual(Guid.Empty, msgIn._SessionID);

            msgOut._MsgID = Helper.NewGuid();
            msgOut._SessionID = Helper.NewGuid();

            es.SetLength(0);
            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = Msg.Load(es);

            Assert.AreEqual(msgOut._MsgID, msgIn._MsgID);
            Assert.AreEqual(msgOut._SessionID, msgIn._SessionID);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_SecurityToken()
        {
            Msg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new Msg();
            Assert.IsNull(msgOut._SecurityToken);

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = Msg.Load(es);

            Assert.IsNull(msgOut._SecurityToken);

            msgOut._SecurityToken = new byte[] { 0, 1, 2, 3, 4 };

            es.SetLength(0);
            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = Msg.Load(es);

            CollectionAssert.AreEqual(msgOut._SecurityToken, msgIn._SecurityToken);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_HubAdvertiseMsg()
        {
            HubAdvertiseMsg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new HubAdvertiseMsg(MsgEP.Parse("physical://root.com:80/hub/leaf"), "appname", "appdescription", MsgRouterInfo.Default, Helper.NewGuid());
            msgOut._FromEP = MsgEP.Parse("physical://root.com:80/hub/leaf?c=tcp://1.2.3.4:57");

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (HubAdvertiseMsg)Msg.Load(es);

            Assert.AreEqual("physical://root.com:80/hub/leaf", msgIn.HubEP.ToString());
            Assert.AreEqual("appname", msgIn.AppName);
            Assert.AreEqual("appdescription", msgIn.AppDescription);
            Assert.AreEqual(IPAddress.Parse("1.2.3.4"), msgIn.IPAddress);
            Assert.AreEqual(57, msgIn.TcpPort);
            Assert.AreEqual(msgOut.LogicalEndpointSetID, msgIn.LogicalEndpointSetID);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Clone_HubAdvertiseMsg()
        {
            HubAdvertiseMsg msgIn, msgOut;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new HubAdvertiseMsg(MsgEP.Parse("physical://root.com:80/hub/leaf"), "appname", "appdescription", new MsgRouterInfo(new Version(1000, 0)), Helper.NewGuid());
            msgOut._FromEP = MsgEP.Parse("physical://root.com:80/hub/leaf?c=tcp://1.2.3.4:57");
            Assert.AreEqual(Guid.Empty, msgOut._MsgID);

            msgIn = (HubAdvertiseMsg)msgOut.Clone();

            Assert.AreEqual("physical://root.com:80/hub/leaf", msgIn.HubEP.ToString());
            Assert.AreEqual("appname", msgIn.AppName);
            Assert.AreEqual("appdescription", msgIn.AppDescription);
            Assert.AreEqual(new Version(1000, 0), msgIn.RouterInfo.ProtocolVersion);
            Assert.AreEqual(IPAddress.Parse("1.2.3.4"), msgIn.IPAddress);
            Assert.AreEqual(57, msgIn.TcpPort);
            Assert.AreEqual(msgOut.LogicalEndpointSetID, msgIn.LogicalEndpointSetID);
            Assert.AreEqual(Guid.Empty, msgIn._MsgID);

            msgOut._MsgID = Helper.NewGuid();
            msgIn = (HubAdvertiseMsg)msgOut.Clone();

            Assert.AreNotEqual(Guid.Empty, msgIn._MsgID);

            TestBaseCloning(msgOut);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_HubSettingsMsg()
        {
            HubSettingsMsg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new HubSettingsMsg(Helper.NewGuid(), TimeSpan.FromSeconds(100));

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (HubSettingsMsg)Msg.Load(es);

            Assert.AreEqual(TimeSpan.FromSeconds(100), msgIn.KeepAliveTime);
            Assert.AreEqual(msgOut.LogicalEndpointSetID, msgIn.LogicalEndpointSetID);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Clone_HubSettingsMsg()
        {
            HubSettingsMsg msgIn, msgOut;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new HubSettingsMsg(Helper.NewGuid(), TimeSpan.FromSeconds(100));
            msgIn = (HubSettingsMsg)msgOut.Clone();

            Assert.AreEqual(TimeSpan.FromSeconds(100), msgIn.KeepAliveTime);
            Assert.AreEqual(msgOut.LogicalEndpointSetID, msgIn.LogicalEndpointSetID);

            TestBaseCloning(msgOut);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_RouteAdvertiseMsg()
        {
            RouterAdvertiseMsg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new RouterAdvertiseMsg(MsgEP.Parse("physical://root.com:80/hub/leaf"), "appname", "appdescription",
                                            new MsgRouterInfo(new Version(1000, 0)), 10, 20, Helper.NewGuid(), false, true);
            msgOut._FromEP = MsgEP.Parse("physical://root.com:80/hub/leaf?c=mcast://1.2.3.4:57");

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (RouterAdvertiseMsg)Msg.Load(es);

            Assert.AreEqual("physical://root.com:80/hub/leaf", msgIn.RouterEP.ToString());
            Assert.AreEqual("appname", msgIn.AppName);
            Assert.AreEqual("appdescription", msgIn.AppDescription);
            Assert.AreEqual(new Version(1000, 0), msgIn.RouterInfo.ProtocolVersion);
            Assert.AreEqual(IPAddress.Parse("1.2.3.4"), msgIn.IPAddress);
            Assert.AreEqual(10, msgIn.UdpPort);
            Assert.AreEqual(20, msgIn.TcpPort);
            Assert.AreEqual(msgOut.LogicalEndpointSetID, msgIn.LogicalEndpointSetID);
            Assert.IsFalse(msgIn.ReplyAdvertise);
            Assert.IsTrue(msgIn.DiscoverLogical);
            Assert.IsTrue(msgIn.RouterInfo.IsP2P);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Clone_RouteAdvertiseMsg()
        {
            RouterAdvertiseMsg msgIn, msgOut;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new RouterAdvertiseMsg(MsgEP.Parse("physical://root.com:80/hub/leaf"), "appname", "appdescription", new MsgRouterInfo(new Version(1000, 0)), 10, 20, Helper.NewGuid(), false, true);
            msgOut._FromEP = MsgEP.Parse("physical://root.com:80/hub/leaf?c=mcast://1.2.3.4:57");
            msgIn = (RouterAdvertiseMsg)msgOut.Clone();

            Assert.AreEqual("physical://root.com:80/hub/leaf", msgIn.RouterEP.ToString());
            Assert.AreEqual("appname", msgIn.AppName);
            Assert.AreEqual("appdescription", msgIn.AppDescription);
            Assert.AreEqual(new Version(1000, 0), msgIn.RouterInfo.ProtocolVersion);
            Assert.AreEqual(IPAddress.Parse("1.2.3.4"), msgIn.IPAddress);
            Assert.AreEqual(10, msgIn.UdpPort);
            Assert.AreEqual(20, msgIn.TcpPort);
            Assert.AreEqual(msgOut.LogicalEndpointSetID, msgIn.LogicalEndpointSetID);
            Assert.IsFalse(msgIn.ReplyAdvertise);
            Assert.IsTrue(msgIn.DiscoverLogical);
            Assert.IsTrue(msgIn.RouterInfo.IsP2P);

            TestBaseCloning(msgOut);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_LogicalAdvertiseMsg()
        {
            LogicalAdvertiseMsg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new LogicalAdvertiseMsg(new MsgEP[] { "logical://ep1", "logical://ep2" }, MsgEP.Parse("physical://root.com:80/hub/leaf"),
                                             "appname", "appdescription", new MsgRouterInfo(new Version(1000, 0)), 10, 20, Helper.NewGuid());
            msgOut._FromEP = MsgEP.Parse("physical://root.com:80/hub/leaf?c=mcast://1.2.3.4:57");

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (LogicalAdvertiseMsg)Msg.Load(es);

            CollectionAssert.AreEqual(msgOut.LogicalEPs, msgIn.LogicalEPs);
            Assert.AreEqual("physical://root.com:80/hub/leaf", msgIn.RouterEP.ToString());
            Assert.AreEqual("appname", msgIn.AppName);
            Assert.AreEqual("appdescription", msgIn.AppDescription);
            Assert.AreEqual(new Version(1000, 0), msgIn.RouterInfo.ProtocolVersion);
            Assert.AreEqual(10, msgIn.UdpPort);
            Assert.AreEqual(20, msgIn.TcpPort);
            Assert.AreEqual(msgOut.LogicalEndpointSetID, msgIn.LogicalEndpointSetID);
            Assert.IsTrue(msgIn.RouterInfo.IsP2P);

            msgOut = (LogicalAdvertiseMsg)msgIn.Clone();

            CollectionAssert.AreEqual(msgOut.LogicalEPs, msgIn.LogicalEPs);
            Assert.AreEqual("physical://root.com:80/hub/leaf", msgIn.RouterEP.ToString());
            Assert.AreEqual("appname", msgIn.AppName);
            Assert.AreEqual("appdescription", msgIn.AppDescription);
            Assert.AreEqual(new Version(1000, 0), msgIn.RouterInfo.ProtocolVersion);
            Assert.AreEqual(10, msgIn.UdpPort);
            Assert.AreEqual(20, msgIn.TcpPort);
            Assert.AreEqual(msgOut.LogicalEndpointSetID, msgIn.LogicalEndpointSetID);
            Assert.IsTrue(msgIn.RouterInfo.IsP2P);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Clone_LogicalAdvertiseMsg()
        {
            LogicalAdvertiseMsg msgIn, msgOut;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new LogicalAdvertiseMsg(new MsgEP[] { "logical://ep1", "logical://ep2" }, MsgEP.Parse("physical://root.com:80/hub/leaf"),
                                             "appname", "appdescription", new MsgRouterInfo(new Version(1000, 0)), 10, 20, Helper.NewGuid());
            msgOut._FromEP = MsgEP.Parse("physical://root.com:80/hub/leaf?c=mcast://1.2.3.4:57");
            msgIn = (LogicalAdvertiseMsg)msgOut.Clone();

            CollectionAssert.AreEqual(msgOut.LogicalEPs, msgIn.LogicalEPs);
            Assert.AreEqual("physical://root.com:80/hub/leaf", msgIn.RouterEP.ToString());
            Assert.AreEqual("appname", msgIn.AppName);
            Assert.AreEqual("appdescription", msgIn.AppDescription);
            Assert.AreEqual(new Version(1000, 0), msgIn.RouterInfo.ProtocolVersion);
            Assert.AreEqual(10, msgIn.UdpPort);
            Assert.AreEqual(20, msgIn.TcpPort);
            Assert.AreEqual(msgOut.LogicalEndpointSetID, msgIn.LogicalEndpointSetID);
            Assert.IsTrue(msgIn.RouterInfo.IsP2P);

            msgOut = (LogicalAdvertiseMsg)msgIn.Clone();

            CollectionAssert.AreEqual(msgOut.LogicalEPs, msgIn.LogicalEPs);
            Assert.AreEqual("physical://root.com:80/hub/leaf", msgIn.RouterEP.ToString());
            Assert.AreEqual("appname", msgIn.AppName);
            Assert.AreEqual("appdescription", msgIn.AppDescription);
            Assert.AreEqual(new Version(1000, 0), msgIn.RouterInfo.ProtocolVersion);
            Assert.AreEqual(10, msgIn.UdpPort);
            Assert.AreEqual(20, msgIn.TcpPort);
            Assert.AreEqual(msgOut.LogicalEndpointSetID, msgIn.LogicalEndpointSetID);
            Assert.IsTrue(msgIn.RouterInfo.IsP2P);

            TestBaseCloning(msgOut);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_LeafSettingsMsg()
        {
            LeafSettingsMsg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new LeafSettingsMsg(MsgEP.Parse("physical://root.com:80/hub/leaf"), 10, 20, new TimeSpan(0, 0, 0, 55), true);
            msgOut._FromEP = MsgEP.Parse("physical://root.com:80/hub/leaf?c=mcast://1.2.3.4:57");

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (LeafSettingsMsg)Msg.Load(es);

            Assert.AreEqual("physical://root.com:80/hub/leaf", msgIn.HubEP.ToString());
            Assert.AreEqual(IPAddress.Parse("1.2.3.4"), msgIn.HubIPAddress);
            Assert.AreEqual(10, msgIn.HubUdpPort);
            Assert.AreEqual(20, msgIn.HubTcpPort);
            Assert.AreEqual(new TimeSpan(0, 0, 0, 55), msgIn.AdvertiseTime);
            Assert.IsTrue(msgIn.DiscoverLogical);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Clone_LeafSettingsMsg()
        {
            LeafSettingsMsg msgIn, msgOut;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new LeafSettingsMsg(MsgEP.Parse("physical://root.com:80/hub/leaf"), 10, 20, new TimeSpan(0, 0, 0, 55), true);
            msgOut._FromEP = MsgEP.Parse("physical://root.com:80/hub/leaf?c=mcast://1.2.3.4:57");
            msgIn = (LeafSettingsMsg)msgOut.Clone();

            Assert.AreEqual("physical://root.com:80/hub/leaf", msgIn.HubEP.ToString());
            Assert.AreEqual(IPAddress.Parse("1.2.3.4"), msgIn.HubIPAddress);
            Assert.AreEqual(10, msgIn.HubUdpPort);
            Assert.AreEqual(20, msgIn.HubTcpPort);
            Assert.AreEqual(new TimeSpan(0, 0, 0, 55), msgIn.AdvertiseTime);
            Assert.IsTrue(msgIn.DiscoverLogical);

            TestBaseCloning(msgOut);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_RouterStopMsg()
        {
            RouterStopMsg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new RouterStopMsg(MsgEP.Parse("physical://root.com:80/hub/leaf"));

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (RouterStopMsg)Msg.Load(es);

            Assert.AreEqual("physical://root.com:80/hub/leaf", msgIn.RouterEP.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Clone_RouterStopMsg()
        {
            RouterStopMsg msgIn, msgOut;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new RouterStopMsg(MsgEP.Parse("physical://root.com:80/hub/leaf"));
            msgIn = (RouterStopMsg)msgOut.Clone();

            Assert.AreEqual("physical://root.com:80/hub/leaf", msgIn.RouterEP.ToString());

            TestBaseCloning(msgOut);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_HubKeepAliveMsg()
        {
            HubKeepAliveMsg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new HubKeepAliveMsg("physical://root", "appname", "appdescription", new MsgRouterInfo(new Version(1000, 0)), Helper.NewGuid());

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (HubKeepAliveMsg)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            Assert.AreEqual(msgOut.ChildEP, msgIn.ChildEP);
            Assert.AreEqual("appname", msgIn.AppName);
            Assert.AreEqual("appdescription", msgIn.AppDescription);
            Assert.AreEqual(msgOut.RouterInfo.ProtocolVersion, msgIn.RouterInfo.ProtocolVersion);
            Assert.AreEqual(msgOut.LogicalEndpointSetID, msgIn.LogicalEndpointSetID);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Clone_KeepAliveMsg()
        {
            HubKeepAliveMsg msgIn, msgOut;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new HubKeepAliveMsg("physical://root", "appname", "appdescription", new MsgRouterInfo(new Version(1000, 0)), Helper.NewGuid());
            msgIn = (HubKeepAliveMsg)msgOut.Clone();

            Assert.IsNotNull(msgIn);
            Assert.AreEqual(msgOut.ChildEP, msgIn.ChildEP);
            Assert.AreEqual("appname", msgIn.AppName);
            Assert.AreEqual("appdescription", msgIn.AppDescription);
            Assert.AreEqual(msgOut.RouterInfo.ProtocolVersion, msgIn.RouterInfo.ProtocolVersion);
            Assert.AreEqual(msgOut.LogicalEndpointSetID, msgIn.LogicalEndpointSetID);

            TestBaseCloning(msgOut);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_ReceiptMsg()
        {
            ReceiptMsg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new ReceiptMsg(Helper.NewGuid());

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (ReceiptMsg)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            Assert.AreEqual(msgOut.ReceiptID, msgIn.ReceiptID);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Clone_ReceiptMsg()
        {
            ReceiptMsg msgIn, msgOut;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new ReceiptMsg(Helper.NewGuid());
            msgIn = (ReceiptMsg)msgOut.Clone();

            Assert.IsNotNull(msgIn);
            Assert.AreEqual(msgOut.ReceiptID, msgIn.ReceiptID);

            TestBaseCloning(msgOut);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_DeadRouterMsg()
        {
            DeadRouterMsg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new DeadRouterMsg("physical://root/hub/leaf", Helper.NewGuid());

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (DeadRouterMsg)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            Assert.AreEqual(msgOut.RouterEP, msgIn.RouterEP);
            Assert.AreEqual(msgOut.LogicalEndpointSetID, msgIn.LogicalEndpointSetID);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Clone_DeadRouterMsg()
        {
            DeadRouterMsg msgIn, msgOut;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new DeadRouterMsg("physical://root/hub/leaf", Helper.NewGuid());
            msgIn = (DeadRouterMsg)msgOut.Clone();

            Assert.AreEqual(msgOut.RouterEP, msgIn.RouterEP);
            Assert.AreEqual(msgOut.LogicalEndpointSetID, msgIn.LogicalEndpointSetID);

            TestBaseCloning(msgOut);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_SessionKeepAliveMsg()
        {
            SessionKeepAliveMsg msgIn, msgOut;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new SessionKeepAliveMsg(TimeSpan.FromMinutes(77));

            Msg.Save(es, msgOut);
            es.Seek(0, SeekOrigin.Begin);
            msgIn = (SessionKeepAliveMsg)Msg.Load(es);

            Assert.IsNotNull(msgIn);
            Assert.AreEqual(msgOut.SessionTTL, msgIn.SessionTTL);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Clone_SessionKeepAliveMsg()
        {
            SessionKeepAliveMsg msgIn, msgOut;

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            msgOut = new SessionKeepAliveMsg(TimeSpan.FromMinutes(77));
            msgIn = (SessionKeepAliveMsg)msgOut.Clone();

            Assert.IsNotNull(msgIn);
            Assert.AreEqual(msgOut.SessionTTL, msgIn.SessionTTL);

            TestBaseCloning(msgOut);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_Serialize_EnvelopeMsg()
        {
            EnvelopeTestMsg evMsgOut, evMsgIn;
            EnvelopeMsg envelopeMsg;
            EnhancedStream es = new EnhancedMemoryStream();

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            // EnvelopeTestMsg is tagged with [MsgIgnore] so it won't loaded
            // into the map by Msg.LoadTypes().  This means that Msg.Load()
            // should return a EnvelopeMsg instance.

            evMsgOut = new EnvelopeTestMsg();
            evMsgOut.Value = "The hills are alive, with the sound of music.  ahhhhhhhhh";

            Msg.LoadType(typeof(EnvelopeTestMsg));
            Msg.Save(es, evMsgOut);

            Msg.ClearTypes();
            Msg.LoadTypes(Assembly.GetExecutingAssembly());

            es.Seek(0, SeekOrigin.Begin);
            envelopeMsg = (EnvelopeMsg)Msg.Load(es);

            Assert.IsNotNull(envelopeMsg);
            Assert.AreEqual(EnvelopeTestMsg.GetTypeID(), envelopeMsg.TypeID);

            // Serialize the EnvelopeMsg instance and then call Msg.LoadType() to
            // force the mapping of EnvelopeTestMsg and then make sure that
            // Msg.Load() was able to deserialize if properly.

            es.SetLength(0);
            Msg.Save(es, envelopeMsg);
            Msg.LoadType(typeof(EnvelopeTestMsg));
            es.Seek(0, SeekOrigin.Begin);
            evMsgIn = (EnvelopeTestMsg)Msg.Load(es);

            Assert.AreEqual(evMsgOut.Value, evMsgIn.Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void Msg_SetChannelEPs()
        {
            Msg msg = new Msg();

            msg._ToEP = "physical://foo/bar";
            Assert.AreEqual("physical://foo/bar", msg._ToEP.ToString(-1, true));
            Assert.IsNull(msg._ToEP.ChannelEP);

            msg._SetToChannel("tcp://1.2.3.4:5");
            Assert.AreEqual("physical://foo/bar?c=tcp://1.2.3.4:5", msg._ToEP.ToString(-1, true));
            Assert.IsNotNull(msg._ToEP.ChannelEP);

            msg._SetToChannel(null);
            Assert.AreEqual("physical://foo/bar", msg._ToEP.ToString(-1, true));
            Assert.IsNull(msg._ToEP.ChannelEP);

            msg._FromEP = "physical://foo/bar";
            Assert.AreEqual("physical://foo/bar", msg._FromEP.ToString(-1, true));
            Assert.IsNull(msg._FromEP.ChannelEP);

            msg._SetFromChannel("tcp://1.2.3.4:5");
            Assert.AreEqual("physical://foo/bar?c=tcp://1.2.3.4:5", msg._FromEP.ToString(-1, true));
            Assert.IsNotNull(msg._FromEP.ChannelEP);

            msg._SetFromChannel(null);
            Assert.AreEqual("physical://foo/bar", msg._FromEP.ToString(-1, true));
            Assert.IsNull(msg._FromEP.ChannelEP);
        }
    }
}

