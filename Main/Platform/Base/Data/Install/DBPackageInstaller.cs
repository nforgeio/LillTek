//-----------------------------------------------------------------------------
// FILE:        DBPackageInstaller.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Handles the deployment of a database package to a database.

using System;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.Collections;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Install;
using LillTek.Data;

// $todo(jeff.lill): Implement unit tests.

// $todo(jeff.lill): 
//
// The UI could be spruced by trying to discover servers 
// and databases.

// $todo(jeff.lill): 
//
// I need to figure out a way to encrypt database server
// accounts and passwords.

// $todo(jeff.lill): 
//
// Implement support for integrated Windows security.  This
// will probably be the answer to the password encryption
// issue.

// $todo(jeff.lill): 
//
// Implement full support for SQL server connection
// string parameters (eg. the different variations on
// parameter names) using the new SqlConnectionInfo
// class.

// $todo(jeff.lill):
//
// Implement support for reading metadata from SQL script
// comments.  The idea here is that we could use this to
// describe whether a sproc should have public access or
// not.  Might also be useful for embedding default
// job parameters as well.

// $todo(jeff.lill): Implement support for public/private sprocs.

// $todo(jeff.lill): 
//
// Implement support for creating and updating SQL
// Agent jobs.

namespace LillTek.Data.Install
{
    /// <summary>
    /// Handles the deployment of a database package to a database.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A database package is an archive of files created by a
    /// <see cref="DBPackageBuilder" /> instance.  See this class for 
    /// information on what is stored in this archive.  This class
    /// handles the deployment of this package to a database by either
    /// presenting a wizard to step the user through the process
    /// or via a touch-free automatic installation.
    /// </para>
    /// <para><b><u>Touch-free Installation (no user interface)</u></b></para>
    /// <para>
    /// Use the <see cref="Install(DBInstallParams)" /> method to perform an automated
    /// installation of the database package.  The location of the
    /// database and account information necessary to access it are
    /// specified in the <see cref="DBInstallParams" /> parameter
    /// passed to this method.
    /// </para>
    /// <para><b><u>User Interface Wizard</u></b></para>
    /// <para>
    /// Use the <see cref="InstallWizard(string)" /> present the user
    /// interface that steps through the deployment process:
    /// </para>
    /// <list type="number">
    ///     <item>Summarize what's going to happen by displaying /Welcome.rtf.</item>
    ///     <item>Prompt for the database server and admin account information.</item>
    ///     <item>Prompt for the database account for the application.</item>
    ///     <item>
    ///         <description>
    ///         Connect to the database server and verify that the database
    ///         exists and is empty or was created for the product being
    ///         installed.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <description>
    ///         If the database is empty, run the \Schema\Schema.sql script.
    ///         The script is responsible for updating the product and 
    ///         schema information in the database.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <description>
    ///         If the database is not empty then check the database's schema
    ///         version against the schema version this package installs.
    ///         Exit if the database version is greater than or equal to 
    ///         setup version.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <description>
    ///         If the database schema version is less than the setup version,
    ///         then delete all stored procedures and functions and then 
    ///         execute each script \Update\*.sql whose file name holds
    ///         a version number that is greater than the current database
    ///         schema version.  These scripts will be run in order of the
    ///         earliest version to the latest version.  If the scripts are
    ///         implemented correctly, the database schema should now be equal
    ///         to the setup schema version.  The scripts are responsible for 
    ///         updating the product and schema information in the database.
    ///         </description>
    ///     </item>
    ///     <item>The function scripts \Funcs\*.sql will be run.</item>
    ///     <item>The procedure scripts \Procs\*.sql will be run.</item>
    ///     <item>Public access will be granted to the procedures and functions.</item>
    /// </list>    
    /// <para>
    /// This class is easy to use.  Simply instantiate the class and
    /// then call the <see cref="InstallWizard(string)" /> method.  Installation will proceed as described
    /// above.  Afterwards, the installer properties can be queried to find
    /// out what happened.
    /// </para>
    /// </remarks>
    public sealed class DBPackageInstaller
    {
        private const string BadPackage = "Invalid database package.";

        internal Package        package;        // The installation package
        internal Version        curVersion;

        internal string         server;
        internal string         database;
        internal string         account;
        internal string         password;

        internal Config         config;         // Loaded from /Setup.ini
        internal string         setupTitle;
        internal string         productName;
        internal string         productID;
        internal string         databaseType;
        internal Version        productVersion;
        internal Version        schemaVersion;

