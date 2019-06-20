//-----------------------------------------------------------------------------
// FILE:        _HttpConnection.cs
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
    public class _HttpConnection
    {
        private const int ServerPort = 5050;

        // Returns a response whose content is set to the value of the request's
        // "Response" header.  The module will close the connection if the
        // "Close" header is present in the request and set to "yes".

        private class TestModule : IHttpModule
        {
            public HttpResponse OnRequest(HttpServer server, HttpRequest request, bool newCon, out bool close)
            {
                HttpResponse response = new HttpResponse(HttpStatus.OK, "OK");
                BlockStream bs = new BlockStream(0, 4096);
                TextWriter writer = new StreamWriter(bs, Encoding.ASCII);

                writer.Write(request["Response"]);
                writer.Flush();

                response.Content = bs.ToBlocks(true);
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
        public void HttpConnection_Query_EndPoint()
        {
            HttpServer server;
            HttpConnection con;
            HttpRequest request;
            HttpResponse response;
            string content;

            server = new HttpServer(new IPEndPoint[] { new IPEndPoint(IPAddress.Any, ServerPort) }, new IHttpModule[] { new TestModule() }, 5, 100, int.MaxValue);
            server.Start();

            try
            {
                con = new HttpConnection(HttpOption.None);
                con.Connect(new IPEndPoint(IPAddress.Loopback, ServerPort));

                for (int i = 0; i < 10; i++)
                {
                    content = "Test: " + i.ToString();
                    request = new HttpRequest("GET", "/foo.htm", null);
                    request["Response"] = content;
                    request["Close"] = "no";

                    response = con.Query(request, DateTime.MaxValue);
                    CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(content), response.Content.ToByteArray());
                }

                con.Close();
            }
            finally
            {
                server.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpConnection_Query_HostPort()
        {
            HttpServer server;
            HttpConnection con;
            HttpRequest request;
            HttpResponse response;
            string content;

            server = new HttpServer(new IPEndPoint[] { new IPEndPoint(IPAddress.Any, ServerPort) }, new IHttpModule[] { new TestModule() }, 5, 100, int.MaxValue);
            server.Start();

            try
            {
                con = new HttpConnection(HttpOption.None);
                con.Connect("localhost", ServerPort);

                for (int i = 0; i < 10; i++)
                {
                    content = "Test: " + i.ToString();
                    request = new HttpRequest("GET", "/foo.htm", null);
                    request["Response"] = content;
                    request["Close"] = "no";

                    response = con.Query(request, DateTime.MaxValue);
                    CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(content), response.Content.ToByteArray());
                }

                con.Close();
            }
            finally
            {
                server.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpConnection_Query_Uri()
        {
            HttpServer server;
            HttpConnection con;
            HttpRequest request;
            HttpResponse response;
            string content;

            server = new HttpServer(new IPEndPoint[] { new IPEndPoint(IPAddress.Any, ServerPort) }, new IHttpModule[] { new TestModule() }, 5, 100, int.MaxValue);
            server.Start();

            try
            {
                con = new HttpConnection(HttpOption.None);
                con.Connect("http://localhost:" + ServerPort.ToString());

                for (int i = 0; i < 10; i++)
                {
                    content = "Test: " + i.ToString();
                    request = new HttpRequest("GET", "/foo.htm", null);
                    request["Response"] = content;
                    request["Close"] = "no";

                    response = con.Query(request, DateTime.MaxValue);
                    CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(content), response.Content.ToByteArray());
                }

                con.Close();
            }
            finally
            {
                server.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpConnection_Query_Timeout()
        {
            EnhancedSocket sockListen = null;
            EnhancedSocket sockAccept = null;
            HttpConnection con;
            HttpRequest request;
            HttpResponse response;
            string content;
            TimeSpan orgTimeout;
            IAsyncResult ar;

            orgTimeout = HttpStack.TimeoutSweepInterval;
            HttpStack.TimeoutSweepInterval = TimeSpan.FromMilliseconds(250);

            sockListen = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sockListen.Bind(new IPEndPoint(IPAddress.Any, ServerPort));
            sockListen.Listen(10);

            try
            {
                ar = sockListen.BeginAccept(null, null);

                con = new HttpConnection(HttpOption.None);
                con.Connect("http://localhost:" + ServerPort.ToString());

                sockAccept = sockListen.EndAccept(ar);

                content = "Test: Timeout";
                request = new HttpRequest("GET", "/foo.htm", null);
                request["Response"] = content;
                request["Close"] = "no";

                try
                {
                    response = con.Query(request, SysTime.Now + TimeSpan.FromMilliseconds(250));
                    Thread.Sleep(1000);
                    Assert.Fail();
                }
                catch (TimeoutException)
                {
                }

                Assert.IsTrue(con.IsClosed);
                con.Close();
            }
            finally
            {
                HttpStack.TimeoutSweepInterval = orgTimeout;

                if (sockListen != null)
                    sockListen.Close();

                if (sockAccept != null)
                    sockAccept.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpConnection_Connect_Fail()
        {
            HttpConnection con;

            try
            {
                con = new HttpConnection(HttpOption.None);
                con.Connect("http://foobar_error:" + ServerPort.ToString());
            }
            catch
            {
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpConnection_Send_Fail()
        {
            EnhancedSocket sockListen = null;
            EnhancedSocket sockAccept = null;
            HttpConnection con;
            HttpRequest request;
            HttpResponse response;
            TimeSpan orgTimeout;
            IAsyncResult ar;

            orgTimeout = HttpStack.TimeoutSweepInterval;
            HttpStack.TimeoutSweepInterval = TimeSpan.FromMilliseconds(250);

            sockListen = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sockListen.Bind(new IPEndPoint(IPAddress.Any, ServerPort));
            sockListen.Listen(10);

            try
            {
                ar = sockListen.BeginAccept(null, null);

                con = new HttpConnection(HttpOption.None);
                con.Connect("http://localhost:" + ServerPort.ToString());

                sockAccept = sockListen.EndAccept(ar);

                request = new HttpRequest("GET", "/foo.htm", null);
                request.Content = new BlockArray(new byte[100000]);
                request["Content-Length"] = request.Content.Size.ToString();

                ar = con.BeginQuery(request, DateTime.MaxValue, null, null);
                sockAccept.Close();
                sockAccept = null;

                try
                {
                    response = con.EndQuery(ar);
                    Assert.Fail();
                }
                catch
                {
                }

                Assert.IsTrue(con.IsClosed);
                con.Close();
            }
            finally
            {
                HttpStack.TimeoutSweepInterval = orgTimeout;

                if (sockListen != null)
                    sockListen.Close();

                if (sockAccept != null)
                    sockAccept.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpConnection_Receive_Fail()
        {
            EnhancedSocket sockListen = null;
            EnhancedSocket sockAccept = null;
            HttpConnection con;
            HttpRequest request;
            HttpResponse response;
            TimeSpan orgTimeout;
            IAsyncResult ar;

            orgTimeout = HttpStack.TimeoutSweepInterval;
            HttpStack.TimeoutSweepInterval = TimeSpan.FromMilliseconds(250);

            sockListen = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sockListen.Bind(new IPEndPoint(IPAddress.Any, ServerPort));
            sockListen.Listen(10);

            try
            {
                ar = sockListen.BeginAccept(null, null);

                con = new HttpConnection(HttpOption.None);
                con.Connect("http://localhost:" + ServerPort.ToString());

                sockAccept = sockListen.EndAccept(ar);

                request = new HttpRequest("GET", "/foo.htm", null);
                request["Content-Length"] = request.Content.Size.ToString();

                ar = con.BeginQuery(request, DateTime.MaxValue, null, null);
                Thread.Sleep(100);

                sockAccept.Close();
                sockAccept = null;

                try
                {
                    response = con.EndQuery(ar);
                    Assert.Fail();
                }
                catch
                {
                }

                Assert.IsTrue(con.IsClosed);
                con.Close();
            }
            finally
            {
                HttpStack.TimeoutSweepInterval = orgTimeout;

                if (sockListen != null)
                    sockListen.Close();

                if (sockAccept != null)
                    sockAccept.Close();
            }
        }
    }
}

