//-----------------------------------------------------------------------------
// FILE:        SelectDatabaseStep.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Prompts for the database to configure.

using System;
using System.IO;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Threading;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

using LillTek.Common;
using LillTek.Install;
using LillTek.Data;

namespace LillTek.Data.Install
{
    /// <summary>
    /// Prompts for the database to configure.
    /// </summary>
    internal class SelectDatabaseStep : System.Windows.Forms.Form, IWizardStep
    {
        private InstallWizard   wizard;
        private string          conString;
        private string          masterDataPath = null;
        private string          masterLogPath  = null;

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button refreshButton;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button createDatabaseButton;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ListBox databaseList;
        private System.ComponentModel.Container components = null;

        public SelectDatabaseStep(InstallWizard wizard)
        {
            this.wizard = wizard;
            InitializeComponent();
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();

            base.Dispose(disposing);
        }

        /// <summary>
        /// Returns the title of the step.
        /// </summary>
        public string Title
        {
            get { return "Select Database"; }
        }

        /// <summary>
        /// Called when the step is activated.
        /// </summary>
        /// <param name="steps">The step list.</param>
        public void OnStepIn(WizardStepList steps)
        {
            this.Show();
            databaseList.Focus();
        }

        /// <summary>
        /// Called when the step is deactivated.
        /// </summary>
        /// <param name="steps">The step list.</param>
        /// <param name="forward"><c>true</c> if we're stepping forward in the wizard.</param>
        /// <returns><c>true</c> if the transition can proceed.</returns>
        public bool OnStepOut(WizardStepList steps, bool forward)
        {
            if (!forward)
                return true;

            string database;

            database = (string)databaseList.SelectedItem;
            if (database == null)
            {
                MessageBox.Show("Select a database from the list or click Create\r\nto create a new one.", wizard.SetupTitle,
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            else if (database.ToUpper() == "MASTER")
            {
                MessageBox.Show("Cannot install into the MASTER database.", wizard.SetupTitle,
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // Take a look at the database and ensure that it is either empty
            // or is already associated with this product ID and database type.

            SqlContext      ctx;
            SqlCommand      cmd;
            DataTable       dt;
            WaitForm        waitForm;
            string      cs;

            wizard.Enabled = false;
            this.Update();

            waitForm = new WaitForm(string.Format("Examining database [{0}]...", database));
            waitForm.TopLevel = true;
            waitForm.Show();
            waitForm.Update();
            Thread.Sleep(2000);

            cs = string.Format("server={0};database={1};uid={2};pwd={3}",
                                wizard.SetupState["server"],
                                database,
                                wizard.SetupState["adminAccount"],
                                wizard.SetupState["adminPassword"]);

            ctx = new SqlContext(cs);
            try
            {
                ctx.Open();

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

                    wizard.SetupState["Action"] = "Install";
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

                        if (SqlHelper.AsString(dt.Rows[0]["ProductID"]) != wizard.SetupState["productID"] ||
                            SqlHelper.AsString(dt.Rows[0]["DatabaseType"]) != wizard.SetupState["databaseType"])
                        {
                            wizard.Enabled = true;
                            waitForm.Close();
                            MessageBox.Show(string.Format("Database [{0}] is configured for use by [{1}:{2}].\r\n\r\nPlease select a different database.",
                                                          database,
                                                          SqlHelper.AsString(dt.Rows[0]["ProductName"]),
                                                          SqlHelper.AsString(dt.Rows[0]["DatabaseType"])),
                                            wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }

                        // The database looks like can accept the installation.

                        wizard.SetupState["Action"] = "Upgrade";
                        wizard.SetupState["CurSchemaVersion"] = SqlHelper.AsString(dt.Rows[0]["SchemaVersion"]);
                    }
                    catch
                    {
                        wizard.Enabled = true;
                        waitForm.Close();
                        MessageBox.Show(string.Format("Database [{0}] is not empty and appears to in use by another application.\r\n\r\nPlease select a different database.", database),
                                        wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                wizard.Enabled = true;
                waitForm.Close();
                MessageBox.Show("Setup could not connect to the database. Please check\r\nthe server name and account settings.\r\n\r\n" + e.Message,
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally
            {
                ctx.Close();
            }

            wizard.Enabled = true;
            waitForm.Close();

            // Success!

            wizard.SetupState["database"] = database;
            wizard.SetupState["connectionString"] = string.Format("server={0};database={1};uid={2};pwd={3}",
                                                                  wizard.SetupState["server"],
                                                                  wizard.SetupState["database"],
                                                                  wizard.SetupState["adminAccount"],
                                                                  wizard.SetupState["adminPassword"]);
            this.Hide();
            return true;
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.databaseList = new System.Windows.Forms.ListBox();
            this.refreshButton = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.createDatabaseButton = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
            this.label1.Location = new System.Drawing.Point(16, 16);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(344, 16);
            this.label1.TabIndex = 0;
            this.label1.Text = "Select the database to be configured from the list below.";
            // 
            // databaseList
            // 
            this.databaseList.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
            this.databaseList.Location = new System.Drawing.Point(56, 40);
            this.databaseList.Name = "databaseList";
            this.databaseList.Size = new System.Drawing.Size(184, 134);
            this.databaseList.Sorted = true;
            this.databaseList.TabIndex = 100;
            this.databaseList.DoubleClick += new System.EventHandler(this.databaseList_DoubleClick);
            // 
            // refreshButton
            // 
            this.refreshButton.Location = new System.Drawing.Point(256, 80);
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.TabIndex = 102;
            this.refreshButton.Text = "Refresh";
            this.refreshButton.Click += new System.EventHandler(this.refreshButton_Click);
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(16, 208);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(352, 40);
            this.label2.TabIndex = 102;
            this.label2.Text = "Alternatively, you can use the Microsoft SQL Server Enterprise Manager and create" +
                " it and then click Refresh and select the new database.";
            // 
            // createDatabaseButton
            // 
            this.createDatabaseButton.Location = new System.Drawing.Point(256, 48);
            this.createDatabaseButton.Name = "createDatabaseButton";
            this.createDatabaseButton.TabIndex = 101;
            this.createDatabaseButton.Text = "Create";
            this.createDatabaseButton.Click += new System.EventHandler(this.createDatabaseButton_Click);
            // 
            // label3
            // 
            this.label3.Location = new System.Drawing.Point(16, 184);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(352, 23);
            this.label3.TabIndex = 103;
            this.label3.Text = "Click Create if you wish to create a new database.";
            // 
            // SelectDatabaseStep
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(376, 256);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.createDatabaseButton);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.refreshButton);
            this.Controls.Add(this.databaseList);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "SelectDatabaseStep";
            this.Text = "SelectDatabaseStep";
            this.Load += new System.EventHandler(this.SelectDatabaseStep_Load);
            this.ResumeLayout(false);

        }
        #endregion

        private void LoadDatabases()
        {
            // Load the listbox with the list of databases on the server and
            // while we're at it we'll extract the master database's data
            // and log file folders.
            //
            // Note that we're not going to add the following system databases
            // to the list:
            //
            //      master
            //      model
            //      msdb
            //      tempdb
            //      ReportServer
            //      ReportServerTempDB

            Dictionary<string, bool>    ignoreDBs = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            SqlContext                  ctx;
            SqlCommand                  cmd;
            DataSet                     ds;
            DataTable                   dt;
            WaitForm                    waitForm;
            string                      dbName;

            ignoreDBs.Add("master", true);
            ignoreDBs.Add("model", true);
            ignoreDBs.Add("msdb", true);
            ignoreDBs.Add("tempdb", true);
            ignoreDBs.Add("ReportServer", true);
            ignoreDBs.Add("ReportServerTempDB", true);

            wizard.Enabled = false;
            this.Update();

            waitForm = new WaitForm("Scanning databases...");
            waitForm.TopLevel = true;
            waitForm.Show();
            waitForm.Update();
            Thread.Sleep(2000);

            ctx = new SqlContext(conString);
            try
            {
                ctx.Open();

                // Get the databases (note that the sp_databases sproc does not
                // exist on SQL Azure).

                if (wizard.IsSqlAzure)
                    cmd = ctx.CreateCommand("select name as DATABASE_NAME from sys.sysdatabases");
                else
                    cmd = ctx.CreateSPCall("sp_databases");

                dt = ctx.ExecuteTable(cmd);
                databaseList.Items.Clear();
                foreach (DataRow row in dt.Rows)
                {
                    dbName = SqlHelper.AsString(row["DATABASE_NAME"]);
                    if (ignoreDBs.ContainsKey(dbName))
                        continue;

                    databaseList.Items.Add(dbName);

                    if (String.Compare(dbName, (string)wizard.SetupState["database"], true) == 0)
                        databaseList.SelectedIndex = databaseList.Items.Count - 1;
                }

                if (!wizard.IsSqlAzure && (masterDataPath == null || masterLogPath == null))
                {
                    // Get the master database file paths

                    cmd = ctx.CreateSPCall("sp_helpdb");
                    cmd.Parameters.Add("@dbname", SqlDbType.NVarChar).Value = "master";

                    ds = ctx.ExecuteSet(cmd);
                    dt = ds.Tables["1"];

                    foreach (DataRow row in dt.Rows)
                    {
                        string  file;
                        int     pos;

                        if (SqlHelper.AsString(row["usage"]).ToLowerInvariant().IndexOf("data") != -1)
                        {
                            file = SqlHelper.AsString(row["filename"]);
                            pos = file.LastIndexOf('\\');
                            if (pos != -1)
                                masterDataPath = file.Substring(0, pos + 1);
                        }
                        else if (SqlHelper.AsString(row["usage"]).ToLowerInvariant().IndexOf("log") != -1)
                        {
                            file = SqlHelper.AsString(row["filename"]);
                            pos = file.LastIndexOf('\\');
                            if (pos != -1)
                                masterLogPath = file.Substring(0, pos + 1);
                        }
                    }

                    // Set the paths to the empty string if all else fails

                    if (masterDataPath == null)
                        masterDataPath = string.Empty;

                    if (masterLogPath == null)
                        masterLogPath = string.Empty;
                }
            }
            catch (Exception e)
            {
                wizard.Enabled = true;
                waitForm.Close();
                MessageBox.Show("Setup could not connect to the database. Please check\r\nthe server name and account settings.\r\n\r\n" + e.Message,
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                ctx.Close();
            }

            wizard.Enabled = true;
            waitForm.Close();
        }

        private void SelectDatabaseStep_Load(object sender, System.EventArgs args)
        {
            conString = (string)wizard.SetupState["connectionString"];
            LoadDatabases();
        }

        private void createDatabaseButton_Click(object sender, System.EventArgs args)
        {
            var form = new CreateDatabaseForm(wizard, masterDataPath, masterLogPath);

            if (form.ShowDialog(wizard) == DialogResult.OK)
            {
                wizard.SetupState["database"] = form.Database;
                LoadDatabases();
            }

            wizard.SetFocusToNext();
        }

        private void refreshButton_Click(object sender, System.EventArgs args)
        {
            LoadDatabases();
            wizard.SetFocusToNext();
        }

        private void databaseList_DoubleClick(object sender, System.EventArgs args)
        {
            wizard.Steps.StepNext();
        }
    }
}