        private DBInstallParams dbParams;

        /// <summary>
        /// The database installation package.
        /// </summary>
        /// <param name="package">The package (opened for reading).</param>
        public DBPackageInstaller(Package package)
        {
            PackageEntry entry;

            this.package = package;

            // Load the settings from /Setup.ini

            entry = package["/Setup.ini"];
            if (entry == null || !entry.IsFile)
                throw new FormatException(BadPackage);

            try
            {
                config = new Config(null, new StreamReader(new EnhancedMemoryStream(entry.GetContents()), Helper.AnsiEncoding));
                setupTitle = config.Get("SetupTitle");
                productName = config.Get("ProductName");
                productID = config.Get("ProductID");
                productVersion = new Version(config.Get("ProductVersion"));
                databaseType = config.Get("DatabaseType");
                schemaVersion = new Version(config.Get("SchemaVersion"));

                if (setupTitle == null)
                    setupTitle = productName + " Database Setup";

                if (setupTitle == null || productName == null || productID == null ||
                    productVersion == null || databaseType == null || schemaVersion == null)
                {
                    throw new ArgumentException();
                }
            }
            catch
            {
                throw new FormatException(BadPackage);
            }

            // Make sure that we at least have a /Schema/Schema.sql file.

            if (package["/Schema/Schema.sql"] == null)
                throw new FormatException("[/Schema/Schema.sql] file is required.");
        }

        /// <summary>
        /// Installs the package via the UI wizard.
        /// </summary>
        /// <param name="connectionString">The current database connection string (or <c>null</c>).</param>
        /// <returns>Information about the disposition of the installation.</returns>
        /// <remarks>
        /// <note>
        /// The current implementation of this class recognizes only
        /// the following connection string parameters: <b>server</b>, <b>database</b>,
        /// <b>uid</b>, <b>pwd</b>.
        /// </note>
        /// </remarks>
        public DBInstallResult InstallWizard(string connectionString)
        {

            try
            {
                var args  = new ArgCollection(Helper.Normalize(connectionString));

                this.server = Helper.Normalize(args["server"]);
                this.database = Helper.Normalize(args["database"]);
                this.account = Helper.Normalize(args["uid"]);
                this.password = Helper.Normalize(args["pwd"]);

                return LillTek.Data.Install.InstallWizard.Install(this);
            }
            catch
            {
                return DBInstallResult.Error;
            }
        }

        /// <summary>
        /// Performs a touch-free (no user interface) deployment of the database package.
        /// </summary>
        /// <returns>Information about the disposition of the installation.</returns>
        public DBInstallResult Install(DBInstallParams dbParams)
        {
            ArgCollection       args;
            DBInstallResult     result = DBInstallResult.Unknown; ;

            this.dbParams = dbParams;
            server = dbParams.Server;
            database = dbParams.Database;

            if (String.Compare(database, "MASTER", true) == 0)
                throw new ArgumentException("Cannot deploy to the [MASTER] database.");

            args     = ArgCollection.Parse(dbParams.AppSecurity);
            account  = args["uid"];
            password = args["pwd"];

            result = CheckDatabase();
            CheckAppAccount();

            switch (result)
            {
                case DBInstallResult.Installed:

                    Install();
                    break;

                case DBInstallResult.Upgraded:

                    result = Upgrade();
                    break;
            }

            return result;
        }

