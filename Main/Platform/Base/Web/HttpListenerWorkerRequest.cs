//-----------------------------------------------------------------------------
// FILE:        HttpListenerWorkerRequest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Internal class implementing HttpWorkerRequest.
//              Adapted from a sample downloaded from MSDN.

using System;
using System.IO;
using System.Text;
using System.Web;
using System.Net;
using System.Diagnostics;
using System.Web.Hosting;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Specialized;

using Microsoft.Win32.SafeHandles;

using LillTek.Common;
using LillTek.Service;

namespace LillTek.Web
{
    /// <summary>
    /// Internal class implementing HttpWorkerRequest.
    /// </summary>
    internal sealed class HttpListenerWorkerRequest : HttpWorkerRequest
    {
        private HttpListenerContext     context;
        private string                  virtualPath;
        private string                  physicalPath;

        public HttpListenerWorkerRequest(HttpListenerContext context, string virtualPath, string physicalPath)
        {
            this.context      = context;
            this.virtualPath  = virtualPath;
            this.physicalPath = physicalPath;
        }

        public HttpListenerContext Context
        {
            get { return context; }
        }

        //---------------------------------------------------------------------
        // Required overrides of abstract methods.

        public override void EndOfRequest()
        {
            context.Response.OutputStream.Close();
            context.Response.Close();
        }

        public override void FlushResponse(bool finalFlush)
        {
            context.Response.OutputStream.Flush();
        }

        public override string GetHttpVerbName()
        {
            return context.Request.HttpMethod;
        }

        public override string GetHttpVersion()
        {

            return string.Format("HTTP/{0}.{1}",
                context.Request.ProtocolVersion.Major,
                context.Request.ProtocolVersion.Minor);
        }

        public override string GetLocalAddress()
        {
            return context.Request.LocalEndPoint.Address.ToString();
        }

        public override int GetLocalPort()
        {
            return context.Request.LocalEndPoint.Port;
        }

        public override string GetQueryString()
        {
            string  rawUri = context.Request.RawUrl;
            int     pos;

            pos = rawUri.IndexOf('?');
            if (pos == -1)
                return string.Empty;
            else
                return rawUri.Substring(pos + 1);
        }

        public override string GetRawUrl()
        {
            return context.Request.RawUrl;
        }

        public override string GetRemoteAddress()
        {
            return context.Request.RemoteEndPoint.Address.ToString();
        }

        public override int GetRemotePort()
        {
            return context.Request.RemoteEndPoint.Port;
        }

        public override string GetUriPath()
        {
            return context.Request.Url.LocalPath;
        }

        public override void SendKnownResponseHeader(int index, string value)
        {
            context.Response.Headers[HttpWorkerRequest.GetKnownResponseHeaderName(index)] = value;
        }

        public override void SendResponseFromMemory(byte[] data, int length)
        {
            context.Response.OutputStream.Write(data, 0, length);
        }

        public override void SendStatus(int statusCode, string statusDescription)
        {
            context.Response.StatusCode = statusCode;
            context.Response.StatusDescription = statusDescription;
        }

        public override void SendUnknownResponseHeader(string name, string value)
        {
            context.Response.Headers[name] = value;
        }

        public override void SendResponseFromFile(IntPtr handle, long offset, long length)
        {
            using (var fsSrc = new EnhancedFileStream(new SafeFileHandle(handle, false), FileAccess.Read))
            {
                fsSrc.Position = offset;
                fsSrc.CopyTo(context.Response.OutputStream, (int)length);
            }
        }

        public override void SendResponseFromFile(string filename, long offset, long length)
        {
            using (var fsSrc = new EnhancedFileStream(filename, FileMode.Open, FileAccess.Read))
            {

                fsSrc.Position = offset;
                fsSrc.CopyTo(context.Response.OutputStream, (int)length);
            }
        }

        //---------------------------------------------------------------------
        // Optional overrides

        public override void CloseConnection()
        {
        }

        public override string GetAppPath()
        {
            return virtualPath;
        }

        public override string GetAppPathTranslated()
        {
            return physicalPath;
        }

        public override string GetUnknownRequestHeader(string name)
        {
            return context.Request.Headers[name];
        }

        public override string[][] GetUnknownRequestHeaders()
        {
            var headers     = context.Request.Headers;
            var headerPairs = new List<string[]>(headers.Count);

            for (int i = 0; i < headers.Count; i++)
            {

                string      headerName = headers.GetKey(i);
                string      headerValue;

                if (GetKnownRequestHeaderIndex(headerName) == -1)
                {
                    headerValue = headers.Get(i);
                    headerPairs.Add(new string[] { headerName, headerValue });
                }
            }

            return headerPairs.ToArray();
        }

        public override string GetKnownRequestHeader(int index)
        {
            switch (index)
            {
                case HeaderUserAgent:

                    return context.Request.UserAgent;

                default:

                    return context.Request.Headers[GetKnownRequestHeaderName(index)];
            }
        }

        public override string GetServerVariable(string name)
        {
            switch (name)
            {
                case "HTTPS":

                    return context.Request.IsSecureConnection ? "on" : "off";

                case "HTTP_USER_AGENT":

                    return context.Request.Headers["UserAgent"];

                default:

                    return null;
            }
        }

        public override string GetFilePath()
        {
            string      path = context.Request.Url.LocalPath;
            string      pathLower = path.ToLowerInvariant();
            int         pos;

            // $hack(jeff.lill): 
            //
            // I need to hack this a bit so that ASP.NET AJAX
            // works properly since it adds a weird "tail"
            // as the URI's GetPathInfo().  Note that I need
            // to special case javascript "code behind" files.

            if (!pathLower.Contains(".aspx.js"))
            {
                pos = pathLower.IndexOf(".aspx");
                if (pos != -1)
                    return path.Substring(0, pos + 5);

                pos = pathLower.IndexOf(".asmx");
                if (pos != -1)
                    return path.Substring(0, pos + 5);
            }

            // Strip off the query string if any.

            pos = path.IndexOf('?');
            if (pos != -1)
                path = path.Substring(0, pos);

            if (path.Length <= virtualPath.Length)
                path = virtualPath;

            return path;
        }

        public override string GetFilePathTranslated()
        {
            var s = GetFilePath();

            s = s.Substring(virtualPath.Length);
            s = s.Replace('/', '\\');

            if (s.Length > 0)
                return physicalPath + s;

            return physicalPath;
        }

        public override string GetPathInfo()
        {
            var s1 = GetFilePath();
            var s2 = context.Request.Url.LocalPath;

            if (s1.Length == s2.Length)
                return string.Empty;
            else
                return s2.Substring(s1.Length);
        }

        public override int ReadEntityBody(byte[] buffer, int size)
        {
            return context.Request.InputStream.Read(buffer, 0, size);
        }

        public override int ReadEntityBody(byte[] buffer, int offset, int size)
        {
            return context.Request.InputStream.Read(buffer, offset, size);
        }
    }
}
