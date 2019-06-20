//-----------------------------------------------------------------------------
// FILE:        _InputOutputSession.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Net;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.ServiceModel;
using LillTek.ServiceModel.Channels;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.ServiceModel.Channels.Test
{
    [TestClass]
    public class _InputOutputSession
    {
        private TimeSpan timeout = TimeSpan.FromSeconds(2);
        private TimeSpan yieldTime = TimeSpan.FromMilliseconds(100);

        [TestInitialize]
        public void Initialize()
        {
            // AsyncTracker.GatherCallStacks = true;

            // NetTrace.Start();
            // NetTrace.Enable(MsgRouter.TraceSubsystem,0);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // AsyncTracker.GatherCallStacks = false;
            // NetTrace.Stop();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Send_Receive()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and receive it
            // on the input channel.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                Assert.AreEqual(uri, listener.Uri);

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                Assert.AreEqual(uri, outputChannel.RemoteAddress.Uri);
                Assert.AreEqual(uri, outputChannel.Via);

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual("world", inMsg.GetBody<string>());
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Send_Wait_Receive()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and wait for
            // it and then receive it on the input channel.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                Assert.IsTrue(inputChannel.WaitForMessage(timeout));

                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual("world", inMsg.GetBody<string>());
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Send_Wait_TryReceive()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and wait for
            // it and then receive it on the input channel.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                Assert.IsTrue(inputChannel.WaitForMessage(timeout));

                Assert.IsTrue(inputChannel.TryReceive(timeout, out inMsg));
                Assert.AreEqual("world", inMsg.GetBody<string>());
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Send_Receive_Logical()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and receive it
            // on the input channel.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = new Uri("lilltek.logical://Wcf/" + Guid.NewGuid().ToString("D"));
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual("world", inMsg.GetBody<string>());
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Send_Receive_Abstract()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and receive it
            // on the input channel.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = new Uri("lilltek.abstract://Wcf/" + Guid.NewGuid().ToString("D"));
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual("world", inMsg.GetBody<string>());
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Send_TryReceive()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and receive it
            // on the input channel using TryReceive()

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                Assert.IsTrue(inputChannel.TryReceive(timeout, out inMsg));
                Assert.AreEqual("world", inMsg.GetBody<string>());
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Send_TryReceive_Timeout()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and receive it
            // on the input channel.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;

            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                Assert.IsTrue(inputChannel.TryReceive(timeout, out inMsg));
                Assert.AreEqual("world", inMsg.GetBody<string>());

                // Here's the test

                Assert.IsFalse(inputChannel.TryReceive(timeout, out inMsg));
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Send_WaitForMessage_TryReceive()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and receive it
            // on the input channel.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                Assert.IsTrue(inputChannel.WaitForMessage(timeout));
                Assert.IsTrue(inputChannel.TryReceive(timeout, out inMsg));
                Assert.AreEqual("world", inMsg.GetBody<string>());
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Send_WaitForMessage_Timeout()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and receive it
            // on the input channel.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                Assert.IsTrue(inputChannel.TryReceive(timeout, out inMsg));
                Assert.AreEqual("world", inMsg.GetBody<string>());

                // Here's the test

                Assert.IsFalse(inputChannel.WaitForMessage(timeout));
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Send_Receive_MultipleSame()
        {
            // Create an input and an output channel and then verify that
            // we can send multiple messages from the output channel and receive 
            // them on the same input channel.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "test", (object)"world"));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual("world", inMsg.GetBody<string>());

                for (int i = 0; i < 1000; i++)
                {
                    outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "test", i));

                    inMsg = inputChannel.Receive(timeout);
                    Assert.AreEqual(i, inMsg.GetBody<int>());
                }
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Send_Receive_QueuedMessageBeforeAccept()
        {
            // Verify that channel listener will handle receiving
            // messages before AcceptChannel() is called.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "test", 100));

                Thread.Sleep(yieldTime);    // This will force the message to be
                // queued by the listener

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual(100, inMsg.GetBody<int>());

                inputChannel.Close();
                inputChannel = null;
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Send_Receive_QueuedMessageAfterAccept()
        {
            // Verify that channel listener will handle receiving
            // messages after AcceptChannel() is called.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();
                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "test", 100));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                Thread.Sleep(yieldTime);
                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "test", 200));

                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual(100, inMsg.GetBody<int>());

                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual(200, inMsg.GetBody<int>());

                inputChannel.Close();
                inputChannel = null;
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Send_Receive_QueuedMessageBeforeReceive()
        {
            // Verify that InputChannel will handle receiving
            // messages before Receive() is called after receiving
            // the initial accept message.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();
                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "test", 300));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual(300, inMsg.GetBody<int>());

                // Here's the test

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "test", 400));

                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual(400, inMsg.GetBody<int>());

                inputChannel.Close();
                inputChannel = null;
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Send_Receive_QueuedMessageAfterReceive()
        {
            // Verify that InputChannel will handle receiving
            // messages before Receive() is called after receiving
            // the initial accept message.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();
                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "test", 300));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual(300, inMsg.GetBody<int>());

                // Here's the test

                IAsyncResult ar;

                ar = inputChannel.BeginReceive(timeout, null, null);

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "test", 400));

                Thread.Sleep(yieldTime);

                inMsg = inputChannel.EndReceive(ar);
                Assert.AreEqual(400, inMsg.GetBody<int>());

                inputChannel.Close();
                inputChannel = null;
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Accept_Timeout()
        {
            // Verify that the channel listener will timeout AcceptChannel().

            IChannelListener<IInputSessionChannel> listener = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                try
                {
                    listener.AcceptChannel(TimeSpan.FromSeconds(1));
                    Assert.Fail("Expected a TimeoutException");
                }
                catch (TimeoutException)
                {
                }
            }
            finally
            {
                if (listener != null)
                    listener.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_WaitForChannel_Timeout()
        {
            // Verify that the channel listener will timeout WaitForChannel().

            IChannelListener<IInputSessionChannel> listener = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                Assert.IsFalse(listener.WaitForChannel(TimeSpan.FromSeconds(1)));
            }
            finally
            {
                if (listener != null)
                    listener.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_WaitForChannelAndAccept()
        {
            // Verify that WaitForChannel() followed by AcceptChannel() works.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                Assert.IsTrue(listener.WaitForChannel(timeout));
                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                for (int i = 0; i < 1000; i++)
                {
                    outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "test", i));

                    inMsg = inputChannel.Receive(timeout);
                    Assert.AreEqual(i, inMsg.GetBody<int>());
                }
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Verify_Session_MultipleSessions()
        {
            // Verify that we're not seeing session behavior by creating one listener
            // and two output channels.  Messages sent by each output channel should
            // should cause two input channels to be accepted.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel1 = null;
            IInputSessionChannel inputChannel2 = null;
            IOutputSessionChannel outputChannel1 = null;
            IOutputSessionChannel outputChannel2 = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel1 = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel1.Open();
                inputChannel1 = listener.AcceptChannel(timeout);
                inputChannel1.Open();

                outputChannel2 = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel2.Open();
                inputChannel2 = listener.AcceptChannel(timeout);
                inputChannel2.Open();

                outputChannel1.Send(Message.CreateMessage(MessageVersion.Default, "test", 10));
                inMsg = inputChannel1.Receive(timeout);
                Assert.AreEqual(10, inMsg.GetBody<int>());

                outputChannel2.Send(Message.CreateMessage(MessageVersion.Default, "test", 20));
                inMsg = inputChannel2.Receive(timeout);
                Assert.AreEqual(20, inMsg.GetBody<int>());
            }
            finally
            {
                if (inputChannel1 != null)
                    inputChannel1.Close();

                if (inputChannel2 != null)
                    inputChannel2.Close();

                if (outputChannel1 != null)
                    outputChannel1.Close();

                if (outputChannel2 != null)
                    outputChannel2.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Broadcast()
        {
            // Verify that an attempt to broadcast on a IOutputSessionChannel fails with
            // an argument exception.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IOutputSessionChannel outputChannel = null;
            Uri uri = new Uri("lilltek.logical://Wcf/" + Guid.NewGuid().ToString("D") + "?broadcast");
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                try
                {
                    outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                    outputChannel.Open();

                    outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "test", 10));

                    Assert.Fail("Expected ArgumentException");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(ArgumentException));
                }
            }
            finally
            {
                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_InputChannelListener_TerminatePendingOnClose()
        {
            // Verify that pending InputChannelListener operations are terminated
            // properly after the listener is closed.

            IChannelListener<IInputSessionChannel> listener = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding = new LillTekBinding(uri);
            IAsyncResult ar;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                // Test: AcceptChannel()

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();
                ar = listener.BeginAcceptChannel(timeout, null, null);
                listener.Close();

                Assert.IsNull(listener.EndAcceptChannel(ar));

                // Test: WaitForChannel()

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();
                ar = listener.BeginWaitForChannel(timeout, null, null);
                listener.Close();
                Assert.IsFalse(listener.EndWaitForChannel(ar));
            }
            finally
            {
                if (listener != null)
                    listener.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_InputChannel_TerminatePendingOnClose()
        {
            // Verify that pending InputChannel operations are terminated
            // properly after the channel is closed.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult ar;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());
                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                // Test: Receive()

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));
                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();
                inputChannel.Receive();

                ar = inputChannel.BeginReceive(timeout, null, null);
                inputChannel.Close();

                Assert.IsNull(inputChannel.EndReceive(ar));

                // Test: WaitForMessage()

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();
                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();
                inputChannel.Receive();

                ar = inputChannel.BeginWaitForMessage(timeout, null, null);
                inputChannel.Close();

                Assert.IsFalse(inputChannel.EndWaitForMessage(ar));

                // Test: TryReceive()

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();
                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();
                inputChannel.Receive();

                ar = inputChannel.BeginTryReceive(timeout, null, null);
                inputChannel.Close();

                Assert.IsTrue(inputChannel.EndTryReceive(ar, out msg));
                Assert.IsNull(msg);
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_InputChannelListener_TerminatePendingOnAbort()
        {
            // Verify that pending InputChannelListener operations are terminated
            // properly after the listener is closed.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IOutputSessionChannel outputChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding = new LillTekBinding(uri);
            IAsyncResult ar;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                // Test: AcceptChannel()

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                ar = listener.BeginAcceptChannel(timeout, null, null);
                listener.Abort();

                Assert.IsNull(listener.EndAcceptChannel(ar));

                // Test: WaitForChannel()

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                ar = listener.BeginWaitForChannel(timeout, null, null);
                listener.Abort();
                Assert.IsFalse(listener.EndWaitForChannel(ar));
            }
            finally
            {
                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_InputChannel_TerminatePendingOnAbort()
        {
            // Verify that pending InputChannel operations are terminated
            // properly after the channel is aborted.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult ar;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());
                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                // Test: Receive()

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));
                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();
                inputChannel.Receive();

                ar = inputChannel.BeginReceive(timeout, null, null);
                inputChannel.Abort();

                Assert.IsNull(inputChannel.EndReceive(ar));

                // Test: WaitForMessage()

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();
                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();
                inputChannel.Receive();

                ar = inputChannel.BeginWaitForMessage(timeout, null, null);
                inputChannel.Abort();

                Assert.IsFalse(inputChannel.EndWaitForMessage(ar));

                // Test: TryReceive()

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();
                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();
                inputChannel.Receive();

                ar = inputChannel.BeginTryReceive(timeout, null, null);
                inputChannel.Abort();

                Assert.IsTrue(inputChannel.EndTryReceive(ar, out msg));
                Assert.IsNull(msg);
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Bulk_Send_Receive()
        {
            // Create an input and an output channel and then verify that
            // we can send multiple messages from the output channel and receive them
            // on the input channel.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                for (int i = 0; i < 100; i++)
                {
                    outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", i));

                    if (inputChannel == null)
                    {
                        inputChannel = listener.AcceptChannel(timeout);
                        inputChannel.Open();
                    }

                    inMsg = inputChannel.Receive(timeout);
                    Assert.AreEqual(i, inMsg.GetBody<int>());
                }
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Bulk_Send_Receive_Queued()
        {
            // Create an input and an output channel and then verify that
            // we can send multiple messages from the output channel, let them
            // queue on the input channel, and then receive receive them all.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            int count = Math.Min(100, ServiceModelHelper.MaxAcceptedMessages);

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                for (int i = 0; i < count; i++)
                    outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", i));

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                bool[] received = new bool[count];

                for (int i = 0; i < count; i++)
                {
                    inMsg = inputChannel.Receive(timeout);
                    received[inMsg.GetBody<int>()] = true;
                }

                for (int i = 0; i < count; i++)
                    Assert.IsTrue(received[i]);
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Bulk_Send_Wait_Receive()
        {
            // Create an input and an output channel and then verify that
            // we can send multiple messages from the output channel and receive them
            // on the input channel.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                for (int i = 0; i < 100; i++)
                {
                    outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", i));

                    Assert.IsTrue(inputChannel.WaitForMessage(timeout));
                    inMsg = inputChannel.Receive(timeout);
                    Assert.AreEqual(i, inMsg.GetBody<int>());
                }
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Bulk_Send_Wait_TryReceive()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and receive it
            // on the input channel.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                for (int i = 0; i < 100; i++)
                {
                    outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", i));

                    if (inputChannel == null)
                    {
                        inputChannel = listener.AcceptChannel(timeout);
                        inputChannel.Open();
                    }

                    Assert.IsTrue(inputChannel.WaitForMessage(timeout));
                    Assert.IsTrue(inputChannel.TryReceive(timeout, out inMsg));
                    Assert.AreEqual(i, inMsg.GetBody<int>());
                }
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Factory_Close()
        {
            // Verify that channels belonging to a channel factory are closed
            // when the factory is closed.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IOutputSessionChannel channel1 = null;
            IOutputSessionChannel channel2 = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                channel1 = factory.CreateChannel(new EndpointAddress(uri));
                channel1.Open();

                channel2 = factory.CreateChannel(new EndpointAddress(uri));
                channel2.Open();

                Assert.AreEqual(CommunicationState.Opened, factory.State);
                Assert.AreEqual(CommunicationState.Opened, channel1.State);
                Assert.AreEqual(CommunicationState.Opened, channel2.State);

                factory.Close();

                Assert.AreEqual(CommunicationState.Closed, factory.State);
                Assert.AreEqual(CommunicationState.Closed, channel1.State);
                Assert.AreEqual(CommunicationState.Closed, channel2.State);

                factory = null;
                channel1 = null;
                channel2 = null;
            }
            finally
            {
                if (channel1 != null)
                    channel1.Close();

                if (channel2 != null)
                    channel2.Close();

                if (factory != null)
                    factory.Close();

                if (listener != null)
                    listener.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_Reject_MessagesWithoutSessionID()
        {
            // Make sure that InputChannels reject messages with a session ID.

            IChannelFactory<IOutputChannel> factory1 = null;
            IChannelFactory<IOutputSessionChannel> factory2 = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputChannel outputChannel1 = null;
            IOutputSessionChannel outputChannel2 = null;
            Message inMsg;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory1 = binding.BuildChannelFactory<IOutputChannel>();
                factory1.Open();

                factory2 = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory2.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel1 = factory1.CreateChannel(new EndpointAddress(uri));
                outputChannel1.Open();

                outputChannel1.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));

                // Make sure that Accept() ignores the message

                try
                {
                    inputChannel = listener.AcceptChannel(timeout);
                    inputChannel.Open();
                    Assert.Fail("Expected TimeoutException");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(TimeoutException));
                }

                // Make sure that Receive() ignores the message

                outputChannel2 = factory2.CreateChannel(new EndpointAddress(uri));
                outputChannel2.Open();

                outputChannel2.Send(Message.CreateMessage(MessageVersion.Default, "hello", 55));
                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                inMsg = inputChannel.Receive();
                Assert.AreEqual(55, inMsg.GetBody<int>());

                outputChannel1.Send(Message.CreateMessage(MessageVersion.Default, "hello", 66));

                try
                {
                    inMsg = inputChannel.Receive(timeout);
                    Assert.Fail("Expected TimeoutException");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(TimeoutException));
                }
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel1 != null)
                    outputChannel1.Close();

                if (outputChannel2 != null)
                    outputChannel2.Close();

                if (factory1 != null)
                    factory1.Close();

                if (factory2 != null)
                    factory2.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_RemoteClose_Receive()
        {
            // Verify that Receive() returns null after the remote side of
            // the session has been closed.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", 0));
                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual(0, inMsg.GetBody<int>());

                outputChannel.Close();

                Assert.IsNull(inputChannel.Receive(timeout));
                Thread.Sleep(yieldTime);
                Assert.IsNull(inputChannel.Receive(timeout));
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_RemoteClose_WaitForMessage()
        {
            // Verify that WaitForMessage() returns false after the remote side of
            // the session has been closed.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", 0));
                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual(0, inMsg.GetBody<int>());

                outputChannel.Close();

                Assert.IsFalse(inputChannel.WaitForMessage(timeout));
                Assert.IsFalse(inputChannel.WaitForMessage(timeout));
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_RemoteClose_Pending()
        {
            // Verify that pending async WaitForMessage() and Receive() operations
            // return false and null when the remote side of the connection is
            // closed.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult arWait;
            IAsyncResult arReceive;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", 0));
                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual(0, inMsg.GetBody<int>());

                arWait = inputChannel.BeginWaitForMessage(timeout, null, null);
                arReceive = inputChannel.BeginReceive(timeout, null, null);

                outputChannel.Close();

                Assert.IsFalse(inputChannel.EndWaitForMessage(arWait));
                Assert.IsNull(inputChannel.EndReceive(arReceive));

                Assert.IsFalse(inputChannel.WaitForMessage(timeout));
                Assert.IsNull(inputChannel.Receive(timeout));
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutputSession_RemoteClose_QueuedReceive()
        {
            // Verify that queued received messages will still be returned
            // properly after the remote side of the session has been closed.

            IChannelFactory<IOutputSessionChannel> factory = null;
            IChannelListener<IInputSessionChannel> listener = null;
            IInputSessionChannel inputChannel = null;
            IOutputSessionChannel outputChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputSessionChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputSessionChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", 0));
                Thread.Sleep(100);
                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", 1));
                Thread.Sleep(100);
                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", 2));
                Thread.Sleep(100);

                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();
                outputChannel.Close();

                Assert.IsTrue(inputChannel.WaitForMessage(timeout));
                Assert.AreEqual(0, inputChannel.Receive(timeout).GetBody<int>());

                Assert.IsTrue(inputChannel.WaitForMessage(timeout));
                Assert.AreEqual(1, inputChannel.Receive(timeout).GetBody<int>());

                Assert.IsTrue(inputChannel.WaitForMessage(timeout));
                Assert.AreEqual(2, inputChannel.Receive(timeout).GetBody<int>());

                Assert.IsFalse(inputChannel.WaitForMessage(timeout));
                Assert.IsNull(inputChannel.Receive(timeout));
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

                if (listener != null)
                    listener.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }
    }
}