        /// <summary>
        /// Used during touch-free deployment to verify that the database doesn't exist 
        /// or is empty (in which case an installation needs to be performed) or if the
        /// database does exist and belongs to the same product (so an upgrade should
        /// be attempted).
        /// </summary>
        /// <returns>Indicates whether an install or upgrade should be performed.</returns>
        private DBInstallResult CheckDatabase()
        {
            // Take a look at the database and ensure that it is either empty
            // or is already associated with this product ID and database type.

            DBInstallResult     result = DBInstallResult.Unknown;
            SqlContext          ctx;
            SqlCommand          cmd;
            DataTable           dt;
            string              cs;
            string              dbName;
            string              query;
            bool                exists;

            cs  = string.Format("server={0};database={1};{2}", server, database, dbParams.AdminSecurity);
            ctx = new SqlContext(cs);

            try
            {
                ctx.Open();

                // Create the database if the doesn't already exist.

                cmd = ctx.CreateSPCall("sp_databases");
                dt = ctx.ExecuteTable(cmd);
                exists = false;

                foreach (DataRow row in dt.Rows)
                {
                    dbName = SqlHelper.AsString(row["DATABASE_NAME"]);
                    if (String.Compare(dbName, database, true) == 0)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    if (dbParams.DBPath != null && dbParams.LogPath != null)
                    {
                        Helper.CreateFolderTree(dbParams.DBPath);
                        Helper.CreateFolderTree(dbParams.LogPath);
                        query = string.Format("create database [{0}] on (name='{0}_data', filename='{1}') log on (name='{0}_log', filename='{2}')", database, dbParams.DBPath, dbParams.LogPath);
                    }
                    else if (dbParams.DBPath != null)
                    {
                        Helper.CreateFolderTree(dbParams.DBPath);
                        query = string.Format("create database [{0}] on (name='{0}_data', filename='{1}')", database, dbParams.DBPath);
                    }
                    else
                        query = string.Format("create database [{0}]", database);

                    cmd = ctx.CreateCommand(query);
                    ctx.Execute(cmd);

                    return DBInstallResult.Installed;
                }

                // I'm going to determine whether the database is empty or
                // not by looking at the sysobjects table.  We'll consider
                // it to be not empty if any these conditions are true:
                //
                //      1. Any user tables are present whose names
                //         don't begin with "dt".
                //
                //      2. Any stored procedures or functions are present
                //         whose names don't begin with "dt".


                cmd = ctx.CreateCommand("select 1 from sysobjects where (xtype='U' or xtype='P' or xtype='FN') and name not like 'dt%'");
                dt  = ctx.ExecuteTable(cmd);

                if (dt.Rows.Count == 0)
                {
                    // The database appears to be empty.

                    result = DBInstallResult.Installed;
                }
                else
                {
                    // The database appears to be not empty.  Try calling the
                    // GetProductInfo procedure.  If this fails then assume that
                    // the database belongs to some other application.  If it
                    // succeeds then check the productID and database type against
                    // the setup settings.

                    try
                    {
                        cmd = ctx.CreateSPCall("GetProductInfo");
                        dt  = ctx.ExecuteTable(cmd);

                        // Compare the database's product ID and database type to
                        // the setup settings.

                        if (SqlHelper.AsString(dt.Rows[0]["ProductID"]) != productID ||
                            SqlHelper.AsString(dt.Rows[0]["DatabaseType"]) != databaseType)
                        {
                            throw new InvalidOperationException(string.Format("Package cannot be deployed. Database [{0}] is configured for use by [{1}:{2}].",
                                                                              database, SqlHelper.AsString(dt.Rows[0]["ProductName"]), SqlHelper.AsString(dt.Rows[0]["DatabaseType"])));
                        }

                        // The database looks like can accept the installation.

                        result = DBInstallResult.Upgraded;
                        curVersion = new Version(SqlHelper.AsString(dt.Rows[0]["SchemaVersion"]));
                    }
                    catch
                    {
                        throw new InvalidOperationException(string.Format("Database [{0}] is not empty and appears to in use by another application.\r\n\r\nPlease select a different database.", database));
                    }
                }
            }
            finally
            {
                ctx.Close();
            }

            Assertion.Test(result != DBInstallResult.Unknown);

            return result;
        }

