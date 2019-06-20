//-----------------------------------------------------------------------------
// FILE:        _RequestReply.cs
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
    public class _RequestReply
    {
        private TimeSpan timeout = TimeSpan.FromSeconds(2);
        private TimeSpan yieldTime = TimeSpan.FromMilliseconds(100);

        [TestInitialize]
        public void Initialize()
        {
            // AsyncTracker.GatherCallStacks = true;

            NetTrace.Start();
            NetTrace.Enable(MsgRouter.TraceSubsystem, 0);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // AsyncTracker.GatherCallStacks = false;
            NetTrace.Stop();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_Basic()
        {
            // Verify that we can send a request and then receive receive a reply.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IRequestChannel requestChannel = null;
            IReplyChannel replyChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult ar;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                Assert.AreEqual(uri, listener.Uri);

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                Assert.AreEqual(uri, requestChannel.RemoteAddress.Uri);
                Assert.AreEqual(uri, requestChannel.Via);

                ar = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", 100), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();
                ctx = replyChannel.ReceiveRequest(timeout);
                Assert.AreEqual(100, ctx.RequestMessage.GetBody<int>());

                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "hello", 200));

                msg = requestChannel.EndRequest(ar);
                Assert.AreEqual(200, msg.GetBody<int>());
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (listener != null)
                    listener.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_Abstract()
        {
            // Verify that we can send a request and then receive receive a reply
            // on an abstract endpoint.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IRequestChannel requestChannel = null;
            IReplyChannel replyChannel = null;
            Uri uri = new Uri("lilltek.abstract://Wcf/" + Guid.NewGuid().ToString("D"));
            LillTekBinding binding;
            IAsyncResult ar;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                Assert.AreEqual(uri, listener.Uri);

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                Assert.AreEqual(uri, requestChannel.RemoteAddress.Uri);
                Assert.AreEqual(uri, requestChannel.Via);

                ar = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", 100), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();
                ctx = replyChannel.ReceiveRequest(timeout);
                Assert.AreEqual(100, ctx.RequestMessage.GetBody<int>());

                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "hello", 200));

                msg = requestChannel.EndRequest(ar);
                Assert.AreEqual(200, msg.GetBody<int>());
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (listener != null)
                    listener.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_Logical()
        {
            // Verify that we can send a request and then receive receive a reply
            // on a logical endpoint.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IRequestChannel requestChannel = null;
            IReplyChannel replyChannel = null;
            Uri uri = new Uri("lilltek.logical://Wcf/" + Guid.NewGuid().ToString("D"));
            LillTekBinding binding;
            IAsyncResult ar;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                Assert.AreEqual(uri, listener.Uri);

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                Assert.AreEqual(uri, requestChannel.RemoteAddress.Uri);
                Assert.AreEqual(uri, requestChannel.Via);

                ar = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", 100), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();
                ctx = replyChannel.ReceiveRequest(timeout);
                Assert.AreEqual(100, ctx.RequestMessage.GetBody<int>());

                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "hello", 200));

                msg = requestChannel.EndRequest(ar);
                Assert.AreEqual(200, msg.GetBody<int>());
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (listener != null)
                    listener.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_DifferentReply()
        {
            // Verify that we can force a new reply channels by closing

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IRequestChannel requestChannel = null;
            IReplyChannel replyChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult ar;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                Assert.AreEqual(uri, listener.Uri);

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                Assert.AreEqual(uri, requestChannel.RemoteAddress.Uri);
                Assert.AreEqual(uri, requestChannel.Via);

                for (int i = 0; i < 1000; i++)
                {
                    ar = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", i), timeout, null, null);

                    replyChannel = listener.AcceptChannel(timeout);
                    replyChannel.Open();
                    ctx = replyChannel.ReceiveRequest(timeout);
                    Assert.AreEqual(i, ctx.RequestMessage.GetBody<int>());

                    ctx.Reply(Message.CreateMessage(MessageVersion.Default, "hello", i * 100));

                    msg = requestChannel.EndRequest(ar);
                    Assert.AreEqual(i * 100, msg.GetBody<int>());

                    replyChannel.Close();
                    replyChannel = null;
                }
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (listener != null)
                    listener.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_SameReply()
        {
            // Verify that we can process multiple requests on the
            // same reply channel.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IRequestChannel requestChannel = null;
            IReplyChannel replyChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult ar;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                Assert.AreEqual(uri, listener.Uri);

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                Assert.AreEqual(uri, requestChannel.RemoteAddress.Uri);
                Assert.AreEqual(uri, requestChannel.Via);

                for (int i = 0; i < 1000; i++)
                {
                    ar = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", i), timeout, null, null);

                    if (replyChannel == null)
                    {
                        replyChannel = listener.AcceptChannel(timeout);
                        replyChannel.Open();
                    }

                    ctx = replyChannel.ReceiveRequest(timeout);
                    Assert.AreEqual(i, ctx.RequestMessage.GetBody<int>());

                    ctx.Reply(Message.CreateMessage(MessageVersion.Default, "hello", i * 100));

                    msg = requestChannel.EndRequest(ar);
                    Assert.AreEqual(i * 100, msg.GetBody<int>());
                }
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (listener != null)
                    listener.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_LongWaitInListener()
        {
            // Verify that requests that spend a long time in the listener
            // queue waiting for a channel to accept and process them
            // will not timeout on the client.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IRequestChannel requestChannel = null;
            IReplyChannel replyChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult ar;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                Assert.AreEqual(uri, listener.Uri);

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                Assert.AreEqual(uri, requestChannel.RemoteAddress.Uri);
                Assert.AreEqual(uri, requestChannel.Via);

                ar = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", 100), timeout, null, null);

                Thread.Sleep(TimeSpan.FromMinutes(1));

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();
                ctx = replyChannel.ReceiveRequest(timeout);
                Assert.AreEqual(100, ctx.RequestMessage.GetBody<int>());

                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "hello", 200));

                msg = requestChannel.EndRequest(ar);
                Assert.AreEqual(200, msg.GetBody<int>());
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (listener != null)
                    listener.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_LongWaitInChannel()
        {
            // Verify that requests that spend a long time in the channel
            // queue waiting for a channel to process them will not timeout 
            // on the client.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IRequestChannel requestChannel = null;
            IReplyChannel replyChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult ar;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                Assert.AreEqual(uri, listener.Uri);

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                Assert.AreEqual(uri, requestChannel.RemoteAddress.Uri);
                Assert.AreEqual(uri, requestChannel.Via);

                ar = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", 100), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();

                Thread.Sleep(TimeSpan.FromMinutes(1));

                ctx = replyChannel.ReceiveRequest(timeout);
                Assert.AreEqual(100, ctx.RequestMessage.GetBody<int>());

                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "hello", 200));

                msg = requestChannel.EndRequest(ar);
                Assert.AreEqual(200, msg.GetBody<int>());
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (listener != null)
                    listener.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_LongProcessTime()
        {
            // Verify that requests that spend a long time in the channel
            // queue processing will not timeout on the client.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IRequestChannel requestChannel = null;
            IReplyChannel replyChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult ar;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                Assert.AreEqual(uri, listener.Uri);

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                Assert.AreEqual(uri, requestChannel.RemoteAddress.Uri);
                Assert.AreEqual(uri, requestChannel.Via);

                ar = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", 100), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();

                ctx = replyChannel.ReceiveRequest(timeout);
                Assert.AreEqual(100, ctx.RequestMessage.GetBody<int>());

                Thread.Sleep(TimeSpan.FromMinutes(1));
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "hello", 200));

                msg = requestChannel.EndRequest(ar);
                Assert.AreEqual(200, msg.GetBody<int>());
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (listener != null)
                    listener.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_Accept_Timeout()
        {
            // Verify that the channel listener will timeout AcceptChannel().

            IChannelListener<IReplyChannel> listener = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
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
        public void RequestReply_Orphaned()
        {
            Assert.Inconclusive("This test hangs, probably while waiting for finalizers.  Need to investigate.");

            // Verify that orphaned RequestContexts will be cancelled properly
            // during garbage collection.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IRequestChannel requestChannel = null;
            IReplyChannel replyChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult ar;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                Assert.AreEqual(uri, listener.Uri);

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                Assert.AreEqual(uri, requestChannel.RemoteAddress.Uri);
                Assert.AreEqual(uri, requestChannel.Via);

                ar = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", 100), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();
                ctx = replyChannel.ReceiveRequest(timeout);
                Assert.AreEqual(100, ctx.RequestMessage.GetBody<int>());

                // Explicitly orphan the RequestContext be removing any references
                // to it and then force a garbage collection.  We should see a 
                // CommunicationCanceledException when we complete the request.

                ((ReplyChannel)replyChannel).RemovePendingRequest((LillTekRequestContext)ctx);
                ctx = null;

                GC.Collect();
                GC.WaitForPendingFinalizers();

                msg = requestChannel.EndRequest(ar);
                Assert.Fail("Expected a CommunicationCanceledException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(CommunicationCanceledException));
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (listener != null)
                    listener.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_Bulk_Receive()
        {
            // Verify that we can process multiple requests on the same
            // reply channel.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            Message msg;
            LillTekBinding binding;
            IAsyncResult arRequest;
            IAsyncResult arReply;
            RequestContext ctx;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                for (int i = 0; i < 100; i++)
                {
                    arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", i), timeout, null, null);

                    if (replyChannel == null)
                    {
                        replyChannel = listener.AcceptChannel(timeout);
                        replyChannel.Open();
                    }

                    ctx = replyChannel.ReceiveRequest(timeout);
                    Assert.AreEqual(i, ctx.RequestMessage.GetBody<int>());
                    arReply = ctx.BeginReply(Message.CreateMessage(MessageVersion.Default, "hello", i * 100), timeout, null, null);
                    ctx.EndReply(arReply);

                    msg = requestChannel.EndRequest(arRequest);
                    Assert.AreEqual(i * 100, msg.GetBody<int>());
                }
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (listener != null)
                    listener.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_Bulk_Queued()
        {
            // Verify that we can queue multiple requests to a single
            // reply channel, process them, and receive the responses
            // back on the original request channel.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            Message msg;
            LillTekBinding binding;
            IAsyncResult arReply;
            RequestContext ctx;
            int count = Math.Min(100, ServiceModelHelper.MaxAcceptedMessages);
            IAsyncResult[] arRequests = new IAsyncResult[count];
            bool[] requestCheck = new bool[count];
            bool[] replyCheck = new bool[count];

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                for (int i = 0; i < count; i++)
                    arRequests[i] = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", i), timeout, null, null);

                for (int i = 0; i < count; i++)
                {
                    int requestNum;

                    if (replyChannel == null)
                    {
                        replyChannel = listener.AcceptChannel(timeout);
                        replyChannel.Open();
                    }

                    ctx = replyChannel.ReceiveRequest(timeout);
                    requestNum = ctx.RequestMessage.GetBody<int>();
                    requestCheck[requestNum] = true;

                    arReply = ctx.BeginReply(Message.CreateMessage(MessageVersion.Default, "hello", requestNum * 100), timeout, null, null);
                    ctx.EndReply(arReply);
                }

                for (int i = 0; i < count; i++)
                {
                    msg = requestChannel.EndRequest(arRequests[i]);
                    replyCheck[msg.GetBody<int>() / 100] = true;
                }

                // Verify that we got all of the requests and replies.

                for (int i = 0; i < count; i++)
                {
                    Assert.IsTrue(requestCheck[i]);
                    Assert.IsTrue(replyCheck[i]);
                }
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (listener != null)
                    listener.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_Bulk_Wait_Receive()
        {
            // Verify that WaitForRequest() works on bulk messages to the
            // same reply channel.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            Message msg;
            LillTekBinding binding;
            IAsyncResult arRequest;
            IAsyncResult arReply;
            RequestContext ctx;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                for (int i = 0; i < 100; i++)
                {
                    arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", i), timeout, null, null);

                    if (replyChannel == null)
                    {
                        replyChannel = listener.AcceptChannel(timeout);
                        replyChannel.Open();
                    }

                    Assert.IsTrue(replyChannel.WaitForRequest(timeout));
                    ctx = replyChannel.ReceiveRequest(timeout);
                    Assert.AreEqual(i, ctx.RequestMessage.GetBody<int>());
                    arReply = ctx.BeginReply(Message.CreateMessage(MessageVersion.Default, "hello", i * 100), timeout, null, null);
                    ctx.EndReply(arReply);

                    msg = requestChannel.EndRequest(arRequest);
                    Assert.AreEqual(i * 100, msg.GetBody<int>());
                }
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (listener != null)
                    listener.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_Wait_Bulk_TryReceive()
        {
            // Verify that WaitForRequest() followed by TryReceiveRequest() works on bulk messages to the
            // same reply channel.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            Message msg;
            LillTekBinding binding;
            IAsyncResult arRequest;
            IAsyncResult arReply;
            RequestContext ctx;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                for (int i = 0; i < 100; i++)
                {
                    arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", i), timeout, null, null);

                    if (replyChannel == null)
                    {
                        replyChannel = listener.AcceptChannel(timeout);
                        replyChannel.Open();
                    }

                    Assert.IsTrue(replyChannel.WaitForRequest(timeout));
                    Assert.IsTrue(replyChannel.TryReceiveRequest(timeout, out ctx));
                    Assert.AreEqual(i, ctx.RequestMessage.GetBody<int>());
                    arReply = ctx.BeginReply(Message.CreateMessage(MessageVersion.Default, "hello", i * 100), timeout, null, null);
                    ctx.EndReply(arReply);

                    msg = requestChannel.EndRequest(arRequest);
                    Assert.AreEqual(i * 100, msg.GetBody<int>());
                }
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (listener != null)
                    listener.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_Factory_Close()
        {
            // Verify that channels belonging to a channel factory are closed
            // when the factory is closed.

            IChannelFactory<IRequestChannel> factory = null;
            IRequestChannel channel1 = null;
            IRequestChannel channel2 = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
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

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_ReplyChannelListener_TerminatePendingOnClose()
        {
            // Verify that pending ReplyChannelListener operations are terminated
            // properly after the listener is closed.

            IChannelListener<IReplyChannel> listener = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding = new LillTekBinding(uri);
            IAsyncResult ar;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                // Test: AcceptChannel()

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();
                ar = listener.BeginAcceptChannel(timeout, null, null);
                listener.Close();

                Assert.IsNull(listener.EndAcceptChannel(ar));

                // Test: WaitForChannel()

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
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
        public void RequestReply_ReplyChannel_TerminatePendingOnClose()
        {
            // Verify that pending ReplyChannel operations are terminated
            // properly after the channel is closed.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult arRequest;
            IAsyncResult arReply;
            RequestContext ctx;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());
                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                // Test: Receive()

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", 0), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();
                ctx = replyChannel.ReceiveRequest();
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "hello", 0));

                requestChannel.EndRequest(arRequest);

                arReply = replyChannel.BeginReceiveRequest(timeout, null, null);
                replyChannel.Close();

                Assert.IsNull(replyChannel.EndReceiveRequest(arReply));

                // Test: WaitForRequest()

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", 0), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();
                ctx = replyChannel.ReceiveRequest();
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "hello", 0));

                requestChannel.EndRequest(arRequest);

                arReply = replyChannel.BeginWaitForRequest(timeout, null, null);
                replyChannel.Close();

                Assert.IsFalse(replyChannel.EndWaitForRequest(arReply));

                // Test: TryReceiveRequest()

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", 0), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();
                ctx = replyChannel.ReceiveRequest();
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "hello", 0));

                requestChannel.EndRequest(arRequest);

                arReply = replyChannel.BeginTryReceiveRequest(timeout, null, null);
                replyChannel.Close();

                Assert.IsTrue(replyChannel.EndTryReceiveRequest(arReply, out ctx));
                Assert.IsNull(ctx);
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (listener != null)
                    listener.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_ReplyChannelListener_TerminatePendingOnAbort()
        {
            // Verify that pending InputChannelListener operations are terminated
            // properly after the listener is closed.

            IChannelListener<IReplyChannel> listener = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding = new LillTekBinding(uri);
            IAsyncResult ar;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                // Test: AcceptChannel()

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();
                ar = listener.BeginAcceptChannel(timeout, null, null);
                listener.Abort();

                Assert.IsNull(listener.EndAcceptChannel(ar));

                // Test: WaitForChannel()

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
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
        public void RequestReply_ReplyChannel_TerminatePendingOnAbort()
        {
            // Verify that pending InputChannel operations are terminated
            // properly after the channel is aborted.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult arRequest;
            IAsyncResult arReply;
            RequestContext ctx;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());
                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                // Test: ReceiveRequest()

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", 0), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();
                ctx = replyChannel.ReceiveRequest(timeout);
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "hello", 0));

                requestChannel.EndRequest(arRequest);

                arReply = replyChannel.BeginReceiveRequest(timeout, null, null);
                replyChannel.Abort();

                Assert.IsNull(replyChannel.EndReceiveRequest(arReply));

                // Test: WaitForRequest()

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", 0), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();
                ctx = replyChannel.ReceiveRequest(timeout);
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "hello", 0));

                requestChannel.EndRequest(arRequest);

                arReply = replyChannel.BeginWaitForRequest(timeout, null, null);
                replyChannel.Abort();

                Assert.IsFalse(replyChannel.EndWaitForRequest(arReply));

                // Test: TryReceiveRequest()

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "hello", 0), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();
                ctx = replyChannel.ReceiveRequest(timeout);
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "hello", 0));

                requestChannel.EndRequest(arRequest);

                arReply = replyChannel.BeginTryReceiveRequest(timeout, null, null);
                replyChannel.Abort();

                Assert.IsTrue(replyChannel.EndTryReceiveRequest(arReply, out ctx));
                Assert.IsNull(ctx);
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (listener != null)
                    listener.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_LoadBalance()
        {
            // Verify that sending requests to a URI shared by two listeners
            // load balances across the listeners.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener1 = null;
            IChannelListener<IReplyChannel> listener2 = null;
            IReplyChannel replyChannel1 = null;
            IReplyChannel replyChannel2 = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            int count = Math.Min(100, ServiceModelHelper.MaxAcceptedMessages);
            IAsyncResult[] arRequests = new IAsyncResult[count];
            int cReply1Msgs;
            int cReply2Msgs;
            RequestContext ctx;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener1 = binding.BuildChannelListener<IReplyChannel>(uri);
                listener1.Open();

                listener2 = binding.BuildChannelListener<IReplyChannel>(uri);
                listener2.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                for (int i = 0; i < count; i++)
                    arRequests[i] = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", i), timeout, null, null);

                Thread.Sleep(yieldTime);

                replyChannel1 = listener1.AcceptChannel(timeout);
                replyChannel1.Open();
                cReply1Msgs = 0;
                while (replyChannel1.WaitForRequest(timeout))
                {
                    cReply1Msgs++;
                    Assert.IsTrue(cReply1Msgs <= count);

                    ctx = replyChannel1.ReceiveRequest();
                    Assert.IsNotNull(ctx);
                    ctx.Reply(Message.CreateMessage(MessageVersion.Default, "test", ctx.RequestMessage.GetBody<int>() * 100));
                }

                replyChannel2 = listener2.AcceptChannel(timeout);
                replyChannel2.Open();
                cReply2Msgs = 0;
                while (replyChannel2.WaitForRequest(timeout))
                {
                    cReply2Msgs++;
                    Assert.IsTrue(cReply2Msgs <= count);

                    ctx = replyChannel2.ReceiveRequest();
                    Assert.IsNotNull(ctx);
                    ctx.Reply(Message.CreateMessage(MessageVersion.Default, "test", ctx.RequestMessage.GetBody<int>() * 100));
                }

                for (int i = 0; i < count; i++)
                    requestChannel.EndRequest(arRequests[i]);

                Assert.IsTrue(cReply1Msgs > 0 && cReply2Msgs > 0);
            }
            finally
            {
                if (replyChannel1 != null)
                    replyChannel1.Close();

                if (replyChannel2 != null)
                    replyChannel2.Close();

                if (requestChannel != null)
                    requestChannel.Close();

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
        public void RequestReply_QueuedBeforeAccept()
        {
            // Verify that channel listener will handle receiving
            // requests before AcceptChannel() is called.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult arRequest;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 100), timeout, null, null);

                Thread.Sleep(yieldTime);    // This will force the message to be
                // queued by the listener

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();

                ctx = replyChannel.ReceiveRequest(timeout);
                Assert.AreEqual(100, ctx.RequestMessage.GetBody<int>());
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "test", 200), timeout);

                msg = requestChannel.EndRequest(arRequest);
                Assert.AreEqual(200, msg.GetBody<int>());
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_QueuedAfterAccept()
        {
            // Verify that channel listener will handle receiving
            // requests after AcceptChannel() is called.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult arRequest;
            IAsyncResult arAccept;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                arAccept = listener.BeginAcceptChannel(timeout, null, null);

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 100), timeout, null, null);

                replyChannel = listener.EndAcceptChannel(arAccept);
                replyChannel.Open();

                ctx = replyChannel.ReceiveRequest(timeout);
                Assert.AreEqual(100, ctx.RequestMessage.GetBody<int>());
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "test", 200), timeout);

                msg = requestChannel.EndRequest(arRequest);
                Assert.AreEqual(200, msg.GetBody<int>());
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_QueuedBeforeReceive()
        {
            // Verify that ReplyChannel will handle receiving
            // messages before ReceiveRequest() is called after handling
            // a request.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult arRequest;
            IAsyncResult arReceive;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 100), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();

                ctx = replyChannel.ReceiveRequest(timeout);
                Assert.AreEqual(100, ctx.RequestMessage.GetBody<int>());
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "test", 200), timeout);

                msg = requestChannel.EndRequest(arRequest);
                Assert.AreEqual(200, msg.GetBody<int>());

                // This is the test

                arReceive = replyChannel.BeginTryReceiveRequest(timeout, null, null);
                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 300), timeout, null, null);

                Assert.IsTrue(replyChannel.EndTryReceiveRequest(arReceive, out ctx));
                Assert.AreEqual(300, ctx.RequestMessage.GetBody<int>());
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "test", 400), timeout);

                msg = requestChannel.EndRequest(arRequest);
                Assert.AreEqual(400, msg.GetBody<int>());
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_QueuedAfterReceive()
        {
            // Verify that ReplyChannel will handle receiving
            // messages after ReceiveRequest() is called after receiving
            // the initial accept message.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult arRequest;
            IAsyncResult arReceive;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 100), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();

                ctx = replyChannel.ReceiveRequest(timeout);
                Assert.AreEqual(100, ctx.RequestMessage.GetBody<int>());
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "test", 200), timeout);

                msg = requestChannel.EndRequest(arRequest);
                Assert.AreEqual(200, msg.GetBody<int>());

                // This is the test

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 300), timeout, null, null);
                Thread.Sleep(yieldTime);

                arReceive = replyChannel.BeginTryReceiveRequest(timeout, null, null);

                Assert.IsTrue(replyChannel.EndTryReceiveRequest(arReceive, out ctx));
                Assert.AreEqual(300, ctx.RequestMessage.GetBody<int>());
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "test", 400), timeout);

                msg = requestChannel.EndRequest(arRequest);
                Assert.AreEqual(400, msg.GetBody<int>());
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_TryReceive()
        {
            // Verify that TryReceiveRequest() works.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult arRequest;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 100), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();

                Assert.IsTrue(replyChannel.TryReceiveRequest(timeout, out ctx));
                Assert.AreEqual(100, ctx.RequestMessage.GetBody<int>());
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "test", 200), timeout);

                msg = requestChannel.EndRequest(arRequest);
                Assert.AreEqual(200, msg.GetBody<int>());
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_TryReceive_Timeout()
        {
            // Verify that TryReceiveRequest() throws a timeout.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult arRequest;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 100), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();

                Assert.IsTrue(replyChannel.TryReceiveRequest(timeout, out ctx));
                Assert.AreEqual(100, ctx.RequestMessage.GetBody<int>());
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "test", 200), timeout);

                msg = requestChannel.EndRequest(arRequest);
                Assert.AreEqual(200, msg.GetBody<int>());

                // Now that we have an accepted and open reply channel, intiate a
                // TryReceiveRequest() and wait for it to timeout.

                Assert.IsFalse(replyChannel.TryReceiveRequest(timeout, out ctx));
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_Wait_Receive()
        {
            // Verify that WaitForRequest() works.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult arRequest;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 100), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();

                Assert.IsTrue(replyChannel.WaitForRequest(timeout));
                Assert.IsTrue(replyChannel.TryReceiveRequest(timeout, out ctx));
                Assert.AreEqual(100, ctx.RequestMessage.GetBody<int>());
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "test", 200), timeout);

                msg = requestChannel.EndRequest(arRequest);
                Assert.AreEqual(200, msg.GetBody<int>());
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_WaitForRequest_Timeout()
        {
            // Verify that WaitForRequest() handles timeouts.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult arRequest;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 100), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();

                Assert.IsTrue(replyChannel.WaitForRequest(timeout));
                Assert.IsTrue(replyChannel.TryReceiveRequest(timeout, out ctx));
                Assert.AreEqual(100, ctx.RequestMessage.GetBody<int>());
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "test", 200), timeout);

                msg = requestChannel.EndRequest(arRequest);
                Assert.AreEqual(200, msg.GetBody<int>());

                // Now that we have an accepted and open reply channel, intiate a
                // WaitForRequest() and wait for it to timeout.

                Assert.IsFalse(replyChannel.WaitForRequest(timeout));
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_ReceiveRequest_Timeout()
        {
            // Verify that ReceiveRequest() handles timeouts.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult arRequest;
            RequestContext ctx;
            Message msg;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 100), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();

                Assert.IsTrue(replyChannel.WaitForRequest(timeout));
                Assert.IsTrue(replyChannel.TryReceiveRequest(timeout, out ctx));
                Assert.AreEqual(100, ctx.RequestMessage.GetBody<int>());
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "test", 200), timeout);

                msg = requestChannel.EndRequest(arRequest);
                Assert.AreEqual(200, msg.GetBody<int>());

                // Now that we have an accepted and open reply channel, intiate a
                // WaitForRequest() and wait for it to timeout.

                try
                {
                    replyChannel.ReceiveRequest(timeout);
                    Assert.Fail("Expected TimeoutException");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(TimeoutException));
                }
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_RequestChannel_ReplyTimeout()
        {
            // Verify that request channels will timeout if they
            // don't see a reply.

            IChannelFactory<IRequestChannel> factory = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;
            IAsyncResult arRequest;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 100), timeout, null, null);

                try
                {
                    requestChannel.EndRequest(arRequest);
                    Assert.Fail("Expected a TimeoutException");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(TimeoutException));
                }
            }
            finally
            {
                if (requestChannel != null)
                    requestChannel.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_RequestChannel_AbortRequest()
        {
            // Verify that request channels can abort transactions.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            int count = Math.Min(10, ServiceModelHelper.MaxAcceptedMessages);
            IAsyncResult[] arRequests = new IAsyncResult[count];
            LillTekBinding binding;
            RequestContext[] contexts = new RequestContext[count];

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                for (int i = 0; i < count; i++)
                    arRequests[i] = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", i), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();

                for (int i = 0; i < count; i++)
                {
                    Assert.IsTrue(replyChannel.WaitForRequest(timeout));
                    contexts[i] = replyChannel.ReceiveRequest(timeout);
                }

                for (int i = 0; i < count; i++)
                    contexts[i].Abort();

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        requestChannel.EndRequest(arRequests[i]);
                        Assert.Fail("Expected a CommunicationCanceledException");
                    }
                    catch (Exception e)
                    {
                        Assert.IsInstanceOfType(e, typeof(CommunicationCanceledException));
                    }
                }
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_RequestChannel_AbortRequestAfterClose()
        {
            // Verify that request channels abort any pending transactions
            // when the channel is aborted.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            int count = Math.Min(10, ServiceModelHelper.MaxAcceptedMessages);
            IAsyncResult[] arRequests = new IAsyncResult[count];
            LillTekBinding binding;
            RequestContext[] contexts = new RequestContext[count];

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                for (int i = 0; i < count; i++)
                    arRequests[i] = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", i), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();

                for (int i = 0; i < count; i++)
                {
                    Assert.IsTrue(replyChannel.WaitForRequest(timeout));
                    contexts[i] = replyChannel.ReceiveRequest(timeout);
                }

                replyChannel.Close();

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        requestChannel.EndRequest(arRequests[i]);
                        Assert.Fail("Expected a CommunicationCanceledException");
                    }
                    catch (Exception e)
                    {
                        Assert.IsInstanceOfType(e, typeof(CommunicationCanceledException));
                    }
                }
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_RequestChannel_AbortRequestAfterAbort()
        {
            // Verify that request channels abort any pending transactions
            // when the channel is aborted.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            int count = Math.Min(10, ServiceModelHelper.MaxAcceptedMessages);
            IAsyncResult[] arRequests = new IAsyncResult[count];
            LillTekBinding binding;
            RequestContext[] contexts = new RequestContext[count];

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                for (int i = 0; i < count; i++)
                    arRequests[i] = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", i), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();

                for (int i = 0; i < count; i++)
                {
                    Assert.IsTrue(replyChannel.WaitForRequest(timeout));
                    contexts[i] = replyChannel.ReceiveRequest(timeout);
                }

                replyChannel.Abort();

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        requestChannel.EndRequest(arRequests[i]);
                        Assert.Fail("Expected a CommunicationCanceledException");
                    }
                    catch (Exception e)
                    {
                        Assert.IsInstanceOfType(e, typeof(CommunicationCanceledException));
                    }
                }
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_RequestChannel_AbortQueuedRequestAfterClose()
        {
            // Verify that request channels abort any queued transactions
            // when the channel is aborted.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            IAsyncResult arRequest;
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 10), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();
                replyChannel.Close();

                listener.Close();   // The listener is actually handling this for non-session reply channels

                try
                {
                    requestChannel.EndRequest(arRequest);
                    Assert.Fail("Expected a CommunicationCanceledException");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(CommunicationCanceledException));
                }
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_RequestChannel_AbortQueuedRequestAfterAbort()
        {
            // Verify that request channels abort any pending transactions
            // when the channel is aborted.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            IAsyncResult arRequest;
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 10), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();
                replyChannel.Abort();

                listener.Abort();   // The listener is actually handling this for non-session reply channels

                try
                {
                    requestChannel.EndRequest(arRequest);
                    Assert.Fail("Expected a CommunicationCanceledException");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(CommunicationCanceledException));
                }
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_RequestChannelListener_AbortReplyAfterClose()
        {
            // Verify that request channel listeners abort any pending transactions
            // when the listener is closed.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            int count = Math.Min(10, ServiceModelHelper.MaxAcceptedMessages);
            IAsyncResult[] arRequests = new IAsyncResult[count];
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                for (int i = 0; i < count; i++)
                    arRequests[i] = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", i), timeout, null, null);

                Thread.Sleep(1000);     // Give the listener a chance to queue all of the requests

                listener.Close();
                listener = null;

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        requestChannel.EndRequest(arRequests[i]);
                        Assert.Fail("Expected a CommunicationCanceledException");
                    }
                    catch (Exception e)
                    {
                        Assert.IsInstanceOfType(e, typeof(CommunicationCanceledException));
                    }
                }
            }
            finally
            {
                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_RequestChannelListener_AbortReplyAfterAbort()
        {
            // Verify that request channel listeners abort any pending transactions
            // when the listener is aborted.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            int count = Math.Min(10, ServiceModelHelper.MaxAcceptedMessages);
            IAsyncResult[] arRequests = new IAsyncResult[count];
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                for (int i = 0; i < count; i++)
                    arRequests[i] = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", i), timeout, null, null);

                Thread.Sleep(1000);     // Give the listener a chance to queue all of the requests

                listener.Abort();
                listener = null;

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        requestChannel.EndRequest(arRequests[i]);
                        Assert.Fail("Expected a CommunicationCanceledException");
                    }
                    catch (Exception e)
                    {
                        Assert.IsInstanceOfType(e, typeof(CommunicationCanceledException));
                    }
                }
            }
            finally
            {
                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_RequestChannelListener_RequestQueueTimeout()
        {
            // Verify that request channel listeners abort any received requests
            // if they remain in the listener's queue too long without being
            // picked up by a reply channel for processing.  This should
            // result in a CommunicationCanceledException being thrown on the client.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            IAsyncResult arRequest;
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 10), timeout, null, null);

                try
                {
                    requestChannel.EndRequest(arRequest);
                    Assert.Fail("Expected a CommunicationCanceledException");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(CommunicationCanceledException));
                }
            }
            finally
            {
                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_RequestChannelListener_RequestQueueMax()
        {
            // Verify that request channel listeners abort any requests that
            // are bumped from the listener's receive queue because the
            // queue has reached its maximum size.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            IAsyncResult arBumpRequest;
            IAsyncResult[] arRequests = new IAsyncResult[ServiceModelHelper.MaxAcceptedMessages];
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                // Initiate the request that will get bumped, and wait enough
                // time for it to be placed at the front of the queue.

                arBumpRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", -1), timeout, null, null);
                Thread.Sleep(500);

                // Initiate just enough additional requests to cause the first one to be
                // bumped from the queue.

                for (int i = 0; i < ServiceModelHelper.MaxAcceptedMessages; i++)
                    arRequests[i] = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", i), timeout, null, null);

                try
                {
                    // The first request should be bumped

                    requestChannel.EndRequest(arBumpRequest);
                    Assert.Fail("Expected a CommunicationCanceledException");
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(CommunicationCanceledException));
                }
                finally
                {
                    // Complete the queued requests

                    listener.Close();
                    listener = null;

                    for (int i = 0; i < ServiceModelHelper.MaxAcceptedMessages; i++)
                    {
                        try
                        {
                            requestChannel.EndRequest(arRequests[i]);
                        }
                        catch
                        {
                            // Ignore
                        }
                    }
                }
            }
            finally
            {
                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_Verify_NoSession()
        {
            // Verify that we're not seeing session behavior by creating one listener
            // and two requests channels.  Requests sent by each request channel should
            // be received by a single reply channel instance.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel1 = null;
            IRequestChannel requestChannel2 = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            Message reply1;
            Message reply2;
            LillTekBinding binding;
            IAsyncResult arRequest1;
            IAsyncResult arRequest2;
            RequestContext ctx1;
            RequestContext ctx2;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel1 = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel1.Open();

                requestChannel2 = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel2.Open();

                arRequest1 = requestChannel1.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 10), timeout, null, null);
                arRequest2 = requestChannel2.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 10), timeout, null, null);

                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();

                ctx1 = replyChannel.ReceiveRequest(timeout);
                Assert.AreEqual(10, ctx1.RequestMessage.GetBody<int>());
                ctx1.Reply(Message.CreateMessage(MessageVersion.Default, "test", 20));

                ctx2 = replyChannel.ReceiveRequest(timeout);
                Assert.AreEqual(10, ctx2.RequestMessage.GetBody<int>());
                ctx2.Reply(Message.CreateMessage(MessageVersion.Default, "test", 20));

                reply1 = requestChannel1.EndRequest(arRequest1);
                reply2 = requestChannel2.EndRequest(arRequest2);

                Assert.AreEqual(20, reply1.GetBody<int>());
                Assert.AreEqual(20, reply2.GetBody<int>());
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (requestChannel1 != null)
                    requestChannel1.Close();

                if (requestChannel2 != null)
                    requestChannel2.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_WaitForChannel_Timeout()
        {
            // Verify that the channel listener will timeout WaitForChannel().

            IChannelListener<IReplyChannel> listener = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            LillTekBinding binding;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
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
        public void RequestReply_WaitForChannelAndAccept()
        {
            // Verify that WaitForChannel() followed by AcceptChannel() works.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            RequestContext ctx;
            Message msg;
            LillTekBinding binding;
            IAsyncResult arRequest;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                for (int i = 0; i < 1000; i++)
                {
                    arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", i), timeout, null, null);

                    Assert.IsTrue(listener.WaitForChannel(timeout));
                    replyChannel = listener.AcceptChannel(timeout);
                    replyChannel.Open();

                    ctx = replyChannel.ReceiveRequest(timeout);
                    Assert.AreEqual(i, ctx.RequestMessage.GetBody<int>());
                    ctx.Reply((Message.CreateMessage(MessageVersion.Default, "test", i * 100)));

                    msg = requestChannel.EndRequest(arRequest);
                    Assert.AreEqual(i * 100, msg.GetBody<int>());

                    replyChannel.Close();
                    replyChannel = null;
                }
            }
            finally
            {
                if (requestChannel != null)
                    requestChannel.Close();

                if (replyChannel != null)
                    replyChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void RequestReply_WaitForChannel_Multiple()
        {
            // Verify that when we have multiple pending WaitForChannels(),
            // they are completed one-by-one as messages are received.

            IChannelFactory<IRequestChannel> factory = null;
            IChannelListener<IReplyChannel> listener = null;
            IReplyChannel replyChannel = null;
            IRequestChannel requestChannel = null;
            Uri uri = ServiceModelHelper.CreateUniqueUri();
            Message msg;
            LillTekBinding binding;
            IAsyncResult arWait1;
            IAsyncResult arWait2;
            IAsyncResult arRequest;
            RequestContext ctx;

            try
            {
                ChannelHost.Start(Assembly.GetExecutingAssembly());

                binding = new LillTekBinding(uri);

                factory = binding.BuildChannelFactory<IRequestChannel>();
                factory.Open();

                listener = binding.BuildChannelListener<IReplyChannel>(uri);
                listener.Open();

                requestChannel = factory.CreateChannel(new EndpointAddress(uri));
                requestChannel.Open();

                arWait1 = listener.BeginWaitForChannel(timeout, null, null);
                arWait2 = listener.BeginWaitForChannel(timeout, null, null);

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 10), timeout, null, null);
                Assert.IsTrue(listener.EndWaitForChannel(arWait1));
                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();
                ctx = replyChannel.ReceiveRequest(timeout);
                Assert.AreEqual(10, ctx.RequestMessage.GetBody<int>());
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "test", 100));
                replyChannel.Close();
                replyChannel = null;
                msg = requestChannel.EndRequest(arRequest);
                Assert.AreEqual(100, msg.GetBody<int>());

                arRequest = requestChannel.BeginRequest(Message.CreateMessage(MessageVersion.Default, "test", 20), timeout, null, null);
                Assert.IsTrue(listener.EndWaitForChannel(arWait2));
                replyChannel = listener.AcceptChannel(timeout);
                replyChannel.Open();
                ctx = replyChannel.ReceiveRequest(timeout);
                Assert.AreEqual(20, ctx.RequestMessage.GetBody<int>());
                ctx.Reply(Message.CreateMessage(MessageVersion.Default, "test", 200));
                replyChannel.Close();
                replyChannel = null;
                msg = requestChannel.EndRequest(arRequest);
                Assert.AreEqual(200, msg.GetBody<int>());
            }
            finally
            {
                if (replyChannel != null)
                    replyChannel.Close();

                if (requestChannel != null)
                    requestChannel.Close();

                if (listener != null)
                    listener.Close();

                if (factory != null)
                    factory.Close();

                ChannelHost.Stop();
            }
        }
    }
}

