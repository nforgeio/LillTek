//-----------------------------------------------------------------------------
// FILE:        CreateDatabaseForm.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Handles database creation.

using System;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Collections;
using System.Threading;
using System.ComponentModel;
using System.Windows.Forms;

using LillTek.Common;
using LillTek.Install;
using LillTek.Data;

namespace LillTek.Data.Install
{
    /// <summary>
    /// Handles database creation.
    /// </summary>
    internal class CreateDatabaseForm : System.Windows.Forms.Form
    {
        private InstallWizard   wizard;
        private string          database;
        private string          dataPath;
        private string          logPath;

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox nameBox;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label dataFileLabel;
        private System.Windows.Forms.Label logFileLabel;
        private System.Windows.Forms.TextBox logFileBox;
        private System.Windows.Forms.Label instructionsLabel1;
        private System.Windows.Forms.Label instructionsLabel2;
        private System.Windows.Forms.Label instructionsLabel3;
        private System.Windows.Forms.CheckBox customizeCheckBox;
        private System.Windows.Forms.TextBox dataFileBox;
        private Label sizeLabel;
        private ComboBox sizeComboBox;
        private Label sizeLabel2;
        private System.ComponentModel.Container components = null;

        public CreateDatabaseForm(InstallWizard wizard, string dataPath, string logPath)
        {
            InitializeComponent();

            this.wizard   = wizard;
            this.dataPath = dataPath;
            this.logPath  = logPath;

            if (wizard.IsSqlAzure)
            {
                // Allow the selection of the SQL Azure database size size (1 or 10GB).

                sizeLabel.Visible          = true;
                sizeLabel2.Visible         = true;
                sizeComboBox.Visible       = true;
                sizeComboBox.SelectedIndex = 0;

                // Database and log files locations cannot be customized 
                // when runing on SQL Azure.

                customizeCheckBox.Enabled  = false;
                dataFileLabel.Enabled      = false;
                dataFileBox.Enabled        = false;
                logFileLabel.Enabled       = false;
                logFileBox.Enabled         = false;
                instructionsLabel1.Visible = false;
                instructionsLabel2.Visible = false;
                instructionsLabel3.Visible = true;
                instructionsLabel3.Text    = "Database file locations cannot be customized on SQL Azure.";
            }
            else
            {
                // Hide the database size controls if this isn't SQL Azure.

                sizeLabel.Visible    = false;
                sizeComboBox.Visible = false;
                sizeLabel2.Visible   = false;
            }
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

        public string Database
        {
            get { return database; }
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.nameBox = new System.Windows.Forms.TextBox();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.instructionsLabel3 = new System.Windows.Forms.Label();
            this.instructionsLabel2 = new System.Windows.Forms.Label();
            this.instructionsLabel1 = new System.Windows.Forms.Label();
            this.customizeCheckBox = new System.Windows.Forms.CheckBox();
            this.dataFileBox = new System.Windows.Forms.TextBox();
            this.dataFileLabel = new System.Windows.Forms.Label();
            this.logFileLabel = new System.Windows.Forms.Label();
            this.logFileBox = new System.Windows.Forms.TextBox();
            this.sizeLabel = new System.Windows.Forms.Label();
            this.sizeComboBox = new System.Windows.Forms.ComboBox();
            this.sizeLabel2 = new System.Windows.Forms.Label();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(6, 19);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(90, 16);
            this.label1.TabIndex = 0;
            this.label1.Text = "Database Name:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // nameBox
            // 
            this.nameBox.Location = new System.Drawing.Point(97, 16);
            this.nameBox.Name = "nameBox";
            this.nameBox.Size = new System.Drawing.Size(193, 20);
            this.nameBox.TabIndex = 1;
            this.nameBox.TextChanged += new System.EventHandler(this.nameBox_TextChanged);
            // 
            // okButton
            // 
            this.okButton.Location = new System.Drawing.Point(360, 8);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 23);
            this.okButton.TabIndex = 6;
            this.okButton.Text = "Create";
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(360, 40);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 7;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.instructionsLabel3);
            this.groupBox2.Controls.Add(this.instructionsLabel2);
            this.groupBox2.Controls.Add(this.instructionsLabel1);
            this.groupBox2.Location = new System.Drawing.Point(-40, 189);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(504, 144);
            this.groupBox2.TabIndex = 10;
            this.groupBox2.TabStop = false;
            // 
            // instructionsLabel3
            // 
            this.instructionsLabel3.Location = new System.Drawing.Point(56, 98);
            this.instructionsLabel3.Name = "instructionsLabel3";
            this.instructionsLabel3.Size = new System.Drawing.Size(422, 32);
            this.instructionsLabel3.TabIndex = 2;
            this.instructionsLabel3.Text = "Note that the directory path entered must already exist on the SQL Server.";
            // 
            // instructionsLabel2
            // 
            this.instructionsLabel2.Location = new System.Drawing.Point(56, 50);
            this.instructionsLabel2.Name = "instructionsLabel2";
            this.instructionsLabel2.Size = new System.Drawing.Size(422, 48);
            this.instructionsLabel2.TabIndex = 1;
            this.instructionsLabel2.Text = "The new database data and log files will be created on the SQL Server at default " +
    "locations.  To locate these files elsewhere, click Customize File Locations and " +
    "enter the new locations.";
            // 
            // instructionsLabel1
            // 
            this.instructionsLabel1.Location = new System.Drawing.Point(56, 18);
            this.instructionsLabel1.Name = "instructionsLabel1";
            this.instructionsLabel1.Size = new System.Drawing.Size(422, 32);
            this.instructionsLabel1.TabIndex = 0;
            this.instructionsLabel1.Text = "Enter the name of the new database.  Database names may include letters, numbers," +
    " and the underscore character.";
            // 
            // customizeCheckBox
            // 
            this.customizeCheckBox.Location = new System.Drawing.Point(16, 69);
            this.customizeCheckBox.Name = "customizeCheckBox";
            this.customizeCheckBox.Size = new System.Drawing.Size(152, 24);
            this.customizeCheckBox.TabIndex = 3;
            this.customizeCheckBox.Text = "Customize File Locations";
            this.customizeCheckBox.CheckedChanged += new System.EventHandler(this.customizeCheckBox_CheckedChanged);
            // 
            // dataFileBox
            // 
            this.dataFileBox.Enabled = false;
            this.dataFileBox.Location = new System.Drawing.Point(16, 117);
            this.dataFileBox.Name = "dataFileBox";
            this.dataFileBox.Size = new System.Drawing.Size(416, 20);
            this.dataFileBox.TabIndex = 4;
            // 
            // dataFileLabel
            // 
            this.dataFileLabel.Location = new System.Drawing.Point(16, 101);
            this.dataFileLabel.Name = "dataFileLabel";
            this.dataFileLabel.Size = new System.Drawing.Size(88, 16);
            this.dataFileLabel.TabIndex = 2;
            this.dataFileLabel.Text = "Data File:";
            // 
            // logFileLabel
            // 
            this.logFileLabel.Location = new System.Drawing.Point(16, 149);
            this.logFileLabel.Name = "logFileLabel";
            this.logFileLabel.Size = new System.Drawing.Size(64, 16);
            this.logFileLabel.TabIndex = 4;
            this.logFileLabel.Text = "Log File:";
            // 
            // logFileBox
            // 
            this.logFileBox.Enabled = false;
            this.logFileBox.Location = new System.Drawing.Point(16, 165);
            this.logFileBox.Name = "logFileBox";
            this.logFileBox.Size = new System.Drawing.Size(416, 20);
            this.logFileBox.TabIndex = 5;
            // 
            // sizeLabel
            // 
            this.sizeLabel.Location = new System.Drawing.Point(9, 45);
            this.sizeLabel.Name = "sizeLabel";
            this.sizeLabel.Size = new System.Drawing.Size(87, 13);
            this.sizeLabel.TabIndex = 11;
            this.sizeLabel.Text = "Database Size:";
            // 
            // sizeComboBox
            // 
            this.sizeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.sizeComboBox.FormattingEnabled = true;
            this.sizeComboBox.Items.AddRange(new object[] {
            "1",
            "10"});
            this.sizeComboBox.Location = new System.Drawing.Point(97, 42);
            this.sizeComboBox.Name = "sizeComboBox";
            this.sizeComboBox.Size = new System.Drawing.Size(46, 21);
            this.sizeComboBox.TabIndex = 2;
            // 
            // sizeLabel2
            // 
            this.sizeLabel2.AutoSize = true;
            this.sizeLabel2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.sizeLabel2.Location = new System.Drawing.Point(149, 45);
            this.sizeLabel2.Name = "sizeLabel2";
            this.sizeLabel2.Size = new System.Drawing.Size(22, 13);
            this.sizeLabel2.TabIndex = 12;
            this.sizeLabel2.Text = "GB";
            // 
            // CreateDatabaseForm
            // 
            this.AcceptButton = this.okButton;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(450, 314);
            this.Controls.Add(this.sizeLabel2);
            this.Controls.Add(this.sizeComboBox);
            this.Controls.Add(this.sizeLabel);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.nameBox);
            this.Controls.Add(this.logFileBox);
            this.Controls.Add(this.dataFileBox);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.customizeCheckBox);
            this.Controls.Add(this.dataFileLabel);
            this.Controls.Add(this.logFileLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CreateDatabaseForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Create  Database";
            this.Load += new System.EventHandler(this.CreateDatabaseForm_Load);
            this.groupBox2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion

        private void CreateDatabaseForm_Load(object sender, System.EventArgs args)
        {
            database     = null;
            nameBox.Text = wizard.SetupState["database"];

            if (dataPath == string.Empty)
                dataFileBox.Text = "[default]";

            if (logPath == string.Empty)
                logFileBox.Text = "[default]";
        }

        private void customizeCheckBox_CheckedChanged(object sender, System.EventArgs args)
        {
            dataFileBox.Enabled = customizeCheckBox.Checked;
            logFileBox.Enabled  = customizeCheckBox.Checked;
        }

        private void nameBox_TextChanged(object sender, System.EventArgs args)
        {
            if (customizeCheckBox.Checked)
                return;

            if (dataPath != string.Empty)
            {
                if (nameBox.Text.Trim() == string.Empty)
                    dataFileBox.Text = string.Empty;
                else
                    dataFileBox.Text = dataPath + nameBox.Text + ".mdf";
            }

            if (logPath != string.Empty)
            {
                if (nameBox.Text.Trim() == string.Empty)
                    logFileBox.Text = string.Empty;
                else
                    logFileBox.Text = logPath + nameBox.Text + ".ldf";
            }
        }

        private void okButton_Click(object sender, System.EventArgs args)
        {
            database = nameBox.Text.Trim();

            // Validate the database name

            if (database.Length == 0)
            {
                MessageBox.Show("Please enter a name for the new database.",
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

                nameBox.Focus();
                nameBox.SelectAll();
                return;
            }

            if (Char.IsDigit(database[0]))
            {
                MessageBox.Show("Database names cannot start with a number.",
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

                nameBox.Focus();
                nameBox.SelectAll();
                return;
            }

            for (int i = 0; i < database.Length; i++)
                if (!Char.IsLetterOrDigit(database[i]) && database[i] != '_')
                {
                    MessageBox.Show("Invalid character in the database name.\r\n\r\nDatabase names may include only letters, numbers,\r\nor underscores.",
                                    wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

                    nameBox.Focus();
                    nameBox.SelectAll();
                    return;
                }

            // Create the database

            SqlContext      ctx;
            SqlCommand      cmd;
            WaitForm        waitForm;
            string          query;
            string          dataFile;
            string          logFile;
            string          maxSize;

            wizard.Enabled = false;
            this.Update();

            waitForm          = new WaitForm("Creating database [" + database + "]...");
            waitForm.TopLevel = true;
            waitForm.Show();
            waitForm.Update();
            Thread.Sleep(2000);

            ctx = new SqlContext((string)wizard.SetupState["connectionString"]);
            try
            {
                ctx.Open();

                if (ctx.IsSqlAzure)
                    maxSize = string.Format(" (maxsize={0}GB)", sizeComboBox.SelectedItem);
                else
                    maxSize = string.Empty;

                dataFile = dataFileBox.Text.Trim();
                if (wizard.IsSqlAzure || dataFile.Length == 0 || dataFile == "[default]")
                    dataFile = null;

                logFile = logFileBox.Text.Trim();
                if (wizard.IsSqlAzure || logFile.Length == 0 || logFile == "[default]")
                    logFile = null;

                if (dataFile != null && logFile != null)
                    query = string.Format("create database [{0}] on (name='{0}_data', filename='{1}') log on (name='{0}_log', filename='{2}')", database, dataFile, logFile);
                else if (dataFile != null)
                    query = string.Format("create database [{0}] on (name='{0}_data', filename='{1}')", database, dataFile);
                else
                    query = string.Format("create database [{0}]{1}", database, maxSize);

                cmd = ctx.CreateCommand(query);
                ctx.Execute(cmd);
            }
            catch (Exception e)
            {
                wizard.Enabled = true;
                waitForm.Close();
                MessageBox.Show("Cannot create the database.\r\n\r\n" + e.Message,
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                ctx.Close();
            }

            wizard.Enabled = true;
            waitForm.Close();

            // Success!

            DialogResult = DialogResult.OK;
        }

        private void cancelButton_Click(object sender, System.EventArgs args)
        {
            DialogResult = DialogResult.Cancel;
        }
    }
}