        /// <summary>
        /// Creates the application database account if one doesn't already exist.
        /// </summary>
        private void CheckAppAccount()
        {

            List<string>    accounts;
            string          login;
            string          cs;
            SqlContext      ctx;
            SqlCommand      cmd;
            DataSet         ds;
            DataTable       dt;
            bool            exists;

            if (account == null)
                return;     // Looks like we're using integrated security

            // Get the current set of accounts from the database

            cs  = string.Format("server={0};database={1};{2}", server, database, dbParams.AdminSecurity);
            ctx = new SqlContext(cs);

            try
            {
                ctx.Open();

                // Get the accounts (note that the sp_helplogins sproc does not exist on SQL Azure).

                if (ctx.IsSqlAzure)
                    cmd = ctx.CreateCommand("select name as 'LoginName' from sys.sql_logins");
                else
                    cmd = ctx.CreateSPCall("sp_helplogins");

                ds = ctx.ExecuteSet(cmd);
                dt = ds.Tables["0"];

                accounts = new List<string>();
                foreach (DataRow row in dt.Rows)
                {
                    login = SqlHelper.AsString(row["LoginName"]);

                    // Append the account, skipping any that are empty or
                    // appear to be a server role or a Windows domain account.

                    if (login != null && login.IndexOf('\\') == -1)
                        accounts.Add(login);
                }
            }
            finally
            {
                ctx.Close();
            }

            // Create the account, recreating it if it already exists.

            exists = false;
            for (int i = 0; i < accounts.Count; i++)
                if (String.Compare(account, accounts[i], true) == 0)
                {
                    exists = true;
                    break;
                }

            cs  = string.Format("server={0};database=master;{1}", server, database, dbParams.AdminSecurity);
            ctx = new SqlContext(cs);
            try
            {
                ctx.Open();

                if (exists)
                {
                    if (ctx.IsSqlAzure)
                        cmd = ctx.CreateCommand("drop login '{0}'", account);
                    else
                    {
                        cmd = ctx.CreateSPCall("sp_droplogin");
                        cmd.Parameters.Add("@loginame", SqlDbType.VarChar).Value = account;
                    }

                    ctx.Execute(cmd);
                }

                if (ctx.IsSqlAzure)
                    ctx.CreateCommand("create login {0} with password='{1}'", account, password);
                else
                {
                    cmd = ctx.CreateSPCall("sp_addlogin");
                    cmd.Parameters.Add("@loginame", SqlDbType.VarChar).Value = account;
                    cmd.Parameters.Add("@passwd", SqlDbType.VarChar).Value = password;
                }

                ctx.Execute(cmd);
            }
            finally
            {
                ctx.Close();
            }
        }

        /// <summary>
        /// Performs touch-free package installation.
        /// </summary>
        private void Install()
        {

            PackageEntry        schemaFile = package["/Schema/Schema.sql"];
            PackageEntry        delProcFile = package["/Schema/DeleteProcs.sql"];
            PackageEntry        grantFile = package["/Schema/GrantAccess.sql"];
            PackageEntry        funcFolder = package["/Funcs"];
            PackageEntry        procFolder = package["/Procs"];
            string              script;
            string              cs;
            SqlConnection       sqlCon;
            QueryDisposition[]  qd;

            if (schemaFile == null || !schemaFile.IsFile)
                throw new InvalidOperationException("Invalid Database Package: /Schema/Schema.sql missing.");

            if (delProcFile == null || !delProcFile.IsFile)
                throw new InvalidOperationException("Invalid Database Package: /Schema/DeleteProcs.sql missing.");

            if (grantFile == null || !grantFile.IsFile)
                throw new InvalidOperationException("Invalid Database Package: /Schema/GrantAccess.sql missing.");

            cs     = string.Format("server={0};database={1};{2}", server, database, dbParams.AdminSecurity);
            sqlCon = new SqlConnection(cs);
            sqlCon.Open();

            try
            {
                script = Helper.FromAnsi(delProcFile.GetContents());
                qd     = new SqlScriptRunner(script).Run(sqlCon, true);

                for (int i = 0; i < qd.Length; i++)
                    if (qd[i].Message != null)
                        throw new InvalidOperationException(qd[i].Message);

                // Create the schema

                script = Helper.FromAnsi(schemaFile.GetContents());
                qd     = new SqlScriptRunner(script).Run(sqlCon, true);

                for (int i = 0; i < qd.Length; i++)
                    if (qd[i].Message != null)
                        throw new InvalidOperationException(qd[i].Message);

                // Add the functions

                if (funcFolder != null)
                {
                    foreach (var file in funcFolder.Children)
                    {
                        if (!file.IsFile)
                            continue;

                        script = Helper.FromAnsi(file.GetContents());
                        qd     = new SqlScriptRunner(script).Run(sqlCon, true);

                        for (int i = 0; i < qd.Length; i++)
                            if (qd[i].Message != null)
                                throw new InvalidOperationException(qd[i].Message);
                    }
                }

                // Add the procedures

                if (procFolder != null)
                {
                    foreach (var file in procFolder.Children)
                    {
                        if (!file.IsFile)
                            continue;

                        script = Helper.FromAnsi(file.GetContents());
                        qd = new SqlScriptRunner(script).Run(sqlCon, true);

                        for (int i = 0; i < qd.Length; i++)
                            if (qd[i].Message != null)
                                throw new InvalidOperationException(qd[i].Message);
                    }
                }

                // Grant access to the application account

                if (account != null)
                {
                    script = Helper.FromAnsi(grantFile.GetContents());
                    script = script.Replace("%account%", account);

                    qd = new SqlScriptRunner(script).Run(sqlCon, true);

                    for (int i = 0; i < qd.Length; i++)
                        if (qd[i].Message != null)
                            throw new InvalidOperationException(qd[i].Message);
                }
            }
            finally
            {
                sqlCon.Close();
            }
        }

