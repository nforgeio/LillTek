//-----------------------------------------------------------------------------
// FILE:        _SqlTestDatabase.cs
// OWNER:       JEFFL
// COPYRIGHT:   Copyright (c) 2005-2014 by LillTek, LLC.  All rights reserved.
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
    public class _SqlTestDatabase
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Data")]
        public void SqlTestDatabase_Basic()
        {
            using (SqlTestDatabase.Create())
            {
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Data")]
        public void SqlTestDatabase_CreateDelete()
        {
            SqlTestDatabase     dbTest;
            SqlConnectionInfo   conInfo;
            SqlContext          ctx = null;
            SqlCommand          cmd;
            DataTable           dt;
            bool                found;

            // Verify that the test database is created.

            dbTest = SqlTestDatabase.Create();
            try
            {
                conInfo = dbTest.ConnectionInfo;
                ctx = new SqlContext(conInfo);
                ctx.Open();

                cmd = ctx.CreateCommand("exec sp_databases");
                dt = ctx.ExecuteTable(cmd);
                found = false;

                for (int i = 0; i < dt.Rows.Count; i++)
                    if (String.Compare(SqlHelper.AsString(dt.Rows[i]["DATABASE_NAME"]), SqlTestDatabase.DefTestDatabase, true) == 0)
                    {
                        found = true;
                        break;
                    }

                Assert.IsTrue(found);
            }
            finally
            {
                if (ctx != null)
                {
                    ctx.Close();
                    ctx = null;
                }

                dbTest.Dispose();
            }

            // Verify that the test database was deleted

            conInfo.Database = "MASTER";

            ctx = new SqlContext(conInfo);
            ctx.Open();
            try
            {
                cmd = ctx.CreateCommand("exec sp_databases");
                dt = ctx.ExecuteTable(cmd);
                found = false;

                for (int i = 0; i < dt.Rows.Count; i++)
                    if (String.Compare(SqlHelper.AsString(dt.Rows[i]["DATABASE_NAME"]), SqlTestDatabase.DefTestDatabase, true) == 0)
                    {
                        found = true;
                        break;
                    }

                Assert.IsFalse(found);
            }
            finally
            {
                if (ctx != null)
                {
                    ctx.Close();
                    ctx = null;
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Data")]
        public void SqlTestDatabase_DeleteExisting()
        {
            SqlTestDatabase     dbTest;
            SqlConnectionInfo   conInfo;
            string              database;
            SqlContext          ctx = null;
            SqlCommand          cmd;
            DataTable           dt;
            bool                found;

            // Create a default test database and create a table within it.

            dbTest = SqlTestDatabase.Create();
            conInfo = dbTest.ConnectionInfo;
            database = conInfo.Database;
            dbTest.Dispose();

            conInfo.Database = "MASTER";
            ctx = new SqlContext(conInfo);
            ctx.Open();

            try
            {
                cmd = ctx.CreateCommand("create database [{0}]", database);
                ctx.Execute(cmd);

                ctx.Close();
                ctx = null;

                conInfo.Database = database;
                ctx = new SqlContext(conInfo);
                ctx.Open();

                cmd = ctx.CreateCommand("create table Test (field1 int)");
                ctx.Execute(cmd);
            }
            finally
            {
                if (ctx != null)
                {
                    ctx.Close();
                    ctx = null;
                }
            }

            // OK, now use SqlTestDatabase to create a new database and verify
            // that it actually deleted the old database by checking to see that
            // the table we created above no longer exists.

            dbTest = SqlTestDatabase.Create();

            try
            {
                conInfo = dbTest.ConnectionInfo;
                ctx = new SqlContext(conInfo);
                ctx.Open();

                cmd = ctx.CreateCommand("exec sp_tables");
                dt = ctx.ExecuteTable(cmd);
                found = false;

                for (int i = 0; i < dt.Rows.Count; i++)
                    if (String.Compare(SqlHelper.AsString(dt.Rows[i]["TABLE_NAME"]), "Test", true) == 0)
                    {
                        found = true;
                        break;
                    }

                Assert.IsFalse(found);
            }
            finally
            {
                if (ctx != null)
                {
                    ctx.Close();
                    ctx = null;
                }

                dbTest.Dispose();
            }
        }
    }
}

