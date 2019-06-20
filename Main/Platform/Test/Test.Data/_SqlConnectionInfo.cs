//-----------------------------------------------------------------------------
// FILE:        _SqlConnectionInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests.

using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Configuration;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Data;
using LillTek.Testing;

namespace LillTek.Data.Test
{
    [TestClass]
    public class _SqlConnectionInfo
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Data")]
        public void SqlConnectionInfo_Basic()
        {
            SqlConnectionInfo info;
            ArgCollection args;

            info = SqlConnectionInfo.Parse("server=foo");
            Assert.AreEqual("foo", info.Server);

            info = SqlConnectionInfo.Parse("SERVER=foo");
            Assert.AreEqual("foo", info.Server);

            info = SqlConnectionInfo.Parse("data source=foo");
            Assert.AreEqual("foo", info.Server);

            info = SqlConnectionInfo.Parse("DATA SOURCE=foo");
            Assert.AreEqual("foo", info.Server);

            info = SqlConnectionInfo.Parse("initial catalog=foo");
            Assert.AreEqual("foo", info.Database);

            info = SqlConnectionInfo.Parse("INITIAL CATALOG=foo");
            Assert.AreEqual("foo", info.Database);

            info = SqlConnectionInfo.Parse("database=foo");
            Assert.AreEqual("foo", info.Database);

            info = SqlConnectionInfo.Parse("DATABASE=foo");
            Assert.AreEqual("foo", info.Database);

            info = SqlConnectionInfo.Parse("uid=foo");
            Assert.AreEqual("foo", info.UserID);

            info = SqlConnectionInfo.Parse("UID=foo");
            Assert.AreEqual("foo", info.UserID);

            info = SqlConnectionInfo.Parse("user id=foo");
            Assert.AreEqual("foo", info.UserID);

            info = SqlConnectionInfo.Parse("USER ID=foo");
            Assert.AreEqual("foo", info.UserID);

            info = SqlConnectionInfo.Parse("pwd=foo");
            Assert.AreEqual("foo", info.Password);

            info = SqlConnectionInfo.Parse("PWD=foo");
            Assert.AreEqual("foo", info.Password);

            info = SqlConnectionInfo.Parse("password=foo");
            Assert.AreEqual("foo", info.Password);

            info = SqlConnectionInfo.Parse("PASSWORD=foo");
            Assert.AreEqual("foo", info.Password);

            info = SqlConnectionInfo.Parse("integrated security=SSPI");
            Assert.AreEqual("SSPI", info.Security);

            info = SqlConnectionInfo.Parse("INTEGRATED security=SSPI");
            Assert.AreEqual("SSPI", info.Security);

            info = SqlConnectionInfo.Parse("Trusted_Connection=true");
            Assert.AreEqual("SSPI", info.Security);

            info = SqlConnectionInfo.Parse("TRUSTED_Connection=TRUE");
            Assert.AreEqual("SSPI", info.Security);

            info = SqlConnectionInfo.Parse("Trusted_Connection=false");
            Assert.IsNull(info.Security);

            info = SqlConnectionInfo.Parse("Other=foo");
            Assert.AreEqual("foo", info["OTHER"]);

            info = new SqlConnectionInfo("server=myserver;database=mydatabase;uid=myuser;pwd=mypassword;other=foo");
            Assert.AreEqual("myserver", info.Server);
            Assert.AreEqual("mydatabase", info.Database);
            Assert.AreEqual("myuser", info.UserID);
            Assert.AreEqual("mypassword", info.Password);
            Assert.AreEqual("foo", info["other"]);

            args = new ArgCollection(info.ToString());
            Assert.AreEqual("myserver", args["server"]);
            Assert.AreEqual("mydatabase", args["database"]);
            Assert.AreEqual("myuser", args["uid"]);
            Assert.AreEqual("mypassword", args["pwd"]);
            Assert.IsNull(args["integrated security"]);

            info = SqlConnectionInfo.Parse("server=foo");
            Assert.IsNull(info["bar"]);

            info = new SqlConnectionInfo();
            info.Server = "myserver";
            info.Database = "mydatabase";
            info.UserID = "myid";
            info.Password = "mypassword";
            info.Security = "SSPI";

            Assert.AreEqual("myserver", info.Server);
            Assert.AreEqual("mydatabase", info.Database);
            Assert.AreEqual("myid", info.UserID);
            Assert.AreEqual("mypassword", info.Password);
            Assert.AreEqual("SSPI", info.Security);

            args = new ArgCollection(info.ToString());
            Assert.AreEqual("myserver", args["server"]);
            Assert.AreEqual("mydatabase", args["database"]);
            Assert.AreEqual("myid", args["uid"]);
            Assert.AreEqual("mypassword", args["pwd"]);
            Assert.AreEqual("SSPI", args["integrated security"]);

            info = new SqlConnectionInfo("server=foo;database=bar");
            args = new ArgCollection(info.ToString());
            Assert.AreEqual("foo", args["server"]);
            Assert.AreEqual("bar", args["database"]);
            Assert.AreEqual("SSPI", args["integrated security"]);
        }
    }
}