        private sealed class UpgradeScript
        {
            public Version  Version;
            public string   Script;

            public UpgradeScript(Version version, string script)
            {
                this.Version = version;
                this.Script  = script;
            }
        }

        private sealed class UpgradeComparer : IComparer
        {
            public int Compare(object o1, object o2)
            {
                UpgradeScript us1 = (UpgradeScript)o1;
                UpgradeScript us2 = (UpgradeScript)o2;

                if (us1.Version < us2.Version)
                    return -1;
                else if (us1.Version == us2.Version)
                    return 0;
                else
                    return +1;
            }
        }

        /// <summary>
        /// Touch-free database upgrade.
        /// </summary>
        private DBInstallResult Upgrade()
        {
            PackageEntry        schemaFile = package["/Schema/Schema.sql"];
            PackageEntry        delProcFile = package["/Schema/DeleteProcs.sql"];
            PackageEntry        grantFile = package["/Schema/GrantAccess.sql"];
            PackageEntry        funcFolder = package["/Funcs"];
            PackageEntry        procFolder = package["/Procs"];
            PackageEntry        upgradeFolder = package["/Upgrade"];
            UpgradeScript[]     upgradeScripts;
            string              script;
            string              cs;
            SqlConnection       sqlCon;
            QueryDisposition[]  qd;
            int                 pos;

            if (schemaFile == null || !schemaFile.IsFile)
                throw new InvalidOperationException("Invalid Database Package: /Schema/Schema.sql missing.");

            if (delProcFile == null || !delProcFile.IsFile)
                throw new InvalidOperationException("Invalid Database Package: /Schema/DeleteProcs.sql missing.");

            if (grantFile == null || !grantFile.IsFile)
                throw new InvalidOperationException("Invalid Database Package: /Schema/GrantAccess.sql missing.");

            if (curVersion >= schemaVersion)
                return DBInstallResult.UpToDate;

            // Build the set of upgrade scripts to be run.

            ArrayList list = new ArrayList();

            foreach (PackageEntry file in upgradeFolder.Children)
            {
                Version ver;

                if (!file.IsFile)
                    continue;

                try
                {
                    ver = new Version(file.Name.Substring(0, file.Name.Length - 4));
                }
                catch
                {
                    continue;
                }

                if (ver > curVersion)
                    list.Add(new UpgradeScript(ver, Helper.FromAnsi(file.GetContents())));
            }

            if (list.Count == 0)
                throw new InvalidOperationException("Invalid Database Package: There are no upgrade scripts.");

            list.Sort(new UpgradeComparer());

            for (pos = 0; pos < list.Count; pos++)
                if (((UpgradeScript)list[pos]).Version > curVersion)
                    break;

            if (pos >= list.Count)
                return DBInstallResult.UpToDate;

            upgradeScripts = new UpgradeScript[list.Count - pos];
            list.CopyTo(pos, upgradeScripts, 0, upgradeScripts.Length);

            cs     = string.Format("server={0};database={1};{2}", server, database, dbParams.AdminSecurity);
            sqlCon = new SqlConnection(cs);
            sqlCon.Open();

            try
            {
                // Remove the functions and procedures

                script = Helper.FromAnsi(delProcFile.GetContents());
                qd     = new SqlScriptRunner(script).Run(sqlCon, true);

                for (int i = 0; i < qd.Length; i++)
                    if (qd[i].Message != null)
                        throw new InvalidOperationException(qd[i].Message);

                // Run the update scripts

                foreach (UpgradeScript us in upgradeScripts)
                {
                    script = us.Script;
                    qd     = new SqlScriptRunner(script).Run(sqlCon, true);

                    for (int i = 0; i < qd.Length; i++)
                        if (qd[i].Message != null)
                            throw new InvalidOperationException(qd[i].Message);
                }

                // Add the functions

                if (funcFolder != null)
                {
                    foreach (var file in funcFolder.Children)
                    {
                        if (!file.IsFile)
                            continue;

                        script = Helper.FromAnsi(file.GetContents());
                        qd = new SqlScriptRunner(script).Run(sqlCon, true);

                        for (int i = 0; i < qd.Length; i++)
                            if (qd[i].Message != null)
                                throw new InvalidOperationException(qd[i].Message);
                    }
                }

                // Add the procedures

                if (procFolder != null)
                {
                    foreach (var file in procFolder.Children)
                    {
                        if (!file.IsFile)
                            continue;

                        script = Helper.FromAnsi(file.GetContents());
                        qd     = new SqlScriptRunner(script).Run(sqlCon, true);

                        for (int i = 0; i < qd.Length; i++)
                            if (qd[i].Message != null)
                                throw new InvalidOperationException(qd[i].Message);
                    }
                }

                // Grant access to the application account

                if (account != null)
                {
                    script = Helper.FromAnsi(grantFile.GetContents());
                    script = script.Replace("%account%", account);
                    qd     = new SqlScriptRunner(script).Run(sqlCon, true);

                    for (int i = 0; i < qd.Length; i++)
                        if (qd[i].Message != null)
                            throw new InvalidOperationException(qd[i].Message);
                }
            }
            finally
            {
                sqlCon.Close();
            }

            return DBInstallResult.Upgraded;
        }

