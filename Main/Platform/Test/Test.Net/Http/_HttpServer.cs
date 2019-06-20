//-----------------------------------------------------------------------------
// FILE:        _HttpServer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Http;
using LillTek.Net.Sockets;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Net.Http.Test
{
    [TestClass]
    public class _HttpServer
    {
        private const int ServerPort = 5050;

        // Returns a response whose content is set to the value of the request's
        // content.  The module will close the connection if the
        // "Close" header is present in the request and set to "yes".

        private class TestModule : IHttpModule
        {
            public HttpResponse OnRequest(HttpServer server, HttpRequest request, bool newCon, out bool close)
            {
                HttpResponse response = new HttpResponse(HttpStatus.OK, "OK");

                response.Content = request.Content.Clone();
                server.AddDefaultHeaders(response);

                close = request["Close"] != null && request["Close"].ToUpper() == "YES";
                return response;
            }
        }

        [TestInitialize]
        public void Initialize()
        {
            AsyncTracker.Enable = false;
            AsyncTracker.Start();
        }

        [TestCleanup]
        public void Cleanup()
        {
            AsyncTracker.Stop();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpServer_QueryResponse_NoClose()
        {
            HttpServer server;
            HttpRequest request;
            HttpResponse response;
            BlockArray blocks;
            byte[] buf;
            int cb;
            EnhancedSocket sock;
            IAsyncResult ar;

            server = new HttpServer(new IPEndPoint[] { new IPEndPoint(IPAddress.Any, ServerPort) }, new IHttpModule[] { new TestModule() }, 5, 100, int.MaxValue);
            server.Start();

            try
            {
                request = new HttpRequest("GET", "/foo.htm", null);
                request["Close"] = "no";
                request.Content = new BlockArray(Encoding.ASCII.GetBytes("abcd"));
                blocks = request.Serialize(4096);

                sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.Connect("localhost", ServerPort);
                Thread.Sleep(100);
                Assert.AreEqual(1, server.ConnectionCount);

                for (int i = 0; i < 10; i++)
                {
                    ar = sock.BeginSendAll(blocks, SocketFlags.None, null, null);
                    sock.EndSendAll(ar);

                    response = new HttpResponse();
                    response.BeginParse();

                    buf = new byte[4096];
                    cb = sock.Receive(buf, buf.Length, SocketFlags.None);

                    Assert.IsTrue(response.Parse(buf, cb));
                    response.EndParse();

                    CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("abcd"), response.Content.ToByteArray());
                }

                sock.Close();
            }
            finally
            {
                server.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpServer_QueryResponse_Close()
        {
            HttpServer server;
            HttpRequest request;
            HttpResponse response;
            BlockArray blocks;
            byte[] buf;
            int cb;
            EnhancedSocket sock;
            IAsyncResult ar;

            server = new HttpServer(new IPEndPoint[] { new IPEndPoint(IPAddress.Any, ServerPort) }, new IHttpModule[] { new TestModule() }, 5, 100, int.MaxValue);
            server.Start();

            try
            {
                request = new HttpRequest("GET", "/foo.htm", null);
                request.Content = new BlockArray(Encoding.ASCII.GetBytes("abcd"));
                request["Close"] = "yes";
                blocks = request.Serialize(4096);

                sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.Connect("localhost", ServerPort);
                Thread.Sleep(100);
                Assert.AreEqual(1, server.ConnectionCount);

                ar = sock.BeginSendAll(blocks, SocketFlags.None, null, null);
                sock.EndSendAll(ar);

                response = new HttpResponse();
                response.BeginParse();

                buf = new byte[4096];
                cb = sock.Receive(buf, buf.Length, SocketFlags.None);

                Assert.IsTrue(response.Parse(buf, cb));
                response.EndParse();

                CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("abcd"), response.Content.ToByteArray());

                buf = new byte[4096];
                cb = sock.Receive(buf, buf.Length, SocketFlags.None);
                Assert.AreEqual(0, cb);

                sock.Close();
            }
            finally
            {
                server.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpServer_MaxQuerySize()
        {
            HttpServer server;
            HttpRequest request;
            HttpResponse response;
            BlockArray blocks;
            byte[] buf;
            int cb;
            EnhancedSocket sock;
            IAsyncResult ar;

            server = new HttpServer(new IPEndPoint[] { new IPEndPoint(IPAddress.Any, ServerPort) }, new IHttpModule[] { new TestModule() }, 5, 100, 200);
            server.Start();

            try
            {
                request = new HttpRequest("PUT", "/foo.htm", null);
                request.Content = new BlockArray(500);
                request["Response"] = "abcd";
                request["Close"] = "yes";
                request["Content-Length"] = request.Content.Size.ToString();
                blocks = request.Serialize(4096);

                sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.Connect("localhost", ServerPort);
                Thread.Sleep(100);
                Assert.AreEqual(1, server.ConnectionCount);

                try
                {
                    ar = sock.BeginSendAll(blocks, SocketFlags.None, null, null);
                    sock.EndSendAll(ar);
                }
                catch
                {
                }

                response = new HttpResponse();
                response.BeginParse();

                buf = new byte[4096];
                cb = sock.Receive(buf, buf.Length, SocketFlags.None);

                Assert.IsTrue(response.Parse(buf, cb));
                response.EndParse();

                Assert.AreEqual(HttpStatus.RequestEntityTooLarge, response.Status);
                sock.Close();
            }
            finally
            {
                server.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpServer_MaxConnections()
        {
            HttpServer server;
            HttpRequest request;
            HttpResponse response;
            BlockArray blocks;
            byte[] buf;
            int cb;
            EnhancedSocket sock1, sock2;
            IAsyncResult ar;

            server = new HttpServer(new IPEndPoint[] { new IPEndPoint(IPAddress.Any, ServerPort) }, new IHttpModule[] { new TestModule() }, 5, 1, int.MaxValue);
            server.Start();

            try
            {
                request = new HttpRequest("PUT", "/foo.htm", null);
                request.Content = new BlockArray(0);
                request["Response"] = "abcd";
                request["Close"] = "yes";
                request["Content-Length"] = request.Content.Size.ToString();
                blocks = request.Serialize(4096);

                sock1 = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock1.Connect("localhost", ServerPort);
                Thread.Sleep(100);
                Assert.AreEqual(1, server.ConnectionCount);

                sock2 = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock2.Connect("localhost", ServerPort);
                Thread.Sleep(100);
                Assert.AreEqual(1, server.ConnectionCount);

                try
                {
                    ar = sock2.BeginSendAll(blocks, SocketFlags.None, null, null);
                    sock2.EndSendAll(ar);
                }
                catch
                {
                }

                response = new HttpResponse();
                response.BeginParse();

                buf = new byte[4096];
                cb = sock2.Receive(buf, buf.Length, SocketFlags.None);

                Assert.IsTrue(response.Parse(buf, cb));
                response.EndParse();

                Assert.AreEqual(HttpStatus.ServiceUnavailable, response.Status);
                sock1.Close();
                sock2.Close();
            }
            finally
            {
                server.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpServer_SweepIdle()
        {
            HttpServer server;
            HttpRequest request;
            BlockArray blocks;
            EnhancedSocket sock;

            server = new HttpServer(new IPEndPoint[] { new IPEndPoint(IPAddress.Any, ServerPort) }, new IHttpModule[] { new TestModule() }, 5, 100, int.MaxValue);
            server.Start();

            try
            {
                request = new HttpRequest("GET", "/foo.htm", null);
                request["Response"] = "abcd";
                request["Close"] = "yes";
                blocks = request.Serialize(4096);

                sock = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.Connect("localhost", ServerPort);
                Thread.Sleep(100);
                Assert.AreEqual(1, server.ConnectionCount);
                Thread.Sleep(1000);
                server.SweepIdle(TimeSpan.FromMilliseconds(500));
                Assert.AreEqual(0, server.ConnectionCount);

                sock.Close();
            }
            finally
            {
                server.Stop();
            }
        }

        private class FunModule : IHttpModule
        {
            public HttpResponse OnRequest(HttpServer server, HttpRequest request, bool newCon, out bool close)
            {
                HttpResponse response = new HttpResponse(HttpStatus.OK, "OK");
                BlockStream bs = new BlockStream(0, 4096);
                TextWriter writer = new StreamWriter(bs, Encoding.ASCII);

                writer.WriteLine("<html>");
                writer.WriteLine("<head><title>Hello World!</title></head>");
                writer.WriteLine("<body>");
                writer.WriteLine("<h1>Hello World!</h1>");
                writer.WriteLine("</body>");
                writer.WriteLine("</html>");
                writer.Flush();

                response.Content = bs.ToBlocks(true);
                server.AddDefaultHeaders(response);
                response["Content-Type"] = "text/html";

                close = true;
                return response;
            }
        }

        // This is a fun test that implements a very simple web server
        // on port ServerPort.  This should not be part of a normal test suite
        // since in runs forever and will hang the test.
        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpServer_HelloWorld()
        {
#if !FALSE
            // Assert.Ignore("Disabled");
#else
            HttpServer      server;

            server = new HttpServer(new IPEndPoint[] {new IPEndPoint(IPAddress.Any,ServerPort)},5,new IHttpModule[] {new FunModule()});
            server.Start();

            while (true)
                Thread.Sleep(1000);
#endif
        }
    }
}

