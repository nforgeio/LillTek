//-----------------------------------------------------------------------------
// FILE:        App_AuthService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.ServiceModel;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.AuthService.Test
{
    [TestClass]
    public class App_AuthService
    {
        [TestInitialize]
        public void Initialize()
        {
            NetTrace.Start();
            NetTrace.Enable(MsgRouter.TraceSubsystem, 0);
        }

        [TestCleanup]
        public void Cleanup()
        {
            NetTrace.Stop();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Apps")]
        public void AuthService_EndToEnd_Basic()
        {
            // Do some authentications against a file realm mapper and a file
            // authentication extension.

            LeafRouter router = null;
            Process svcProcess = null;
            Authenticator authenticator = null;
            Assembly assembly;
            StreamWriter writer;
            StreamReader reader;
            string realmsPath;
            string accountsPath;
            string orgRealms = null;
            string orgAccounts = null;

            assembly = typeof(LillTek.Datacenter.AuthService.Program).Assembly;
            realmsPath = Helper.GetAssemblyFolder(assembly) + "Realms.txt";
            accountsPath = Helper.GetAssemblyFolder(assembly) + "Accounts.txt";

            try
            {
                // Start a client router for the test and create an authenticator.

                Config.SetConfig(@"

//-----------------------------------------------------------------------------
// LeafRouter Settings

&section MsgRouter

    AppName                = LillTek.Test Router
    AppDescription         = Extensible authentication hub
    RouterEP			   = physical://DETACHED/$(LillTek.DC.DefHubName)/$(Guid)
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
    
    // This maps the abstract authentication service client and server endpoints
    // to their default logical endpoints.

    AbstractMap[abstract://LillTek/DataCenter/Auth/Client]  = logical://LillTek/DataCenter/Auth/Client
    AbstractMap[abstract://LillTek/DataCenter/Auth/Service] = logical://LillTek/DataCenter/Auth/Service
    AbstractMap[abstract://LillTek/DataCenter/Auth/*]       = logical://LillTek/DataCenter/Auth/*

&endsection
".Replace('&', '#'));

                router = new LeafRouter();
                router.Start(); ;

                authenticator = new Authenticator();
                authenticator.Open(router, new AuthenticatorSettings());

                // Initialize Realms.txt and Accounts.txt with some test accounts

                try
                {
                    reader = new StreamReader(realmsPath, Helper.AnsiEncoding);
                    orgRealms = reader.ReadToEnd();
                    reader.Close();
                }
                catch
                {
                    orgRealms = "";
                }

                writer = new StreamWriter(realmsPath, false, Helper.AnsiEncoding);
                writer.WriteLine("$$LillTek.Datacenter.Server.FileAuthenticationExtension:LillTek.Datacenter.Server.dll$$path=$(AppPath)/accounts.txt;reload=yes;maxCacheTime=5m$$");
                writer.Close();

                try
                {
                    reader = new StreamReader(accountsPath, Helper.AnsiEncoding);
                    orgAccounts = reader.ReadToEnd();
                    reader.Close();
                }
                catch
                {
                    orgAccounts = "";
                }

                writer = new StreamWriter(accountsPath, false, Helper.AnsiEncoding);
                writer.WriteLine(";account1;password1");
                writer.WriteLine(";account2;password2");
                writer.Close();

                // Start the authentication service

                svcProcess = Helper.StartProcess(assembly, "-mode:form -start");
                Thread.Sleep(10000);     // Give the process a chance to spin up

                // Perform the tests.

                Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate("", "account1", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate("", "account2", "password2").Status);

                Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate("x", "account1", "password1").Status);
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate("", "account2x", "password2").Status);
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate("", "account2x", "password2x").Status);
            }
            finally
            {
                if (svcProcess != null)
                {
                    svcProcess.Kill();
                    svcProcess.Close();
                }

                if (authenticator != null)
                    authenticator.Close();

                if (router != null)
                    router.Stop();

                // Restore the Realms.txt and Accounts.txt files.

                if (orgRealms != null)
                {
                    writer = new StreamWriter(realmsPath, false, Helper.AnsiEncoding);
                    writer.Write(orgRealms);
                    writer.Close();
                }

                if (orgAccounts != null)
                {
                    writer = new StreamWriter(accountsPath, false, Helper.AnsiEncoding);
                    writer.Write(orgAccounts);
                    writer.Close();
                }

                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Apps")]
        public void AuthService_EndToEnd_Advanced()
        {
            // Perform tests against LDAP, ODBC, FILE, and RADIUS sources.  Note that
            // I'm explicitly avoiding testing the CONFIG source.

            LeafRouter router = null;
            Process svcProcess = null;
            Authenticator authenticator = null;
            AuthTestState state = null;
            Assembly assembly;
            StreamWriter writer;
            StreamReader reader;
            string realmsPath;
            string orgRealms = null;
            AuthServiceHandlerClient wcfClient = null;
            AuthTestJsonClient jsonClient = null;

            assembly = typeof(LillTek.Datacenter.AuthService.Program).Assembly;
            realmsPath = Helper.GetAssemblyFolder(assembly) + "Realms.txt";

            try
            {
                // Start a client router for the test and create an authenticator
                // and WCF client.

                Config.SetConfig(@"

//-----------------------------------------------------------------------------
// LeafRouter Settings

&section MsgRouter

    AppName                = LillTek.Test Router
    AppDescription         = Extensible authentication hub
    RouterEP			   = physical://DETACHED/$(LillTek.DC.DefHubName)/$(Guid)
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
    
    // This maps the abstract authentication service client and server endpoints
    // to their default logical endpoints.

    AbstractMap[abstract://LillTek/DataCenter/Auth/Client]  = logical://LillTek/DataCenter/Auth/Client
    AbstractMap[abstract://LillTek/DataCenter/Auth/Service] = logical://LillTek/DataCenter/Auth/Service
    AbstractMap[abstract://LillTek/DataCenter/Auth/*]       = logical://LillTek/DataCenter/Auth/*

&endsection
".Replace('&', '#'));
                router = new LeafRouter();
                router.Start();

                authenticator = new Authenticator();
                authenticator.Open(router, new AuthenticatorSettings());

                // Initialize Realms.txt

                state = new AuthTestState();
                state.Initialize();

                try
                {
                    reader = new StreamReader(realmsPath, Helper.AnsiEncoding);
                    orgRealms = reader.ReadToEnd();
                    reader.Close();
                }
                catch
                {
                    orgRealms = "";
                }

                writer = new StreamWriter(realmsPath, false, Helper.AnsiEncoding);

                foreach (AuthTestExtensionType type in Enum.GetValues(typeof(AuthTestExtensionType)))
                {
                    if (type != AuthTestExtensionType.Config)
                        writer.WriteLine("{0}$${1}$${2}$${3}", state.GetRealm(type), state.GetType(type), state.GetArgs(type), state.GetQuery(type));
                }

                writer.Close();

                // Start the authentication service

                svcProcess = Helper.StartProcess(assembly, "-mode:form -start");
                Thread.Sleep(10000);     // Give the process a chance to spin up

                // Perform the tests.

                wcfClient = new AuthServiceHandlerClient(new BasicHttpBinding(), new EndpointAddress(string.Format("http://{0}:80/WCF-AuthService/Auth.svc", Helper.MachineName)));
                wcfClient.Open();

                jsonClient = new AuthTestJsonClient(string.Format("http://{0}:80/AuthService/Auth.json", Helper.MachineName));

                foreach (AuthTestAccount account in state.Accounts)
                {
                    if (account.ExtensionType == AuthTestExtensionType.Config)
                        continue;

                    // Test using the authenticator client 

                    Assert.AreEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password).Status);

                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, authenticator.Authenticate(account.Realm, account.Account, account.Password + "x").Status);

                    // Test using the JSON HTTP/GET

                    Assert.AreEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaGet(account.Realm, account.Account, account.Password).Status);

                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaGet(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaGet(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaGet(account.Realm, account.Account, account.Password + "x").Status);

                    // Test using the JSON HTTP/POST

                    Assert.AreEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaPost(account.Realm, account.Account, account.Password).Status);

                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaPost(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaPost(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, jsonClient.AuthenticateViaPost(account.Realm, account.Account, account.Password + "x").Status);

                    // Test using the WCF client proxy

                    Assert.AreEqual(AuthenticationStatus.Authenticated, wcfClient.Authenticate(account.Realm, account.Account, account.Password).Status);

                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, wcfClient.Authenticate(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, wcfClient.Authenticate(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, wcfClient.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                }
            }
            finally
            {
                if (authenticator != null)
                    authenticator.Close();

                if (router != null)
                    router.Stop();

                // Restore Realms.txt.

                if (orgRealms != null)
                {
                    writer = new StreamWriter(realmsPath, false, Helper.AnsiEncoding);
                    writer.Write(orgRealms);
                    writer.Close();
                }

                if (wcfClient != null && wcfClient.State == CommunicationState.Opened)
                    wcfClient.Close();

                if (svcProcess != null)
                {
                    svcProcess.Kill();
                    svcProcess.Close();
                }

                if (state != null)
                    state.Cleanup();

                Config.SetConfig(null);
            }
        }
    }
}