        /// <summary>
        /// Grants access to the database.
        /// </summary>
        private void GrantOnly()
        {

            PackageEntry        schemaFile = package["/Schema/Schema.sql"];
            PackageEntry        delProcFile = package["/Schema/DeleteProcs.sql"];
            PackageEntry        grantFile = package["/Schema/GrantAccess.sql"];
            PackageEntry        funcFolder = package["/Funcs"];
            PackageEntry        procFolder = package["/Procs"];
            string              script;
            string              cs;
            SqlConnection       sqlCon;
            QueryDisposition[]  qd;

            if (schemaFile == null || !schemaFile.IsFile)
                throw new InvalidOperationException("Invalid Database Package: /Schema/Schema.sql missing.");

            if (delProcFile == null || !delProcFile.IsFile)
                throw new InvalidOperationException("Invalid Database Package: /Schema/DeleteProcs.sql missing.");

            if (grantFile == null || !grantFile.IsFile)
                throw new InvalidOperationException("Invalid Database Package: /Schema/GrantAccess.sql missing.");

            cs     = string.Format("server={0};database={1};{2}", server, database, dbParams.AdminSecurity);
            sqlCon = new SqlConnection(cs);
            sqlCon.Open();

            try
            {
                // Grant access to the application account

                if (account == null)
                {
                    script = Helper.FromAnsi(grantFile.GetContents());
                    script = script.Replace("%account%", account);
                    qd     = new SqlScriptRunner(script).Run(sqlCon, true);

                    for (int i = 0; i < qd.Length; i++)
                        if (qd[i].Message != null)
                            throw new InvalidOperationException(qd[i].Message);
                }
            }
            finally
            {
                sqlCon.Close();
            }
        }

        /// <summary>
        /// Returns the database connection string after a successful install.
        /// </summary>
        public string ConnectionString
        {
            get
            {
                if (account == null || password == null)
                    return string.Format("server={0};database={1};integrated security=SSPI", server, database);
                else
                    return string.Format("server={0};database={1};uid={2};pwd={3}", server, database, account, password);
            }
        }

        /// <summary>
        /// Returns the database server name after a successful install.
        /// </summary>
        public string Server
        {
            get { return server; }
        }

        /// <summary>
        /// Returns the database name after a successful install.
        /// </summary>
        public string Database
        {
            get { return database; }
        }

        /// <summary>
        /// Returns the service database account name after a successful install (or <c>null</c>
        /// if Windows security is to be used).
        /// </summary>
        public string Account
        {
            get { return account; }
        }

        /// <summary>
        /// Returns the service database account password after a successful install (or <c>null</c>
        /// if Windows security is to be used).
        /// </summary>
        public string Password
        {
            get { return password; }
        }

        /// <summary>
        /// Returns the connection information to be used to access the database after a successful
        /// install.
        /// </summary>
        public SqlConnectionInfo ConnectionInfo
        {
            get
            {
                if (account != null)
                    return SqlConnectionInfo.Parse(string.Format("server={0};database={1};uid={2};pwd={3}", server, database, account, password));
                else
                    return SqlConnectionInfo.Parse(string.Format("server={0};database={1};uid={2};integrated security=SSPI", server, database));
            }
        }

    }
}

