//-----------------------------------------------------------------------------
// FILE:        EnhancedHttpListener.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements some enhancements over the .NET Framework
//              HttpListener class.

using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Http
{
    /// <summary>
    /// Implements some enhancements over the stock .NET <see cref="HttpListener" /> class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The basic functionality works almost identically to <see cref="HttpListener" />.  See
    /// the .NET Framework documentation for more information.  The main additions include:
    /// </para>
    /// <list type="bullet">
    ///     <item>
    ///     The constructor <see cref="EnhancedHttpListener(string)" /> has been added that
    ///     accepts the name of the service as a parameter.  This name can be added to response
    ///     headers via <see cref="AddResponseHeaders" />.
    ///     </item>
    ///     <item>
    ///     <see cref="AddResponseHeaders" /> adds standard headers to HTTP responses including
    ///     <b>Server</b>, <b>Date</b>, <b>Cache</b>
    ///     </item>
    /// </list>
    /// </remarks>
    public class EnhancedHttpListener
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Gets a value that indicates whether <see cref="EnhancedHttpListener" /> can 
        /// be used with the current operating system. 
        /// </summary>
        public static bool IsSupported
        {
            get { return HttpListener.IsSupported; }
        }

        //---------------------------------------------------------------------
        // Instance members

        private HttpListener listener;
        private string serverName;

        /// <summary>
        /// Constructor.
        /// </summary>
        public EnhancedHttpListener()
        {
            this.serverName = "";
            this.listener = new HttpListener();
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serverName">The server name to appear in response headers added by <see cref="AddResponseHeaders" />.</param>
        public EnhancedHttpListener(string serverName)
        {
            this.serverName = serverName;
            this.listener = new HttpListener();
        }

        /// <summary>
        /// Gets or sets the scheme used to authenticate clients. 
        /// </summary>
        public AuthenticationSchemes AuthenticationSchemes
        {

            get { return listener.AuthenticationSchemes; }
            set { listener.AuthenticationSchemes = value; }
        }

        /// <summary>
        /// Gets or sets the delegate called to determine the protocol used to authenticate clients. 
        /// </summary>
        public AuthenticationSchemeSelector AuthenticationSchemeSelectorDelegate
        {
            get { return listener.AuthenticationSchemeSelectorDelegate; }
            set { listener.AuthenticationSchemeSelectorDelegate = value; }
        }

        /// <summary>
        /// Gets or sets a Boolean value that specifies whether your application receives exceptions 
        /// that occur when an HttpListener sends the response to the client. 
        /// </summary>
        public bool IgnoreWriteExceptions
        {
            get { return listener.IgnoreWriteExceptions; }
            set { listener.IgnoreWriteExceptions = value; }
        }

        /// <summary>
        /// Gets a value that indicates whether HttpListener has been started. 
        /// </summary>
        public bool IsListening
        {
            get { return listener.IsListening; }
        }

        /// <summary>
        /// Gets the Uniform Resource Identifier (URI) prefixes handled by this <see cref="EnhancedHttpListener" /> object. 
        /// </summary>
        public HttpListenerPrefixCollection Prefixes
        {
            get { return listener.Prefixes; }
        }

        /// <summary>
        /// Gets or sets the realm, or resource partition, associated with this <see cref="EnhancedHttpListener" /> object. 
        /// </summary>
        public string Realm
        {
            get { return listener.Realm; }
            set { listener.Realm = value; }
        }

        /// <summary>
        /// Gets or sets a Boolean value that controls whether, when NTLM is used, additional requests using the same 
        /// Transmission Control Protocol (TCP) connection are required to authenticate. 
        /// </summary>
        public bool UnsafeConnectionNtlmAuthentication
        {
            get { return listener.UnsafeConnectionNtlmAuthentication; }
            set { listener.UnsafeConnectionNtlmAuthentication = value; }
        }

        /// <summary>
        /// Shuts down the <see cref="EnhancedHttpListener" /> object immediately, discarding all currently queued requests. 
        /// </summary>
        public void Abort()
        {
            listener.Abort();
        }

        /// <summary>
        /// Begins asynchronously retrieving an incoming request. 
        /// </summary>
        /// <param name="callback">An <see cref="AsyncCallback" /> delegate that references the method to invoke when a client request is available.</param>
        /// <param name="state">A user-defined object that contains information about the operation. This object is passed to the callback delegate when the operation completes.</param>
        /// <returns>An <see cref="IAsyncResult" /> object that indicates the status of the asynchronous operation. </returns>
        public IAsyncResult BeginGetContext(AsyncCallback callback, object state)
        {
            return listener.BeginGetContext(callback, state);
        }

        /// <summary>
        /// Shuts down the <see cref="EnhancedHttpListener" /> after processing all currently queued requests. 
        /// </summary>
        public void Close()
        {
            listener.Close();
        }

        /// <summary>
        /// Completes an asynchronous operation to retrieve an incoming client request. 
        /// </summary>
        /// <param name="asyncResult">An <see cref="IAsyncResult" /> object that was obtained when the asynchronous operation was started.</param>
        /// <returns>An <see cref="HttpListenerContext" /> object that represents the client request. ></returns>
        public HttpListenerContext EndGetContext(IAsyncResult asyncResult)
        {
            return listener.EndGetContext(asyncResult);
        }

        /// <summary>
        /// Waits for an incoming request and returns when one is received. 
        /// </summary>
        /// <returns>An <see cref="HttpListenerContext" /> object that represents the client request. ></returns>
        public HttpListenerContext GetContext()
        {
            return listener.GetContext();
        }

        /// <summary>
        /// Allows this instance to receive incoming requests. 
        /// </summary>
        public void Start()
        {
            listener.Start();
        }

        /// <summary>
        /// Causes this instance to stop receiving incoming requests. 
        /// </summary>
        public void Stop()
        {
            listener.Stop();
        }

        /// <summary>
        /// Adds standard HTTP headers to a <see cref="HttpListenerResponse" />.
        /// </summary>
        /// <param name="response">The <see cref="HttpListenerResponse" />.</param>
        /// <param name="headers">The header flag bits.</param>
        /// <remarks>
        /// This method provides an easy way to add some of the standard HTTP
        /// headers to a response.
        /// </remarks>
        public void AddResponseHeaders(HttpListenerResponse response, HttpHeaderFlag headers)
        {
            if ((headers & HttpHeaderFlag.Server) != 0)
                response.AddHeader("Server", serverName);

            if ((headers & HttpHeaderFlag.NoCache) != 0)
            {
                response.AddHeader("Pragma", "no-cache");
                response.AddHeader("Cache-Control", "private");
            }
        }
    }
}
