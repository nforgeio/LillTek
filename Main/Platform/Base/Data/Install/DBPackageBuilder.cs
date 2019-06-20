//-----------------------------------------------------------------------------
// FILE:        DBPackageBuilder.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Handles the creation of deployable database package.

using System;
using System.IO;
using System.Text;
using System.Data;
using System.Data.SqlClient;

using LillTek.Common;
using LillTek.Data;
using LillTek.Install;

namespace LillTek.Data.Install
{
    /// <summary>
    /// Handles the creation of a deployable database installaion package.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A database installation package is an archive holding files describing 
    /// the database schema creation, schema upgrade, function and stored
    /// procedure scripts.  Database packages provide a clean and unified
    /// way of installing and upgrading databases.
    /// </para>
    /// <para>
    /// This class implements the creation of a database package from 
    /// database script source files. A database package installation 
    /// package is simply a LillTek.Install.Package with a well-defined 
    /// folder and file  structure:
    /// </para>
    /// <code language="none">
    ///     /Setup.ini              Installation settings
    ///     /Welcome.rtf            Welcome screen RTF text
    ///     
    ///     /Schema                 Holds the schema installation information.
    ///     
    ///         Schema.sql          Database schema initialization script
    ///         DeleteProcs.sql     Deletes functions and stored procedures
    ///         GrantAccess.sql     Grants access to an account marked by
    ///                             %account% within the script
    /// 
    ///     /Procs
    ///     
    ///         *.sql               Stored procedure creation scripts (typically
    ///                             one procedure per file).
    /// 
    ///     /Funcs
    ///     
    ///         *.sql               Function creation scripts (typically one
    ///                             function per file).
    /// 
    ///     /Upgrade
    ///     
    ///         ###.sql             Schema upgrade scripts.  The portion of the
    ///                             file name to the left of the period should be
    ///                             a valid version string (such as #.#.####.#).
    /// </code>
    /// <para>
    /// The /Setup.ini file is simply a set of name/value pairs formatted one
    /// per line as:
    /// </para>
    /// <code language="cs">
    ///     name = value
    /// </code>
    /// <para>
    /// These values are read from the setup information file specified to
    /// to the constructor.  Database packages require the following 
    /// values to be defined:
    /// </para>
    /// <code language="cs">
    ///     SetupTitle      = [database setup wizard title]
    ///     ProductName     = [product name]
    ///     ProductID       = [GUID]
    ///     ProductVersion  = [version]
    ///     DatabaseType    = [purpose]
    ///     SchemaVersion   = [version]
    /// </code>
    /// <para>
    /// where ProductName is the human readable product name, ProductID
    /// is a globally unique product identifier, ProductVersion is a
    /// version string describing the current build/release of the product,
    /// and SchemaVersion is a version string identifying the version 
    /// of the database schema that this package installs.  DatabaseType
    /// is used to describe the purpose of the database for products
    /// that may be built on more than one database.
    /// </para>
    /// <para>
    /// /Welcome.rtf is the file that will be presented to the user
    /// in the welcome dialog.  This file is optional, the welcome dialog
    /// won't be displayed is this isn't present.
    /// </para>
    /// <para>
    /// The /Schema/Schema.sql file is the database script that initializes the
    /// schema in an empty database.
    /// </para>
    /// <para>
    /// The function and stored procedure files hold the database scripts
    /// used to create the stored procedures and functions in the database.
    /// By convention, each script installs only a single procedure or
    /// function, after first deleting the existing object if present.
    /// </para>
    /// <para>
    /// The Upgrade folder holds the scripts that upgrade an existing
    /// database to the current schema version.  The file name for
    /// each script is the schema version that the script upgrades the
    /// schema to.
    /// </para>
    /// <para>
    /// The <see creg="DBPackageInstaller">DBPackageInstaller</see> class handles
    /// the actual installation of a database package.
    /// </para>
    /// </remarks>
    public sealed class DBPackageBuilder : IDisposable
    {
        private Package                 package;
        private EnhancedBlockStream     bs;

