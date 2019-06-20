//-----------------------------------------------------------------------------
// FILE:        _MsgDispatcherLogical.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Unit tests for the logical dispatching by the MsgDispatcher class.

using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Testing;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _MsgDispatcherLogical
    {
        private int DispatchWait = 250;     // # of milliseconds to wait for messages
        // dispatches to be handled on worker threads

        /// <summary>
        /// Handles the dispatching of a message via a dispatcher and then
        /// waits a bit of time to give the background threads to actually
        /// process the message.
        /// </summary>
        /// <param name="dispatcher"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        private bool Dispatch(MsgDispatcher dispatcher, Msg msg)
        {
            bool result;

            result = dispatcher.Dispatch(msg);
            Thread.Sleep(DispatchWait);
            return result;
        }

        [TestInitialize]
        public void Initialize()
        {
            NetTrace.Start();
            NetTrace.Enable(MsgRouter.TraceSubsystem, 0);
        }

        [TestCleanup]
        public void Cleanup()
        {
            NetTrace.Stop();
        }

        public class _DispatchMsg1 : Msg
        {
            public _DispatchMsg1()
            {
            }

            public _DispatchMsg1(MsgEP toEP)
            {
                base._ToEP = toEP;
                base._TTL = 5;
            }
        }

        public class _DispatchMsg2 : Msg
        {
            public _DispatchMsg2()
            {
            }

            public _DispatchMsg2(MsgEP toEP)
            {
                base._ToEP = toEP;
                base._TTL = 5;
            }
        }

        public class _DispatchMsg3 : Msg
        {
            public _DispatchMsg3()
            {
            }

            public _DispatchMsg3(MsgEP toEP)
            {
                base._ToEP = toEP;
                base._TTL = 5;
            }
        }

        public class _DispatchMsg4 : _DispatchMsg3
        {
            public _DispatchMsg4()
            {
            }

            public _DispatchMsg4(MsgEP toEP)
                : base(toEP)
            {
            }
        }

        private class Target1
        {
            public bool dispatch1 = false;
            public bool dispatch2 = false;

            public void OnMsg(_DispatchMsg1 msg)
            {
                dispatch1 = true;
            }

            public void OnMsg(_DispatchMsg2 msg)
            {
                dispatch2 = true;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherLogical_NoHandlers()
        {
            Target1 target = new Target1();
            MsgDispatcher dispatcher = new MsgDispatcher();

            dispatcher.AddTarget(target);
            dispatcher.Dispatch(new _DispatchMsg1("logical://Foo"));
            dispatcher.Dispatch(new _DispatchMsg2("logical://Foo"));
            Thread.Sleep(DispatchWait);

            Assert.IsFalse(target.dispatch1);
            Assert.IsFalse(target.dispatch2);
        }

        private class Target2
        {
            public bool dispatch1 = false;
            public bool dispatch2 = false;
            public bool dispatch3 = false;

            [MsgHandler(LogicalEP = "logical://Dispatch1")]
            public void OnMsg(_DispatchMsg1 msg)
            {
                dispatch1 = true;
            }

            [MsgHandler(LogicalEP = "logical://Dispatch2")]
            public void OnMsg(_DispatchMsg2 msg)
            {
                dispatch2 = true;
            }

            [MsgHandler(LogicalEP = "logical://Dispatch3")]
            public void OnMsg(_DispatchMsg3 msg)
            {
                dispatch3 = true;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherLogical_ValidHandlers()
        {
            Target2 target = new Target2();
            MsgDispatcher dispatcher = new MsgDispatcher();

            dispatcher.AddTarget(target);
            dispatcher.Dispatch(new _DispatchMsg1("logical://Dispatch1"));
            dispatcher.Dispatch(new _DispatchMsg2("logical://Dispatch2"));
            dispatcher.Dispatch(new _DispatchMsg3("logical://Dispatch3"));
            Thread.Sleep(DispatchWait);

            Assert.IsTrue(target.dispatch1);
            Assert.IsTrue(target.dispatch2);
            Assert.IsTrue(target.dispatch3);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherLogical_LogicalEndpointSetID()
        {
            Target2 target = new Target2();
            MsgDispatcher dispatcher = new MsgDispatcher();
            Guid orgID;

            orgID = dispatcher.LogicalEndpointSetID;
            dispatcher.AddTarget(target);
            Assert.AreNotEqual(orgID, dispatcher.LogicalEndpointSetID);
        }

        private class Target3
        {
            public bool dispatch1 = false;
            public bool dispatch2 = false;
            public bool defHandler = false;

            [MsgHandler(LogicalEP = "logical://Dispatch1")]
            public void OnMsg(_DispatchMsg1 msg)
            {
                dispatch1 = true;
            }

            [MsgHandler(LogicalEP = "logical://Dispatch2")]
            public void OnMsg(_DispatchMsg2 msg)
            {
                dispatch2 = true;
            }

            [MsgHandler(LogicalEP = "logical://Dispatch3", Default = true)]
            public void OnMsg(Msg msg)
            {
                defHandler = msg is _DispatchMsg3;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherLogical_DefaultHandler()
        {
            Target3 target = new Target3();
            MsgDispatcher dispatcher = new MsgDispatcher();

            dispatcher.AddTarget(target);
            dispatcher.Dispatch(new _DispatchMsg1("logical://Dispatch1"));
            dispatcher.Dispatch(new _DispatchMsg2("logical://Dispatch2"));
            Thread.Sleep(DispatchWait);

            Assert.IsTrue(target.dispatch1);
            Assert.IsTrue(target.dispatch2);
            Assert.IsFalse(target.defHandler);

            dispatcher.Dispatch(new _DispatchMsg3("logical://Dispatch3"));
            Thread.Sleep(DispatchWait);
            Assert.IsTrue(target.defHandler);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherLogical_Multi_Handlers_Send()
        {
            Target3 target1 = new Target3();
            Target3 target2 = new Target3();
            MsgDispatcher dispatcher = new MsgDispatcher();

            dispatcher.AddTarget(target1);
            dispatcher.AddTarget(target2);

            dispatcher.Dispatch(new _DispatchMsg1("logical://Dispatch1"));
            Thread.Sleep(DispatchWait);
            Assert.IsTrue(target1.dispatch1 || target2.dispatch1);

            for (int i = 0; i < 100; i++)
                dispatcher.Dispatch(new _DispatchMsg1("logical://Dispatch1"));

            Thread.Sleep(DispatchWait);
            Assert.IsTrue(target1.dispatch1 && target2.dispatch1);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherLogical_Multi_Handlers_Broadcast()
        {
            Target3 target1 = new Target3();
            Target3 target2 = new Target3();
            MsgDispatcher dispatcher = new MsgDispatcher();
            _DispatchMsg1 msg;

            dispatcher.AddTarget(target1);
            dispatcher.AddTarget(target2);

            msg = new _DispatchMsg1("logical://Dispatch1");
            msg._Flags |= MsgFlag.Broadcast;

            dispatcher.Dispatch(msg);

            Thread.Sleep(DispatchWait);
            Assert.IsTrue(target1.dispatch1 && target2.dispatch1);
        }

        private class Target4A
        {
            public int Count = 0;

            [MsgHandler(LogicalEP = "logical://Foo")]
            public void OnMsg(_DispatchMsg1 msg)
            {
                this.Count++;
            }
        }

        private class Target4B
        {
            public int Count = 0;

            [MsgHandler(LogicalEP = "logical://Foo")]
            public void OnMsg(_DispatchMsg2 msg)
            {
                this.Count++;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherLogical_Multi_Handlers_Grouped()
        {
            Target4A target1 = new Target4A();
            Target4B target2 = new Target4B();
            MsgDispatcher dispatcher = new MsgDispatcher();

            // I'm going to test whether the handlers for the two targets
            // were grouped properly in the routing table by sending
            // instances of the two message types to logical://Foo.
            //
            // The message receive counts should match the number
            // of messages sent if grouping worked properly.  If
            // grouping didn't work, we'd expect to see messages
            // routed randomly to one target or the other with
            // messages that aren't handled by each target being
            // dropped.

            dispatcher.AddTarget(target1, null, null, target1);
            dispatcher.AddTarget(target2, null, null, target1);

            for (int i = 0; i < 10; i++)
            {
                dispatcher.Dispatch(new _DispatchMsg1("logical://Foo"));
                dispatcher.Dispatch(new _DispatchMsg2("logical://Foo"));
            }

            Thread.Sleep(1000);

            Assert.AreEqual(10, target1.Count);
            Assert.AreEqual(10, target2.Count);
        }

        private class Target5
        {
            [MsgHandler(LogicalEP = "logical://Foo", Default = true)]
            public void OnMsg(_DispatchMsg1 msg)
            {
            }

            [MsgHandler(LogicalEP = "logical://Foo", Default = true)]
            public void OnMsg(_DispatchMsg2 msg)
            {
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherLogical_MultipleDefault()
        {
            try
            {
                new MsgDispatcher().AddTarget(new Target5());
                Assert.Fail("Expected the detection of multiple default handlers.");
            }
            catch (MsgException)
            {
            }
        }

        private class Target6
        {
            [MsgHandler(LogicalEP = "logical://Foo")]
            void OnMsg(_DispatchMsg1 msg)
            {
            }
        }

        private class Target7
        {
            [MsgHandler(LogicalEP = "logical://Foo")]
            public int OnMsg(_DispatchMsg1 msg)
            {
                return 0;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherLogical_VerifyVoid()
        {
            try
            {
                new MsgDispatcher().AddTarget(new Target7());
                Assert.Fail("Expected the detection of a non-void handler.");
            }
            catch (MsgException)
            {
            }
        }

        private class Target8
        {
            [MsgHandler(LogicalEP = "logical://Foo")]
            public void OnMsg(int msg)
            {
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherLogical_VerifyParamType()
        {
            try
            {
                new MsgDispatcher().AddTarget(new Target8());
                Assert.Fail("Expected the detection of a handler with a non-message parameter.");
            }
            catch (MsgException)
            {
            }
        }

        private class Target9
        {
            [MsgHandler(LogicalEP = "logical://Foo")]
            public void OnMsg()
            {
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherLogical_VerifyParamCount()
        {
            try
            {
                new MsgDispatcher().AddTarget(new Target9());
                Assert.Fail("Expected the detection of a handler with the wrong number of parameters.");
            }
            catch (MsgException)
            {
            }
        }

        private Dictionary<string, Msg> dispatchInfo = new Dictionary<string, Msg>();

        [MsgHandler(LogicalEP = "logical://Foo/Bar/1")]
        public void OnHandler1(_DispatchMsg1 msg)
        {

            dispatchInfo.Add("OnHandler1", msg);
        }

        [MsgHandler(LogicalEP = "logical://Foo/Bar/1")]
        public void OnHandler2(_DispatchMsg2 msg)
        {
            dispatchInfo.Add("OnHandler2", msg);
        }

        [MsgHandler(LogicalEP = "logical://Foo/Bar/1")]
        public void OnHandler3(_DispatchMsg3 msg)
        {
            dispatchInfo.Add("OnHandler3", msg);
        }

        [MsgHandler(LogicalEP = "logical://Foo/Bar/1", Default = true)]
        public void OnHandler4(Msg msg)
        {
            dispatchInfo.Add("OnHandler4", msg);
        }

        [MsgHandler(LogicalEP = "logical://Foo/Bar/2")]
        public void OnHandler5(_DispatchMsg1 msg)
        {
            dispatchInfo.Add("OnHandler5", msg);
        }

        [MsgHandler(LogicalEP = "logical://Foo/Bar/2")]
        public void OnHandler6(_DispatchMsg2 msg)
        {
            dispatchInfo.Add("OnHandler6", msg);
        }

        [MsgHandler(LogicalEP = "logical://Foo/Bar/2")]
        public void OnHandler7(_DispatchMsg3 msg)
        {
            dispatchInfo.Add("OnHandler7", msg);
        }

        [MsgHandler(LogicalEP = "logical://Foo/Bar/2", Default = true)]
        public void OnHandler8(Msg msg)
        {
            dispatchInfo.Add("OnHandler8", msg);
        }

        [MsgHandler(LogicalEP = "logical://FooBar/*")]
        public void OnHandler9(_DispatchMsg1 msg)
        {
            dispatchInfo.Add("OnHandler9", msg);
        }

        [MsgHandler(LogicalEP = "logical://FooBar/*", Default = true)]
        public void OnHandler10(Msg msg)
        {
            dispatchInfo.Add("OnHandler10", msg);
        }

        [MsgHandler(LogicalEP = "logical://Multiple/1", Default = true)]
        [MsgHandler(LogicalEP = "logical://Multiple/2", Default = true)]
        public void OnHandler11(Msg msg)
        {
            if (msg._ToEP.Equals(MsgEP.Parse("logical://Multiple/1")))
                dispatchInfo.Add("OnHandler11-1", msg);
            else
                dispatchInfo.Add("OnHandler11-2", msg);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherLogical_MultipleHandlers()
        {
            MsgDispatcher dispatcher = new MsgDispatcher();

            dispatcher.AddTarget(this);

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg1("logical://Foo/Bar/1")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler1"));

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg2("logical://Foo/Bar/1")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler2"));

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg3("logical://Foo/Bar/1")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler3"));

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg4("logical://Foo/Bar/1")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler4"));

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg1("logical://Foo/Bar/2")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler5"));

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg2("logical://Foo/Bar/2")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler6"));

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg3("logical://Foo/Bar/2")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler7"));

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg4("logical://Foo/Bar/2")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler8"));

            Clear();
            Assert.IsFalse(Dispatch(dispatcher, new _DispatchMsg1("logical://NotPresent")));
            Assert.AreEqual(0, dispatchInfo.Count);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherLogical_MultipleHandlerAttributes()
        {
            MsgDispatcher dispatcher = new MsgDispatcher();

            dispatcher.AddTarget(this);

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg1("logical://Multiple/1")));
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg1("logical://Multiple/2")));
            Assert.AreEqual(2, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler11-1"));
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler11-2"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherLogical_Wildcards()
        {
            MsgDispatcher dispatcher = new MsgDispatcher();

            dispatcher.AddTarget(this);

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg1("logical://Foo/Bar/*")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler1") || dispatchInfo.ContainsKey("OnHandler5"));

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg2("logical://Foo/Bar/*")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler2") || dispatchInfo.ContainsKey("OnHandler6"));

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg3("logical://Foo/Bar/*")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler3") || dispatchInfo.ContainsKey("OnHandler7"));

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg4("logical://Foo/Bar/*")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler4") || dispatchInfo.ContainsKey("OnHandler8"));

            Clear();
            Assert.IsFalse(Dispatch(dispatcher, new _DispatchMsg1("logical://NotPresent/*")));
            Assert.AreEqual(0, dispatchInfo.Count);

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg1("logical://FooBar/*")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler9"));

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg1("logical://FooBar/1")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler9"));

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg1("logical://FooBar/2")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler9"));

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg1("logical://FooBar")));
            Assert.AreEqual(1, dispatchInfo.Count);

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg2("logical://FooBar/*")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler10"));

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg2("logical://FooBar/1")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler10"));

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg2("logical://FooBar/2")));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler10"));

            Clear();
            Assert.IsTrue(Dispatch(dispatcher, new _DispatchMsg2("logical://FooBar")));
            Assert.AreEqual(1, dispatchInfo.Count);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherLogical_Broadcast()
        {
            MsgDispatcher dispatcher = new MsgDispatcher();
            Msg msg;

            dispatcher.AddTarget(this);

            Clear();
            msg = new _DispatchMsg1("logical://Foo/Bar/1");
            msg._Flags |= MsgFlag.Broadcast;
            Assert.IsTrue(Dispatch(dispatcher, msg));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler1"));

            Clear();
            msg = new _DispatchMsg1("logical://Foo/Bar/2");
            msg._Flags |= MsgFlag.Broadcast;
            Assert.IsTrue(Dispatch(dispatcher, msg));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler5"));

            Clear();
            msg = new _DispatchMsg1("logical://Foo/Bar/*");
            msg._Flags |= MsgFlag.Broadcast;
            Assert.IsTrue(Dispatch(dispatcher, msg));
            Assert.AreEqual(2, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler1") && dispatchInfo.ContainsKey("OnHandler5"));

            //-------------------------

            Clear();
            msg = new _DispatchMsg2("logical://Foo/Bar/1");
            msg._Flags |= MsgFlag.Broadcast;
            Assert.IsTrue(Dispatch(dispatcher, msg));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler2"));

            Clear();
            msg = new _DispatchMsg2("logical://Foo/Bar/2");
            msg._Flags |= MsgFlag.Broadcast;
            Assert.IsTrue(Dispatch(dispatcher, msg));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler6"));

            Clear();
            msg = new _DispatchMsg2("logical://Foo/Bar/*");
            msg._Flags |= MsgFlag.Broadcast;
            Assert.IsTrue(Dispatch(dispatcher, msg));
            Assert.AreEqual(2, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler2") && dispatchInfo.ContainsKey("OnHandler6"));

            //-------------------------

            Clear();
            msg = new _DispatchMsg3("logical://Foo/Bar/1");
            msg._Flags |= MsgFlag.Broadcast;
            Assert.IsTrue(Dispatch(dispatcher, msg));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler3"));

            Clear();
            msg = new _DispatchMsg3("logical://Foo/Bar/2");
            msg._Flags |= MsgFlag.Broadcast;
            Assert.IsTrue(Dispatch(dispatcher, msg));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler7"));

            Clear();
            msg = new _DispatchMsg3("logical://Foo/Bar/*");
            msg._Flags |= MsgFlag.Broadcast;
            Assert.IsTrue(Dispatch(dispatcher, msg));
            Assert.AreEqual(2, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler3") && dispatchInfo.ContainsKey("OnHandler7"));

            //-------------------------

            Clear();
            msg = new _DispatchMsg4("logical://Foo/Bar/1");
            msg._Flags |= MsgFlag.Broadcast;
            Assert.IsTrue(Dispatch(dispatcher, msg));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler4"));

            Clear();
            msg = new _DispatchMsg4("logical://Foo/Bar/2");
            msg._Flags |= MsgFlag.Broadcast;
            Assert.IsTrue(Dispatch(dispatcher, msg));
            Assert.AreEqual(1, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler8"));

            Clear();
            msg = new _DispatchMsg4("logical://Foo/Bar/*");
            msg._Flags |= MsgFlag.Broadcast;
            Assert.IsTrue(Dispatch(dispatcher, msg));
            Assert.AreEqual(2, dispatchInfo.Count);
            Assert.IsTrue(dispatchInfo.ContainsKey("OnHandler4") && dispatchInfo.ContainsKey("OnHandler8"));
        }

        private bool dispatchMsg1;
        private bool dispatchMsg2;
        private bool dispatchDynamic;
        private bool defaultMsg;

        private void Clear()
        {
            dispatchMsg1 = false;
            dispatchMsg2 = false;
            dispatchDynamic = false;
            defaultMsg = false;

            dispatchInfo.Clear();
        }

        [MsgHandler(LogicalEP = "logical://Dispatch1")]
        public void OnMsg(_DispatchMsg1 msg)
        {
            dispatchMsg1 = true;
        }

        [MsgHandler(LogicalEP = "logical://Dispatch2")]
        public void OnMsg(_DispatchMsg2 msg)
        {
            dispatchMsg2 = true;
        }

        [MsgHandler(LogicalEP = "logical://Dispatch3", Default = true)]
        public void OnMsg(Msg msg)
        {
            defaultMsg = msg is _DispatchMsg3;
        }

        [MsgHandler(LogicalEP = "logical://Dynamic", DynamicScope = "foo")]
        public void OnMsgDynamic(_DispatchMsg1 msg)
        {
            dispatchDynamic = true;
        }

        private class Munger : IDynamicEPMunger
        {
            public MsgEP Munge(MsgEP logicalEP, MsgHandler handler)
            {
                return "logical://MungedDynamic";
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherLogical_DynamicEP()
        {
            MsgDispatcher dispatcher;
            MsgRouter router;
            ChannelEP target;

            dispatcher = new MsgDispatcher();
            router = new MsgRouter(dispatcher);

            router.Dispatcher.AddTarget(this, "foo", new Munger(), null);
            router.RouterEP = MsgEP.Parse("physical://foo.com/" + Helper.NewGuid().ToString());
            router.Start(IPAddress.Any, null, new IPEndPoint(IPAddress.Any, 0), new IPEndPoint(IPAddress.Any, 0), 5, TimeSpan.FromSeconds(60));
            target = new ChannelEP(Transport.Udp, router.UdpEP);

            try
            {
                Clear();
                router.Transmit(target, new _DispatchMsg1("logical://MungedDynamic"));
                Thread.Sleep(DispatchWait);
                Assert.IsTrue(dispatchDynamic);
            }
            finally
            {
                router.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherLogical_Router()
        {
            MsgDispatcher dispatcher;
            MsgRouter router;
            ChannelEP target;

            dispatcher = new MsgDispatcher();
            router = new MsgRouter(dispatcher);

            router.Dispatcher.AddTarget(this);
            router.RouterEP = MsgEP.Parse("physical://foo.com/" + Helper.NewGuid().ToString());
            router.Start(IPAddress.Any, null, new IPEndPoint(IPAddress.Any, 0), new IPEndPoint(IPAddress.Any, 0), 5, TimeSpan.FromSeconds(60));
            target = new ChannelEP(Transport.Udp, router.UdpEP);

            try
            {
                Clear();
                router.Transmit(target, new _DispatchMsg1("logical://Dispatch1"));
                Thread.Sleep(DispatchWait);
                Assert.IsTrue(dispatchMsg1);
                Assert.IsFalse(dispatchMsg2);
                Assert.IsFalse(defaultMsg);

                Clear();
                router.Transmit(target, new _DispatchMsg2("logical://Dispatch2"));
                Thread.Sleep(DispatchWait);
                Assert.IsFalse(dispatchMsg1);
                Assert.IsTrue(dispatchMsg2);
                Assert.IsFalse(defaultMsg);

                Clear();
                router.Transmit(target, new _DispatchMsg3("logical://Dispatch3"));
                Thread.Sleep(DispatchWait);
                Assert.IsFalse(dispatchMsg1);
                Assert.IsFalse(dispatchMsg2);
                Assert.IsTrue(defaultMsg);
            }
            finally
            {
                router.Stop();
            }
        }
    }
}

