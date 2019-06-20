//-----------------------------------------------------------------------------
// FILE:        _MsgDispatcher_Physical.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Unit tests for the physical dispatching by the MsgDispatcher class.

using System;
using System.Net;
using System.Reflection;
using System.Threading;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _MsgDispatcherPhysical
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
        }

        public class _DispatchMsg2 : Msg
        {
        }

        public class _DispatchMsg3 : Msg
        {
        }

        public class _DispatchMsg4 : _DispatchMsg3
        {
        }

        private bool onExplicit1 = false;
        private bool onExplicit2 = false;
        private bool onExplicit3 = false;

        private void OnExplicit1(Msg _msg)
        {
            _DispatchMsg1 msg = (_DispatchMsg1)_msg;

            onExplicit1 = true;
        }

        private void OnExplicit2(Msg _msg)
        {
            _DispatchMsg2 msg = (_DispatchMsg2)_msg;

            onExplicit2 = true;
        }

        private void OnExplicit3(Msg _msg)
        {
            _DispatchMsg3 msg = (_DispatchMsg3)_msg;

            onExplicit3 = true;
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
        public void MsgDispatcherPhysical_NoHandlers()
        {
            Target1 target = new Target1();
            MsgDispatcher dispatcher = new MsgDispatcher();

            dispatcher.AddTarget(target);
            dispatcher.Dispatch(new _DispatchMsg1());
            dispatcher.Dispatch(new _DispatchMsg2());
            Thread.Sleep(DispatchWait);

            Assert.IsFalse(target.dispatch1);
            Assert.IsFalse(target.dispatch2);
        }

        private class Target2
        {
            public bool dispatch1 = false;
            public bool dispatch2 = false;
            public bool dispatch3 = false;

            [MsgHandler]
            public void OnMsg(_DispatchMsg1 msg)
            {
                dispatch1 = true;
            }

            [MsgHandler]
            public void OnMsg(_DispatchMsg2 msg)
            {
                dispatch2 = true;
            }

            [MsgHandler]
            public void OnMsg(_DispatchMsg3 msg)
            {
                dispatch3 = true;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherPhysical_ValidHandlers()
        {
            Target2 target = new Target2();
            MsgDispatcher dispatcher = new MsgDispatcher();

            dispatcher.AddTarget(target);
            dispatcher.Dispatch(new _DispatchMsg1());
            dispatcher.Dispatch(new _DispatchMsg2());
            dispatcher.Dispatch(new _DispatchMsg3());
            Thread.Sleep(DispatchWait);

            Assert.IsTrue(target.dispatch1);
            Assert.IsTrue(target.dispatch2);
            Assert.IsTrue(target.dispatch3);
        }

        private class Target3
        {
            public bool dispatch1 = false;
            public bool dispatch2 = false;
            public bool defHandler = false;

            [MsgHandler]
            public void OnMsg(_DispatchMsg1 msg)
            {
                dispatch1 = true;
            }

            [MsgHandler]
            public void OnMsg(_DispatchMsg2 msg)
            {
                dispatch2 = true;
            }

            [MsgHandler(Default = true)]
            public void OnMsg(Msg msg)
            {
                defHandler = true;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherPhysical_DefaultHandler()
        {
            Target3 target = new Target3();
            MsgDispatcher dispatcher = new MsgDispatcher();

            dispatcher.AddTarget(target);
            dispatcher.Dispatch(new _DispatchMsg1());
            dispatcher.Dispatch(new _DispatchMsg2());
            Thread.Sleep(DispatchWait);

            Assert.IsTrue(target.dispatch1);
            Assert.IsTrue(target.dispatch2);
            Assert.IsFalse(target.defHandler);

            dispatcher.Dispatch(new _DispatchMsg3());
            Thread.Sleep(DispatchWait);
            Assert.IsTrue(target.defHandler);
        }

        private class Target4
        {
            [MsgHandler]
            public void OnMsg1(_DispatchMsg1 msg)
            {
            }

            [MsgHandler]
            public void OnMsg2(_DispatchMsg1 msg)
            {
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherPhysical_MultipleHandlers()
        {
            MsgDispatcher dispatcher;

            try
            {
                new MsgDispatcher().AddTarget(new Target4());
                Assert.Fail("Expected the detection of multiple handlers with the same message type.");
            }
            catch (MsgException)
            {
            }

            try
            {
                dispatcher = new MsgDispatcher();

                dispatcher.AddTarget(new Target2());
                dispatcher.AddPhysical(new MsgHandlerDelegate(OnExplicit1), typeof(_DispatchMsg1), null);
                Assert.Fail("Expected the detection of multiple handlers with the same message type.");
            }
            catch (MsgException)
            {
            }
        }

        private class Target5
        {
            [MsgHandler(Default = true)]
            public void OnMsg(_DispatchMsg1 msg)
            {
            }

            [MsgHandler(Default = true)]
            public void OnMsg(_DispatchMsg2 msg)
            {
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherPhysical_MultipleDefault()
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
            [MsgHandler]
            void OnMsg(_DispatchMsg1 msg)
            {
            }
        }

        private class Target7
        {
            [MsgHandler]
            public int OnMsg(_DispatchMsg1 msg)
            {
                return 0;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherPhysical_VerifyVoid()
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
            [MsgHandler]
            public void OnMsg(int msg)
            {
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherPhysical_VerifyParamType()
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
            [MsgHandler]
            public void OnMsg()
            {
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherPhysical_VerifyParamCount()
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

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherPhysical_ExplicitHandlers()
        {
            MsgDispatcher dispatcher;

            dispatcher = new MsgDispatcher();
            dispatcher.AddPhysical(new MsgHandlerDelegate(OnExplicit1), typeof(_DispatchMsg1), null);
            dispatcher.AddPhysical(new MsgHandlerDelegate(OnExplicit2), typeof(_DispatchMsg2), null);
            dispatcher.AddPhysical(new MsgHandlerDelegate(OnExplicit3), typeof(_DispatchMsg3), null);

            Clear();
            dispatcher.Dispatch(new _DispatchMsg1());
            Thread.Sleep(DispatchWait);
            Assert.IsTrue(onExplicit1);
            Assert.IsFalse(onExplicit2);
            Assert.IsFalse(onExplicit3);

            Clear();
            dispatcher.Dispatch(new _DispatchMsg2());
            Thread.Sleep(DispatchWait);
            Assert.IsFalse(onExplicit1);
            Assert.IsTrue(onExplicit2);
            Assert.IsFalse(onExplicit3);

            Clear();
            dispatcher.Dispatch(new _DispatchMsg3());
            Thread.Sleep(DispatchWait);
            Assert.IsFalse(onExplicit1);
            Assert.IsFalse(onExplicit2);
            Assert.IsTrue(onExplicit3);
        }

        private bool dispatchMsg1;
        private bool dispatchMsg2;
        private bool defaultMsg;

        private void Clear()
        {
            onExplicit1 = false;
            onExplicit2 = false;
            onExplicit3 = false;

            dispatchMsg1 = false;
            dispatchMsg2 = false;
            defaultMsg = false;
        }

        [MsgHandler]
        public void OnMsg(_DispatchMsg1 msg)
        {
            dispatchMsg1 = true;
        }

        [MsgHandler]
        public void OnMsg(_DispatchMsg2 msg)
        {
            dispatchMsg2 = true;
        }

        [MsgHandler(Default = true)]
        public void OnMsg(Msg msg)
        {
            defaultMsg = true;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgDispatcherPhysical_Router()
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
                router.Transmit(target, new _DispatchMsg1());
                Thread.Sleep(DispatchWait);
                Assert.IsTrue(dispatchMsg1);
                Assert.IsFalse(dispatchMsg2);
                Assert.IsFalse(defaultMsg);

                Clear();
                router.Transmit(target, new _DispatchMsg2());
                Thread.Sleep(DispatchWait);
                Assert.IsFalse(dispatchMsg1);
                Assert.IsTrue(dispatchMsg2);
                Assert.IsFalse(defaultMsg);

                Clear();
                router.Transmit(target, new _DispatchMsg3());
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

