//-----------------------------------------------------------------------------
// FILE:        SqlTestDatabase.cs
// OWNER:       JEFFL
// COPYRIGHT:   Copyright (c) 2005-2014 by LillTek, LLC.  All rights reserved.
// DESCRIPTION: Initializes a database suitable for use in unit tests.

using System;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Data.Odbc;
using System.Collections;

using LillTek.Common;

namespace LillTek.Data
{
    /// <summary>
    /// Initializes a database suitable for use in unit tests.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is designed to be used in UNIT tests that need to gain
    /// access to a clean database.  Use <see cref="Create(string)" /> method to
    /// initialize a database and get its connection string and <see cref="Dispose" />
    /// to delete the database when you're done with it.
    /// </para>
    /// <para>
    /// By default <see cref="Create(string)" /> will create an empty SQLEXPRESS 
    /// database on the local machine using the name passed using integrated
    /// Windows security.  This can be overriden by specifying a connection
    /// in the <b>LT_TEST_DB</b> environment variable.  The database account
    /// must have administrator rights to the database.
    /// </para>
    /// </remarks>
    public sealed class SqlTestDatabase : IDisposable
    {
        /// <summary>
        /// The default unit test database name.
        /// </summary>
        public const string DefTestDatabase = "UnitTest";

        //---------------------------------------------------------------------
        // Static members

        private static string conString;   // The default test database connection string

        /// <summary>
        /// Static constructor.
        /// </summary>
        static SqlTestDatabase()
        {
            conString = EnvironmentVars.Get("LT_TEST_DB");
            if (conString == null)
                conString = "server=.\\SQLEXPRESS;Integrated Security=SSPI";
        }

        /// <summary>
        /// Creates an empty database to be used for unit testing.
        /// </summary>
        /// <param name="database">Name of the database to be created.</param>
        /// <returns>The database connection string.</returns>
        /// <remarks>
        /// <para>
        /// By default, the method will attempt to create the named database on the
        /// local SQLEXPRESS database instance, deleting an existing database if
        /// one is present.
        /// </para>
        /// <para>
        /// This default behavor can be overridden by specifying the a connection
        /// string in the <b>LT_TEST_DB</b> configuration environment variable.
        /// The method will use the server and authentication portions of this
        /// connection string and replace the database name with the name passed
        /// to this method.  The account must have administrator rights to the
        /// database.
        /// </para>
        /// </remarks>
        public static SqlTestDatabase Create(string database)
        {
            return new SqlTestDatabase(database);
        }

        /// <summary>
        /// Creates an empty database with the default test database name to be used 
        /// for unit testing.
        /// </summary>
        /// <returns>The database connection string.</returns>
        /// <remarks>
        /// <para>
        /// By default, the method will attempt to create the database on the
        /// local SQLEXPRESS database instance, deleting an existing database if
        /// one is present.
        /// </para>
        /// <para>
        /// This default behavor can be overridden by specifying the a connection
        /// string in the <b>LT_TEST_DB</b> configuration environment variable.
        /// The method will use the server and authentication portions of this
        /// connection string and replace the database name with the name passed
        /// to this method.  The account must have administrator rights to the
        /// database.
        /// </para>
        /// </remarks>
        public static SqlTestDatabase Create()
        {
            return new SqlTestDatabase(DefTestDatabase);
        }

        //---------------------------------------------------------------------
        // Instance members

        private SqlConnectionInfo    conInfo;
        private string              database;
        private bool                dbExists = false;

        /// <summary>
        /// Constructs a test database instance along with the underlying 
        /// database.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <remarks>
        /// If a database with this name already exists on the server then it
        /// will be deleted.  Then an empty database with this name will be
        /// created.
        /// </remarks>
        private SqlTestDatabase(string database)
        {
            SqlContext      ctx;
            SqlCommand      cmd;
            DataTable       dt;

            this.database = database;

            // Connect the the database server and the MASTER database to
            // handle the creation of the database.

            conInfo = new SqlConnectionInfo(conString);
            conInfo.Database = "MASTER";

            ctx = new SqlContext(conInfo);
            ctx.Open();

            try
            {
                // Delete any existing database with this name

                cmd = ctx.CreateCommand("exec sp_databases");
                dt  = ctx.ExecuteTable(cmd);

                for (int i = 0; i < dt.Rows.Count; i++)
                    if (String.Compare(SqlHelper.AsString(dt.Rows[i]["DATABASE_NAME"]), database, true) == 0)
                    {
                        OdbcConnection.ReleaseObjectPool(); // This necessary because any cached connections
                        SqlConnection.ClearAllPools();      // to this database will prevent us from
                                                            // dropping the database.

                        cmd = ctx.CreateCommand("drop database [{0}]", database);
                        ctx.Execute(cmd);
                        break;
                    }

                // Create the database

                cmd = ctx.CreateCommand("create database [{0}]", database);
                ctx.Execute(cmd);
                dbExists = true;

                // Set the connection information to reference the database.

                conInfo.Database = database;
            }
            finally
            {
                ctx.Close();
            }
        }

        /// <summary>
        /// Returns the connection information necessary for connecting to 
        /// the test database.
        /// </summary>
        public SqlConnectionInfo ConnectionInfo
        {
            get { return conInfo; }
        }

        /// <summary>
        /// Creates and opens a connection to the database.
        /// </summary>
        /// <returns>The connection instance.</returns>
        public SqlConnection OpenConnection()
        {
            var sqlCon = new SqlConnection(conInfo.ToString());

            sqlCon.Open();
            return sqlCon;
        }

        /// <summary>
        /// Deletes the associated database from the server if it exists.
        /// </summary>
        public void Dispose()
        {

            if (!dbExists)
                return;

            OdbcConnection.ReleaseObjectPool(); // This necessary because any cached connections
            SqlConnection.ClearAllPools();      // to this database will prevent us from
                                                // dropping the database.
            SqlContext      ctx;
            SqlCommand      cmd;

            conInfo.Database = "MASTER";
            ctx = new SqlContext(conInfo);
            ctx.Open();

            try
            {
                cmd = ctx.CreateCommand("drop database [{0}]", database);
                ctx.Execute(cmd);
            }
            finally
            {
                ctx.Close();
                dbExists = false;
            }
        }
    }
}
