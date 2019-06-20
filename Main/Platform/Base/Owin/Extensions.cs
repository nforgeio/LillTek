//-----------------------------------------------------------------------------
// FILE:        Extensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Owin related extension methods.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using Microsoft.Owin.Host.HttpListener;

using Owin;

using LillTek.Common;

namespace LillTek.Owin
{
    /// <summary>
    /// Owin related extension methods.
    /// </summary>
    public static class Extensions
    {
        //---------------------------------------------------------------------
        // IAppBuilder extensions

        /// <summary>
        /// Adds a handler to the <see cref="IAppBuilder"/> pipeline that holds an object instance
        /// and then presents the <see cref="IOwinContext"/> and the object to a handler for 
        /// processing.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="app">The <see cref="IAppBuilder"/>.</param>
        /// <param name="action">The handler.</param>
        /// <param name="instance">The object instance.</param>
        public static void RunWith<T>(this IAppBuilder app, Func<IOwinContext, T, Task> action, T instance)
        {
            app.Use<RunWithComponent<T>>(action, instance);
        }

        /// <summary>
        /// Adds a handler that will compress the response body using DEFLATE or GZIP if 
        /// acceptable to the client.
        /// </summary>
        /// <param name="app">The <see cref="IAppBuilder"/>.</param>
        /// <remarks>
        /// This implementation will hold all of the compressed response data in memory via
        /// a <see cref="BlockStream"/> until control has returned from the components further 
        /// down in the pipeline.  Use of the <see cref="BlockStream"/> will avoid any large
        /// object heap issues, but applications should take the memory consumption into
        /// consideration when using this component.
        /// </remarks>
        public static void CompressResponse(this IAppBuilder app)
        {
            app.Use<CompressResponseComponent>();
        }

        /// <summary>
        /// Controls the maximum number of requests the server will attempt to process concurrently.
        /// </summary>
        /// <param name="app">The target <see cref="IAppBuilder"/>.</param>
        /// <param name="maxAccepts">The maximum number of pending requests waiting to be accepted.</param>
        /// <param name="maxRequests">The maximum number of requests to be processed concurrently.</param>
        /// <remarks>
        /// <note>
        /// At the time of this writing, the default OWIN/Katana values are <paramref name="maxAccepts"/><b>=5</b>
        /// and <paramref name="maxRequests"/><b>=int.MaxValue</b>.
        /// </note>
        /// </remarks>
        public static void SetRequestLimits(this IAppBuilder app, int maxAccepts, int maxRequests)
        {
            var owinListener = app.Properties["Microsoft.Owin.Host.HttpListener.OwinHttpListener"] as OwinHttpListener;

            if (owinListener == null)
            {
                return;
            }

            owinListener.SetRequestProcessingLimits(maxAccepts, maxRequests);
        }

        /// <summary>
        /// Sets the maximum number of requests to be queued by the underlying HTTP.SYS listener.
        /// </summary>
        /// <param name="app">The target <see cref="IAppBuilder"/>.</param>
        /// <param name="requestQueueLimit">The maximum size of the request queue.</param>
        /// <remarks>
        /// <note>
        /// At the time of this writing, the default Windows HTTP.SYS queue limit is 100.
        /// </note>
        /// </remarks>
        public static void SetRequestQueueLimit(this IAppBuilder app, long requestQueueLimit)
        {
            var owinListener = app.Properties["Microsoft.Owin.Host.HttpListener.OwinHttpListener"] as OwinHttpListener;

            if (owinListener == null)
            {
                return;
            }

            owinListener.SetRequestQueueLimit(requestQueueLimit);
        }

        //---------------------------------------------------------------------
        // IOwinResponse extensions

        private static byte[] empty = new byte[0];

        /// <summary>
        /// Writes an empty byte array to the response as a convienent way to indicate to the
        /// OWIN response pipeline that all content has been written.
        /// </summary>
        /// <param name="response">The response.</param>
        public static async Task WriteAsync(this IOwinResponse response)
        {
            await response.WriteAsync(empty);
        }

