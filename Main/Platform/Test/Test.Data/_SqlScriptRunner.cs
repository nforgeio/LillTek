//-----------------------------------------------------------------------------
// FILE:        _SqlScriptRunner.cs
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
    public class _SqlScriptRunner
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Data")]
        public void SqlScriptRunner_Parse()
        {
            SqlScriptRunner runner;

            runner = new SqlScriptRunner("");
            Assert.AreEqual(0, runner.Count);

            runner = new SqlScriptRunner("create database foo");
            Assert.AreEqual(1, runner.Count);
            Assert.AreEqual("create database foo\r\n", runner[0]);

            runner = new SqlScriptRunner("create database foo\r\ngo");
            Assert.AreEqual(1, runner.Count);
            Assert.AreEqual("create database foo\r\n", runner[0]);

            runner = new SqlScriptRunner("create database foo\r\ngo\r\n");
            Assert.AreEqual(1, runner.Count);
            Assert.AreEqual("create database foo\r\n", runner[0]);

            runner = new SqlScriptRunner("create database foo\r\ngo\r\n\r\n");
            Assert.AreEqual(1, runner.Count);
            Assert.AreEqual("create database foo\r\n", runner[0]);

            runner = new SqlScriptRunner("create database foo\r\ndrop database foo\r\n    go    \r\n\r\n");
            Assert.AreEqual(1, runner.Count);
            Assert.AreEqual("create database foo\r\ndrop database foo\r\n", runner[0]);

            runner = new SqlScriptRunner("create database foo\r\ngo\r\ndrop database foo\r\ngo");
            Assert.AreEqual(2, runner.Count);
            Assert.AreEqual("create database foo\r\n", runner[0]);
            Assert.AreEqual("drop database foo\r\n", runner[1]);

            runner = new SqlScriptRunner("create database foo\r\n    GO    \r\ndrop database foo\r\nGO");
            Assert.AreEqual(2, runner.Count);
            Assert.AreEqual("create database foo\r\n", runner[0]);
            Assert.AreEqual("drop database foo\r\n", runner[1]);
        }

        private const string CreateSchema = @"

create table Foo (

    Message     varchar(256)
)
go

insert into Foo(Message) values('Hello World')
insert into Foo(Message) values('Delete me')
go

delete from Foo where Message='Delete me'
go
";
        private const string DeleteSchema = @"

delete from Foo
go
drop table Foo
go
";
        [TestMethod]
        [TestProperty("Lib", "LillTek.Data")]
        public void SqlScriptRunner_Run()
        {
            SqlTestDatabase     dbTest;
            SqlConnection       con;
            SqlCommand          cmd;
            SqlDataReader       reader;
            int                 cRows;
            QueryDisposition[]  dispositions;

            using (dbTest = SqlTestDatabase.Create())
            {
                con = new SqlConnection(dbTest.ConnectionInfo);
                con.Open();

                try
                {
                    dispositions = new SqlScriptRunner(CreateSchema).Run(con, true);
                    Assert.AreEqual(3, dispositions.Length);
                    for (int i = 0; i < dispositions.Length; i++)
                    {
                        Assert.IsNull(dispositions[i].Exception);
                        Assert.IsNull(dispositions[i].Message);
                    }

                    reader = null;
                    try
                    {
                        cmd = con.CreateCommand();
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandText = "select * from Foo";

                        reader = cmd.ExecuteReader();

                        cRows = 0;
                        while (reader.Read())
                            cRows++;

                        Assert.AreEqual(1, cRows);
                    }
                    finally
                    {
                        if (reader != null)
                            reader.Close();
                    }

                    dispositions = new SqlScriptRunner(DeleteSchema).Run(con, true);
                    Assert.AreEqual(2, dispositions.Length);
                    for (int i = 0; i < dispositions.Length; i++)
                    {
                        Assert.IsNull(dispositions[i].Exception);
                        Assert.IsNull(dispositions[i].Message);
                    }
                }
                finally
                {
                    new SqlScriptRunner(DeleteSchema).Run(con, true);
                    con.Close();
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Data")]
        public void SqlScriptRunner_RunWithErrors()
        {
            SqlTestDatabase     dbTest;
            SqlConnection       con;
            QueryDisposition[]  dispositions;

            using (dbTest = SqlTestDatabase.Create())
            {
                con = new SqlConnection(dbTest.ConnectionInfo);
                con.Open();

                try
                {
                    dispositions = new SqlScriptRunner(CreateSchema).Run(con, true);
                    Assert.AreEqual(3, dispositions.Length);
                    for (int i = 0; i < dispositions.Length; i++)
                    {
                        Assert.IsNull(dispositions[i].Exception);
                        Assert.IsNull(dispositions[i].Message);
                    }

                    dispositions = new SqlScriptRunner("select * from bar\r\ngo\r\nselect * from foo\r\ngo\r\n").Run(con, true);
                    Assert.AreEqual(2, dispositions.Length);
                    Assert.IsNotNull(dispositions[0].Exception);
                    Assert.IsNull(dispositions[1].Exception);

                    dispositions = new SqlScriptRunner("select * from bar\r\ngo\r\nselect * from foo\r\ngo\r\n").Run(con, false);
                    Assert.AreEqual(1, dispositions.Length);
                    Assert.IsNotNull(dispositions[0].Exception);

                    dispositions = new SqlScriptRunner("select * from foo\r\ngo\r\nselect * from bar\r\ngo\r\n").Run(con, true);
                    Assert.AreEqual(2, dispositions.Length);
                    Assert.IsNull(dispositions[0].Exception);
                    Assert.IsNotNull(dispositions[1].Exception);

                    dispositions = new SqlScriptRunner("select * from foo\r\ngo\r\nselect * from bar\r\ngo\r\n").Run(con, false);
                    Assert.AreEqual(2, dispositions.Length);
                    Assert.IsNull(dispositions[0].Exception);
                    Assert.IsNotNull(dispositions[1].Exception);

                    dispositions = new SqlScriptRunner(DeleteSchema).Run(con, true);
                    Assert.AreEqual(2, dispositions.Length);
                    for (int i = 0; i < dispositions.Length; i++)
                    {
                        Assert.IsNull(dispositions[i].Exception);
                        Assert.IsNull(dispositions[i].Message);
                    }
                }
                finally
                {
                    new SqlScriptRunner(DeleteSchema).Run(con, true);
                    con.Close();
                }
            }
        }
    }
}