        /// <summary>
        /// Constructs a database package builder that will create a
        /// package at the specified file system path.
        /// </summary>
        /// <param name="setupPath">Path of the setup information.</param>
        /// <remarks>
        /// <para>
        /// The setupInfo file must define these values:
        /// </para>
        /// <code language="cs">
        ///     SetupTitle      = [database setup wizard title]
        ///     ProductName     = [product name]
        ///     ProductID       = [GUID]
        ///     ProductVersion  = [version]
        ///     DatabaseType    = [purpose]
        ///     SchemaVersion   = [version]
        ///  </code>
        /// </remarks>
        public DBPackageBuilder(string setupPath)
        {
            const string MissingValue = "Missing setup value [{0}].";

            bs      = new EnhancedBlockStream();
            package = new Package();
            package.Create(bs);

            package.AddFolder("/Schema");
            package.AddFolder("/Procs");
            package.AddFolder("/Funcs");
            package.AddFolder("/Upgrade");

            package.AddFile("/Setup.ini", setupPath);

            // Validate setup.ini

            Config          config;
            StreamReader    reader;

            reader = new StreamReader(setupPath, Helper.AnsiEncoding);
            try
            {
                config = new Config(null, reader);
                if (config.Get("SetupTitle") == null)
                    throw new ArgumentException(string.Format(MissingValue, "SetupTitle"));
                if (config.Get("ProductName") == null)
                    throw new ArgumentException(string.Format(MissingValue, "ProductName"));
                if (config.Get("ProductID") == null)
                    throw new ArgumentException(string.Format(MissingValue, "ProductID"));
                if (config.Get("ProductVersion") == null)
                    throw new ArgumentException(string.Format(MissingValue, "ProductVersion"));
                if (config.Get("DatabaseType") == null)
                    throw new ArgumentException(string.Format(MissingValue, "DatabaseType"));
                if (config.Get("SchemaVersion") == null)
                    throw new ArgumentException(string.Format(MissingValue, "SchemaVersion"));
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        /// Adds the /Welcome.rtf file.
        /// </summary>
        /// <param name="path">Path to the source file.</param>
        public void AddWelcomeRtf(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string fileName;

            fileName = fullPath.Substring(fullPath.LastIndexOf(Helper.PathSepChar) + 1);
            package.AddFile("/Welcome.rtf", fullPath);
        }

        /// <summary>
        /// Adds a schema related script to the package.
        /// </summary>
        /// <param name="path">Path to the source file.</param>
        public void AddSchemaScript(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string fileName;
            fileName = fullPath.Substring(fullPath.LastIndexOf(Helper.PathSepChar) + 1);
            package.AddFile("/Schema/" + fileName, fullPath);
        }

        /// <summary>
        /// Adds a function creation script to the package.
        /// </summary>
        /// <param name="path">Path to the source file.</param>
        public void AddFuncScript(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string fileName;

            fileName = fullPath.Substring(fullPath.LastIndexOf(Helper.PathSepChar) + 1);
            package.AddFile("/Funcs/" + fileName, fullPath);
        }

        /// <summary>
        /// Adds a procedure creation script to the package.
        /// </summary>
        /// <param name="path">Path to the source file.</param>
        public void AddProcScript(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string fileName;

            fileName = fullPath.Substring(fullPath.LastIndexOf(Helper.PathSepChar) + 1);
            package.AddFile("/Procs/" + fileName, fullPath);
        }

        /// <summary>
        /// Adds a schema upgrade script to the package.
        /// </summary>
        /// <param name="path">Path to the source file.</param>
        public void AddUpgradeScript(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string fileName;
            string version;

            if (!fullPath.ToUpper().EndsWith(".SQL"))
                return;

            fileName = fullPath.Substring(fullPath.LastIndexOf(Helper.PathSepChar) + 1);
            version = fileName.Substring(0, fileName.Length - 4);

            try
            {
                new Version(version);
            }
            catch
            {
                throw new ArgumentException("Script file name is not a valid version number.");
            }

            package.AddFile("/Upgrade/" + fileName, fullPath);
        }

        /// <summary>
        /// Closes the builder without saving the package built.
        /// </summary>
        public void Close()
        {
            if (package != null)
            {
                package.Close();
                package = null;
            }
        }

        /// <summary>
        /// Closes the underlying package after saving it to the specified file.
        /// </summary>
        /// <param name="path">File system path where the package will be written.</param>
        public void CloseWrite(string path)
        {
            FileStream fs = null;

            if (package == null)
                throw new InvalidOperationException("Package already closed.");

            try
            {
                fs = new FileStream(path, FileMode.Create);
                package.Close(false);

                bs.Position = 0;
                bs.CopyTo(fs, (int)bs.Length);
            }
            finally
            {
                if (fs != null)
                    fs.Close();

                bs.Close();
            }
        }

        /// <summary>
        /// Closes the underlying package and then returns a stream holding
        /// the contents of the package.
        /// </summary>
        public EnhancedStream CloseDetach()
        {
            if (package == null)
                throw new InvalidOperationException("Package already closed.");

            package.Close(true);
            package = null;

            return bs;
        }

        /// <summary>
        /// Releases all resources.
        /// </summary>
        public void Dispose()
        {
            Close();
        }
    }
}