        /// <summary>
        /// Writes an empty byte array to the response optionally capturing thrown <see cref="IOException"/>s
        /// as a convienent way to indicate to the OWIN response pipeline that all content has been written.
        /// </summary>
        /// <param name="response">The response.</param>
        /// <param name="ignoreIoException">
        /// Indicates whether <see cref="IOException"/>s should be captured and returned as 
        /// a <c>bool</c> result instead.
        /// </param>
        /// <returns>
        /// <c>true</c> if the operation completed without an error or <c>false</c> if 
        /// <paramref name="ignoreIoException"/> was passed as <c>true</c> and an <see cref="IOException"/>
        /// was captured.
        /// </returns>
        /// <remarks>
        /// The base <see cref="IOwinResponse"/> method throws an <see cref="IOException"/> when the
        /// client has disconnected while the server is still writing content.  This method provides
        /// a nice way for the server to avoid having to implement specialized <c>try</c>...<c>catch</c> 
        /// blocks everywhere.
        /// </remarks>
        public static async Task<bool> WriteAsync(this IOwinResponse response, bool ignoreIoException)
        {
            if (!ignoreIoException)
            {
                await response.WriteAsync();
                return true;
            }

            try
            {
                await response.WriteAsync();
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// Writes a byte array to the response optionally capturing thrown <see cref="IOException"/>s
        /// as a convienent way to indicate to the OWIN response pipeline that all content has been written.
        /// </summary>
        /// <param name="response">The response.</param>
        /// <param name="buffer">The bytes to be written.</param>
        /// <param name="ignoreIoException">
        /// Indicates whether <see cref="IOException"/>s should be captured and returned as 
        /// a <c>bool</c> result instead.
        /// </param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>
        /// <c>true</c> if the operation completed without an error or <c>false</c> if 
        /// <paramref name="ignoreIoException"/> was passed as <c>true</c> and an <see cref="IOException"/>
        /// was captured.
        /// </returns>
        /// <remarks>
        /// The base <see cref="IOwinResponse"/> method throws an <see cref="IOException"/> when the
        /// client has disconnected while the server is still writing content.  This method provides
        /// a nice way for the server to avoid having to implement specialized <c>try</c>...<c>catch</c> 
        /// blocks everywhere.
        /// </remarks>
        public static async Task<bool> WriteAsync(this IOwinResponse response, byte[] buffer, bool ignoreIoException, CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken.HasValue ? cancellationToken.Value : CancellationToken.None;

            if (!ignoreIoException)
            {
                await response.WriteAsync(buffer, token);
                return true;
            }

            try
            {
                await response.WriteAsync(buffer, token);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// Writes text to the response optionally capturing thrown <see cref="IOException"/>s
        /// as a convienent way to indicate to the OWIN response pipeline that all content has been written.
        /// </summary>
        /// <param name="response">The response.</param>
        /// <param name="text">The text to be written.</param>
        /// <param name="ignoreIoException">
        /// Indicates whether <see cref="IOException"/>s should be captured and returned as 
        /// a <c>bool</c> result instead.
        /// </param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>
        /// <c>true</c> if the operation completed without an error or <c>false</c> if 
        /// <paramref name="ignoreIoException"/> was passed as <c>true</c> and an <see cref="IOException"/>
        /// was captured.
        /// </returns>
        /// <remarks>
        /// The base <see cref="IOwinResponse"/> method throws an <see cref="IOException"/> when the
        /// client has disconnected while the server is still writing content.  This method provides
        /// a nice way for the server to avoid having to implement specialized <c>try</c>...<c>catch</c> 
        /// blocks everywhere.
        /// </remarks>
        public static async Task<bool> WriteAsync(this IOwinResponse response, string text, bool ignoreIoException, CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken.HasValue ? cancellationToken.Value : CancellationToken.None;

            if (!ignoreIoException)
            {
                await response.WriteAsync(text, token);
                return true;
            }

            try
            {
                await response.WriteAsync(text, token);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// Writes a portion of a byte array to the response optionally capturing thrown <see cref="IOException"/>s
        /// as a convienent way to indicate to the OWIN response pipeline that all content has been written.
        /// </summary>
        /// <param name="response">The response.</param>
        /// <param name="buffer">The bytes to be written.</param>
        /// <param name="index">Idex of the first byte to be written.</param>
        /// <param name="length">Number of bytes to be written.</param>
        /// <param name="ignoreIoException">
        /// Indicates whether <see cref="IOException"/>s should be captured and returned as 
        /// a <c>bool</c> result instead.
        /// </param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>
        /// <c>true</c> if the operation completed without an error or <c>false</c> if 
        /// <paramref name="ignoreIoException"/> was passed as <c>true</c> and an <see cref="IOException"/>
        /// was captured.
        /// </returns>
        /// <remarks>
        /// The base <see cref="IOwinResponse"/> method throws an <see cref="IOException"/> when the
        /// client has disconnected while the server is still writing content.  This method provides
        /// a nice way for the server to avoid having to implement specialized <c>try</c>...<c>catch</c> 
        /// blocks everywhere.
        /// </remarks>
        public static async Task<bool> WriteAsync(this IOwinResponse response, byte[] buffer, int index, int length, 
                                                  bool ignoreIoException, CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken.HasValue ? cancellationToken.Value : CancellationToken.None;

            if (!ignoreIoException)
            {
                await response.WriteAsync(buffer, index, length, token);
                return true;
            }

            try
            {
                await response.WriteAsync(buffer, index, length, token);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}
