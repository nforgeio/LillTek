//-----------------------------------------------------------------------------
// FILE:        DBInstallParams.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the parameters necessary for a silent (no UI) 
//              database installation.

using System;
using System.IO;
using System.Data;
using System.Data.SqlClient;

using LillTek.Common;
using LillTek.Install;
using LillTek.Data;
using LillTek.Cryptography;

namespace LillTek.Data.Install
{
    /// <summary>
    /// Describes the parameters necessary for a silent (no UI) database installation.
    /// </summary>
    public sealed class DBInstallParams
    {
        private string      appName;
        private string      server;
        private string      database;
        private string      adminSecurity;
        private string      appSecurity;
        private string      dbPath;
        private string      logPath;
        private string      defDBFolder;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="appName">The application name.</param>
        /// <param name="database">The database name.</param>
        /// <remarks>
        /// <note>
        /// The application name must be no longer than 32 characters
        /// and can include only letters or digit characters.
        /// The application name is used to generate a unique database 
        /// user account and password to be used by the application to gain 
        /// gain access to the database.
        /// </note>
        /// </remarks>
        public DBInstallParams(string appName, string database)
        {
            SqlConnectionInfo   conInfo;
            string              conString;
            string              path;
            int                 pos;

            if (string.IsNullOrWhiteSpace(appName))
                throw new ArgumentException("Invalid application name.");
            else if (appName.Length > 32)
                throw new ArgumentException("Application name exceeds 32 characters.");
            else
            {
                foreach (char ch in appName)
                {
                    if (Char.IsLetterOrDigit(ch))
                        continue;
                    else
                        throw new ArgumentException("Application name may include only letters or digits characters.");
                }
            }

            if (string.IsNullOrWhiteSpace(database))
                throw new ArgumentException("Invalid database path.");

            conString = EnvironmentVars.Get("LT_TEST_DB");
            if (conString != null)
                conInfo = new SqlConnectionInfo(conString);
            else
                conInfo = new SqlConnectionInfo("server=.\\SQLEXPRESS;Integrated Security=SSPI");

            this.appName       = appName;
            this.server        = conInfo.Server;
            this.database      = database;
            this.adminSecurity = string.IsNullOrWhiteSpace(conInfo.Security) ? "" : "Integrated Security=" + conInfo.Security;
            this.appSecurity   = string.Format("uid={0}User;pwd={1}", appName, Crypto.GeneratePassword(8, true));
            this.dbPath        = null;
            this.logPath       = null;

            path = Environment.SystemDirectory;
            pos = path.IndexOf(':');
            if (pos == -1)
                throw new InvalidOperationException("Unexpected system directory path.");

            this.defDBFolder = path.Substring(0, pos + 1) + @"\LillTek\Data";
        }

        /// <summary>
        /// Specifies the server name and database instance.  This defaults to <b>$(MachineName)\SQLEXPRESS</b>.
        /// </summary>
        public string Server
        {
            get { return server; }
            set { server = value; }
        }

        /// <summary>
        /// Specifies the database name.  This has no default and must be specified
        /// in the constructor.
        /// </summary>
        public string Database
        {
            get { return database; }
            set { database = value; }
        }

        /// <summary>
        /// The SQL connection string fragment that describes the parameters
        /// necessary for establishing a database connection with administrator
        /// rights.  This defaults to <b>Integrated Security=SSPI</b>.
        /// </summary>
        public string AdminSecurity
        {
            get { return adminSecurity; }
            set { adminSecurity = value; }
        }

        /// <summary>
        /// The SQL connection string fragment that describes the parameters
        /// necessary for establishing a database connection with application rights.
        /// </summary>
        /// <remarks>
        /// This defaults to <b>uid=&lt;app name&gt;-User;pwd=&lt;random string&gt;</b>.
        /// </remarks>
        public string AppSecurity
        {
            get { return appSecurity; }
            set { appSecurity = value; }
        }

        /// <summary>
        /// The fully qualified path to the database file.
        /// </summary>
        /// <remarks>
        /// This defaults to <b>&lt;drive&gt;:\LillTek\Data\&lt;database name&gt;.mdf</b>
        /// where <b>&lt;drive&gt;</b> is the drive holding the operating system installation. 
        /// </remarks>
        public string DBPath
        {
            get
            {
                if (dbPath != null)
                    return dbPath;

                return defDBFolder + Helper.PathSepString + database + ".mdf";
            }

            set { dbPath = value; }
        }

        /// <summary>
        /// The fully qualified path to the database log file.
        /// </summary>
        /// <remarks>
        /// This defaults to <b>&lt;drive&gt;:\LillTek\Data\&lt;database name&gt;.ldf</b>
        /// where <b>&lt;drive&gt;</b> is the drive holding the operating system installation. 
        /// </remarks>
        /// <summary>
        /// The fully qualified path to the database log file.
        /// </summary>
        public string LogPath
        {
            get
            {
                if (logPath != null)
                    return logPath;

                return defDBFolder + Helper.PathSepString + database + ".ldf";
            }

            set { logPath = value; }
        }
    }
}
