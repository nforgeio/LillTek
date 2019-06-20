//-----------------------------------------------------------------------------
// FILE:        _AuthServiceHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests.

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Management;
using System.Reflection;
using System.Diagnostics;
using System.ServiceModel;
using System.Collections;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Json;
using LillTek.Net.Http;
using LillTek.Net.Radius;
using LillTek.Net.Sockets;
using LillTek.Service;
using LillTek.Messaging;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Server.Test
{
    [TestClass]
    public class _AuthServiceHandler
    {
        private LeafRouter router = null;
        private AuthTestState state = null;

        [TestInitialize]
        public void Initialize()
        {
            int c;

            NetTrace.Start();
            NetTrace.Enable(MsgRouter.TraceSubsystem, 0);

            const string settings =
@"
&section MsgRouter

    AppName                = Test
    AppDescription         = Test Description
    RouterEP			   = physical://detached/test/leaf
    CloudEP    			   = $(LillTek.DC.CloudEP)
    CloudAdapter    	   = ANY
    UdpEP				   = ANY:0
    TcpEP				   = ANY:0
    TcpBacklog			   = 100
    TcpDelay			   = off
    BkInterval			   = 1s
    MaxIdle				   = 5m
    EnableP2P              = yes
    AdvertiseTime		   = 1m
    DefMsgTTL			   = 5
    SharedKey		 	   = PLAINTEXT
    SessionCacheTime       = 2m
    SessionRetries         = 3
    SessionTimeout         = 10s
    MaxLogicalAdvertiseEPs = 256
    DeadRouterTTL          = 2s

AbstractMap[abstract://LillTek/DataCenter/Auth/Service] = logical://LillTek/DataCenter/Auth/Service
AbstractMap[abstract://LillTek/DataCenter/Auth/Client]  = logical://LillTek/DataCenter/Auth/Client

&endsection

// Use this section for most tests (including LillTek Messaging protocol tests).

&section LillTek.Datacenter.AuthService

    RealmMapProvider = LillTek.Datacenter.Server.ConfigRealmMapProvider:LillTek.Datacenter.Server.dll
    RealmMapArgs     = RealmMap
    RSAKeyPair       = {{
    
        <RSAKeyValue>
            <Modulus>rJweYNfUOPRhr0ATty5eKdDaYxqO0AMiJJ7w9A25Z+6vye/gYfJ6/9rEyx/p8dW0id+r0uxhdL9HdFsftFIHX2jCz7jdql92VDsTuiwaHRw+3edsDCkpSG10WvLMqvH6Rxi0f/CMet/2ge1kAW8lzqSLoCRgShN7lmm9htD/lNU=</Modulus>
            <Exponent>AQAB</Exponent>
            <P>0+/kN6zPDyPtm8Fzv3TUHWnSoitu9DSwFru5Z0LbGta7cqyXnL7aISZ1o2jCf+15zacY+x7HG8RzIzNT67vsUQ==</P>
            <Q>0H8UdyhqtKhEJy32/dJGA2EoTPqhmGHLBI/aGlc6F3EfrzPXL9Y+Zc7iKN+jGX8rA6+Znq9d3Y8MrdAR64jzRQ==</Q>
            <DP>gKE4ghIAGdBUhhQDjE/77V4s2QBDdzQDK8kD3ghVsxRg8FiQLDIpcbVF8MfERKB9LLQeFUu4zMGOn+6nIIwOAQ==</DP>
            <DQ>Y5Ecc98QPh/RFCjGN+Zv2vNN7J0QCJZC/nW4ATZAnqs+J0wJamXUvIe0xzItUGLDZuo34Wj72W+T7Xlc5W8sRQ==</DQ>
            <InverseQ>OvHiJmpMN/8wruM8HLQeTgnqfEhTlNzH/09kup40Voym7ci7KM24AVP4ucTjS77hOlOJ7LFz1/nm3YsMGaTRDg==</InverseQ>
            <D>D2Of5bx4ZFeNegV5fIR6yrmfLuTIRM1yttcg3nF2zUhfjd6AH9txkewcTYvb3L7T6NLzS8vdaH5BTaNuMTJ7C6YcX3i6heerBIW3nwlfcM2gaGUAQE2WXQ3tKtLx0p4Tt0J7Z8fQH4mV6/6lqoAkkREOj/shyoMJffJB7fy148E=</D>
        </RSAKeyValue>        
    }}

&endsection

// Use this section for RADIUS protocol tests

&section RADIUS.AuthService

    RealmMapProvider = LillTek.Datacenter.Server.ConfigRealmMapProvider:LillTek.Datacenter.Server.dll
    RealmMapArgs     = RealmMap
    RSAKeyPair       = {{
    
        <RSAKeyValue>
            <Modulus>rJweYNfUOPRhr0ATty5eKdDaYxqO0AMiJJ7w9A25Z+6vye/gYfJ6/9rEyx/p8dW0id+r0uxhdL9HdFsftFIHX2jCz7jdql92VDsTuiwaHRw+3edsDCkpSG10WvLMqvH6Rxi0f/CMet/2ge1kAW8lzqSLoCRgShN7lmm9htD/lNU=</Modulus>
            <Exponent>AQAB</Exponent>
            <P>0+/kN6zPDyPtm8Fzv3TUHWnSoitu9DSwFru5Z0LbGta7cqyXnL7aISZ1o2jCf+15zacY+x7HG8RzIzNT67vsUQ==</P>
            <Q>0H8UdyhqtKhEJy32/dJGA2EoTPqhmGHLBI/aGlc6F3EfrzPXL9Y+Zc7iKN+jGX8rA6+Znq9d3Y8MrdAR64jzRQ==</Q>
            <DP>gKE4ghIAGdBUhhQDjE/77V4s2QBDdzQDK8kD3ghVsxRg8FiQLDIpcbVF8MfERKB9LLQeFUu4zMGOn+6nIIwOAQ==</DP>
            <DQ>Y5Ecc98QPh/RFCjGN+Zv2vNN7J0QCJZC/nW4ATZAnqs+J0wJamXUvIe0xzItUGLDZuo34Wj72W+T7Xlc5W8sRQ==</DQ>
            <InverseQ>OvHiJmpMN/8wruM8HLQeTgnqfEhTlNzH/09kup40Voym7ci7KM24AVP4ucTjS77hOlOJ7LFz1/nm3YsMGaTRDg==</InverseQ>
            <D>D2Of5bx4ZFeNegV5fIR6yrmfLuTIRM1yttcg3nF2zUhfjd6AH9txkewcTYvb3L7T6NLzS8vdaH5BTaNuMTJ7C6YcX3i6heerBIW3nwlfcM2gaGUAQE2WXQ3tKtLx0p4Tt0J7Z8fQH4mV6/6lqoAkkREOj/shyoMJffJB7fy148E=</D>
        </RSAKeyValue>        
    }}

    &section Radius[0]

        NetworkBinding = ANY:1645
        DefaultSecret  = mysecret

    &endsection

    &section Radius[1]

        NetworkBinding = ANY:1646
        DefaultSecret  = mysecret

    &endsection

&endsection

// Use this section for HTTP protocol tests.

&section HTTP.AuthService

    RealmMapProvider = LillTek.Datacenter.Server.ConfigRealmMapProvider:LillTek.Datacenter.Server.dll
    RealmMapArgs     = RealmMap
    RSAKeyPair       = <RSAKeyValue><Modulus>rJweYNfUOPRhr0ATty5eKdDaYxqO0AMiJJ7w9A25Z+6vye/gYfJ6/9rEyx/p8dW0id+r0uxhdL9HdFsftFIHX2jCz7jdql92VDsTuiwaHRw+3edsDCkpSG10WvLMqvH6Rxi0f/CMet/2ge1kAW8lzqSLoCRgShN7lmm9htD/lNU=</Modulus><Exponent>AQAB</Exponent><P>0+/kN6zPDyPtm8Fzv3TUHWnSoitu9DSwFru5Z0LbGta7cqyXnL7aISZ1o2jCf+15zacY+x7HG8RzIzNT67vsUQ==</P><Q>0H8UdyhqtKhEJy32/dJGA2EoTPqhmGHLBI/aGlc6F3EfrzPXL9Y+Zc7iKN+jGX8rA6+Znq9d3Y8MrdAR64jzRQ==</Q><DP>gKE4ghIAGdBUhhQDjE/77V4s2QBDdzQDK8kD3ghVsxRg8FiQLDIpcbVF8MfERKB9LLQeFUu4zMGOn+6nIIwOAQ==</DP><DQ>Y5Ecc98QPh/RFCjGN+Zv2vNN7J0QCJZC/nW4ATZAnqs+J0wJamXUvIe0xzItUGLDZuo34Wj72W+T7Xlc5W8sRQ==</DQ><InverseQ>OvHiJmpMN/8wruM8HLQeTgnqfEhTlNzH/09kup40Voym7ci7KM24AVP4ucTjS77hOlOJ7LFz1/nm3YsMGaTRDg==</InverseQ><D>D2Of5bx4ZFeNegV5fIR6yrmfLuTIRM1yttcg3nF2zUhfjd6AH9txkewcTYvb3L7T6NLzS8vdaH5BTaNuMTJ7C6YcX3i6heerBIW3nwlfcM2gaGUAQE2WXQ3tKtLx0p4Tt0J7Z8fQH4mV6/6lqoAkkREOj/shyoMJffJB7fy148E=</D></RSAKeyValue>
    WarnUnencrypted  = no

    HttpEndpoint[0]  = http://$(machinename):37614/authenticate/
    HttpEndpoint[1]  = http://mymachine.com:37614/authenticate/
    HttpEndpoint[2]  = http://*:80/authenticate/

&endsection

// Use this section for WCF tests.

&section WCF.AuthService

    RealmMapProvider = LillTek.Datacenter.Server.ConfigRealmMapProvider:LillTek.Datacenter.Server.dll
    RealmMapArgs     = RealmMap
    RSAKeyPair       = {{
    
        <RSAKeyValue>
            <Modulus>rJweYNfUOPRhr0ATty5eKdDaYxqO0AMiJJ7w9A25Z+6vye/gYfJ6/9rEyx/p8dW0id+r0uxhdL9HdFsftFIHX2jCz7jdql92VDsTuiwaHRw+3edsDCkpSG10WvLMqvH6Rxi0f/CMet/2ge1kAW8lzqSLoCRgShN7lmm9htD/lNU=</Modulus>
            <Exponent>AQAB</Exponent>
            <P>0+/kN6zPDyPtm8Fzv3TUHWnSoitu9DSwFru5Z0LbGta7cqyXnL7aISZ1o2jCf+15zacY+x7HG8RzIzNT67vsUQ==</P>
            <Q>0H8UdyhqtKhEJy32/dJGA2EoTPqhmGHLBI/aGlc6F3EfrzPXL9Y+Zc7iKN+jGX8rA6+Znq9d3Y8MrdAR64jzRQ==</Q>
            <DP>gKE4ghIAGdBUhhQDjE/77V4s2QBDdzQDK8kD3ghVsxRg8FiQLDIpcbVF8MfERKB9LLQeFUu4zMGOn+6nIIwOAQ==</DP>
            <DQ>Y5Ecc98QPh/RFCjGN+Zv2vNN7J0QCJZC/nW4ATZAnqs+J0wJamXUvIe0xzItUGLDZuo34Wj72W+T7Xlc5W8sRQ==</DQ>
            <InverseQ>OvHiJmpMN/8wruM8HLQeTgnqfEhTlNzH/09kup40Voym7ci7KM24AVP4ucTjS77hOlOJ7LFz1/nm3YsMGaTRDg==</InverseQ>
            <D>D2Of5bx4ZFeNegV5fIR6yrmfLuTIRM1yttcg3nF2zUhfjd6AH9txkewcTYvb3L7T6NLzS8vdaH5BTaNuMTJ7C6YcX3i6heerBIW3nwlfcM2gaGUAQE2WXQ3tKtLx0p4Tt0J7Z8fQH4mV6/6lqoAkkREOj/shyoMJffJB7fy148E=</D>
        </RSAKeyValue>        
    }}

    WarnUnencrypted  = no

    WcfEndpoint[0]   = binding=BasicHTTP;uri=http://localhost:37615/authenticate/auth.svc
    WsdlUri          = http://localhost:37615/authenticate/auth.wsdl?wsdl

&endsection

// Use this section for lockout tests.

&section Lockout.AuthService

    RealmMapProvider = LillTek.Datacenter.Server.ConfigRealmMapProvider:LillTek.Datacenter.Server.dll
    RealmMapArgs     = Lockout.AuthService.RealmMap
    RSAKeyPair       = {{
    
        <RSAKeyValue>
            <Modulus>rJweYNfUOPRhr0ATty5eKdDaYxqO0AMiJJ7w9A25Z+6vye/gYfJ6/9rEyx/p8dW0id+r0uxhdL9HdFsftFIHX2jCz7jdql92VDsTuiwaHRw+3edsDCkpSG10WvLMqvH6Rxi0f/CMet/2ge1kAW8lzqSLoCRgShN7lmm9htD/lNU=</Modulus>
            <Exponent>AQAB</Exponent>
            <P>0+/kN6zPDyPtm8Fzv3TUHWnSoitu9DSwFru5Z0LbGta7cqyXnL7aISZ1o2jCf+15zacY+x7HG8RzIzNT67vsUQ==</P>
            <Q>0H8UdyhqtKhEJy32/dJGA2EoTPqhmGHLBI/aGlc6F3EfrzPXL9Y+Zc7iKN+jGX8rA6+Znq9d3Y8MrdAR64jzRQ==</Q>
            <DP>gKE4ghIAGdBUhhQDjE/77V4s2QBDdzQDK8kD3ghVsxRg8FiQLDIpcbVF8MfERKB9LLQeFUu4zMGOn+6nIIwOAQ==</DP>
            <DQ>Y5Ecc98QPh/RFCjGN+Zv2vNN7J0QCJZC/nW4ATZAnqs+J0wJamXUvIe0xzItUGLDZuo34Wj72W+T7Xlc5W8sRQ==</DQ>
            <InverseQ>OvHiJmpMN/8wruM8HLQeTgnqfEhTlNzH/09kup40Voym7ci7KM24AVP4ucTjS77hOlOJ7LFz1/nm3YsMGaTRDg==</InverseQ>
            <D>D2Of5bx4ZFeNegV5fIR6yrmfLuTIRM1yttcg3nF2zUhfjd6AH9txkewcTYvb3L7T6NLzS8vdaH5BTaNuMTJ7C6YcX3i6heerBIW3nwlfcM2gaGUAQE2WXQ3tKtLx0p4Tt0J7Z8fQH4mV6/6lqoAkkREOj/shyoMJffJB7fy148E=</D>
        </RSAKeyValue>        
    }}

    WarnUnencrypted  = no

    RealmMap[0] = {{

        test.com$$
        LillTek.Datacenter.Server.ConfigAuthenticationExtension:LillTek.Datacenter.Server.dll$$
        key=Lockout.AuthService.Accounts;LockoutCount=4;LockoutThreshold=5m;LockoutTime=10s$$
    }}

    Accounts[0] = test.com;account0;password0
    Accounts[1] = test.com;account1;password1
    Accounts[2] = test.com;account2;password2
    Accounts[3] = test.com;account3;password3

&endsection
";
            state = new AuthTestState();
            state.Initialize();

            Config.AppendConfig(settings.Replace('&', '#'));

            c = 0;
            foreach (AuthTestExtensionType type in Enum.GetValues(typeof(AuthTestExtensionType)))
                Config.Global.Add(string.Format("RealmMap[{0}]", c++),
                                  string.Format("{0}$${1}$${2}$${3}", state.GetRealm(type), state.GetType(type), state.GetArgs(type), state.GetQuery(type)));

            MsgEP.ReloadAbstractMap();

            router = new LeafRouter();
            router.Start();
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (state != null)
                state.Cleanup();

            if (router != null)
                router.Stop();

            NetTrace.Stop();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthServiceHandler_Cached()
        {
            // Crank up an AuthServiceHandler instance and then verify that
            // an Authenticator instance can issue authentications against it.

            AuthServiceHandler authHandler = new AuthServiceHandler();
            Authenticator authenticator = new Authenticator();
            AuthenticatorSettings settings;
            AuthenticationResult result;
            int count;

            try
            {
                authHandler.Start(router, null, null, null);

                settings = new AuthenticatorSettings();
                authenticator.Open(router, settings);

                // Verify that valid credentials are authenticated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);

                    result = authenticator.GetCachedResult(account.Realm, account.Account, account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreEqual(AuthenticationStatus.Authenticated, result.Status);

                    // Authenticate again with the same credentials and verify that the 
                    // cached result was used rather then performing another query.

                    count = authHandler.AuthCount;
                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);
                    Assert.AreEqual(count, authHandler.AuthCount);
                }

                // Verify that invalid credentials are not authenicated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password + "x").Status);

                    result = authenticator.GetCachedResult(account.Realm + "x", account.Account, account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    result = authenticator.GetCachedResult(account.Realm.ToUpper() + "x", account.Account, account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    result = authenticator.GetCachedResult(account.Realm, account.Account + "x", account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    result = authenticator.GetCachedResult(account.Realm, account.Account.ToUpper() + "x", account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    result = authenticator.GetCachedResult(account.Realm, account.Account, account.Password + "x");
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    // Authenticate again with the same credentials and verify that the 
                    // cached result was used rather then performing another query.

                    count = authHandler.AuthCount;
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                    Assert.AreEqual(count, authHandler.AuthCount);
                }
            }
            finally
            {
                authenticator.Close();
                authHandler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthServiceHandler_No_Success_Cache()
        {
            // Verify that the authenticator doesn't cache successful authentications
            // when SuccessTTL=Zero.

            AuthServiceHandler authHandler = new AuthServiceHandler();
            Authenticator authenticator = new Authenticator();
            AuthenticatorSettings settings;
            AuthenticationResult result;

            try
            {
                authHandler.Start(router, null, null, null);

                settings = new AuthenticatorSettings();
                settings.SuccessTTL = TimeSpan.Zero;
                authenticator.Open(router, settings);

                // Verify that valid credentials are authenticated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);

                    Assert.IsNull(authenticator.GetCachedResult(account.Realm, account.Account, account.Password));
                }

                // Verify that invalid credentials are not authenicated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password + "x").Status);

                    result = authenticator.GetCachedResult(account.Realm + "x", account.Account, account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    result = authenticator.GetCachedResult(account.Realm.ToUpper() + "x", account.Account, account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    result = authenticator.GetCachedResult(account.Realm, account.Account + "x", account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    result = authenticator.GetCachedResult(account.Realm, account.Account.ToUpper() + "x", account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    result = authenticator.GetCachedResult(account.Realm, account.Account, account.Password + "x");
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);
                }
            }
            finally
            {
                authenticator.Close();
                authHandler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthServiceHandler_No_Fail_Cache()
        {
            // Verify that the authenticator doesn't cache failed authentications
            // when FailTTL=Zero.

            AuthServiceHandler authHandler = new AuthServiceHandler();
            Authenticator authenticator = new Authenticator();
            AuthenticatorSettings settings;
            AuthenticationResult result;

            try
            {
                authHandler.Start(router, null, null, null);

                settings = new AuthenticatorSettings();
                settings.FailTTL = TimeSpan.Zero;
                authenticator.Open(router, settings);

                // Verify that valid credentials are authenticated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);

                    result = authenticator.GetCachedResult(account.Realm, account.Account, account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreEqual(AuthenticationStatus.Authenticated, result.Status);
                }

                // Verify that invalid credentials are not authenicated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password + "x").Status);

                    Assert.IsNull(authenticator.GetCachedResult(account.Realm + "x", account.Account, account.Password));
                    Assert.IsNull(authenticator.GetCachedResult(account.Realm.ToUpper() + "x", account.Account, account.Password));
                    Assert.IsNull(authenticator.GetCachedResult(account.Realm, account.Account + "x", account.Password));
                    Assert.IsNull(authenticator.GetCachedResult(account.Realm, account.Account.ToUpper() + "x", account.Password));
                    Assert.IsNull(authenticator.GetCachedResult(account.Realm, account.Account, account.Password + "x"));
                }
            }
            finally
            {
                authenticator.Close();
                authHandler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthServiceHandler_No_Cache()
        {
            // Verify that the authenticator doesn't cache failed authentications
            // when FailTTL=Zero.

            AuthServiceHandler authHandler = new AuthServiceHandler();
            Authenticator authenticator = new Authenticator();
            AuthenticatorSettings settings;

            try
            {
                authHandler.Start(router, null, null, null);

                settings = new AuthenticatorSettings();
                settings.MaxCacheSize = 0;
                authenticator.Open(router, settings);

                // Verify that valid credentials are authenticated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);

                    Assert.IsNull(authenticator.GetCachedResult(account.Realm, account.Account, account.Password));
                }

                // Verify that invalid credentials are not authenicated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password + "x").Status);

                    Assert.IsNull(authenticator.GetCachedResult(account.Realm + "x", account.Account, account.Password));
                    Assert.IsNull(authenticator.GetCachedResult(account.Realm.ToUpper() + "x", account.Account, account.Password));
                    Assert.IsNull(authenticator.GetCachedResult(account.Realm, account.Account + "x", account.Password));
                    Assert.IsNull(authenticator.GetCachedResult(account.Realm, account.Account.ToUpper() + "x", account.Password));
                    Assert.IsNull(authenticator.GetCachedResult(account.Realm, account.Account, account.Password + "x"));
                }
            }
            finally
            {
                authenticator.Close();
                authHandler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthServiceHandler_Flush_Success_Cache()
        {
            // Verify that the success cache entries are flushed.

            AuthServiceHandler authHandler = new AuthServiceHandler();
            Authenticator authenticator = new Authenticator();
            AuthenticatorSettings settings;
            AuthenticationResult result;
            int count;

            try
            {
                authHandler.Start(router, null, null, null);

                settings = new AuthenticatorSettings();
                settings.SuccessTTL = TimeSpan.FromSeconds(8);
                settings.CacheFlushInterval = TimeSpan.FromSeconds(1);
                authenticator.Open(router, settings);

                // Verify that valid credentials are authenticated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);

                    result = authenticator.GetCachedResult(account.Realm, account.Account, account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreEqual(AuthenticationStatus.Authenticated, result.Status);

                    // Authenticate again with the same credentials and verify that the 
                    // cached result was used rather then performing another query.

                    count = authHandler.AuthCount;
                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);
                    Assert.AreEqual(count, authHandler.AuthCount);
                }

                // Verify that invalid credentials are not authenicated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password + "x").Status);

                    result = authenticator.GetCachedResult(account.Realm + "x", account.Account, account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    result = authenticator.GetCachedResult(account.Realm.ToUpper() + "x", account.Account, account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    result = authenticator.GetCachedResult(account.Realm, account.Account + "x", account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    result = authenticator.GetCachedResult(account.Realm, account.Account.ToUpper() + "x", account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    result = authenticator.GetCachedResult(account.Realm, account.Account, account.Password + "x");
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    // Authenticate again with the same credentials and verify that the 
                    // cached result was used rather then performing another query.

                    count = authHandler.AuthCount;
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                    Assert.AreEqual(count, authHandler.AuthCount);
                }

                Thread.Sleep(TimeSpan.FromSeconds(10));

                // Verify that the success items have been flushed

                foreach (AuthTestAccount account in state.Accounts)
                    Assert.IsNull(authenticator.GetCachedResult(account.Realm, account.Account, account.Password));

                // Verify that the failed items have not been flushed

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.IsNotNull(authenticator.GetCachedResult(account.Realm + "x", account.Account, account.Password));
                    Assert.IsNotNull(authenticator.GetCachedResult(account.Realm, account.Account + "x", account.Password));
                    Assert.IsNotNull(authenticator.GetCachedResult(account.Realm, account.Account, account.Password + "x"));
                }
            }
            finally
            {
                authenticator.Close();
                authHandler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthServiceHandler_Flush_Fail_Cache()
        {
            // Verify that the fail cache entries are flushed.

            AuthServiceHandler authHandler = new AuthServiceHandler();
            Authenticator authenticator = new Authenticator();
            AuthenticatorSettings settings;
            AuthenticationResult result;
            int count;

            try
            {
                authHandler.Start(router, null, null, null);

                settings = new AuthenticatorSettings();
                settings.FailTTL = TimeSpan.FromSeconds(8);
                settings.CacheFlushInterval = TimeSpan.FromSeconds(1);
                authenticator.Open(router, settings);

                // Verify that valid credentials are authenticated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);

                    result = authenticator.GetCachedResult(account.Realm, account.Account, account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreEqual(AuthenticationStatus.Authenticated, result.Status);

                    // Authenticate again with the same credentials and verify that the 
                    // cached result was used rather then performing another query.

                    count = authHandler.AuthCount;
                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);
                    Assert.AreEqual(count, authHandler.AuthCount);
                }

                // Verify that invalid credentials are not authenicated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password + "x").Status);

                    result = authenticator.GetCachedResult(account.Realm + "x", account.Account, account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    result = authenticator.GetCachedResult(account.Realm.ToUpper() + "x", account.Account, account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    result = authenticator.GetCachedResult(account.Realm, account.Account + "x", account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    result = authenticator.GetCachedResult(account.Realm, account.Account.ToUpper() + "x", account.Password);
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    result = authenticator.GetCachedResult(account.Realm, account.Account, account.Password + "x");
                    Assert.IsNotNull(result);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, result.Status);

                    // Authenticate again with the same credentials and verify that the 
                    // cached result was used rather then performing another query.

                    count = authHandler.AuthCount;
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                    Assert.AreEqual(count, authHandler.AuthCount);
                }

                Thread.Sleep(TimeSpan.FromSeconds(10));

                // Verify that the success items have not been flushed

                foreach (AuthTestAccount account in state.Accounts)
                    Assert.IsNotNull(authenticator.GetCachedResult(account.Realm, account.Account, account.Password));

                // Verify that the failed items have been flushed

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.IsNull(authenticator.GetCachedResult(account.Realm + "x", account.Account, account.Password));
                    Assert.IsNull(authenticator.GetCachedResult(account.Realm, account.Account + "x", account.Password));
                    Assert.IsNull(authenticator.GetCachedResult(account.Realm, account.Account, account.Password + "x"));
                }
            }
            finally
            {
                authenticator.Close();
                authHandler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthServiceHandler_KeyUpdate()
        {
            // Verify that authenticator instances respond to a BroadcastKeyUpdate().

            AuthServiceHandler authHandler = new AuthServiceHandler();
            Authenticator authenticator1 = new Authenticator();
            Authenticator authenticator2 = new Authenticator();
            AuthenticatorSettings settings;
            int count;

            try
            {
                authHandler.Start(router, null, null, null);

                settings = new AuthenticatorSettings();
                authenticator1.Open(router, settings);
                authenticator2.Open(router, settings);

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator1.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);

                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator2.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);

                    break;
                }

                count = authHandler.GetPublicKeyCount;

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator1.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);

                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator2.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);

                }

                Assert.AreEqual(count, authHandler.GetPublicKeyCount);

                authenticator1.ClearCache();
                authenticator2.ClearCache();
                authenticator1.BroadcastKeyUpdate();
                Thread.Sleep(1000);

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator1.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);

                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator2.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);
                }

                Assert.AreEqual(count + 2, authHandler.GetPublicKeyCount);
            }
            finally
            {
                authenticator1.Close();
                authenticator2.Close();
                authHandler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthServiceHandler_Cache_Control()
        {
            // Verify that authenticator instances respond to a cache control broadcasts.

            AuthServiceHandler authHandler = new AuthServiceHandler();
            Authenticator authenticator1 = new Authenticator();
            Authenticator authenticator2 = new Authenticator();
            AuthenticatorSettings settings;

            try
            {
                authHandler.Start(router, null, null, null);

                settings = new AuthenticatorSettings();
                authenticator1.Open(router, settings);
                authenticator2.Open(router, settings);

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator1.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);
                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator2.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);
                }

                Assert.IsNotNull(authenticator1.GetCachedResult("file.com", "file1", "file-password1"));
                Assert.IsNotNull(authenticator1.GetCachedResult("file.com", "file2", "file-password2"));
                Assert.IsNotNull(authenticator1.GetCachedResult("config.com", "config1", "config-password1"));
                Assert.IsNotNull(authenticator1.GetCachedResult("config.com", "config2", "config-password2"));
                Assert.IsNotNull(authenticator1.GetCachedResult("odbc.com", "odbc1", "odbc-password1"));
                Assert.IsNotNull(authenticator1.GetCachedResult("odbc.com", "odbc2", "odbc-password2"));

                Assert.IsNotNull(authenticator2.GetCachedResult("file.com", "file1", "file-password1"));
                Assert.IsNotNull(authenticator2.GetCachedResult("file.com", "file2", "file-password2"));
                Assert.IsNotNull(authenticator2.GetCachedResult("config.com", "config1", "config-password1"));
                Assert.IsNotNull(authenticator2.GetCachedResult("config.com", "config2", "config-password2"));
                Assert.IsNotNull(authenticator2.GetCachedResult("odbc.com", "odbc1", "odbc-password1"));
                Assert.IsNotNull(authenticator2.GetCachedResult("odbc.com", "odbc2", "odbc-password2"));

                // Test removal of a specific account.

                authenticator2.BroadcastCacheRemove("file.com", "file1");
                Thread.Sleep(1000);
                Assert.IsNull(authenticator1.GetCachedResult("file.com", "file1", "file-password1"));
                Assert.IsNull(authenticator2.GetCachedResult("file.com", "file1", "file-password1"));
                Assert.IsNotNull(authenticator1.GetCachedResult("file.com", "file2", "file-password2"));
                Assert.IsNotNull(authenticator2.GetCachedResult("file.com", "file2", "file-password2"));

                // Test removal of a realm

                authenticator1.BroadcastCacheRemove("config.com");
                Thread.Sleep(1000);
                Assert.IsNull(authenticator1.GetCachedResult("config.com", "config1", "config-password1"));
                Assert.IsNull(authenticator1.GetCachedResult("config.com", "config2", "config-password2"));
                Assert.IsNull(authenticator2.GetCachedResult("config.com", "config1", "config-password1"));
                Assert.IsNull(authenticator2.GetCachedResult("config.com", "config2", "config-password2"));

                // Test cache clearing

                authenticator2.BroadcastCacheClear();
                Thread.Sleep(1000);
                Assert.IsNull(authenticator1.GetCachedResult("odbc.com", "odbc1", "odbc-password1"));
                Assert.IsNull(authenticator1.GetCachedResult("odbc.com", "odbc2", "odbc-password2"));
                Assert.IsNull(authenticator2.GetCachedResult("odbc.com", "odbc1", "odbc-password1"));
                Assert.IsNull(authenticator2.GetCachedResult("odbc.com", "odbc2", "odbc-password2"));
            }
            finally
            {
                authenticator1.Close();
                authenticator2.Close();
                authHandler.Stop();
            }
        }

        private void CacheControl(Uri uri, string command, string realm, string account)
        {
            HttpConnection con = new HttpConnection(HttpOption.None);
            HttpRequest request;
            HttpResponse response;
            string query;

            try
            {
                query = "command=" + Helper.EscapeUri(command);

                if (realm != null)
                    query += "&realm=" + Helper.EscapeUri(realm);

                if (account != null)
                    query += "&account=" + Helper.EscapeUri(account);

                con.Connect(uri.Host, uri.Port);
                request = new HttpRequest(HttpStack.Http11, "get",
                                           string.Format("{0}?{1}", uri, query),
                                           null);

                request["host"] = uri.Host;
                request["accept"] = "*/*";

                response = con.Query(request, SysTime.Now + TimeSpan.FromSeconds(10));
                if ((int)response.Status < 200 || (int)response.Status > 299)
                    throw new HttpException(response.Status);
            }
            finally
            {
                con.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthServiceHandler_Cache_Control_Via_HTTP()
        {
            // Verify that authenticator instances respond to a cache control broadcasts
            // initiated by HTTP CACHE.JSON commands.

            AuthServiceHandler authHandler = new AuthServiceHandler();
            Authenticator authenticator1 = new Authenticator();
            Authenticator authenticator2 = new Authenticator();
            AuthenticatorSettings settings;
            string machineName = Helper.MachineName;
            Uri uri = new Uri(string.Format("http://{0}:37614/authenticate/Cache.json", machineName));

            try
            {
                authHandler.Start(router, "HTTP.AuthService", null, null);

                settings = new AuthenticatorSettings();
                authenticator1.Open(router, settings);
                authenticator2.Open(router, settings);

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator1.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);
                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator2.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);
                }

                Assert.IsNotNull(authenticator1.GetCachedResult("file.com", "file1", "file-password1"));
                Assert.IsNotNull(authenticator1.GetCachedResult("file.com", "file2", "file-password2"));
                Assert.IsNotNull(authenticator1.GetCachedResult("config.com", "config1", "config-password1"));
                Assert.IsNotNull(authenticator1.GetCachedResult("config.com", "config2", "config-password2"));
                Assert.IsNotNull(authenticator1.GetCachedResult("odbc.com", "odbc1", "odbc-password1"));
                Assert.IsNotNull(authenticator1.GetCachedResult("odbc.com", "odbc2", "odbc-password2"));

                Assert.IsNotNull(authenticator2.GetCachedResult("file.com", "file1", "file-password1"));
                Assert.IsNotNull(authenticator2.GetCachedResult("file.com", "file2", "file-password2"));
                Assert.IsNotNull(authenticator2.GetCachedResult("config.com", "config1", "config-password1"));
                Assert.IsNotNull(authenticator2.GetCachedResult("config.com", "config2", "config-password2"));
                Assert.IsNotNull(authenticator2.GetCachedResult("odbc.com", "odbc1", "odbc-password1"));
                Assert.IsNotNull(authenticator2.GetCachedResult("odbc.com", "odbc2", "odbc-password2"));

                // Test removal of a specific account.

                CacheControl(uri, "cache-remove-account", "file.com", "file1");
                Thread.Sleep(1000);
                Assert.IsNull(authenticator1.GetCachedResult("file.com", "file1", "file-password1"));
                Assert.IsNull(authenticator2.GetCachedResult("file.com", "file1", "file-password1"));
                Assert.IsNotNull(authenticator1.GetCachedResult("file.com", "file2", "file-password2"));
                Assert.IsNotNull(authenticator2.GetCachedResult("file.com", "file2", "file-password2"));

                // Test removal of a realm

                CacheControl(uri, "cache-remove-realm", "config.com", null);
                Thread.Sleep(1000);
                Assert.IsNull(authenticator1.GetCachedResult("config.com", "config1", "config-password1"));
                Assert.IsNull(authenticator1.GetCachedResult("config.com", "config2", "config-password2"));
                Assert.IsNull(authenticator2.GetCachedResult("config.com", "config1", "config-password1"));
                Assert.IsNull(authenticator2.GetCachedResult("config.com", "config2", "config-password2"));

                // Test cache clearing

                CacheControl(uri, "cache-clear", null, null);
                Thread.Sleep(1000);
                Assert.IsNull(authenticator1.GetCachedResult("odbc.com", "odbc1", "odbc-password1"));
                Assert.IsNull(authenticator1.GetCachedResult("odbc.com", "odbc2", "odbc-password2"));
                Assert.IsNull(authenticator2.GetCachedResult("odbc.com", "odbc1", "odbc-password1"));
                Assert.IsNull(authenticator2.GetCachedResult("odbc.com", "odbc2", "odbc-password2"));
            }
            finally
            {
                authenticator1.Close();
                authenticator2.Close();
                authHandler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthServiceHandler_Radius()
        {
            // Crank up an AuthServiceHandler instance with two RADIUS ports and
            // then use the RADIUS client to perform test authentications against them.

            AuthServiceHandler authHandler = new AuthServiceHandler();
            RadiusClient client1 = new RadiusClient();
            RadiusClient client2 = new RadiusClient();

            try
            {
                client1.Open(new RadiusClientSettings(NetworkBinding.Parse("localhost:1645"), "mysecret"));
                client2.Open(new RadiusClientSettings(NetworkBinding.Parse("localhost:1646"), "mysecret"));

                authHandler.Start(router, "RADIUS.AuthService", null, null);

                // Verify that valid credentials are authenticated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.IsTrue(client1.Authenticate(account.Realm, account.Account, account.Password));
                    Assert.IsTrue(client2.Authenticate(account.Realm, account.Account, account.Password));
                }

                // Verify that invalid credentials are not authenicated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.IsFalse(client1.Authenticate(account.Realm + "x", account.Account, account.Password));
                    Assert.IsFalse(client1.Authenticate(account.Realm, account.Account + "x", account.Password));
                    Assert.IsFalse(client1.Authenticate(account.Realm, account.Account, account.Password + "x"));

                    Assert.IsFalse(client2.Authenticate(account.Realm + "x", account.Account, account.Password));
                    Assert.IsFalse(client2.Authenticate(account.Realm, account.Account + "x", account.Password));
                    Assert.IsFalse(client2.Authenticate(account.Realm, account.Account, account.Password + "x"));
                }
            }
            finally
            {
                client1.Close();
                client2.Close();
                authHandler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthServiceHandler_Broadcast_Cache_Control()
        {
            // Verify that AuthServiceHandler implements broadcast cache control messages.

            AuthServiceHandler authHandler = new AuthServiceHandler();
            Authenticator authenticator = new Authenticator();
            AuthenticatorSettings settings;

            try
            {
                authHandler.Start(router, null, null, null);

                settings = new AuthenticatorSettings();
                authenticator.Open(router, settings);

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    account.Realm + "/" + account.Account + ":" + account.Password);

                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password + "x").Status,
                                       account.Realm + "/" + account.Account + ":" + account.Password + "x");
                }

                Assert.IsTrue(authHandler.Engine.IsCached("file.com", "file1", "file-password1"));
                Assert.IsTrue(authHandler.Engine.IsCached("file.com", "file2", "file-password2"));
                Assert.IsTrue(authHandler.Engine.IsCached("config.com", "config1", "config-password1"));
                Assert.IsTrue(authHandler.Engine.IsCached("config.com", "config2", "config-password2"));
                Assert.IsTrue(authHandler.Engine.IsCached("odbc.com", "odbc1", "odbc-password1"));
                Assert.IsTrue(authHandler.Engine.IsCached("odbc.com", "odbc2", "odbc-password2"));

                Assert.IsTrue(authHandler.Engine.IsNakCached("file.com", "file1", "file-password1x"));
                Assert.IsTrue(authHandler.Engine.IsNakCached("file.com", "file2", "file-password2x"));
                Assert.IsTrue(authHandler.Engine.IsNakCached("config.com", "config1", "config-password1x"));
                Assert.IsTrue(authHandler.Engine.IsNakCached("config.com", "config2", "config-password2x"));
                Assert.IsTrue(authHandler.Engine.IsNakCached("odbc.com", "odbc1", "odbc-password1x"));
                Assert.IsTrue(authHandler.Engine.IsNakCached("odbc.com", "odbc2", "odbc-password2x"));

                // Test removal of a specific account.

                authenticator.BroadcastCacheRemove("file.com", "file1");
                Thread.Sleep(1000);
                Assert.IsFalse(authHandler.Engine.IsCached("file.com", "file1", "file-password1"));
                Assert.IsFalse(authHandler.Engine.IsNakCached("file.com", "file1", "file-password1x"));
                Assert.IsTrue(authHandler.Engine.IsCached("file.com", "file2", "file-password2"));
                Assert.IsTrue(authHandler.Engine.IsNakCached("file.com", "file2", "file-password2x"));

                // Test removal of a realm

                authenticator.BroadcastCacheRemove("config.com");
                Thread.Sleep(1000);
                Assert.IsFalse(authHandler.Engine.IsCached("config.com", "config1", "config-password1"));
                Assert.IsFalse(authHandler.Engine.IsNakCached("config.com", "config1", "config-password1x"));
                Assert.IsFalse(authHandler.Engine.IsCached("config.com", "config2", "config-password2"));
                Assert.IsFalse(authHandler.Engine.IsNakCached("config.com", "config2", "config-password2x"));

                // Test cache clearing

                authenticator.BroadcastCacheClear();
                Thread.Sleep(1000);
                Assert.IsFalse(authHandler.Engine.IsCached("odbc.com", "odbc1", "odbc-password1"));
                Assert.IsFalse(authHandler.Engine.IsNakCached("odbc.com", "odbc1", "odbc-password1x"));
                Assert.IsFalse(authHandler.Engine.IsCached("odbc.com", "odbc2", "odbc-password2"));
                Assert.IsFalse(authHandler.Engine.IsNakCached("odbc.com", "odbc2", "odbc-password2x"));
            }
            finally
            {
                authenticator.Close();
                authHandler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthServiceHandler_Protocol_JSON_GET()
        {
            // Crank up an AuthServiceHandler instance on HTTP/JSON URIs and
            // then use HttpConnection to perform GET authentications against them.

            AuthServiceHandler authHandler = new AuthServiceHandler();
            string machineName = Helper.MachineName;
            AuthTestJsonClient jsonClient = new AuthTestJsonClient(string.Format("http://{0}:37614/authenticate/Auth.json", machineName));

            try
            {
                EnhancedDns.AddHost("mymachine.com", IPAddress.Loopback);

                authHandler.Start(router, "HTTP.AuthService", null, null);

                // Verify that valid credentials are authenticated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {

                    jsonClient.Uri = "http://mymachine.com:37614/authenticate/Auth.json";
                    Assert.AreEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaGet(account.Realm, account.Account, account.Password).Status,
                                    string.Format("mymachine.com: realm={0} account={1}", account.Realm, account.Account));

                    jsonClient.Uri = string.Format("http://{0}:37614/authenticate/Auth.json", machineName);
                    Assert.AreEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaGet(account.Realm, account.Account, account.Password).Status,
                                    string.Format("MACHINENAME: realm={0} account={1}", account.Realm, account.Account));

                    jsonClient.Uri = "http://localhost:80/authenticate/Auth.json";
                    Assert.AreEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaGet(account.Realm, account.Account, account.Password).Status,
                                    string.Format("localhost: realm={0} account={1}", account.Realm, account.Account));
                }

                // Verify that invalid credentials are not authenicated and then cached.

                jsonClient.Uri = string.Format("http://{0}:80/authenticate/Auth.json", machineName);
                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaGet(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaGet(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaGet(account.Realm, account.Account, account.Password + "x").Status);

                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaGet(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaGet(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaGet(account.Realm, account.Account, account.Password + "x").Status);
                }
            }
            finally
            {
                EnhancedDns.RemoveHosts();
                authHandler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthServiceHandler_Protocol_JSON_POST()
        {
            // Crank up an AuthServiceHandler instance on HTTP/JSON URIs and
            // then use HttpConnection to perform POST authentications against them.

            AuthServiceHandler authHandler = new AuthServiceHandler();
            string machineName = Helper.MachineName;
            AuthTestJsonClient jsonClient = new AuthTestJsonClient(string.Format("http://{0}:80/authenticate/Auth.json", machineName));

            try
            {
                EnhancedDns.AddHost("mymachine.com", IPAddress.Loopback);

                authHandler.Start(router, "HTTP.AuthService", null, null);

                // Verify that valid credentials are authenticated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {

                    jsonClient.Uri = "http://mymachine.com:37614/authenticate/Auth.json";
                    Assert.AreEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaPost(account.Realm, account.Account, account.Password).Status,
                                    string.Format("mymachine.com: realm={0} account={1}", account.Realm, account.Password));

                    jsonClient.Uri = string.Format("http://{0}:37614/authenticate/Auth.json", machineName);
                    Assert.AreEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaPost(account.Realm, account.Account, account.Password).Status,
                                    string.Format("MACHINENAME: realm={0} account={1}", account.Realm, account.Password));

                    jsonClient.Uri = "http://localhost:80/authenticate/Auth.json";
                    Assert.AreEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaPost(account.Realm, account.Account, account.Password).Status,
                                    string.Format("localhost: realm={0} account={1}", account.Realm, account.Password));
                }

                // Verify that invalid credentials are not authenicated and then cached.

                jsonClient.Uri = string.Format("http://{0}:80/authenticate/Auth.json", machineName);
                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaPost(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaPost(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaPost(account.Realm, account.Account, account.Password + "x").Status);

                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaPost(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaPost(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaPost(account.Realm, account.Account, account.Password + "x").Status);
                }
            }
            finally
            {
                EnhancedDns.RemoveHosts();
                authHandler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthServiceHandler_Protocol_WCF_BasicHTTP()
        {
            // Crank up an AuthServiceHandler instance with a WCF endpoint
            // and run some authentications.

            AuthServiceHandler authHandler = new AuthServiceHandler();
            AuthServiceHandlerClient client = new AuthServiceHandlerClient(new BasicHttpBinding(), new EndpointAddress("http://localhost:37615/authenticate/auth.svc"));

            try
            {
                authHandler.Start(router, "WCF.AuthService", null, null);
                client.Open();

                // Verify that valid credentials are authenticated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreEqual(AuthenticationStatus.Authenticated, client.Authenticate(account.Realm, account.Account, account.Password).Status,
                                    string.Format("realm={0} account={1} password={2}", account.Realm, account.Account, account.Password));
                }

                // Verify that invalid credentials are not authenicated and then cached.

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, client.Authenticate(account.Realm + "x", account.Account, account.Password).Status,
                                       string.Format("realm={0} account={1} password={2}", account.Realm + "x", account.Account, account.Password));

                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, client.Authenticate(account.Realm, account.Account + "x", account.Password).Status,
                                       string.Format("realm={0} account={1} password={2}", account.Realm, account.Account + "x", account.Password));

                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, client.Authenticate(account.Realm, account.Account, account.Password + "x").Status,
                                       string.Format("realm={0} account={1} password={2}", account.Realm, account.Account, account.Password + "x"));
                }
            }
            finally
            {
                if (client.State == CommunicationState.Opened)
                    client.Close();

                authHandler.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthServiceHandler_Clustered_AccountLock()
        {
            // Verify that account lockout work properly with two authentication
            // service instances and two authenticator client instances.

            const int lockCount = 4;
            const string realm = "test.com";
            const string account = "account0";
            const string password = "password0";

            AuthServiceHandler svc1 = new AuthServiceHandler();
            AuthServiceHandler svc2 = new AuthServiceHandler();
            Authenticator client1 = new Authenticator();
            Authenticator client2 = new Authenticator();

            svc1 = new AuthServiceHandler();
            svc2 = new AuthServiceHandler();


            try
            {
                svc1.Start(router, "Lockout.AuthService", null, null);
                svc2.Start(router, "Lockout.AuthService", null, null);

                client1.Open(router, new AuthenticatorSettings());
                client2.Open(router, new AuthenticatorSettings());

                // I'm going to run the test below serveral times to make sure that
                // we get a good distribution of load balancing against the
                // service instances.

                for (int i = 0; i < 1; i++)
                {
                    // Preload successful authentications into both clients

                    Assert.AreEqual(AuthenticationStatus.Authenticated, client1.Authenticate(realm, account, password).Status);
                    Assert.AreEqual(AuthenticationStatus.Authenticated, client1.GetCachedResult(realm, account, password).Status);
                    Assert.AreEqual(AuthenticationStatus.Authenticated, client2.Authenticate(realm, account, password).Status);
                    Assert.AreEqual(AuthenticationStatus.Authenticated, client2.GetCachedResult(realm, account, password).Status);

                    for (int j = 0; j < lockCount; j++)
                    {
                        // Attempt enough bad authentications to force an account lock

                        if ((j & 1) == 0)
                            Assert.AreNotEqual(AuthenticationStatus.Authenticated, client1.Authenticate(realm, account, password + "x"));
                        else
                            Assert.AreNotEqual(AuthenticationStatus.Authenticated, client2.Authenticate(realm, account, password + "x"));
                    }

                    Thread.Sleep(1000);

                    // Verify that the account is locked on the servers and that the
                    // success cache has been cleared on the clients.

                    Assert.IsTrue(svc1.Engine.IsNakLocked(realm, account));
                    Assert.IsTrue(svc2.Engine.IsNakLocked(realm, account));
                    Assert.IsNull(client1.GetCachedResult(realm, account, password));
                    Assert.IsNull(client2.GetCachedResult(realm, account, password));

                    // Broadcast a message to remove the account from all caches
                    // and then verify that they were deleted.

                    if ((i & 1) == 0)
                        client1.BroadcastCacheRemove(realm, account);
                    else
                        client2.BroadcastCacheRemove(realm, account);

                    Thread.Sleep(1000);

                    Assert.IsFalse(svc1.Engine.IsNakLocked(realm, account));
                    Assert.IsFalse(svc2.Engine.IsNakLocked(realm, account));
                    Assert.IsNull(client1.GetCachedResult(realm, account, password));
                    Assert.IsNull(client2.GetCachedResult(realm, account, password));
                }
            }
            finally
            {
                client1.Close();
                client2.Close();

                svc1.Stop();
                svc2.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthServiceHandler_Clustered_CredentialBroadcast()
        {
            // Verify that account credentials authenticated by one service instance
            // are transmitted to the others.

            Authenticator client = new Authenticator();
            AuthServiceHandler[] cluster;

            cluster = new AuthServiceHandler[4];
            for (int i = 0; i < cluster.Length; i++)
                cluster[i] = new AuthServiceHandler();

            try
            {
                foreach (AuthServiceHandler svc in cluster)
                    svc.Start(router, null, null, null);

                client.Open(router, new AuthenticatorSettings());

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.AreEqual(AuthenticationStatus.Authenticated, client.Authenticate(account.Realm, account.Account, account.Password).Status);
                    Thread.Sleep(1000);

                    foreach (AuthServiceHandler svc in cluster)
                        Assert.IsTrue(svc.Engine.IsCached(account.Realm, account.Account, account.Password));
                }
            }
            finally
            {
                client.Close();

                foreach (AuthServiceHandler svc in cluster)
                    svc.Stop();
            }
        }
    }
}

