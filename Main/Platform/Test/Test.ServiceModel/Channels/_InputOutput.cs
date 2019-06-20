//-----------------------------------------------------------------------------
// FILE:        _InputOutput.cs
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
    public class _InputOutput
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
        public void InputOutput_Send_Receive()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and receive it
            // on the input channel.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Send_Wait_Receive()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and wait for
            // it and then receive it on the input channel.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Send_Wait_TryReceive()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and wait for
            // it and then receive it on the input channel.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Send_Receive_Logical()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and receive it
            // on the input channel.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = new Uri("lilltek.logical://Wcf/" + Guid.NewGuid().ToString("D"));
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Send_Receive_Abstract()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and receive it
            // on the input channel.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = new Uri("lilltek.abstract://Wcf/" + Guid.NewGuid().ToString("D"));
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Send_TryReceive()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and receive it
            // on the input channel using TryReceive()

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Send_TryReceive_Timeout()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and receive it
            // on the input channel.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Send_WaitForMessage_TryReceive()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and receive it
            // on the input channel.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Send_WaitForMessage_Timeout()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and receive it
            // on the input channel.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Send_Receive_MultipleSame()
        {
            // Create an input and an output channel and then verify that
            // we can send multiple messages from the output channel and receive 
            // them on the same input channel.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;

            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Send_Receive_DifferentInput()
        {
            // Verify that we can force new input channels by closing
            // the existing one after receiving a message.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                for (int i = 0; i < 1000; i++)
                {
                    outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "test", i));

                    inputChannel = listener.AcceptChannel(timeout);
                    inputChannel.Open();

                    inMsg = inputChannel.Receive(timeout);
                    Assert.AreEqual(i, inMsg.GetBody<int>());

                    inputChannel.Close();
                    inputChannel = null;
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
        public void InputOutput_Send_Receive_QueuedMessageBeforeAccept()
        {
            // Verify that channel listener will handle receiving
            // messages before AcceptChannel() is called.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Send_Receive_QueuedMessageAfterAccept()
        {
            // Verify that channel listener will handle receiving
            // messages after AcceptChannel() is called.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Send_Receive_QueuedMessageBeforeReceive()
        {
            // Verify that InputChannel will handle receiving
            // messages before Receive() is called after receiving
            // the initial accept message.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Send_Receive_QueuedMessageAfterReceive()
        {
            // Verify that InputChannel will handle receiving
            // messages before Receive() is called after receiving
            // the initial accept message.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Accept_Timeout()
        {
            // Verify that the channel listener will timeout AcceptChannel().

            IChannelListener<IInputChannel> listener = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_WaitForChannel_Timeout()
        {
            // Verify that the channel listener will timeout WaitForChannel().

            IChannelListener<IInputChannel> listener = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_WaitForChannelAndAccept()
        {
            // Verify that WaitForChannel() followed by AcceptChannel() works.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                for (int i = 0; i < 1000; i++)
                {
                    outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "test", i));

                    Assert.IsTrue(listener.WaitForChannel(timeout));
                    inputChannel = listener.AcceptChannel(timeout);
                    inputChannel.Open();

                    inMsg = inputChannel.Receive(timeout);
                    Assert.AreEqual(i, inMsg.GetBody<int>());

                    inputChannel.Close();
                    inputChannel = null;
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
        public void InputOutput_WaitForChannel_Multiple()
        {
            // Verify that when we have multiple pending WaitForChannels(),
            // they are completed one-by-one as messages are received.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult arWait1;
            IAsyncResult arWait2;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                arWait1 = listener.BeginWaitForChannel(timeout, null, null);
                arWait2 = listener.BeginWaitForChannel(timeout, null, null);

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "test", 10));
                Assert.IsTrue(listener.EndWaitForChannel(arWait1));
                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();
                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual(10, inMsg.GetBody<int>());
                inputChannel.Close();
                inputChannel = null;

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "test", 20));
                Assert.IsTrue(listener.EndWaitForChannel(arWait2));
                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();
                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual(20, inMsg.GetBody<int>());
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
        public void InputOutput_Verify_NoSession()
        {
            // Verify that we're not seeing session behavior by creating one listener
            // and two output channels.  Messages sent by each output channel should
            // be received by a single input channel instance.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel1 = null;
            IOutputChannel outputChannel2 = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
                listener.Open();

                outputChannel1 = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel1.Open();

                outputChannel2 = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel2.Open();

                outputChannel1.Send(Message.CreateMessage(MessageVersion.Default, "test", 10));
                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();

                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual(10, inMsg.GetBody<int>());

                outputChannel2.Send(Message.CreateMessage(MessageVersion.Default, "test", 20));
                inMsg = inputChannel.Receive(timeout);
                Assert.AreEqual(20, inMsg.GetBody<int>());
            }
            finally
            {
                if (inputChannel != null)
                    inputChannel.Close();

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
        public void InputOutput_LoadBalance()
        {
            // Verify that sending messages to a URI shared by two listeners
            // load balances the messages across the two listeners.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener1 = null;
            IChannelListener<IInputChannel> listener2 = null;
            IInputChannel inputChannel1 = null;
            IInputChannel inputChannel2 = null;
            IOutputChannel outputChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            int cInput1Msgs;
            int cInput2Msgs;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener1 = binding.BuildChannelListener<IInputChannel>(uri);
                listener1.Open();

                listener2 = binding.BuildChannelListener<IInputChannel>(uri);
                listener2.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                for (int i = 0; i < Math.Min(100, ServiceModelHelper.MaxAcceptedMessages); i++)
                    outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "test", i));

                Thread.Sleep(yieldTime);

                inputChannel1 = listener1.AcceptChannel(timeout);
                inputChannel1.Open();
                cInput1Msgs = 0;
                while (inputChannel1.WaitForMessage(timeout))
                {
                    cInput1Msgs++;
                    inputChannel1.Receive();
                }

                inputChannel2 = listener2.AcceptChannel(timeout);
                inputChannel2.Open();
                cInput2Msgs = 0;
                while (inputChannel2.WaitForMessage(timeout))
                {
                    cInput2Msgs++;
                    inputChannel2.Receive();
                }

                Assert.IsTrue(cInput1Msgs > 0 && cInput2Msgs > 0);
            }
            finally
            {
                if (inputChannel1 != null)
                    inputChannel1.Close();

                if (inputChannel2 != null)
                    inputChannel2.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (listener1 != null)
                    listener1.Close();

                if (listener2 != null)
                    listener2.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutput_Broadcast()
        {
            // Verify that broadcasting a messages to a URI shared by two listeners
            // delivers the message to both.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener1 = null;
            IChannelListener<IInputChannel> listener2 = null;
            IInputChannel inputChannel1 = null;
            IInputChannel inputChannel2 = null;
            IOutputChannel outputChannel = null;
            Uri uri = new Uri("lilltek.logical://Wcf/" + Guid.NewGuid().ToString("D") + "?broadcast");
            LillTekBinding binding;
            int cInput1Msgs;
            int cInput2Msgs;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener1 = binding.BuildChannelListener<IInputChannel>(uri);
                listener1.Open();

                listener2 = binding.BuildChannelListener<IInputChannel>(uri);
                listener2.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "test", 10));

                Thread.Sleep(yieldTime);

                inputChannel1 = listener1.AcceptChannel(timeout);
                inputChannel1.Open();
                cInput1Msgs = 0;
                while (inputChannel1.WaitForMessage(timeout))
                {
                    cInput1Msgs++;
                    inputChannel1.Receive();
                }

                inputChannel2 = listener2.AcceptChannel(timeout);
                inputChannel2.Open();
                cInput2Msgs = 0;
                while (inputChannel2.WaitForMessage(timeout))
                {
                    cInput2Msgs++;
                    inputChannel2.Receive();
                }

                Assert.IsTrue(cInput1Msgs == 1 && cInput2Msgs == 1);
            }
            finally
            {
                if (inputChannel1 != null)
                    inputChannel1.Close();

                if (inputChannel2 != null)
                    inputChannel2.Close();

                if (outputChannel != null)
                    outputChannel.Close();

                if (listener1 != null)
                    listener1.Close();

                if (listener2 != null)
                    listener2.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void InputOutput_InputChannelListener_TerminatePendingOnClose()
        {
            // Verify that pending InputChannelListener operations are terminated
            // properly after the listener is closed.

            IChannelListener<IInputChannel> listener = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding = new LillTekBinding(uri);
            IAsyncResult ar;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                // Test: AcceptChannel()

                listener = binding.BuildChannelListener<IInputChannel>(uri);
                listener.Open();
                ar = listener.BeginAcceptChannel(timeout, null, null);
                listener.Close();

                Assert.IsNull(listener.EndAcceptChannel(ar)); ;

                // Test: WaitForChannel()

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_InputChannel_TerminatePendingOnClose()
        {
            // Verify that pending InputChannel operations are terminated
            // properly after the channel is closed.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult ar;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());
                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
                listener.Open();

                outputChannel = factory.CreateChannel(new EndpointAddress(uri));
                outputChannel.Open();

                // Test: Receive()

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));
                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();
                inputChannel.Receive();

                ar = inputChannel.BeginReceive(timeout, null, null);
                inputChannel.Close();

                Assert.IsNull(inputChannel.EndReceive(ar));

                // Test: WaitForMessage()

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));
                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();
                inputChannel.Receive();

                ar = inputChannel.BeginWaitForMessage(timeout, null, null);
                inputChannel.Close();

                Assert.IsFalse(inputChannel.EndWaitForMessage(ar));

                // Test: TryReceive()

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
        public void InputOutput_InputChannelListener_TerminatePendingOnAbort()
        {
            // Verify that pending InputChannelListener operations are terminated
            // properly after the listener is closed.

            IChannelListener<IInputChannel> listener = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding = new LillTekBinding(uri);
            IAsyncResult ar;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                // Test: AcceptChannel()

                listener = binding.BuildChannelListener<IInputChannel>(uri);
                listener.Open();
                ar = listener.BeginAcceptChannel(timeout, null, null);
                listener.Abort();

                Assert.IsNull(listener.EndAcceptChannel(ar));

                // Test: WaitForChannel()

                listener = binding.BuildChannelListener<IInputChannel>(uri);
                listener.Open();
                ar = listener.BeginWaitForChannel(timeout, null, null);
                listener.Abort();
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
        public void InputOutput_InputChannel_TerminatePendingOnAbort()
        {
            // Verify that pending InputChannel operations are terminated
            // properly after the channel is aborted.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult ar;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());
                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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

                outputChannel.Send(Message.CreateMessage(MessageVersion.Default, "hello", (object)"world"));
                inputChannel = listener.AcceptChannel(timeout);
                inputChannel.Open();
                inputChannel.Receive();

                ar = inputChannel.BeginWaitForMessage(timeout, null, null);
                inputChannel.Abort();

                Assert.IsFalse(inputChannel.EndWaitForMessage(ar));

                // Test: TryReceive()

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
        public void InputOutput_Bulk_Send_Receive()
        {
            // Create an input and an output channel and then verify that
            // we can send multiple messages from the output channel and receive them
            // on the input channel.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Bulk_Send_Receive_Queued()
        {
            // Create an input and an output channel and then verify that
            // we can send multiple messages from the output channel, let them
            // queue on the input channel, and then receive receive them all.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            int count = Math.Min(100, ServiceModelHelper.MaxAcceptedMessages);
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Bulk_Send_Wait_Receive()
        {
            // Create an input and an output channel and then verify that
            // we can send multiple messages from the output channel and receive them
            // on the input channel.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Bulk_Send_Wait_TryReceive()
        {
            // Create an input and an output channel and then verify that
            // we can send a message from the output channel and receive it
            // on the input channel.

            IChannelFactory<IOutputChannel> factory = null;
            IChannelListener<IInputChannel> listener = null;
            IInputChannel inputChannel = null;
            IOutputChannel outputChannel = null;
            Message inMsg = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IInputChannel>(uri);
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
        public void InputOutput_Factory_Close()
        {
            // Verify that channels belonging to a channel factory are closed
            // when the factory is closed.

            IChannelFactory<IOutputChannel> factory = null;
            IOutputChannel channel1 = null;
            IOutputChannel channel2 = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IOutputChannel>();
                factory.Open();

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

                ChannelHost.Stop();
            }
        }
    }
}

