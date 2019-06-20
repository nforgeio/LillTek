//-----------------------------------------------------------------------------
// FILE:        _HeartbeatHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.Messaging;
using LillTek.Net.Http;
using LillTek.Net.Sockets;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Server.Test
{
    [TestClass]
    public class _HeartbeatHandler : IHttpModule
    {
        private Dictionary<string, bool> host2Health;    // Maps host names to health status results

        /// <summary>
        /// Simulates the target services.
        /// </summary>
        /// <param name="server"></param>
        /// <param name="request"></param>
        /// <param name="firstRequest"></param>
        /// <param name="close"></param>
        /// <returns></returns>
        public HttpResponse OnRequest(HttpServer server, HttpRequest request, bool firstRequest, out bool close)
        {
            close = true;

            if (host2Health[request.Uri.Host])
                return new HttpResponse(HttpStatus.OK, "Server is healthy");
            else
                return new HttpResponse(HttpStatus.ServiceUnavailable, "Service is unhealthy");
        }

        /// <summary>
        /// Submits a HTTP request to the heartbeat service to get an indication 
        /// of whether the monitored services are healthy or not.
        /// </summary>
        /// <returns><c>true</c> if the services are healthy.</returns>
        private bool GetHealth()
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(string.Format("http://localhost:{0}/", NetworkPort.HttpHeartbeat));
            HttpWebResponse response;

            try
            {
                response = (HttpWebResponse)request.GetResponse();
                return response.StatusCode == HttpStatusCode.OK;
            }
            catch (WebException)
            {
                return false;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void HeartbeatHandler_SingleService()
        {
            // Configure a heartbeart service to monitor a single URI and verify
            // that it can detect health and failure.

            HeartbeatHandler handler = null;
            HttpServer httpServer = null;
            const string cfg = @"

&section LillTek.Datacenter.HeartbeatService

    NetworkBinding = ANY:HTTP-HEARTBEAT
    Service[-]     = http://testservice01.lilltek.com:666/Heartbeat.aspx?global=0
    PollInterval   = 1s
    BkInterval     = 1s

&endsection
";
            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                // Initialize the health status dictionary and crank up a HTTP listerner
                // to simulate target services.

                host2Health = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                host2Health["testservice01.lilltek.com"] = true;

                httpServer = new HttpServer(new IPEndPoint[] { new IPEndPoint(IPAddress.Any, 666) }, new IHttpModule[] { this }, 10, 10, 8196);
                httpServer.Start();

                // Start the heartbeat handler

                handler = new HeartbeatHandler();
                handler.Start(null, null, null, null);

                // Give the service a chance to ping the test a few times and then 
                // make a request to the heartbeat handler to verify that it indicates
                // that the server is healthy.

                Thread.Sleep(3000);
                Assert.IsTrue(GetHealth());

                // Now indicate that the target service is unhealthy, give the heartbeat
                // handler a chance to ping the service, and verify that it determined
                // the server is unhealthy.

                host2Health["testservice01.lilltek.com"] = false;
                Thread.Sleep(3000);
                Assert.IsFalse(GetHealth());

                // Now indicate that the target service is healthy, give the heartbeat
                // handler a chance to ping the service, and verify that it determined
                // the server is healthy.

                host2Health["testservice01.lilltek.com"] = true;
                Thread.Sleep(3000);
                Assert.IsTrue(GetHealth());

                // Stop the simulated target HTTP server and verify that the handler
                // detects the failure.

                httpServer.Stop();
                httpServer = null;
                Thread.Sleep(3000);
                Assert.IsFalse(GetHealth());
            }
            finally
            {
                Config.SetConfig(null);

                if (handler != null)
                    handler.Stop();

                if (httpServer != null)
                {
                    httpServer.Stop();
                    httpServer = null;
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void HeartbeatHandler_MultipleServices()
        {
            // Configure a heartbeart service to monitor multiple URIs and verify
            // that it can detect health and failure.

            HeartbeatHandler handler = null;
            HttpServer httpServer = null;
            const string cfg = @"

&section LillTek.Datacenter.HeartbeatService

    NetworkBinding = ANY:HTTP-HEARTBEAT
    Service[-]     = http://testservice01.lilltek.com:666/Heartbeat.aspx?global=0
    Service[-]     = http://testservice02.lilltek.com:666/Heartbeat.aspx?global=0
    Service[-]     = http://testservice03.lilltek.com:666/Heartbeat.aspx?global=0
    PollInterval   = 1s
    BkInterval     = 1s

&endsection
";
            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                // Initialize the health status dictionary and crank up a HTTP listerner
                // to simulate target services.

                host2Health = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                host2Health["testservice01.lilltek.com"] = true;
                host2Health["testservice02.lilltek.com"] = true;
                host2Health["testservice03.lilltek.com"] = true;

                httpServer = new HttpServer(new IPEndPoint[] { new IPEndPoint(IPAddress.Any, 666) }, new IHttpModule[] { this }, 10, 10, 8196);
                httpServer.Start();

                // Start the heartbeat handler

                handler = new HeartbeatHandler();
                handler.Start(null, null, null, null);

                // Give the service a chance to ping the test a few times and then 
                // make a request to the heartbeat handler to verify that it indicates
                // that the server is healthy.

                Thread.Sleep(3000);
                Assert.IsTrue(GetHealth());

                // Now indicate that the first target service is unhealthy, give the heartbeat
                // handler a chance to ping the service, and verify that it determined
                // the server is unhealthy.

                host2Health["testservice01.lilltek.com"] = false;
                Thread.Sleep(3000);
                Assert.IsFalse(GetHealth());

                // Now indicate that the first target service is healthy, give the heartbeat
                // handler a chance to ping the service, and verify that it determined
                // the server is healthy.

                host2Health["testservice01.lilltek.com"] = true;
                Thread.Sleep(3000);
                Assert.IsTrue(GetHealth());

                // Now indicate that the  second and third target services are unhealthy, give the heartbeat
                // handler a chance to ping the service, and verify that it determined
                // the server is unhealthy.

                host2Health["testservice02.lilltek.com"] = false;
                host2Health["testservice03.lilltek.com"] = false;
                Thread.Sleep(3000);
                Assert.IsFalse(GetHealth());

                // Now mark the second service as healthy and verify that the 
                // server is still unhealthy (due to the third service).

                host2Health["testservice02.lilltek.com"] = true;
                Thread.Sleep(3000);
                Assert.IsFalse(GetHealth());

                // Now mark all services as healthy and verify that
                // the server is considered healthy.

                host2Health["testservice03.lilltek.com"] = true;
                Thread.Sleep(3000);
                Assert.IsTrue(GetHealth());

                // Stop the simulated target HTTP server and verify that the handler
                // detects the failure.

                httpServer.Stop();
                httpServer = null;
                Thread.Sleep(7000);
                Assert.IsFalse(GetHealth());
            }
            finally
            {
                Config.SetConfig(null);

                if (handler != null)
                    handler.Stop();

                if (httpServer != null)
                {
                    httpServer.Stop();
                    httpServer = null;
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void HeartbeatHandler_NonCriticalServices()
        {
            // Configure a heartbeart service to monitor a mix on critical and noncritical
            // services and verify that it does the right thing.

            HeartbeatHandler handler = null;
            HttpServer httpServer = null;
            const string cfg = @"

&section LillTek.Datacenter.HeartbeatService

    NetworkBinding = ANY:HTTP-HEARTBEAT
    Service[-]     = http://testservice01.lilltek.com:666/Heartbeat.aspx?global=0,critical
    Service[-]     = http://testservice02.lilltek.com:666/Heartbeat.aspx?global=0,critical
    Service[-]     = http://testservice03.lilltek.com:666/Heartbeat.aspx?global=0,noncritical
    PollInterval   = 1s
    BkInterval     = 1s

&endsection
";
            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                // Initialize the health status dictionary and crank up a HTTP listerner
                // to simulate target services.

                host2Health = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                host2Health["testservice01.lilltek.com"] = true;
                host2Health["testservice02.lilltek.com"] = true;
                host2Health["testservice03.lilltek.com"] = true;

                httpServer = new HttpServer(new IPEndPoint[] { new IPEndPoint(IPAddress.Any, 666) }, new IHttpModule[] { this }, 10, 10, 8196);
                httpServer.Start();

                // Start the heartbeat handler

                handler = new HeartbeatHandler();
                handler.Start(null, null, null, null);

                // Give the service a chance to ping the test a few times and then 
                // make a request to the heartbeat handler to verify that it indicates
                // that the server is healthy.

                Thread.Sleep(3000);
                Assert.IsTrue(GetHealth());

                // Now indicate that the first target service is unhealthy, give the heartbeat
                // handler a chance to ping the service, and verify that it determined
                // the server is unhealthy.

                host2Health["testservice01.lilltek.com"] = false;
                Thread.Sleep(3000);
                Assert.IsFalse(GetHealth());

                // Now indicate that the first target service is healthy, give the heartbeat
                // handler a chance to ping the service, and verify that it determined
                // the server is healthy.

                host2Health["testservice01.lilltek.com"] = true;
                Thread.Sleep(3000);
                Assert.IsTrue(GetHealth());

                // Now indicate that the second and third target services are unhealthy, give the heartbeat
                // handler a chance to ping the service, and verify that it determined
                // the server is unhealthy.

                host2Health["testservice02.lilltek.com"] = false;
                host2Health["testservice03.lilltek.com"] = false;
                Thread.Sleep(3000);
                Assert.IsFalse(GetHealth());

                // Now mark the second service as healthy and verify that the 
                // server is not considered to be health since the third server 
                // is noncritical.

                host2Health["testservice02.lilltek.com"] = true;
                Thread.Sleep(3000);
                Assert.IsTrue(GetHealth());

                // Now mark all services as healthy and verify that
                // the server is considered healthy.

                host2Health["testservice03.lilltek.com"] = true;
                Thread.Sleep(3000);
                Assert.IsTrue(GetHealth());

                // Stop the simulated target HTTP server and verify that the handler
                // detects the failure.

                httpServer.Stop();
                httpServer = null;
                Thread.Sleep(7000);
                Assert.IsFalse(GetHealth());
            }
            finally
            {
                Config.SetConfig(null);

                if (handler != null)
                    handler.Stop();

                if (httpServer != null)
                {
                    httpServer.Stop();
                    httpServer = null;
                }
            }
        }
    }
}

