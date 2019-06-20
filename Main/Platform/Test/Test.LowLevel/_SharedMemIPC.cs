//-----------------------------------------------------------------------------
// FILE:        _SharedMemIPC.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.LowLevel;
using LillTek.Testing;
using LillTek.Windows;

namespace LillTek.LowLevel.Test
{
    [TestClass]
    public class _SharedMemICP
    {
        private class RequestMessage : SharedMemMessage
        {
            public RequestMessage()
            {
            }

            public override int TypeCode
            {
                get { return 1; }
            }

            public override void ReadFrom(EnhancedStream input)
            {
                this.Arg = input.ReadString32();
            }

            public override void WriteTo(EnhancedStream output)
            {
                output.WriteString32(Arg);
            }

            public string Arg { get; set; }
        }

        private class ResponseMessage : SharedMemMessage
        {
            public ResponseMessage()
            {
            }

            public override int TypeCode
            {
                get { return 2; }
            }

            public override void ReadFrom(EnhancedStream input)
            {
                this.Result = input.ReadString32();
            }

            public override void WriteTo(EnhancedStream output)
            {
                output.WriteString32(Result);
            }

            public string Result { get; set; }
        }

        private class MessageFactory : ISharedMemMessageFactory
        {
            public SharedMemMessage Create(int typeCode)
            {
                switch (typeCode)
                {
                    case 1:     return new RequestMessage();
                    case 2:     return new ResponseMessage();
                    default:    throw new ArgumentException();
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.LowLevel")]
        public async Task SharedMemIPC_Basic()
        {
            // Verify basic client/server IPC.

            SharedMemServer<MessageFactory> server = null;
            SharedMemClient<MessageFactory> client = null;

            try
            {
                // Configure a test server that echos the argument passed.

                server = new SharedMemServer<MessageFactory>("test-server",
                    rawRequest =>
                    {
                        var request = (RequestMessage)rawRequest;

                        return new ResponseMessage() { Result = request.Arg };
                    });

                // Configure the client.

                client = new SharedMemClient<MessageFactory>("test-server");

                // Verify that we can handle query/response end-to-end.

                var response = await client.CallAsync<ResponseMessage>(new RequestMessage() { Arg = "Hello World!" });

                Assert.AreEqual("Hello World!", response.Result);
            }
            finally
            {
                if (server != null)
                {
                    server.Dispose();
                }

                if (client != null)
                {
                    client.Dispose();
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.LowLevel")]
        public void SharedMemIPC_Parallel()
        {
            // Verify multiple client/server IPC calls happening in parallel.

            SharedMemServer<MessageFactory> server = null;
            SharedMemClient<MessageFactory> client = null;

            try
            {
                // Configure a test server that echos the argument passed.

                server = new SharedMemServer<MessageFactory>("test-server",
                    rawRequest =>
                    {
                        var request = (RequestMessage)rawRequest;

                        return new ResponseMessage() { Result = request.Arg };
                    });

                // Configure the client.

                client = new SharedMemClient<MessageFactory>("test-server");

                // Peform the parallel operations.

                var count = 50000;
                var tasks = new Task<ResponseMessage>[count];

                for (int i = 0; i < count; i++)
                {
                    tasks[i] = client.CallAsync<ResponseMessage>(new RequestMessage() { Arg = i.ToString() });
                }

                Task.WaitAll(tasks, TimeSpan.FromSeconds(10));

                for (int i = 0; i < count; i++)
                {
                    var response = (ResponseMessage)tasks[i].Result;
                    Assert.AreEqual<string>(i.ToString(), response.Result);
                }
            }
            finally
            {
                if (server != null)
                {
                    server.Dispose();
                }

                if (client != null)
                {
                    client.Dispose();
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.LowLevel")]
        public async Task SharedMemIPC_ServerException()
        {
            // Verify that exceptions thrown by the server are rethrown by the client.

            SharedMemServer<MessageFactory> server = null;
            SharedMemClient<MessageFactory> client = null;

            try
            {
                // Configure a test server that echos the argument passed.

                server = new SharedMemServer<MessageFactory>("test-server",
                    rawRequest =>
                    {
                        throw new ArgumentException("Bad arg");
                    });

                // Configure the client.

                client = new SharedMemClient<MessageFactory>("test-server");

                // Make the call.

                try
                {
                    await client.CallAsync(new RequestMessage());
                    Assert.Fail("Expected a [{0}].", typeof(SharedMemException).FullName);
                }
                catch (SharedMemException e)
                {
                    Assert.AreEqual("System.ArgumentException: Bad arg", e.Message);
                }
                catch
                {
                    Assert.Fail("Expected a [{0}].", typeof(SharedMemException).FullName);
                }
            }
            finally
            {
                if (server != null)
                {
                    server.Dispose();
                }

                if (client != null)
                {
                    client.Dispose();
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.LowLevel")]
        public async Task SharedMemIPC_Timeout()
        {
            // Verify that the client implements timeout.

            SharedMemServer<MessageFactory> server = null;
            SharedMemClient<MessageFactory> client = null;

            try
            {
                // Configure a test server that can delay for 5 seconds before returning.

                server = new SharedMemServer<MessageFactory>("test-server",
                    rawRequest =>
                    {
                        var request = (RequestMessage)rawRequest;

                        if (request.Arg == "DELAY")
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(5));
                        }

                        return new ResponseMessage() { Result = request.Arg };
                    });

                // Configure the client.

                client = new SharedMemClient<MessageFactory>("test-server");

                // Verify that we get a timeout.

                await ExtendedAssert.ThrowsAsync<TimeoutException>(
                    async () => await client.CallAsync<ResponseMessage>(new RequestMessage() { Arg = "DELAY" }, TimeSpan.FromSeconds(1)));

                // Verify that regular requests still work.

                var response = await client.CallAsync<ResponseMessage>(new RequestMessage() { Arg = "NODELAY" }, TimeSpan.FromSeconds(1));

                Assert.AreEqual("NODELAY", response.Result);
            }
            finally
            {
                if (server != null)
                {
                    server.Dispose();
                }

                if (client != null)
                {
                    client.Dispose();
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.LowLevel")]
        public async Task SharedMemIPC_Performance()
        {
            // Measure calls per second.

            SharedMemServer<MessageFactory> server = null;
            SharedMemClient<MessageFactory> client = null;

            try
            {
                // Configure a test server that echos the argument passed.

                server = new SharedMemServer<MessageFactory>("test-server",
                    rawRequest =>
                    {
                        var request = (RequestMessage)rawRequest;

                        return new ResponseMessage() { Result = request.Arg };
                    });

                // Configure the client.

                client = new SharedMemClient<MessageFactory>("test-server");

                // Peform the operations.

                var count     = 10000;
                var stopwatch = new Stopwatch();

                stopwatch.Start();

                for (int i = 0; i < count; i++)
                {
                    await client.CallAsync(new RequestMessage() { Arg = "Test" });
                }

                stopwatch.Stop();

                var callsPerSecond = count / stopwatch.Elapsed.TotalSeconds;
                var msPerCall      = stopwatch.Elapsed.TotalMilliseconds / count;

                Debug.WriteLine("Calls/sec:         {0}", callsPerSecond);
                Debug.WriteLine("Milliseconds/call: {0}", msPerCall);
            }
            finally
            {
                if (server != null)
                {
                    server.Dispose();
                }

                if (client != null)
                {
                    client.Dispose();
                }
            }
        }
    }
}
