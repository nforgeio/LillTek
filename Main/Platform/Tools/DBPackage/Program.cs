//-----------------------------------------------------------------------------
// FILE:        Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the DBPackage tool entrypoint.

using System;
using System.IO;
using System.Drawing;
using System.Collections;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows.Forms;

using LillTek.Common;
using LillTek.Install;
using LillTek.Data.Install;

namespace LillTek.Tools.DBPackage
{
    /// <summary>
    /// The DBPackage tool provides a way to create and install database packages.
    /// See the application entry point method <see cref="Program.Main">Main</see>
    /// for a description of the command line parameters.
    /// </summary>
    public class Program
    {
        private enum Mode
        {
            Create,
            Install
        }

        /// <summary>
        /// Returns the application name.
        /// </summary>
        internal static string Name
        {
            get { return "DBPackage"; }
        }

        /// <summary>
        /// Application entry point.
        /// </summary>
        /// <remarks>
        /// See <see cref="OverviewDoc"/> for a description of the operation
        /// of the tool as well as the command line parameters.
        /// </remarks>
        [STAThread]
        public static void Main(string[] args)
        {
            Mode        mode        = Mode.Create;
            string      packagePath = null;

            args = CommandLine.ExpandFiles(args);

            // Figure the mode.

            foreach (string arg in args)
            {
                if (arg.StartsWith("-install:"))
                {
                    mode = Mode.Install;
                    packagePath = arg.Substring(9);
                    break;
                }
                else if (arg.StartsWith("-create"))
                {
                    mode = Mode.Create;
                    break;
                }
            }

            switch (mode)
            {
                case Mode.Create:

                    Create(args);
                    break;

                case Mode.Install:

                    Install(args, packagePath);
                    break;

                default:

                    UsageForm.Display();
                    break;
            }
        }

        /// <summary>
        /// Implemnts -install mode.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <param name="packagePath">Path to the database package.</param>
        private static void Install(string[] args, string packagePath)
        {
            DBPackageInstaller      installer;
            Package                 package        = null;
            DBInstallResult         result;
            string                  configFile     = null;
            string                  conStringKey   = null;
            string                  conStringMacro = null;
            string                  defDB          = null;
            Config                  config;
            string                  conString      = null;
            StreamReader            reader;
            FileStream              fs;

            foreach (string arg in args)
            {
                if (arg.StartsWith("-config:"))
                    configFile = arg.Substring(8);
                else if (arg.StartsWith("-setting:"))
                    conStringKey = arg.Substring(9);
                else if (arg.StartsWith("-macro:"))
                    conStringMacro = arg.Substring(7);
                else if (arg.StartsWith("-defdb:"))
                    defDB = arg.Substring(7);
            }

            if (defDB == null)
                defDB = string.Empty;

            try
            {
                if (configFile != null && conStringKey != null)
                {
                    reader = new StreamReader(configFile, Helper.AnsiEncoding);

                    try
                    {
                        config = new Config(null, reader);
                    }
                    finally
                    {
                        reader.Close();
                    }

                    conString = config.Get(conStringKey, (string)null);
                    if (conString == null || conString.StartsWith("$("))
                        conString = null;
                }

                if (conString == null)
                    conString = "server=localhost;database=" + defDB + ";uid=;pwd=";

                package = new Package(packagePath);
                installer = new DBPackageInstaller(package);
                result = installer.InstallWizard(conString);

                if (result != DBInstallResult.Installed && result != DBInstallResult.Upgraded)
                    return;

                conString = installer.ConnectionString;
                if (configFile != null && conStringMacro != null)
                {

                    fs = new FileStream(configFile, FileMode.Open, FileAccess.ReadWrite);
                    try
                    {
                        Config.EditMacro(fs, Helper.AnsiEncoding, conStringMacro, conString);
                    }
                    finally
                    {
                        fs.Close();
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, Program.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (package != null)
                    package.Close();
            }
        }

        /// <summary>
        /// Returns the files in the directory path that match the pattern.
        /// </summary>
        /// <param name="path">The directory path (or <c>null</c>).</param>
        /// <param name="pattern">The file search pattern.</param>
        /// <returns>The set of fully qualified file names that match.</returns>
        private static string[] GetFiles(string path, string pattern)
        {
            if (path == null)
                return new string[0];

            try
            {
                return Directory.GetFiles(path, pattern);
            }
            catch
            {
                return new string[0];
            }
        }

        /// <summary>
        /// Implemnts -create mode.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        private static void Create(string[] args)
        {

            DBPackageBuilder    builder;
            string              setupPath   = null;
            string              welcomePath = null;
            string              upgradePath = null;
            string              schemaPath  = null;
            string              funcsPath   = null;
            string              procsPath   = null;
            string              outPath     = null;

            foreach (string arg in args)
            {
                if (arg.StartsWith("-setup:"))
                    setupPath = arg.Substring(7);
                else if (arg.StartsWith("-welcome:"))
                    welcomePath = arg.Substring(9);
                else if (arg.StartsWith("-upgrade:"))
                    upgradePath = arg.Substring(9);
                else if (arg.StartsWith("-schema:"))
                    schemaPath = arg.Substring(8);
                else if (arg.StartsWith("-funcs:"))
                    funcsPath = arg.Substring(7);
                else if (arg.StartsWith("-procs:"))
                    procsPath = arg.Substring(7);
                else if (arg.StartsWith("-out:"))
                    outPath = arg.Substring(5);
            }

            if (setupPath == null || schemaPath == null || outPath == null)
            {
                UsageForm.Display();
                return;
            }

            try
            {
                builder = new DBPackageBuilder(setupPath);

                if (welcomePath != null)
                    builder.AddWelcomeRtf(welcomePath);

                foreach (string file in GetFiles(schemaPath, "*.def"))
                    builder.AddSchemaScript(file);

                foreach (string file in GetFiles(schemaPath, "*.sql"))
                    builder.AddSchemaScript(file);

                foreach (string file in GetFiles(upgradePath, "*.sql"))
                    builder.AddUpgradeScript(file);

                foreach (string file in GetFiles(funcsPath, "*.sql"))
                    builder.AddFuncScript(file);

                foreach (string file in GetFiles(procsPath, "*.sql"))
                    builder.AddProcScript(file);

                builder.CloseWrite(outPath);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, Program.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
