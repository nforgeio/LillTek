//-----------------------------------------------------------------------------
// FILE:        AuthTestJsonClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit test JSON Authentication client.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Reflection;
using System.ServiceModel;
using System.Threading;

using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Datacenter.Msgs.SentinelService;
using LillTek.Json;
using LillTek.Messaging;
using LillTek.Net.Http;
using LillTek.Net.Radius;
using LillTek.Net.Sockets;
using LillTek.Service;

namespace LillTek.Datacenter
{
    /// <summary>
    /// JSON authentication client to be used for authentication unit testing.
    /// </summary>
    public sealed class AuthTestJsonClient
    {
        //-----------------------------------------------------------
        // Private classes

        /// <summary>
        /// Used for serializing JSON authentication requests.
        /// </summary>
        private class JsonAuthRequest
        {
            public string Realm;
            public string Account;
            public string Password;

            public JsonAuthRequest()
            {
            }

            public JsonAuthRequest(string realm, string account, string password)
            {
                this.Realm = realm;
                this.Account = account;
                this.Password = password;
            }
        }

        /// <summary>
        /// Used for serializing JSON authentication responses.
        /// </summary>
        private class JsonAuthResponse
        {
            public string Status;
            public string Message;
            public int MaxCacheTime;

            public JsonAuthResponse()
            {
            }

            public JsonAuthResponse(AuthenticationStatus status, string message, TimeSpan maxCacheTime)
            {
                this.Status = status.ToString();
                this.Message = message;
                this.MaxCacheTime = (int)maxCacheTime.TotalSeconds;
            }

            public JsonAuthResponse(AuthenticationResult result)
            {
                this.Status = result.ToString();
                this.Message = result.Message;
                this.MaxCacheTime = (int)result.MaxCacheTime.TotalSeconds;
            }

            public AuthenticationResult ToAuthResult()
            {
                return new AuthenticationResult((AuthenticationStatus)Enum.Parse(typeof(AuthenticationStatus), this.Status, true),
                                                this.Message,
                                                TimeSpan.FromSeconds(MaxCacheTime));
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private Uri uri;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="uri">The authentication service URI.</param>
        public AuthTestJsonClient(string uri)
        {
            this.uri = new Uri(uri);
        }

        /// <summary>
        /// Specifies the authentication server URI.
        /// </summary>
        public string Uri
        {
            get { return uri.ToString(); }
            set { uri = new Uri(value); }
        }

        /// <summary>
        /// Authenticates against the authentication service via HTTP/GET.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">The password.</param>
        /// <returns>A see cref="AuthenticationResult" /> instance.</returns>
        public AuthenticationResult Authenticate(string realm, string account, string password)
        {
            return AuthenticateViaGet(realm, account, password);
        }

        /// <summary>
        /// Authenticates against the authentication service via a HTTP/GET.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">The password.</param>
        /// <returns>A see cref="AuthenticationResult" /> instance.</returns>
        public AuthenticationResult AuthenticateViaGet(string realm, string account, string password)
        {
            HttpConnection con = new HttpConnection(HttpOption.None);
            HttpRequest request;
            HttpResponse response;
            string content;

            try
            {

                con.Connect(uri.Host, uri.Port);
                request = new HttpRequest(HttpStack.Http11, "get",
                                           string.Format("{0}?realm={1}&account={2}&password={3}",
                                                         uri.AbsolutePath, Helper.EscapeUri(realm), Helper.EscapeUri(account), Helper.EscapeUri(password)),
                                           null);

                request["host"] = uri.Host;
                request["accept"] = "*/*";

                response = con.Query(request, SysTime.Now + TimeSpan.FromSeconds(10));
                if ((int)response.Status < 200 || (int)response.Status > 299)
                    throw new HttpException(response.Status);

                content = Helper.FromUTF8(response.Content.ToByteArray());

                return ((JsonAuthResponse)JsonSerializer.Read(content, typeof(JsonAuthResponse))).ToAuthResult();
            }
            finally
            {
                con.Close();
            }
        }

        /// <summary>
        /// Authenticates against the authentication service via a HTTP/POST.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">The password.</param>
        /// <returns>A see cref="AuthenticationResult" /> instance.</returns>
        public AuthenticationResult AuthenticateViaPost(string realm, string account, string password)
        {
            HttpConnection con = new HttpConnection(HttpOption.None);
            JsonAuthRequest authRequest = new JsonAuthRequest(realm, account, password);
            HttpRequest request;
            HttpResponse response;
            string content;

            try
            {
                con.Connect(uri.Host, uri.Port);
                request = new HttpRequest(HttpStack.Http11, "post", uri.AbsolutePath,
                                           new BlockArray(Helper.ToUTF8(JsonSerializer.ToString(authRequest))));

                request["host"] = uri.Host;
                request["accept"] = "*/*";
                request["content-type"] = "application/json";

                response = con.Query(request, SysTime.Now + TimeSpan.FromSeconds(10));
                if ((int)response.Status < 200 || (int)response.Status > 299)
                    throw new HttpException(response.Status);

                content = Helper.FromUTF8(response.Content.ToByteArray());

                return ((JsonAuthResponse)JsonSerializer.Read(content, typeof(JsonAuthResponse))).ToAuthResult();
            }
            finally
            {
                con.Close();
            }
        }
    }
}

