//-----------------------------------------------------------------------------
// FILE:        _AuthenticationEngine.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Data;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.Datacenter.Msgs;
using LillTek.Messaging;
using LillTek.Net.Radius;
using LillTek.Net.Sockets;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Server.Test
{
    [TestClass]
    public class _AuthenticationEngine
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_AuthEngineSettings()
        {
            AuthenticationEngineSettings settings = new AuthenticationEngineSettings();
            ConfigRealmMapProvider realmMapProvider = new ConfigRealmMapProvider();

            realmMapProvider.Open(settings, "xxx");
            Assert.AreEqual(TimeSpan.FromMinutes(10), settings.RealmMapLoadInterval);
            Assert.AreEqual(TimeSpan.FromMinutes(10), settings.CacheTTL);
            Assert.AreEqual(100000, settings.MaxCacheSize);
            Assert.AreEqual(TimeSpan.FromMinutes(5), settings.NakCacheTTL);
            Assert.AreEqual(100000, settings.MaxNakCacheSize);
            Assert.AreEqual(TimeSpan.FromMinutes(1), settings.CacheFlushInterval);
            Assert.AreEqual(TimeSpan.FromSeconds(5), settings.BkTaskInterval);
            Assert.AreEqual(true, settings.LogAuthSuccess);
            Assert.AreEqual(true, settings.LogAuthFailure);

            try
            {
                Config.SetConfig(null);

                settings = new AuthenticationEngineSettings();
                Assert.AreEqual(TimeSpan.FromMinutes(10), settings.RealmMapLoadInterval);
                Assert.AreEqual(100000, settings.MaxCacheSize);
                Assert.AreEqual(TimeSpan.FromMinutes(5), settings.NakCacheTTL);
                Assert.AreEqual(100000, settings.MaxNakCacheSize);
                Assert.AreEqual(TimeSpan.FromMinutes(1), settings.CacheFlushInterval);
                Assert.AreEqual(TimeSpan.FromSeconds(5), settings.BkTaskInterval);
                Assert.AreEqual(true, settings.LogAuthSuccess);
                Assert.AreEqual(true, settings.LogAuthFailure);
                Assert.AreEqual(TimeSpan.FromMinutes(5), settings.NakCacheTTL);

                Config.SetConfig(@"

Test.RealmMapLoadInterval = 1m
Test.CacheTTL             = 2m
Test.MaxCacheSize         = 4
Test.CacheFlushInterval   = 3m
Test.NakCacheTTL          = 11m
Test.MaxNakCacheSize      = 77
Test.BkTaskInterval       = 5m
Test.LogAuthSuccess       = YES
Test.LogAuthFailure       = no
Test.NakCacheTime         = 6m
");

                settings = AuthenticationEngineSettings.LoadConfig("Test");
                Assert.AreEqual(TimeSpan.FromMinutes(1), settings.RealmMapLoadInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(2), settings.CacheTTL);
                Assert.AreEqual(4, settings.MaxCacheSize);
                Assert.AreEqual(TimeSpan.FromMinutes(11), settings.NakCacheTTL);
                Assert.AreEqual(77, settings.MaxNakCacheSize);
                Assert.AreEqual(TimeSpan.FromMinutes(3), settings.CacheFlushInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(5), settings.BkTaskInterval);
                Assert.AreEqual(true, settings.LogAuthSuccess);
                Assert.AreEqual(false, settings.LogAuthFailure);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_FileRealmMapProvider()
        {
            string path = Path.GetTempFileName();
            AuthenticationEngineSettings settings = new AuthenticationEngineSettings();
            StreamWriter writer;
            FileRealmMapProvider realmProvider;
            List<RealmMapping> map;

            Helper.InitializeApp(Assembly.GetExecutingAssembly());

            try
            {
                writer = new StreamWriter(path, false, Helper.AnsiEncoding);

                writer.WriteLine("// This is a comment line");
                writer.WriteLine("    // Indented comment line");
                writer.WriteLine();
                writer.WriteLine("file.com$$LillTek.Datacenter.Server.FileAuthenticationExtension:LillTek.Datacenter.Server.dll$$test=file$$");
                writer.WriteLine("  ldap.com $$ LillTek.Datacenter.Server.LdapAuthenticationExtension:LillTek.Datacenter.Server.dll $$ test=ldap $$ query1 ");
                writer.WriteLine("odbc.com$$LillTek.Datacenter.Server.OdbcAuthenticationExtension:LillTek.Datacenter.Server.dll$$test=odbc$$query2");
                writer.WriteLine("radius.com$$LillTek.Datacenter.Server.RadiusAuthenticationExtension:LillTek.Datacenter.Server.dll$$test=radius$$query3");
                writer.WriteLine("config.com$$LillTek.Datacenter.Server.ConfigAuthenticationExtension:LillTek.Datacenter.Server.dll$$test=config;LockoutCount=2;LockoutThreshold=3m;LockoutTime=4m$$query4");

                writer.Close();

                realmProvider = new FileRealmMapProvider();
                realmProvider.Open(settings, path);
                map = realmProvider.GetMap();
                realmProvider.Close();

                Assert.AreEqual(5, map.Count);

                Assert.AreEqual(settings.LockoutCount, map[0].LockoutCount);
                Assert.AreEqual(settings.LockoutThreshold, map[0].LockoutThreshold);
                Assert.AreEqual(settings.LockoutTime, map[0].LockoutTime);

                Assert.AreEqual("file.com", map[0].Realm);
                Assert.AreEqual(typeof(FileAuthenticationExtension).FullName, map[0].ExtensionType.FullName);
                Assert.AreEqual("file", map[0].Args["test"]);
                Assert.AreEqual("", map[0].Query);

                Assert.AreEqual("ldap.com", map[1].Realm);
                Assert.AreEqual(typeof(LdapAuthenticationExtension).FullName, map[1].ExtensionType.FullName);
                Assert.AreEqual("ldap", map[1].Args["test"]);
                Assert.AreEqual("query1", map[1].Query);

                Assert.AreEqual("odbc.com", map[2].Realm);
                Assert.AreEqual(typeof(OdbcAuthenticationExtension).FullName, map[2].ExtensionType.FullName);
                Assert.AreEqual("odbc", map[2].Args["test"]);
                Assert.AreEqual("query2", map[2].Query);

                Assert.AreEqual("radius.com", map[3].Realm);
                Assert.AreEqual(typeof(RadiusAuthenticationExtension).FullName, map[3].ExtensionType.FullName);
                Assert.AreEqual("radius", map[3].Args["test"]);
                Assert.AreEqual("query3", map[3].Query);

                Assert.AreEqual("config.com", map[4].Realm);
                Assert.AreEqual(typeof(ConfigAuthenticationExtension).FullName, map[4].ExtensionType.FullName);
                Assert.AreEqual("config", map[4].Args["test"]);
                Assert.AreEqual("query4", map[4].Query);

                Assert.AreEqual(2, map[4].LockoutCount);
                Assert.AreEqual(TimeSpan.FromMinutes(3), map[4].LockoutThreshold);
                Assert.AreEqual(TimeSpan.FromMinutes(4), map[4].LockoutTime);

                // Make sure the provider actually reloads the file.

                writer = new StreamWriter(path, false, Helper.AnsiEncoding);
                writer.WriteLine("file.com$$LillTek.Datacenter.Server.FileAuthenticationExtension:LillTek.Datacenter.Server.dll$$test=file$$upload");
                writer.Close();

                realmProvider = new FileRealmMapProvider();
                realmProvider.Open(new AuthenticationEngineSettings(), path);
                map = realmProvider.GetMap();
                realmProvider.Close();

                Assert.AreEqual(1, map.Count);

                Assert.AreEqual("file.com", map[0].Realm);
                Assert.AreEqual(typeof(FileAuthenticationExtension).FullName, map[0].ExtensionType.FullName);
                Assert.AreEqual("file", map[0].Args["test"]);
                Assert.AreEqual("upload", map[0].Query);
            }
            finally
            {
                Helper.DeleteFile(path);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_ConfigRealmMapProvider()
        {
            string path = Path.GetTempFileName();
            AuthenticationEngineSettings settings = new AuthenticationEngineSettings();
            ConfigRealmMapProvider realmProvider;
            List<RealmMapping> map;

            Helper.InitializeApp(Assembly.GetExecutingAssembly());

            Config.SetConfig(@"

RealmMap[0] = file.com$$LillTek.Datacenter.Server.FileAuthenticationExtension:LillTek.Datacenter.Server.dll$$test=file$$
RealmMap[1] = ldap.com$$LillTek.Datacenter.Server.LdapAuthenticationExtension:LillTek.Datacenter.Server.dll$$test=ldap$$query1
RealmMap[2] = odbc.com$$LillTek.Datacenter.Server.OdbcAuthenticationExtension:LillTek.Datacenter.Server.dll$$test=odbc$$query2
RealmMap[3] = radius.com$$LillTek.Datacenter.Server.RadiusAuthenticationExtension:LillTek.Datacenter.Server.dll$$test=radius$$query3
RealmMap[4] = config.com$$LillTek.Datacenter.Server.ConfigAuthenticationExtension:LillTek.Datacenter.Server.dll$$test=config;LockoutCount=2;LockoutThreshold=3m;LockoutTime=4m$$query4
");

            realmProvider = new ConfigRealmMapProvider();
            realmProvider.Open(settings, "RealmMap");
            map = realmProvider.GetMap();
            realmProvider.Close();

            Assert.AreEqual(5, map.Count);

            Assert.AreEqual(settings.LockoutCount, map[0].LockoutCount);
            Assert.AreEqual(settings.LockoutThreshold, map[0].LockoutThreshold);
            Assert.AreEqual(settings.LockoutTime, map[0].LockoutTime);

            Assert.AreEqual("file.com", map[0].Realm);
            Assert.AreEqual(typeof(FileAuthenticationExtension).FullName, map[0].ExtensionType.FullName);
            Assert.AreEqual("file", map[0].Args["test"]);
            Assert.AreEqual("", map[0].Query);

            Assert.AreEqual("ldap.com", map[1].Realm);
            Assert.AreEqual(typeof(LdapAuthenticationExtension).FullName, map[1].ExtensionType.FullName);
            Assert.AreEqual("ldap", map[1].Args["test"]);
            Assert.AreEqual("query1", map[1].Query);

            Assert.AreEqual("odbc.com", map[2].Realm);
            Assert.AreEqual(typeof(OdbcAuthenticationExtension).FullName, map[2].ExtensionType.FullName);
            Assert.AreEqual("odbc", map[2].Args["test"]);
            Assert.AreEqual("query2", map[2].Query);

            Assert.AreEqual("radius.com", map[3].Realm);
            Assert.AreEqual(typeof(RadiusAuthenticationExtension).FullName, map[3].ExtensionType.FullName);
            Assert.AreEqual("radius", map[3].Args["test"]);
            Assert.AreEqual("query3", map[3].Query);

            Assert.AreEqual("config.com", map[4].Realm);
            Assert.AreEqual(typeof(ConfigAuthenticationExtension).FullName, map[4].ExtensionType.FullName);
            Assert.AreEqual("config", map[4].Args["test"]);
            Assert.AreEqual("query4", map[4].Query);

            Assert.AreEqual(2, map[4].LockoutCount);
            Assert.AreEqual(TimeSpan.FromMinutes(3), map[4].LockoutThreshold);
            Assert.AreEqual(TimeSpan.FromMinutes(4), map[4].LockoutTime);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_OdbcRealmMapProvider()
        {
            AuthenticationEngineSettings settings = new AuthenticationEngineSettings();
            SqlTestDatabase db = null;
            SqlConnection sqlCon = null;
            SqlScriptRunner scriptRunner;
            OdbcRealmMapProvider realmProvider;
            List<RealmMapping> map;
            string initScript =
@"
create table RealmMap (
    
    Realm           varchar(512),
    ProviderType    varchar(512),
    Args            varchar(512),
    Query           varchar(512)
)
go

insert into RealmMap(Realm,ProviderType,Args,Query)
    values ('file.com','LillTek.Datacenter.Server.FileAuthenticationExtension:LillTek.Datacenter.Server.dll','test=file',' ')

insert into RealmMap(Realm,ProviderType,Args,Query)
    values ('ldap.com','LillTek.Datacenter.Server.LdapAuthenticationExtension:LillTek.Datacenter.Server.dll','test=ldap','query1')

insert into RealmMap(Realm,ProviderType,Args,Query)
    values ('odbc.com','LillTek.Datacenter.Server.OdbcAuthenticationExtension:LillTek.Datacenter.Server.dll','test=odbc','query2')

insert into RealmMap(Realm,ProviderType,Args,Query)
    values ('radius.com','LillTek.Datacenter.Server.RadiusAuthenticationExtension:LillTek.Datacenter.Server.dll','test=radius','query3')

insert into RealmMap(Realm,ProviderType,Args,Query)
    values ('config.com','LillTek.Datacenter.Server.ConfigAuthenticationExtension:LillTek.Datacenter.Server.dll','test=config;LockoutCount=2;LockoutThreshold=3m;LockoutTime=4m','query4')

go
";
            Helper.InitializeApp(Assembly.GetExecutingAssembly());

            db = SqlTestDatabase.Create();

            try
            {
                sqlCon = db.OpenConnection();
                scriptRunner = new SqlScriptRunner(initScript);
                scriptRunner.Run(sqlCon);

                Config.SetConfig(string.Format("Odbc=Driver={{SQL Server}};{0}$$select * from RealmMap", db.ConnectionInfo));

                realmProvider = new OdbcRealmMapProvider();
                realmProvider.Open(settings, "Odbc");
                map = realmProvider.GetMap();
                realmProvider.Close();

                Assert.AreEqual(5, map.Count);

                Assert.AreEqual(settings.LockoutCount, map[0].LockoutCount);
                Assert.AreEqual(settings.LockoutThreshold, map[0].LockoutThreshold);
                Assert.AreEqual(settings.LockoutTime, map[0].LockoutTime);

                Assert.AreEqual("file.com", map[0].Realm);
                Assert.AreEqual(typeof(FileAuthenticationExtension).FullName, map[0].ExtensionType.FullName);
                Assert.AreEqual("file", map[0].Args["test"]);
                Assert.AreEqual(" ", map[0].Query);

                Assert.AreEqual("ldap.com", map[1].Realm);
                Assert.AreEqual(typeof(LdapAuthenticationExtension).FullName, map[1].ExtensionType.FullName);
                Assert.AreEqual("ldap", map[1].Args["test"]);
                Assert.AreEqual("query1", map[1].Query);

                Assert.AreEqual("odbc.com", map[2].Realm);
                Assert.AreEqual(typeof(OdbcAuthenticationExtension).FullName, map[2].ExtensionType.FullName);
                Assert.AreEqual("odbc", map[2].Args["test"]);
                Assert.AreEqual("query2", map[2].Query);

                Assert.AreEqual("radius.com", map[3].Realm);
                Assert.AreEqual(typeof(RadiusAuthenticationExtension).FullName, map[3].ExtensionType.FullName);
                Assert.AreEqual("radius", map[3].Args["test"]);
                Assert.AreEqual("query3", map[3].Query);

                Assert.AreEqual("config.com", map[4].Realm);
                Assert.AreEqual(typeof(ConfigAuthenticationExtension).FullName, map[4].ExtensionType.FullName);
                Assert.AreEqual("config", map[4].Args["test"]);
                Assert.AreEqual("query4", map[4].Query);

                Assert.AreEqual(2, map[4].LockoutCount);
                Assert.AreEqual(TimeSpan.FromMinutes(3), map[4].LockoutThreshold);
                Assert.AreEqual(TimeSpan.FromMinutes(4), map[4].LockoutTime);

                // Make sure we pick up changes to the map when we requery

                scriptRunner = new SqlScriptRunner(@"

delete from RealmMap
go

insert into RealmMap(Realm,ProviderType,Args,Query)
    values ('file.com','LillTek.Datacenter.Server.FileAuthenticationExtension:LillTek.Datacenter.Server.dll','test=file','updated')

go
");
                scriptRunner.Run(sqlCon);

                realmProvider = new OdbcRealmMapProvider();
                realmProvider.Open(new AuthenticationEngineSettings(), "Odbc");
                map = realmProvider.GetMap();
                realmProvider.Close();

                Assert.AreEqual(1, map.Count);

                Assert.AreEqual("file.com", map[0].Realm);
                Assert.AreEqual(typeof(FileAuthenticationExtension).FullName, map[0].ExtensionType.FullName);
                Assert.AreEqual("file", map[0].Args["test"]);
                Assert.AreEqual("updated", map[0].Query);
            }
            finally
            {
                Config.SetConfig(null);

                if (sqlCon != null)
                    sqlCon.Close();

                if (db != null)
                    db.Dispose();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_FileAuthenticationExtension()
        {
            FileAuthenticationExtension authExtension = null;
            StreamWriter writer = null;
            string path;

            path = Path.GetTempFileName();

            try
            {
                writer = new StreamWriter(path, false, Helper.AnsiEncoding);

                writer.WriteLine("// Comment line");
                writer.WriteLine("    // Indented comment line");
                writer.WriteLine();
                writer.WriteLine("lilltek.com;jeff.lill;foobar");
                writer.WriteLine("lilltek.com;joe.blow;little.debbie");
                writer.WriteLine("lilltek.com;jane.doe;fancy.pants");
                writer.WriteLine("test1.com;jane.doe@amex.com;password.123");
                writer.WriteLine("test1.com;gail.hatt@amex.com;cronkite");
                writer.WriteLine("test2.com;gail.hatt@amex.com;xxxx");

                writer.Close();

                authExtension = new FileAuthenticationExtension();
                authExtension.Open("path=" + path + ";reload=no;maxCacheTime=10m", null, null, null);

                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("lilltek.com", "jeff.lill", "foobar").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("LILLTEK.com", "jeff.LILL", "foobar").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("abcd.com", "jeff.LILL", "foobar").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("lilltek.com", "bad.account", "foobar").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("lilltek.com", "jeff.lill", "FOOBAR").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("lilltek.com", "jeff.lill", "foobarx").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("lilltek.com", "joe.blow", "little.debbie").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test1.com", "jane.doe@amex.com", "password.123").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test1.com", "gail.hatt@amex.com", "cronkite").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test2.com", "gail.hatt@amex.com", "xxxx").Status);

                Assert.AreEqual(TimeSpan.FromMinutes(10), authExtension.Authenticate("test3.com", "gail.hatt@amex.com", "xxxx").MaxCacheTime);

                // Verify that the "reload=no" option is being honored.

                writer = new StreamWriter(path, false, Helper.AnsiEncoding);
                writer.WriteLine("lilltek.com;jane.doe;foobar");
                writer.Close();

                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("lilltek.com", "jeff.lill", "foobar").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("LILLTEK.com", "jeff.LILL", "foobar").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("abcd.com", "jeff.LILL", "foobar").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("lilltek.com", "bad.account", "foobar").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("lilltek.com", "jeff.lill", "FOOBAR").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("lilltek.com", "jeff.lill", "foobarx").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("lilltek.com", "joe.blow", "little.debbie").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test1.com", "jane.doe@amex.com", "password.123").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test1.com", "gail.hatt@amex.com", "cronkite").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test2.com", "gail.hatt@amex.com", "xxxx").Status);

                authExtension.Close();
                authExtension = null;

                // Verify the with "reload=yes"

                authExtension = new FileAuthenticationExtension();
                authExtension.Open("path=" + path + ";reload=yes;maxCacheTime=7m", null, null, null);

                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("lilltek.com", "jane.doe", "foobar").Status);
                Assert.AreEqual(TimeSpan.FromMinutes(7), authExtension.Authenticate("test3.com", "gail.hatt@amex.com", "xxxx").MaxCacheTime);

                writer = new StreamWriter(path, false, Helper.AnsiEncoding);
                writer.WriteLine("lilltek.com;jane.doe;xxxx");
                writer.Close();

                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("lilltek.com", "jane.doe", "xxxx").Status);

                authExtension.Close();
                authExtension = null;
            }
            finally
            {
                if (authExtension != null)
                    authExtension.Close();

                if (writer != null)
                    writer.Close();

                Helper.DeleteFile(path);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_ConfigAuthenticationExtension()
        {
            ConfigAuthenticationExtension authExtension = null;

            try
            {
                Config.SetConfig(@"

Accounts[0] = lilltek.com;jeff.lill;foobar
Accounts[1] = lilltek.com;joe.blow;little.debbie
Accounts[2] = lilltek.com;jane.doe;fancy.pants
Accounts[3] = test1.com;jane.doe@amex.com;password.123
Accounts[4] = test1.com;gail.hatt@amex.com;cronkite
Accounts[5] = test2.com;gail.hatt@amex.com;xxxx
");

                authExtension = new ConfigAuthenticationExtension();
                authExtension.Open("key=Accounts;reload=no;maxCacheTime=10m", null, null, null);

                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("lilltek.com", "jeff.lill", "foobar").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("LILLTEK.com", "jeff.LILL", "foobar").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("abcd.com", "jeff.LILL", "foobar").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("lilltek.com", "bad.account", "foobar").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("lilltek.com", "jeff.lill", "FOOBAR").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("lilltek.com", "jeff.lill", "foobarx").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("lilltek.com", "joe.blow", "little.debbie").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test1.com", "jane.doe@amex.com", "password.123").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test1.com", "gail.hatt@amex.com", "cronkite").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test2.com", "gail.hatt@amex.com", "xxxx").Status);

                Assert.AreEqual(TimeSpan.FromMinutes(10), authExtension.Authenticate("test3.com", "gail.hatt@amex.com", "xxxx").MaxCacheTime);

                authExtension.Close();
                authExtension = null;
            }
            finally
            {
                if (authExtension != null)
                    authExtension.Close();

                Config.SetConfig(null);
            }
        }

        private void TestLdapAuth(string args)
        {
            LdapAuthenticationExtension authExtension = null;
            ADTestSettings adSettings = new ADTestSettings();

            try
            {
                // Test using default arguments

                authExtension = new LdapAuthenticationExtension();
                authExtension.Open("servers=" + adSettings.GetServerList() + ";" + args, null, null, null);

                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate(adSettings.Domain, adSettings.Account, adSettings.Password).Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate(adSettings.Domain, adSettings.Account + "x", adSettings.Password).Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate(adSettings.Domain, adSettings.Account, adSettings.Password + "x").Status);

                // $todo(jeff.lill): 
                //
                // LdapAuthenticationExtension doesn't currently validate the realm so
                // this test will fail.  I'm not sure I'm going to fix this since it
                // would require an additional query on the LDAP server with the 
                // associated overhead.

                // Assert.AreEqual(AuthenticationStatus.AccessDenied,authExtension.Authenticate(di.Domain + "x",di.Account,di.Password).Status);

                authExtension.Close();
                authExtension = null;
            }
            finally
            {
                if (authExtension != null)
                    authExtension.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_LdapAuthenticationExtension_AD()
        {
            // This test requires the existance of a valid Active Directory account
            // as specified by the LT_TEST_AD environment variable.

            // Test the possible authentication methods

            // $todo(jeff.lill): 
            //
            // Many of the authentication types that should
            // work aren't.  The problems don't seem to be
            // in my code.  Perhaps there's a problem with
            // how I've configured my AD (perhaps I need to
            // install an SSL certificate).
            //
            // I'm not going to worry about this for now since
            // DIGEST authentication should work fine for most
            // (if not all) deployments.

            TestLdapAuth("");
            TestLdapAuth("authtype=Digest");
            // TestLdapAuth("authtype=Basic");          // Probably not implemented by AD due to insecurity
            TestLdapAuth("authtype=NTLM");
            TestLdapAuth("authtype=Negotiate");         // Works but is really slow
            // TestLdapAuth("authtype=Kerberos");       // Returned "Local Error"
            TestLdapAuth("authtype=Sicily");
            // TestLdapAuth("authtype=MSN");            // Threw an AccessViolationException
            // TestLdapAuth("authtype=DPA");            // Threw an AccessViolationException
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_LdapAuthenticationExtension_SLAPD()
        {
            Assert.Inconclusive("$todo(jeff.lill): Implement this");

            // $todo(jeff.lill)
            //
            // Ultimately, I'd like to include a build of SLAPD (www.openldap.org) in the
            // LillTek tree, start it, initialize with some accounts, and then verify
            // that the extension can authenticate against it.  I don't have the time
            // to do this at the moment.
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_OdbcAuthenticationExtension()
        {
            SqlTestDatabase db = null;
            SqlConnection sqlCon = null;
            OdbcAuthenticationExtension authExtension = null;
            SqlScriptRunner scriptRunner;
            MacroProcessor processor;
            string initScript =
@"
create table Accounts (
    
    Realm           varchar(64),
    Account         varchar(64),
    Password        varchar(64),
    MD5             varbinary(128),
    SHA1            varbinary(128),
    SHA256          varbinary(128),
    SHA512          varbinary(128)
)
go

insert into Accounts(Realm,Account,Password,MD5,SHA1,SHA256,SHA512)
    values ('test1.com','jeff','password1',$(md5-1),$(sha1-1),$(sha256-1),$(sha512-1))

insert into Accounts(Realm,Account,Password,MD5,SHA1,SHA256,SHA512)
    values ('test1.com','nancy','password2',$(md5-2),$(sha1-2),$(sha256-2),$(sha512-2))

insert into Accounts(Realm,Account,Password,MD5,SHA1,SHA256,SHA512)
    values ('test2.com','jeff','password1',$(md5-1),$(sha1-1),$(sha256-1),$(sha512-1))

go
";
            processor = new MacroProcessor();
            processor.Add("md5-1", SqlHelper.Literal(MD5Hasher.Compute("password1")));
            processor.Add("sha1-1", SqlHelper.Literal(SHA1Hasher.Compute("password1")));
            processor.Add("sha256-1", SqlHelper.Literal(SHA256Hasher.Compute("password1")));
            processor.Add("sha512-1", SqlHelper.Literal(SHA512Hasher.Compute("password1")));

            processor.Add("md5-2", SqlHelper.Literal(MD5Hasher.Compute("password2")));
            processor.Add("sha1-2", SqlHelper.Literal(SHA1Hasher.Compute("password2")));
            processor.Add("sha256-2", SqlHelper.Literal(SHA256Hasher.Compute("password2")));
            processor.Add("sha512-2", SqlHelper.Literal(SHA512Hasher.Compute("password2")));

            initScript = processor.Expand(initScript);

            Helper.InitializeApp(Assembly.GetExecutingAssembly());

            db = SqlTestDatabase.Create();

            try
            {
                sqlCon = db.OpenConnection();
                scriptRunner = new SqlScriptRunner(initScript);
                scriptRunner.Run(sqlCon);

                // Literal password tests

                authExtension = new OdbcAuthenticationExtension();
                authExtension.Open(string.Format("Driver={{SQL Server}};{0};MaxCacheTime=55m", db.ConnectionInfo),
                                   "select cast(0 as tinyint) from Accounts where Realm=$(realm) and Account=$(account) and Password=$(password)",
                                   null, null);

                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test1.com", "jeff", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test1.com", "nancy", "password2").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test2.com", "jeff", "password1").Status);

                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("x-test1.com", "jeff", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("test1.com", "x-jeff", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("test1.com", "jeff", "x-password1").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("test2.com", "jeff", "password2").Status);

                Assert.AreEqual(TimeSpan.FromMinutes(55), authExtension.Authenticate("test1.com", "jeff", "password1").MaxCacheTime);

                authExtension.Close();
                authExtension = null;

                // MD5 Password hashing

                authExtension = new OdbcAuthenticationExtension();
                authExtension.Open(string.Format("Driver={{SQL Server}};{0}", db.ConnectionInfo),
                                   "select cast(0 as int) from Accounts where Realm=$(realm) and Account=$(account) and MD5=$(md5-password)",
                                   null, null);

                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test1.com", "jeff", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test1.com", "nancy", "password2").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test2.com", "jeff", "password1").Status);

                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("x-test1.com", "jeff", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("test1.com", "x-jeff", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("test1.com", "jeff", "x-password1").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("test2.com", "jeff", "password2").Status);

                authExtension.Close();
                authExtension = null;

                // SHA-1 Password hashing

                authExtension = new OdbcAuthenticationExtension();
                authExtension.Open(string.Format("Driver={{SQL Server}};{0}", db.ConnectionInfo),
                                   "select cast(0 as bigint) from Accounts where Realm=$(realm) and Account=$(account) and SHA1=$(sha1-password)",
                                   null, null);

                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test1.com", "jeff", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test1.com", "nancy", "password2").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test2.com", "jeff", "password1").Status);

                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("x-test1.com", "jeff", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("test1.com", "x-jeff", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("test1.com", "jeff", "x-password1").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("test2.com", "jeff", "password2").Status);

                authExtension.Close();
                authExtension = null;

                // SHA-256 Password hashing

                authExtension = new OdbcAuthenticationExtension();
                authExtension.Open(string.Format("Driver={{SQL Server}};{0}", db.ConnectionInfo),
                                   "select 0 from Accounts where Realm=$(realm) and Account=$(account) and SHA256=$(sha256-password)",
                                   null, null);

                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test1.com", "jeff", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test1.com", "nancy", "password2").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test2.com", "jeff", "password1").Status);

                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("x-test1.com", "jeff", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("test1.com", "x-jeff", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("test1.com", "jeff", "x-password1").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("test2.com", "jeff", "password2").Status);

                authExtension.Close();
                authExtension = null;

                // SHA-512 Password hashing

                authExtension = new OdbcAuthenticationExtension();
                authExtension.Open(string.Format("Driver={{SQL Server}};{0}", db.ConnectionInfo),
                                   "select 0 from Accounts where Realm=$(realm) and Account=$(account) and SHA512=$(sha512-password)",
                                   null, null);

                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test1.com", "jeff", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test1.com", "nancy", "password2").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("test2.com", "jeff", "password1").Status);

                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("x-test1.com", "jeff", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("test1.com", "x-jeff", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("test1.com", "jeff", "x-password1").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("test2.com", "jeff", "password2").Status);

                authExtension.Close();
                authExtension = null;
            }
            finally
            {
                if (authExtension != null)
                    authExtension.Close();

                if (sqlCon != null)
                    sqlCon.Close();

                if (db != null)
                    db.Dispose();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_RadiusAuthenticationExtension()
        {
            RadiusAuthenticationExtension authExtension = null;
            RadiusServer radiusServer = null;
            RadiusServerSettings serverSettings = new RadiusServerSettings();

            try
            {
                serverSettings.RealmFormat = RealmFormat.Email;
                serverSettings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, "test_secret"));
                serverSettings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), "test_secret"));

                radiusServer = new RadiusServer();
                radiusServer.Start(serverSettings);
                radiusServer.LoadAccountsFromString(@"
lilltek.com;jeff;password1
blow.com;joe;password2
");

                authExtension = new RadiusAuthenticationExtension();
                authExtension.Open("servers=localhost:RADIUS;secret=test_secret", null, null, null);

                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("lilltek.com", "jeff", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, authExtension.Authenticate("blow.com", "joe", "password2").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("lilltek.com", "jeff", "passwordXXX").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("lilltek.com", "jane", "password1").Status);
                Assert.AreEqual(AuthenticationStatus.AccessDenied, authExtension.Authenticate("nowhere.com", "jeff", "password1").Status);
            }
            finally
            {
                if (authExtension != null)
                    authExtension.Close();

                if (radiusServer != null)
                    radiusServer.Stop();
            }
        }

        //---------------------------------------------------------------------
        // AuthenticationEngine tests

        private void BasicAuthTest(AuthTestState state, AuthenticationEngine engine)
        {
            // Verify that valid credentials are authenticated.

            foreach (AuthTestAccount account in state.Accounts)
                Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status,
                                account.Realm + "/" + account.Account + ":" + account.Password);

            // Verify that invalid credentials are not authenicated.

            foreach (AuthTestAccount account in state.Accounts)
            {
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm + "x", account.Account, account.Password).Status);
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account + "x", account.Password).Status);
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_ConfigRealmMap()
        {
            AuthTestState state = new AuthTestState();
            IRealmMapProvider realmMapper = null;
            AuthenticationEngine engine = null;
            AuthenticationEngineSettings settings;
            int c;

            try
            {
                state.Initialize();

                c = 0;
                foreach (AuthTestExtensionType type in Enum.GetValues(typeof(AuthTestExtensionType)))
                    Config.Global.Add(string.Format("RealmMap[{0}]", c++),
                                      string.Format("{0}$${1}$${2}$${3}", state.GetRealm(type), state.GetType(type), state.GetArgs(type), state.GetQuery(type)));

                settings = new AuthenticationEngineSettings();
                realmMapper = new ConfigRealmMapProvider();
                engine = new AuthenticationEngine(null);
                realmMapper.Open(settings, "RealmMap");
                engine.Start(realmMapper, settings, null, null);

                BasicAuthTest(state, engine);
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (realmMapper != null)
                    realmMapper.Close();

                state.Cleanup();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_FileRealmMap()
        {
            AuthTestState state = new AuthTestState();
            string filePath = Path.GetTempFileName();
            IRealmMapProvider realmMapper = null;
            AuthenticationEngine engine = null;
            StringBuilder sb;

            try
            {
                state.Initialize();

                sb = new StringBuilder();
                foreach (AuthTestExtensionType type in Enum.GetValues(typeof(AuthTestExtensionType)))
                    sb.AppendFormat("{0}$${1}$${2}$${3}\r\n", state.GetRealm(type), state.GetType(type), state.GetArgs(type), state.GetQuery(type));

                Helper.WriteToFile(filePath, sb.ToString());

                realmMapper = new FileRealmMapProvider();
                realmMapper.Open(new AuthenticationEngineSettings(), filePath);

                engine = new AuthenticationEngine(null);
                engine.Start(realmMapper, new AuthenticationEngineSettings(), null, null);

                BasicAuthTest(state, engine);
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (realmMapper != null)
                    realmMapper.Close();

                state.Cleanup();

                Helper.DeleteFile(filePath);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_OdbcRealmMap()
        {
            AuthTestState state = new AuthTestState();
            string filePath = Path.GetTempFileName();
            IRealmMapProvider realmMapper = null;
            AuthenticationEngine engine = null;
            SqlConnection sqlCon = null;
            AuthenticationEngineSettings settings;
            StringBuilder sb;
            SqlScriptRunner scriptRunner;

            try
            {
                state.Initialize();

                sb = new StringBuilder();
                sb.Append(@"

create table RealmMap (
    
    Realm           varchar(512),
    ProviderType    varchar(512),
    Args            varchar(512),
    Query           varchar(512)
)
go
");
                foreach (AuthTestExtensionType type in Enum.GetValues(typeof(AuthTestExtensionType)))
                    sb.AppendFormat("insert into RealmMap(Realm,ProviderType,Args,Query) values({0},{1},{2},{3})\r\n",
                                    SqlHelper.Literal(state.GetRealm(type)),
                                    SqlHelper.Literal(state.GetType(type)),
                                    SqlHelper.Literal(state.GetArgs(type)),
                                    SqlHelper.Literal(state.GetQuery(type)));

                sb.AppendLine("go");

                scriptRunner = new SqlScriptRunner(sb.ToString());
                sqlCon = state.DB.OpenConnection();

                scriptRunner.Run(sqlCon);

                sqlCon.Close();
                sqlCon = null;

                Config.Global.Add("Odbc", string.Format("Driver={{SQL Server}};{0}$$select * from RealmMap", state.DB.ConnectionInfo));

                settings = new AuthenticationEngineSettings();
                realmMapper = new OdbcRealmMapProvider();
                engine = new AuthenticationEngine(null);
                realmMapper.Open(settings, "Odbc");
                engine.Start(realmMapper, settings, null, null);

                BasicAuthTest(state, engine);
            }
            finally
            {
                if (sqlCon != null)
                    sqlCon.Close();

                if (engine != null)
                    engine.Stop();

                if (realmMapper != null)
                    realmMapper.Close();

                state.Cleanup();

                Helper.DeleteFile(filePath);
            }
        }

        //---------------------------------------------------------------------
        // Caching tests

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_Cache_Both_Disable()
        {
            // Verify that the engine works with all caching disabled.

            AuthTestState state = new AuthTestState();
            string filePath = Path.GetTempFileName();
            IRealmMapProvider realmMapper = null;
            AuthenticationEngine engine = null;
            AuthenticationEngineSettings settings;
            StringBuilder sb;

            try
            {
                state.Initialize();

                sb = new StringBuilder();
                foreach (AuthTestExtensionType type in Enum.GetValues(typeof(AuthTestExtensionType)))
                    sb.AppendFormat("{0}$${1}$${2}$${3}\r\n", state.GetRealm(type), state.GetType(type), state.GetArgs(type), state.GetQuery(type));

                Helper.WriteToFile(filePath, sb.ToString());

                settings = new AuthenticationEngineSettings();
                settings.MaxCacheSize = 0;
                settings.MaxNakCacheSize = 0;

                realmMapper = new FileRealmMapProvider();
                realmMapper.Open(settings, filePath);

                engine = new AuthenticationEngine(null);
                engine.Start(realmMapper, settings, null, null);

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.IsFalse(engine.IsCached(account.Realm, account.Account, account.Password));
                    Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
                    Assert.IsFalse(engine.IsCached(account.Realm, account.Account, account.Password));
                    Assert.IsFalse(engine.IsNakCached(account.Realm, account.Account, account.Password));
                    Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));

                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.IsFalse(engine.IsNakCached(account.Realm + "x", account.Account, account.Password));
                    Assert.IsFalse(engine.IsNakLocked(account.Realm + "x", account.Account));

                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.IsFalse(engine.IsNakCached(account.Realm, account.Account + "x", account.Password));
                    Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account + "x"));

                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                    Assert.IsFalse(engine.IsNakCached(account.Realm, account.Account, account.Password + "x"));
                    Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));

                    // Repeat the authentications

                    Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm + "x", account.Account, account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account + "x", account.Password).Status);
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                }
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (realmMapper != null)
                    realmMapper.Close();

                state.Cleanup();

                Helper.DeleteFile(filePath);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_Cache_Success_Disable()
        {
            // Verify that the engine works with success caching disabled.

            AuthTestState state = new AuthTestState();
            string filePath = Path.GetTempFileName();
            IRealmMapProvider realmMapper = null;
            AuthenticationEngine engine = null;
            AuthenticationEngineSettings settings;
            StringBuilder sb;

            try
            {
                state.Initialize();

                sb = new StringBuilder();
                foreach (AuthTestExtensionType type in Enum.GetValues(typeof(AuthTestExtensionType)))
                    sb.AppendFormat("{0}$${1}$${2}$${3}\r\n", state.GetRealm(type), state.GetType(type), state.GetArgs(type), state.GetQuery(type));

                Helper.WriteToFile(filePath, sb.ToString());

                settings = new AuthenticationEngineSettings();
                settings.MaxCacheSize = 0;

                realmMapper = new FileRealmMapProvider();
                realmMapper.Open(settings, filePath);

                engine = new AuthenticationEngine(null);
                engine.Start(realmMapper, settings, null, null);

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.IsFalse(engine.IsCached(account.Realm, account.Account, account.Password));
                    Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
                    Assert.IsFalse(engine.IsCached(account.Realm, account.Account, account.Password));

                    Assert.IsFalse(engine.IsNakCached(account.Realm, account.Account, account.Password));
                    Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));

                    // Repeat the authentications

                    Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
                }
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (realmMapper != null)
                    realmMapper.Close();

                state.Cleanup();

                Helper.DeleteFile(filePath);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_Cache_Success()
        {
            // Verify that successful authentications are cached.

            AuthTestState state = new AuthTestState();
            string filePath = Path.GetTempFileName();
            IRealmMapProvider realmMapper = null;
            AuthenticationEngine engine = null;
            AuthenticationEngineSettings settings;
            StringBuilder sb;

            try
            {
                state.Initialize();

                sb = new StringBuilder();
                foreach (AuthTestExtensionType type in Enum.GetValues(typeof(AuthTestExtensionType)))
                    sb.AppendFormat("{0}$${1}$${2}$${3}\r\n", state.GetRealm(type), state.GetType(type), state.GetArgs(type), state.GetQuery(type));

                Helper.WriteToFile(filePath, sb.ToString());

                settings = new AuthenticationEngineSettings();
                settings.BkTaskInterval = TimeSpan.FromSeconds(1);
                settings.LockoutCount = int.MaxValue;
                settings.CacheTTL = TimeSpan.FromMinutes(5);
                settings.NakCacheTTL = TimeSpan.FromMinutes(5);

                realmMapper = new FileRealmMapProvider();
                realmMapper.Open(settings, filePath);

                engine = new AuthenticationEngine(null);
                engine.Start(realmMapper, settings, null, null);

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.IsFalse(engine.IsCached(account.Realm, account.Account, account.Password));
                    Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
                    Assert.IsTrue(engine.IsCached(account.Realm, account.Account, account.Password));

                    Assert.IsFalse(engine.IsNakCached(account.Realm, account.Account, account.Password));
                    Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));

                    // Repeat the authentications against the cached item and
                    // verify that the realm's authentication extension wasn't
                    // called again.

                    RealmMapping realmMapping;
                    int cAuth;

                    realmMapping = engine.GetRealmMapping(account.Realm);
                    cAuth = realmMapping.AuthExtension.AuthenticationCount;

                    Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
                    Assert.AreEqual(cAuth, realmMapping.AuthExtension.AuthenticationCount);
                }
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (realmMapper != null)
                    realmMapper.Close();

                state.Cleanup();

                Helper.DeleteFile(filePath);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_Cache_Success_Flush_Explicit()
        {
            // Verify that we can explicitly flush all cached successful authentications.

            AuthTestState state = new AuthTestState();
            string filePath = Path.GetTempFileName();
            IRealmMapProvider realmMapper = null;
            AuthenticationEngine engine = null;
            AuthenticationEngineSettings settings;
            StringBuilder sb;

            try
            {
                state.Initialize();

                sb = new StringBuilder();
                foreach (AuthTestExtensionType type in Enum.GetValues(typeof(AuthTestExtensionType)))
                    sb.AppendFormat("{0}$${1}$${2}$${3}\r\n", state.GetRealm(type), state.GetType(type), state.GetArgs(type), state.GetQuery(type));

                Helper.WriteToFile(filePath, sb.ToString());

                settings = new AuthenticationEngineSettings();
                settings.BkTaskInterval = TimeSpan.FromSeconds(1);
                settings.LockoutCount = int.MaxValue;
                settings.CacheTTL = TimeSpan.FromMinutes(5);
                settings.NakCacheTTL = TimeSpan.FromMinutes(5);

                realmMapper = new FileRealmMapProvider();
                realmMapper.Open(settings, filePath);

                engine = new AuthenticationEngine(null);
                engine.Start(realmMapper, settings, null, null);

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.IsFalse(engine.IsCached(account.Realm, account.Account, account.Password));
                    Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
                    Assert.IsTrue(engine.IsCached(account.Realm, account.Account, account.Password));

                    Assert.IsFalse(engine.IsNakCached(account.Realm, account.Account, account.Password));
                    Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));

                    // Repeat the authentications against the cached item and
                    // verify that the realm's authentication extension wasn't
                    // called again.

                    RealmMapping realmMapping;
                    int cAuth;

                    realmMapping = engine.GetRealmMapping(account.Realm);
                    cAuth = realmMapping.AuthExtension.AuthenticationCount;

                    Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
                    Assert.AreEqual(cAuth, realmMapping.AuthExtension.AuthenticationCount);
                }

                // Explicitly clear the success cache and the verify that the
                // entries were actually cleared and authentication still works.

                engine.ClearCache();

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.IsFalse(engine.IsCached(account.Realm, account.Account, account.Password));
                    Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
                    Assert.IsTrue(engine.IsCached(account.Realm, account.Account, account.Password));

                    Assert.IsFalse(engine.IsNakCached(account.Realm, account.Account, account.Password));
                    Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));

                    // Repeat the authentications against the cached item and
                    // verify that the realm's authentication extension wasn't
                    // called again.

                    RealmMapping realmMapping;
                    int cAuth;

                    realmMapping = engine.GetRealmMapping(account.Realm);
                    cAuth = realmMapping.AuthExtension.AuthenticationCount;

                    Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
                    Assert.AreEqual(cAuth, realmMapping.AuthExtension.AuthenticationCount);
                }
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (realmMapper != null)
                    realmMapper.Close();

                state.Cleanup();

                Helper.DeleteFile(filePath);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_Cache_Success_Flush_Partial()
        {
            // Verify that we can flush specific cached successful authentications.

            AuthTestState state = new AuthTestState();
            string filePath = Path.GetTempFileName();
            IRealmMapProvider realmMapper = null;
            AuthenticationEngine engine = null;
            AuthenticationEngineSettings settings;
            StringBuilder sb;

            try
            {
                state.Initialize();

                sb = new StringBuilder();
                foreach (AuthTestExtensionType type in Enum.GetValues(typeof(AuthTestExtensionType)))
                    sb.AppendFormat("{0}$${1}$${2}$${3}\r\n", state.GetRealm(type), state.GetType(type), state.GetArgs(type), state.GetQuery(type));

                Helper.WriteToFile(filePath, sb.ToString());

                settings = new AuthenticationEngineSettings();
                settings.BkTaskInterval = TimeSpan.FromSeconds(1);
                settings.LockoutCount = int.MaxValue;
                settings.CacheTTL = TimeSpan.FromMinutes(5);
                settings.NakCacheTTL = TimeSpan.FromMinutes(5);

                realmMapper = new FileRealmMapProvider();
                realmMapper.Open(settings, filePath);

                engine = new AuthenticationEngine(null);
                engine.Start(realmMapper, settings, null, null);

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.IsFalse(engine.IsCached(account.Realm, account.Account, account.Password));
                    Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
                    Assert.IsTrue(engine.IsCached(account.Realm, account.Account, account.Password));

                    Assert.IsFalse(engine.IsNakCached(account.Realm, account.Account, account.Password));
                    Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));

                    // Repeat the authentications against the cached item and
                    // verify that the realm's authentication extension wasn't
                    // called again.

                    RealmMapping realmMapping;
                    int cAuth;

                    realmMapping = engine.GetRealmMapping(account.Realm);
                    cAuth = realmMapping.AuthExtension.AuthenticationCount;

                    Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
                    Assert.AreEqual(cAuth, realmMapping.AuthExtension.AuthenticationCount);
                }

                // Remove all cached items from "file.com" verify that they are gone.  This depends on the 
                // fact that only the accounts "file.com/file1/file-password1" and "file.com/file2/file-password2" were initialized by
                // AuthTestState.

                engine.FlushCache("file.com", null);
                Assert.IsFalse(engine.IsCached("file.com", "file1", "file-password1"));
                Assert.IsFalse(engine.IsCached("file.com", "file2", "file-password2"));

                // Now try removing just "odbc.com/odbc1".

                engine.FlushCache("odbc.com", "odbc1");
                Assert.IsFalse(engine.IsCached("odbc.com", "odbc1", "odbc-password1"));
                Assert.IsTrue(engine.IsCached("odbc.com", "odbc2", "odbc-password2"));
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (realmMapper != null)
                    realmMapper.Close();

                state.Cleanup();

                Helper.DeleteFile(filePath);
            }
        }


        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_Cache_NAK_Flush_Partial()
        {
            // Verify that we can flush specific cached NAK items.

            AuthTestState state = new AuthTestState();
            string filePath = Path.GetTempFileName();
            IRealmMapProvider realmMapper = null;
            AuthenticationEngine engine = null;
            AuthenticationEngineSettings settings;
            StringBuilder sb;

            try
            {
                state.Initialize();

                sb = new StringBuilder();
                foreach (AuthTestExtensionType type in Enum.GetValues(typeof(AuthTestExtensionType)))
                    sb.AppendFormat("{0}$${1}$${2}$${3}\r\n", state.GetRealm(type), state.GetType(type), state.GetArgs(type), state.GetQuery(type));

                Helper.WriteToFile(filePath, sb.ToString());

                settings = new AuthenticationEngineSettings();
                settings.BkTaskInterval = TimeSpan.FromSeconds(1);
                settings.LockoutCount = int.MaxValue;
                settings.CacheTTL = TimeSpan.FromMinutes(5);
                settings.NakCacheTTL = TimeSpan.FromMinutes(5);

                realmMapper = new FileRealmMapProvider();
                realmMapper.Open(settings, filePath);

                engine = new AuthenticationEngine(null);
                engine.Start(realmMapper, settings, null, null);

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.IsFalse(engine.IsCached(account.Realm, account.Account, account.Password + "x"));
                    Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                    Assert.IsTrue(engine.IsNakCached(account.Realm, account.Account, account.Password + "x"));
                }

                // Remove all cached items from "file.com" verify that they are gone.  This depends on the 
                // fact that only the accounts "file.com/file1/file-password1" and "file.com/file2/file-passwordx" were initialized by
                // AuthTestState.

                engine.FlushNakCache("file.com", null);
                Assert.IsFalse(engine.IsNakCached("file.com", "file1", "file-password1x"));
                Assert.IsFalse(engine.IsNakCached("file.com", "file2", "file-password2x"));

                // Now try removing just "odbc.com/odbc1".

                engine.FlushNakCache("odbc.com", "odbc1");
                Assert.IsFalse(engine.IsNakCached("odbc.com", "odbc1", "odbc-password1x"));
                Assert.IsTrue(engine.IsNakCached("odbc.com", "odbc2", "odbc-password2x"));
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (realmMapper != null)
                    realmMapper.Close();

                state.Cleanup();

                Helper.DeleteFile(filePath);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_Cache_Success_Flush_Timed()
        {
            // Verify that we can explicitly flush all cached successful authentications.

            AuthTestState state = new AuthTestState();
            string filePath = Path.GetTempFileName();
            IRealmMapProvider realmMapper = null;
            AuthenticationEngine engine = null;
            AuthenticationEngineSettings settings;
            StringBuilder sb;

            try
            {
                state.Initialize();

                sb = new StringBuilder();
                foreach (AuthTestExtensionType type in Enum.GetValues(typeof(AuthTestExtensionType)))
                    sb.AppendFormat("{0}$${1}$${2}$${3}\r\n", state.GetRealm(type), state.GetType(type), state.GetArgs(type), state.GetQuery(type));

                Helper.WriteToFile(filePath, sb.ToString());

                settings = new AuthenticationEngineSettings();
                settings.BkTaskInterval = TimeSpan.FromSeconds(1);
                settings.CacheFlushInterval = TimeSpan.FromMilliseconds(100);
                settings.LockoutCount = int.MaxValue;
                settings.CacheTTL = TimeSpan.FromSeconds(5);
                settings.NakCacheTTL = TimeSpan.FromSeconds(5);
                settings.BkTaskInterval = TimeSpan.FromSeconds(1);

                realmMapper = new FileRealmMapProvider();
                realmMapper.Open(settings, filePath);

                engine = new AuthenticationEngine(null);
                engine.Start(realmMapper, settings, null, null);

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.IsFalse(engine.IsCached(account.Realm, account.Account, account.Password));
                    Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
                    Assert.IsTrue(engine.IsCached(account.Realm, account.Account, account.Password));

                    Assert.IsFalse(engine.IsNakCached(account.Realm, account.Account, account.Password));
                    Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));

                    // Repeat the authentications against the cached item and
                    // verify that the realm's authentication extension wasn't
                    // called again.

                    RealmMapping realmMapping;
                    int cAuth;

                    realmMapping = engine.GetRealmMapping(account.Realm);
                    cAuth = realmMapping.AuthExtension.AuthenticationCount;

                    Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
                    Assert.AreEqual(cAuth, realmMapping.AuthExtension.AuthenticationCount);
                }

                // Wait for the engine to clear the success cache and the verify that the
                // entries were actually cleared and authentication still works.

                Thread.Sleep(settings.CacheTTL + settings.BkTaskInterval + settings.CacheFlushInterval + TimeSpan.FromSeconds(1));

                foreach (AuthTestAccount account in state.Accounts)
                {
                    Assert.IsFalse(engine.IsCached(account.Realm, account.Account, account.Password));
                    Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
                    Assert.IsTrue(engine.IsCached(account.Realm, account.Account, account.Password));

                    Assert.IsFalse(engine.IsNakCached(account.Realm, account.Account, account.Password));
                    Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));

                    // Repeat the authentications against the cached item and
                    // verify that the realm's authentication extension wasn't
                    // called again.

                    RealmMapping realmMapping;
                    int cAuth;

                    realmMapping = engine.GetRealmMapping(account.Realm);
                    cAuth = realmMapping.AuthExtension.AuthenticationCount;

                    Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
                    Assert.AreEqual(cAuth, realmMapping.AuthExtension.AuthenticationCount);
                }
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (realmMapper != null)
                    realmMapper.Close();

                state.Cleanup();

                Helper.DeleteFile(filePath);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_Cache_NAK_Disable()
        {
            // Verify that disabling the NAK cache doesn't cause problems.

            AuthTestState state = new AuthTestState();
            string filePath = Path.GetTempFileName();
            IRealmMapProvider realmMapper = null;
            AuthenticationEngine engine = null;
            AuthenticationEngineSettings settings;
            StringBuilder sb;
            AuthTestAccount account;
            RealmMapping realmMapping;
            int cAuth;

            try
            {
                state.Initialize();

                sb = new StringBuilder();
                foreach (AuthTestExtensionType type in Enum.GetValues(typeof(AuthTestExtensionType)))
                    sb.AppendFormat("{0}$${1}$${2}$${3}\r\n", state.GetRealm(type), state.GetType(type), state.GetArgs(type), state.GetQuery(type));

                Helper.WriteToFile(filePath, sb.ToString());

                settings = new AuthenticationEngineSettings();
                settings.BkTaskInterval = TimeSpan.FromSeconds(1);
                settings.CacheFlushInterval = TimeSpan.FromMilliseconds(100);
                settings.CacheTTL = TimeSpan.FromMinutes(5);
                settings.NakCacheTTL = TimeSpan.Zero;
                settings.LockoutCount = 3;
                settings.LockoutThreshold = TimeSpan.FromSeconds(2);
                settings.LockoutTime = TimeSpan.FromSeconds(5);

                realmMapper = new FileRealmMapProvider();
                realmMapper.Open(settings, filePath);

                engine = new AuthenticationEngine(null);
                engine.Start(realmMapper, settings, null, null);

                // I'm going to work against a file.com account to avoid
                // potential timing issues with the more sophisticated 
                // authentication extensions.

                account = null;
                foreach (AuthTestAccount a in state.Accounts)
                    if (a.Realm == "file.com")
                    {
                        account = a;
                        break;
                    }

                Assert.IsNotNull(account);
                realmMapping = engine.GetRealmMapping(account.Realm);

                // Verify that hitting the engine with the same account/bad-password
                // hits the auth extension again since caching is disabled.

                cAuth = realmMapping.AuthExtension.AuthenticationCount;
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.AreEqual(++cAuth, realmMapping.AuthExtension.AuthenticationCount);
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.AreEqual(++cAuth, realmMapping.AuthExtension.AuthenticationCount);
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (realmMapper != null)
                    realmMapper.Close();

                state.Cleanup();

                Helper.DeleteFile(filePath);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_Cache_NAK_Lockout()
        {
            // Verify that account lockout behavior

            AuthTestState state = new AuthTestState();
            string filePath = Path.GetTempFileName();
            IRealmMapProvider realmMapper = null;
            AuthenticationEngine engine = null;
            AuthenticationEngineSettings settings;
            StringBuilder sb;
            AuthTestAccount account;
            RealmMapping realmMapping;
            int cAuth;

            try
            {
                state.Initialize();

                sb = new StringBuilder();
                foreach (AuthTestExtensionType type in Enum.GetValues(typeof(AuthTestExtensionType)))
                    sb.AppendFormat("{0}$${1}$${2}$${3}\r\n", state.GetRealm(type), state.GetType(type), state.GetArgs(type), state.GetQuery(type));

                Helper.WriteToFile(filePath, sb.ToString());

                settings = new AuthenticationEngineSettings();
                settings.BkTaskInterval = TimeSpan.FromSeconds(1);
                settings.CacheFlushInterval = TimeSpan.FromMilliseconds(100);
                settings.CacheTTL = TimeSpan.FromMinutes(5);
                settings.NakCacheTTL = TimeSpan.FromMinutes(5);
                settings.LockoutCount = 3;
                settings.LockoutThreshold = TimeSpan.FromSeconds(2);
                settings.LockoutTime = TimeSpan.FromSeconds(5);

                realmMapper = new FileRealmMapProvider();
                realmMapper.Open(settings, filePath);

                engine = new AuthenticationEngine(null);
                engine.Start(realmMapper, settings, null, null);

                // I'm going to work against a file.com account to avoid
                // potential timing issues with the more sophisticated 
                // authentication extensions.

                account = null;
                foreach (AuthTestAccount a in state.Accounts)
                    if (a.Realm == "file.com")
                    {
                        account = a;
                        break;
                    }

                Assert.IsNotNull(account);
                realmMapping = engine.GetRealmMapping(account.Realm);

                // Verify that hitting the engine with the same account/bad-password
                // does not hit the auth extension again.

                engine.ClearCache();
                engine.ClearNakCache();
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                cAuth = realmMapping.AuthExtension.AuthenticationCount;
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.AreEqual(cAuth, realmMapping.AuthExtension.AuthenticationCount);

                // Verify that hitting the engine with an account/bad-password and
                // then again with an account/good-password succeeds the second time

                engine.ClearCache();
                engine.ClearNakCache();
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);

                // Verify that hitting the engine with three account/same-bad-passwords
                // actually locks the account, preventing even good account/password attempts
                // from succeeding.

                engine.ClearCache();
                engine.ClearNakCache();
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.IsTrue(engine.IsNakLocked(account.Realm, account.Account));
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);

                // Verify that hitting the engine with three account/different-bad-passwords
                // eventually locks the account, preventing even good account/password attempts
                // from succeeding.

                engine.ClearCache();
                engine.ClearNakCache();
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "y").Status);
                Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "z").Status);
                Assert.IsTrue(engine.IsNakLocked(account.Realm, account.Account));
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);

                // Verify that the accounts eventually unlock when their lockout timers
                // expire.

                engine.ClearCache();
                engine.ClearNakCache();
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));

                cAuth = realmMapping.AuthExtension.AuthenticationCount;
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.AreEqual(cAuth, realmMapping.AuthExtension.AuthenticationCount);
                Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));

                cAuth = realmMapping.AuthExtension.AuthenticationCount;
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.AreEqual(cAuth, realmMapping.AuthExtension.AuthenticationCount);
                Assert.IsTrue(engine.IsNakLocked(account.Realm, account.Account));
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);

                Thread.Sleep(settings.LockoutTime + settings.BkTaskInterval + settings.CacheFlushInterval + TimeSpan.FromSeconds(1));

                Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));
                cAuth = realmMapping.AuthExtension.AuthenticationCount;
                Assert.AreEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
                Assert.AreEqual(cAuth + 1, realmMapping.AuthExtension.AuthenticationCount);

                // Verify that hitting the engine with an account/bad-password, waiting longer than
                // LockoutThreshold + CacheFlushInterval + a bit more and then hitting the engine
                // with the same account/bad-password will not prevent the authentication extension
                // from being queried the second time and also that the first attempt was not
                // counted towards a lockout.

                engine.ClearCache();
                engine.ClearNakCache();
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));

                Thread.Sleep(settings.LockoutThreshold + settings.CacheFlushInterval + TimeSpan.FromSeconds(1));

                cAuth = realmMapping.AuthExtension.AuthenticationCount;
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.AreEqual(++cAuth, realmMapping.AuthExtension.AuthenticationCount);
                Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));

                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.AreEqual(cAuth, realmMapping.AuthExtension.AuthenticationCount);
                Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));

                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.AreEqual(cAuth, realmMapping.AuthExtension.AuthenticationCount);
                Assert.IsTrue(engine.IsNakLocked(account.Realm, account.Account));

                // Verify that hitting the engine with an account/bad-password, performing an
                // explicit NAK cache flush and then hitting the engine with the same 
                // account/bad-password will not prevent the authentication extension
                // from being queried the second time and also that the first attempt was 
                // not counted towards a lockout.

                engine.ClearCache();
                engine.ClearNakCache();
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));

                engine.ClearNakCache();

                cAuth = realmMapping.AuthExtension.AuthenticationCount;
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.AreEqual(++cAuth, realmMapping.AuthExtension.AuthenticationCount);
                Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));

                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.AreEqual(cAuth, realmMapping.AuthExtension.AuthenticationCount);
                Assert.IsFalse(engine.IsNakLocked(account.Realm, account.Account));

                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.AreEqual(cAuth, realmMapping.AuthExtension.AuthenticationCount);
                Assert.IsTrue(engine.IsNakLocked(account.Realm, account.Account));

                // Restart the engine with a lockout count=1 and verify that it will actually
                // lock an account after only one bad authentication.

                engine.Stop();
                realmMapper.Close();

                settings.LockoutCount = 1;

                realmMapper = new FileRealmMapProvider();
                realmMapper.Open(settings, filePath);

                engine = new AuthenticationEngine(null);
                engine.Start(realmMapper, settings, null, null);

                engine.ClearCache();
                engine.ClearNakCache();
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password + "x").Status);
                Assert.IsTrue(engine.IsNakLocked(account.Realm, account.Account));
                Assert.AreNotEqual(AuthenticationStatus.Authenticated, engine.Authenticate(account.Realm, account.Account, account.Password).Status);
            }
            finally
            {
                if (engine != null)
                    engine.Stop();

                if (realmMapper != null)
                    realmMapper.Close();

                state.Cleanup();

                Helper.DeleteFile(filePath);
            }
        }

        private class NullRealmMapProvider : IRealmMapProvider
        {
            private bool isOpen = false;

            public void Open(AuthenticationEngineSettings engineSettings, string args)
            {
                isOpen = true;
            }

            public void Close()
            {
                isOpen = false;
            }

            public void Dispose()
            {
                Close();
            }

            public bool IsOpen
            {
                get { return isOpen; }
            }

            public List<RealmMapping> GetMap()
            {
                return new List<RealmMapping>();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_AddCredentials()
        {
            // Verify that explicitly adding credentials works.

            AuthenticationEngine engine = null;
            AuthenticationEngineSettings settings;

            try
            {

                settings = new AuthenticationEngineSettings();
                engine = new AuthenticationEngine(null);
                engine.Start(new NullRealmMapProvider(), settings, null, null);

                engine.AddCredentials("test.com", "account", "password", TimeSpan.FromMinutes(1));
                Assert.IsTrue(engine.IsCached("test.com", "account", "password"));
                engine.AddCredentials("test.com", "account", "password-new", TimeSpan.FromMinutes(1));
                Assert.IsFalse(engine.IsCached("test.com", "account", "password"));
                Assert.IsTrue(engine.IsCached("test.com", "account", "password-new"));

                engine.Stop();
                engine = null;

                // Make sure that we don't crash if caching is disabled

                settings.CacheTTL = TimeSpan.Zero;

                engine = new AuthenticationEngine(null);
                engine.Start(new NullRealmMapProvider(), settings, null, null);

                engine.AddCredentials("test.com", "account", "password", TimeSpan.FromMinutes(1));
                Assert.IsFalse(engine.IsCached("test.com", "account", "password"));
                engine.AddCredentials("test.com", "account", "password-new", TimeSpan.FromMinutes(1));
                Assert.IsFalse(engine.IsCached("test.com", "account", "password"));
                Assert.IsFalse(engine.IsCached("test.com", "account", "password-new"));

                engine.Stop();
                engine = null;
            }
            finally
            {
                if (engine != null)
                    engine.Stop();
            }
        }

        //---------------------------------------------------------------------
        // Blast some transactions at the engine with background task and
        // cache purge interval set to very short times.  We're looking
        // for deadlocks and other strange behaviors.

        const int cBlastThreads = 15;

        int cBlastIterations;
        AuthTestState blastState;
        AuthenticationEngine blastEngine;
        Thread[] blastThreads;
        Exception[] blastExceptions;
        bool blastCached;

        private void BlastThread(object state)
        {
            int threadIndex = (int)state;

            try
            {
                for (int i = 0; i < cBlastIterations; i++)
                {
                    BasicAuthTest(blastState, blastEngine);

                    if (!blastCached)
                    {

                        blastEngine.ClearCache();
                        blastEngine.ClearNakCache();
                    }
                }
            }
            catch (Exception e)
            {
                blastExceptions[threadIndex] = e;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_MultiThread_Blast_Cached()
        {
            // Crank up a bunch of threads that will throw traffic against a single
            // authentication engine and look for problems.  The engine will be 
            // initialized with very short background task and cache flush intervals
            // to put more stress on the thread locking code.

            IRealmMapProvider realmMapper = null;
            AuthenticationEngineSettings settings;
            StringBuilder sb;
            int c;

            try
            {
                blastCached = true;
                cBlastIterations = 500;
                blastState = new AuthTestState();
                blastState.Initialize();

                sb = new StringBuilder();
                c = 0;
                foreach (AuthTestExtensionType type in Enum.GetValues(typeof(AuthTestExtensionType)))
                    Config.Global.Add(string.Format("RealmMap[{0}]", c++),
                                      string.Format("{0}$${1}$${2}$${3}", blastState.GetRealm(type), blastState.GetType(type), blastState.GetArgs(type), blastState.GetQuery(type)));

                settings = new AuthenticationEngineSettings();
                settings.CacheFlushInterval = TimeSpan.FromMilliseconds(1);
                settings.BkTaskInterval = TimeSpan.FromMilliseconds(1);
                settings.LockoutCount = int.MaxValue;     // Use this to avoid account lockouts

                realmMapper = new ConfigRealmMapProvider();
                blastEngine = new AuthenticationEngine(null);
                realmMapper.Open(settings, "RealmMap");
                blastEngine.Start(realmMapper, settings, null, null);

                // Start the threads and then wait for them to terminate.
                // Then if any of them threw an exception, rethrow it
                // on this thread so unit tests can report it.

                blastThreads = new Thread[cBlastThreads];
                blastExceptions = new Exception[cBlastThreads];

                for (int i = 0; i < cBlastThreads; i++)
                {
                    blastThreads[i] = new Thread(new ParameterizedThreadStart(BlastThread));
                    blastThreads[i].Start(i);
                }

                for (int i = 0; i < cBlastThreads; i++)
                    blastThreads[i].Join();

                for (int i = 0; i < cBlastThreads; i++)
                    if (blastExceptions[i] != null)
                        throw new Exception(blastExceptions[i].Message, blastExceptions[i]);
            }
            finally
            {
                if (blastEngine != null)
                    blastEngine.Stop();

                if (realmMapper != null)
                    realmMapper.Close();

                blastState.Cleanup();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void AuthenticationEngine_MultiThread_Blast_CacheClear()
        {
            // Crank up a bunch of threads that will throw traffic against a single
            // authentication engine and look for problems.  The engine will be 
            // initialized with very short background task and cache flush intervals
            // to put more stress on the thread locking code.

            IRealmMapProvider realmMapper = null;
            AuthenticationEngineSettings settings;
            StringBuilder sb;
            int c;

            try
            {
                blastCached = false;
                cBlastIterations = 500;
                blastState = new AuthTestState();
                blastState.Initialize();

                sb = new StringBuilder();
                c = 0;
                foreach (AuthTestExtensionType type in Enum.GetValues(typeof(AuthTestExtensionType)))
                    Config.Global.Add(string.Format("RealmMap[{0}]", c++),
                                      string.Format("{0}$${1}$${2}$${3}", blastState.GetRealm(type), blastState.GetType(type), blastState.GetArgs(type), blastState.GetQuery(type)));

                settings = new AuthenticationEngineSettings();
                settings.CacheFlushInterval = TimeSpan.FromMilliseconds(1);
                settings.BkTaskInterval = TimeSpan.FromMilliseconds(1);
                settings.LockoutCount = int.MaxValue;     // Use this to avoid account lockouts

                realmMapper = new ConfigRealmMapProvider();
                blastEngine = new AuthenticationEngine(null);
                realmMapper.Open(settings, "RealmMap");
                blastEngine.Start(realmMapper, settings, null, null);

                // Start the threads and then wait for them to terminate.
                // Then if any of them threw an exception, rethrow it
                // on this thread so unit tests can report it.

                blastThreads = new Thread[cBlastThreads];
                blastExceptions = new Exception[cBlastThreads];

                for (int i = 0; i < cBlastThreads; i++)
                {
                    blastThreads[i] = new Thread(new ParameterizedThreadStart(BlastThread));
                    blastThreads[i].Start(i);
                }

                for (int i = 0; i < cBlastThreads; i++)
                    blastThreads[i].Join();

                for (int i = 0; i < cBlastThreads; i++)
                    if (blastExceptions[i] != null)
                        throw new Exception(blastExceptions[i].Message, blastExceptions[i]);
            }
            finally
            {
                if (blastEngine != null)
                    blastEngine.Stop();

                if (realmMapper != null)
                    realmMapper.Close();

                blastState.Cleanup();
            }
        }
    }
}

