//-----------------------------------------------------------------------------
// FILE:        _Tests.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT Tests

#if TODO

using System;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Threading;

using LillTek.Common;
using LillTek.Data;
using LillTek.Data.Install;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.Install;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Server.Test
{
    [TestClass]
    public class _SentinelServiceDB
    {
        [TestMethod][TestProperty("Lib", "LillTek.Datacenter.Server")]
        public void SentinelServiceDB_DeployDB()
        {
            SqlTestDatabase dbTest;
            Package dbPackage = null;
            DBPackageInstaller dbInstaller;
            DBInstallParams dbParams;
            DBInstallResult result;

            using (dbTest = SqlTestDatabase.Create())
            {
                SqlConnectionInfo conInfo;
                SqlContext ctx = null;
                SqlCommand cmd;
                DataTable dt;

                try
                {
                    // Deploy to a non-existent database

                    dbPackage = new Package(EnvironmentVars.Expand("$(LT_BUILD)\\LillTek.SentinelService.dbpack"));
                    dbParams = new DBInstallParams("SentinelService", dbTest.ConnectionInfo.Database);
                    dbInstaller = new DBPackageInstaller(dbPackage);

                    result = dbInstaller.Install(dbParams);
                    Assert.AreEqual(DBInstallResult.Installed, result);

                    conInfo = SqlConnectionInfo.Parse(dbInstaller.ConnectionString);
                    ctx = new SqlContext(conInfo);
                    ctx.Open();

                    cmd = ctx.CreateSPCall("GetProductInfo");
                    dt = ctx.ExecuteTable(cmd);
                    Assert.AreEqual(1, dt.Rows.Count);

                    cmd = ctx.CreateSPCall("Ping");
                    dt = ctx.ExecuteTable(cmd);
                    Assert.AreEqual(1, dt.Rows.Count);
                    Assert.AreEqual("OK", SqlHelper.AsString(dt.Rows[0]["STATUS"]));

                    ctx.Close();
                    ctx = null;

                    // Deploy again and we should see that the database is up-to-date.

                    SqlConnection.ClearAllPools();
                    result = dbInstaller.Install(dbParams);
                    Assert.AreEqual(DBInstallResult.UpToDate, result);
                }
                finally
                {
                    if (dbPackage != null)
                        dbPackage.Close();

                    if (ctx != null)
                        ctx.Close();
                }
            }
        }
    }
}

#endif // TODO
