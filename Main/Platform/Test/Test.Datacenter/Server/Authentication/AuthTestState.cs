//-----------------------------------------------------------------------------
// FILE:        AuthTestState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit test

#define TEST_AD         // Define this to enable Active Directory tests
#undef  TEST_AD_LDAP    // Define this to enable AD tests via LDAP,
                        // undefine this to enable AD tests via RADIUS/IAS.

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Data;
using LillTek.Cryptography;
using LillTek.Messaging;
using LillTek.Net.Radius;
using LillTek.Net.Sockets;
using LillTek.Datacenter;
using LillTek.Datacenter.Msgs;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Available for unit tests.
    /// </summary>
    public enum AuthTestExtensionType
    {
        File,
        Ldap,
        Odbc,
        Radius,
        Config
    }

    /// <summary>
    /// Available for unit tests.
    /// </summary>
    public sealed class AuthTestAccount
    {
        public AuthTestExtensionType ExtensionType;
        public string Realm;
        public string Account;
        public string Password;

        public AuthTestAccount(AuthTestExtensionType extensionType, string realm, string account, string password)
        {
            this.ExtensionType = extensionType;
            this.Realm = realm;
            this.Account = account;
            this.Password = password;
        }
    }

    /// <summary>
    /// Available for unit tests to make authentication testing easier.
    /// </summary>
    public sealed class AuthTestState
    {
        public SqlTestDatabase DB = null;
        public string AuthFilePath = null;
        public RadiusServer RadiusServer = null;
        public string RadiusSecret = "test secret";
        public List<AuthTestAccount> Accounts = new List<AuthTestAccount>();
        public ADTestSettings ADSettings;

        public void Initialize()
        {
            Helper.InitializeApp(Assembly.GetExecutingAssembly());

            this.ADSettings = new ADTestSettings();
            this.DB = SqlTestDatabase.Create();
            this.AuthFilePath = Path.GetTempFileName();

            //-------------------------------------------------------------
            // Initialize file authentication

            Helper.WriteToFile(this.AuthFilePath, @"

file.com;file1;file-password1
file.com;file2;file-password2
");
            this.Accounts.Add(new AuthTestAccount(AuthTestExtensionType.File, "file.com", "file1", "file-password1"));
            this.Accounts.Add(new AuthTestAccount(AuthTestExtensionType.File, "file.com", "file2", "file-password2"));

            //-------------------------------------------------------------
            // Initialize RADIUS authentication

            RadiusServerSettings radiusSettings = new RadiusServerSettings();

            radiusSettings.NetworkBinding = NetworkBinding.Parse("ANY:52111");
            radiusSettings.Devices.Add(new RadiusNasInfo(IPAddress.Loopback, this.RadiusSecret));
            radiusSettings.Devices.Add(new RadiusNasInfo(NetHelper.GetActiveAdapter(), this.RadiusSecret));

            this.RadiusServer = new RadiusServer();
            this.RadiusServer.Start(radiusSettings);
            this.RadiusServer.LoadAccountsFromString(@"

radius.com;radius1;radius-password1
radius.com;radius2;radius-password2
");
            this.Accounts.Add(new AuthTestAccount(AuthTestExtensionType.Radius, "radius.com", "radius1", "radius-password1"));
            this.Accounts.Add(new AuthTestAccount(AuthTestExtensionType.Radius, "radius.com", "radius2", "radius-password2"));

            //-------------------------------------------------------------
            // Initialize config authentication

            Config.SetConfig(@"

Accounts[0] = config.com;config1;config-password1
Accounts[1] = config.com;config2;config-password2
");
            this.Accounts.Add(new AuthTestAccount(AuthTestExtensionType.Config, "config.com", "config1", "config-password1"));
            this.Accounts.Add(new AuthTestAccount(AuthTestExtensionType.Config, "config.com", "config2", "config-password2"));

#if TEST_AD
            //-------------------------------------------------------------
            // Initialize active directory authentication

#if !TEST_AD_LDAP
            if (ADSettings.NasSecret != string.Empty)   // Disable the test if the NAS secret is blank
#endif
                this.Accounts.Add(new AuthTestAccount(AuthTestExtensionType.Ldap, ADSettings.Domain, ADSettings.Account, ADSettings.Password));
#endif

            //-------------------------------------------------------------
            // Initalize ODBC authentication

            SqlConnection sqlCon = null;
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
values ('odbc.com','odbc1','odbc-password1',$(md5-1),$(sha1-1),$(sha256-1),$(sha512-1))

insert into Accounts(Realm,Account,Password,MD5,SHA1,SHA256,SHA512)
values ('odbc.com','odbc2','odbc-password2',$(md5-2),$(sha1-2),$(sha256-2),$(sha512-2))

go
";
            try
            {
                processor = new MacroProcessor();
                processor.Add("md5-1", SqlHelper.Literal(MD5Hasher.Compute("odbc-password1")));
                processor.Add("sha1-1", SqlHelper.Literal(SHA1Hasher.Compute("odbc-password1")));
                processor.Add("sha256-1", SqlHelper.Literal(SHA256Hasher.Compute("odbc-password1")));
                processor.Add("sha512-1", SqlHelper.Literal(SHA512Hasher.Compute("odbc-password1")));

                processor.Add("md5-2", SqlHelper.Literal(MD5Hasher.Compute("odbc-password2")));
                processor.Add("sha1-2", SqlHelper.Literal(SHA1Hasher.Compute("odbc-password2")));
                processor.Add("sha256-2", SqlHelper.Literal(SHA256Hasher.Compute("odbc-password2")));
                processor.Add("sha512-2", SqlHelper.Literal(SHA512Hasher.Compute("odbc-password2")));

                initScript = processor.Expand(initScript);

                sqlCon = DB.OpenConnection();
                scriptRunner = new SqlScriptRunner(initScript);
                scriptRunner.Run(sqlCon);

                this.Accounts.Add(new AuthTestAccount(AuthTestExtensionType.Odbc, "odbc.com", "odbc1", "odbc-password1"));
                this.Accounts.Add(new AuthTestAccount(AuthTestExtensionType.Odbc, "odbc.com", "odbc2", "odbc-password2"));
            }
            finally
            {
                if (sqlCon != null)
                    sqlCon.Close();
            }
        }

        public void Cleanup()
        {
            if (this.DB != null)
            {
                this.DB.Dispose();
                this.DB = null;
            }

            if (this.AuthFilePath != null)
            {
                Helper.DeleteFile(this.AuthFilePath);
                this.AuthFilePath = null;
            }

            if (this.RadiusServer != null)
            {
                this.RadiusServer.Stop();
                this.RadiusServer = null;
            }

            Config.SetConfig(null);
        }

        public string GetRealm(AuthTestExtensionType type)
        {
            switch (type)
            {
                case AuthTestExtensionType.File: return "file.com";
                case AuthTestExtensionType.Ldap: return ADSettings.Domain;
                case AuthTestExtensionType.Odbc: return "odbc.com";
                case AuthTestExtensionType.Radius: return "radius.com";
                case AuthTestExtensionType.Config: return "config.com";
                default: throw new NotImplementedException();
            }
        }

        public string GetType(AuthTestExtensionType type)
        {
            switch (type)
            {
                case AuthTestExtensionType.File: return "LillTek.Datacenter.Server.FileAuthenticationExtension:LillTek.Datacenter.Server.dll";
#if TEST_AD_LDAP
                case AuthTestExtensionType.Ldap :   return "LillTek.Datacenter.Server.LdapAuthenticationExtension:LillTek.Datacenter.Server.dll";
#else
                case AuthTestExtensionType.Ldap: return "LillTek.Datacenter.Server.RadiusAuthenticationExtension:LillTek.Datacenter.Server.dll";
#endif
                case AuthTestExtensionType.Odbc: return "LillTek.Datacenter.Server.OdbcAuthenticationExtension:LillTek.Datacenter.Server.dll";
                case AuthTestExtensionType.Radius: return "LillTek.Datacenter.Server.RadiusAuthenticationExtension:LillTek.Datacenter.Server.dll";
                case AuthTestExtensionType.Config: return "LillTek.Datacenter.Server.ConfigAuthenticationExtension:LillTek.Datacenter.Server.dll";
                default: throw new NotImplementedException();
            }
        }

        public string GetArgs(AuthTestExtensionType type)
        {
            switch (type)
            {
                case AuthTestExtensionType.File: return string.Format("path={0};reload=yes;maxCacheTime=10m", AuthFilePath);
#if TEST_AD_LDAP
                case AuthTestExtensionType.Ldap :   return string.Format("servers={0};authtype=Digest;maxCacheTime=55m",ADSettings.GetServerList());
#else
                case AuthTestExtensionType.Ldap: return string.Format("servers={0}:RADIUS;portcount=4;secret={1}",
                                                                      ADSettings.Servers[0], ADSettings.NasSecret);
#endif
                case AuthTestExtensionType.Odbc: return string.Format("Driver={{SQL Server}};{0};maxCacheTime=55m", DB.ConnectionInfo);
                case AuthTestExtensionType.Radius: return string.Format("servers=localhost:52111;portcount=4;secret={0}", RadiusSecret);
                case AuthTestExtensionType.Config: return "key=Accounts;reload=yes;maxCacheTime=10m";
                default: throw new NotImplementedException();
            }
        }

        public string GetQuery(AuthTestExtensionType type)
        {
            switch (type)
            {
                case AuthTestExtensionType.File: return "";
                case AuthTestExtensionType.Ldap: return "";
                case AuthTestExtensionType.Odbc: return "select 0 from Accounts where Realm=$(realm) and Account=$(account) and SHA512=$(sha512-password)";
                case AuthTestExtensionType.Radius: return "";
                case AuthTestExtensionType.Config: return "";
                default: throw new NotImplementedException();
            }
        }
    }
}
