//-----------------------------------------------------------------------------
// FILE:        HttpListenerWrapper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Internal marshable implementation of HttpListener

using System;
using System.IO;
using System.Text;
using System.Web;
using System.Net;
using System.Threading;
using System.Diagnostics;
using System.Web.Hosting;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.Remoting.Lifetime;

using LillTek.Common;
using LillTek.Service;

namespace LillTek.Web
{
    /// <summary>
    /// Internal implementation of <see cref="HttpListener" /> that can be
    /// marshalled across application domains.
    /// </summary>
    /// <threadsafety instance="true" />
    internal sealed class HttpListenerWrapper : MarshalByRefObject
    {
        private object          syncLock = new object();
        private HttpListener    listener = null;
        private bool            stop     = false;
        private string          virtualPath;
        private string          physicalPath;
        private Thread          receiveThread;
        private WaitCallback    onHttpRequest;

        /// <summary>
        /// Constructor.
        /// </summary>
        public HttpListenerWrapper()
        {
            onHttpRequest = new WaitCallback(OnHttpRequest);
        }

        public override object InitializeLifetimeService()
        {
            var lease = (ILease)base.InitializeLifetimeService();

            lease.InitialLeaseTime = TimeSpan.FromMinutes(5);
            lease.SponsorshipTimeout = TimeSpan.FromSeconds(10);
            lease.RenewOnCallTime = TimeSpan.FromSeconds(10);

            return lease;
        }

        /// <summary>
        /// Starts the HTTP listener and begins processing inbound requests.
        /// </summary>
        /// <param name="prefixes">The HTTP URI prefixes.</param>
        /// <param name="virtualPath">The virtual path to the application files.</param>
        /// <param name="physicalPath">The physical path to the application files.</param>
        public void Start(string[] prefixes, string virtualPath, string physicalPath)
        {
            lock (syncLock)
            {
                if (listener != null)
                    throw new InvalidOperationException("Listener has already started.");

                this.virtualPath  = virtualPath;
                this.physicalPath = physicalPath;
                this.listener     = new HttpListener();

                foreach (string prefix in prefixes)
                    listener.Prefixes.Add(prefix);

                listener.Start();

                receiveThread = new Thread(new ThreadStart(ReceiveThread));
                receiveThread.Start();
            }
        }

        /// <summary>
        /// Stops the listener if it's running.
        /// </summary>
        public void Stop()
        {
            lock (syncLock)
            {
                if (listener != null)
                {
                    stop = true;

                    listener.Close();
                    listener = null;

                    // Give the HTTP request receive thread a few seconds to terminate
                    // normally before aborting it.

                    if (receiveThread != null && !receiveThread.Join(TimeSpan.FromSeconds(5)))
                        receiveThread.Abort();
                }
            }
        }

        /// <summary>
        /// Handles the reception of inbound HTTP requests by queuing them to
        /// worker threads until <see cref="Stop" /> is called.
        /// </summary>
        private void ReceiveThread()
        {
            HttpListenerContext         ctx;
            HttpListenerWorkerRequest   request;

            while (!stop)
            {
                try
                {
                    ctx     = listener.GetContext();
                    request = new HttpListenerWorkerRequest(ctx, virtualPath, physicalPath);
                    Helper.UnsafeQueueUserWorkItem(onHttpRequest, request);
                }
                catch (Exception e)
                {
                    if (!stop)
                        SysLog.LogException(e);
                }
                finally
                {
                    receiveThread = null;
                }
            }
        }

        /// <summary>
        /// Processes inbound HTTP requests on a worker thread.
        /// </summary>
        /// <param name="state">The <see cref="HttpListenerWorkerRequest" /> to be processed.</param>
        private void OnHttpRequest(object state)
        {
            var request = (HttpListenerWorkerRequest)state;

            // If the request URI references the application root then
            // probe for a reasonable default page and then redirect the
            // browser to that page.

            if (request.GetFilePathTranslated().Length <= physicalPath.Length)
            {
                // $todo(jeff.lill): 
                //
                // At some point these should be specified in
                //a configuration setting

                HttpListenerResponse response = request.Context.Response;
                string[] defaults = new string[] {

                    "Default.aspx",
                    "Default.html",
                    "Default.htm",
                    "Index.html",
                    "Index.htm"
                };

                for (int i = 0; i < defaults.Length; i++)
                {
                    if (File.Exists(physicalPath + defaults[i]))
                    {
                        const string template = @"
<html>
    <head><title>Object moved</title></head>
    <body>
    <h2>Object moved to <a href=""{0}"">here</a>.</h2>
    </body>
</html>
";
                        string redirect = virtualPath + defaults[i];
                        string html = string.Format(template, redirect);
                        byte[] encoded = Helper.ToUTF8(html);

                        response.StatusCode = 302;
                        response.StatusDescription = "Redirect";
                        response.ContentType = "text/html; charset=utf-8";
                        response.ContentLength64 = encoded.Length;

                        response.AppendHeader("Cache-Control", "private");
                        response.AppendHeader("Location", redirect);
                        response.AppendHeader("Server", "LillTek-WebHost");
                        response.AppendHeader("Date", Helper.ToInternetDate(DateTime.UtcNow));

                        response.OutputStream.Write(encoded, 0, encoded.Length);
                        response.OutputStream.Close();
                        return;
                    }
                }

                // We couldn't find an appropriate default page so return a 404
                // error to the client.

                byte[] errEncoded = Helper.ToUTF8(@"
<html>
    <head><title>Not found</title></head>
    <body>
    <h2>404 Error: Page not found.</h2>
    </body>
</html>");
                response.StatusCode = 404;
                response.StatusDescription = "Not Found";
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = errEncoded.Length;

                response.AppendHeader("Cache-Control", "private");
                response.AppendHeader("Server", "LillTek-WebHost");
                response.AppendHeader("Date", Helper.ToInternetDate(DateTime.UtcNow));

                response.OutputStream.Write(errEncoded, 0, errEncoded.Length);
                response.OutputStream.Close();
                return;
            }


            // Let ASP.NET process the request normally.

            HttpRuntime.ProcessRequest(request);
        }
    }
}
