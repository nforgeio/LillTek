//-----------------------------------------------------------------------------
// FILE:        App_HeartbeatService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.ServiceModel;
using System.Threading;

using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.Messaging;
using LillTek.Net.Http;
using LillTek.Net.Sockets;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.HeartbeatService.NUnit
{
    [TestClass]
    public class App_HeartbeatService : IHttpModule
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
        [TestProperty("Lib", "LillTek.Datacenter.Apps")]
        public void HeartbeatService_Basic()
        {
            // Verify that heartbeat service works by modifying its configuration file
            // to monitor two simulated services and then verify that the monitoring
            // works.

            HttpServer httpServer = null;
            Process svcProcess = null;
            ConfigRewriter rewriter;
            Assembly assembly;
            string iniPath;

            assembly = typeof(LillTek.Datacenter.HeartbeatService.Program).Assembly;
            iniPath = Config.GetConfigPath(assembly);
            rewriter = new ConfigRewriter(iniPath);

            try
            {
                // Initialize the local HTTP server that will simulate the monitored services.

                httpServer = new HttpServer(new IPEndPoint[] { new IPEndPoint(IPAddress.Any, 666) }, new IHttpModule[] { this }, 10, 10, 8196);
                httpServer.Start();

                host2Health = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                host2Health["testservice01.lilltek.com"] = true;
                host2Health["testservice02.lilltek.com"] = true;
                host2Health["testservice03.lilltek.com"] = true;

                // Rewrite the config file to have it start in hub mode
                // and then start the router service as a form application.

                rewriter.Rewrite(new ConfigRewriteTag[0]);

                // $hack(jeff.lill): 
                //
                // Append test related settings to the configuration file.  Since these settings
                // appear after the defaults, the written settings will override.

                using (var file = File.AppendText(iniPath))
                {
                    const string prefix = "LillTek.Datacenter.HeartbeatService.";

                    file.WriteLine(prefix + "MonitorUri[0] = http://testservice01.lilltek.com:666/Heartbeat.aspx?global=0");
                    file.WriteLine(prefix + "MonitorUri[1] = http://testservice02.lilltek.com:666/Heartbeat.aspx?global=0");
                    file.WriteLine(prefix + "MonitorUri[2] = http://testservice03.lilltek.com:666/Heartbeat.aspx?global=0");
                    file.WriteLine(prefix + "BkInterval = 1s");
                    file.WriteLine(prefix + "PollInterval = 1s");
                }

                svcProcess = Helper.StartProcess(assembly, "-mode:form -start");
                Thread.Sleep(10000);    // Give the process a chance to spin up

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
                Thread.Sleep(3000);
                Assert.IsFalse(GetHealth());
            }
            finally
            {
                rewriter.Restore();

                if (httpServer != null)
                {
                    httpServer.Stop();
                    httpServer = null;
                }

                if (svcProcess != null)
                {
                    svcProcess.Kill();
                    svcProcess.Close();
                }
            }
        }
    }
}

