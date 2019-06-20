//-----------------------------------------------------------------------------
// FILE:        ServerAdminStep.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Prompts for the database server name and admin account
//              information.

using System;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

using LillTek.Common;
using LillTek.Install;
using LillTek.Data;

namespace LillTek.Data.Install
{
    /// <summary>
    /// Prompts for the database server name and admin account information.
    /// </summary>
    internal class ServerAdminStep : System.Windows.Forms.Form, IWizardStep
    {
        private InstallWizard   wizard;

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox serverName;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox adminAccount;
        private System.Windows.Forms.TextBox adminPassword;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.ComponentModel.Container components = null;

        public ServerAdminStep(InstallWizard wizard)
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
            get { return "Server Information"; }
        }

        /// <summary>
        /// Called when the step is activated.
        /// </summary>
        /// <param name="steps">The step list.</param>
        public void OnStepIn(WizardStepList steps)
        {
            this.Show();

            if (serverName.Text.Trim() == string.Empty)
            {
                serverName.Focus();
                serverName.SelectAll();
            }
            else
            {
                adminAccount.Focus();
                adminAccount.SelectAll();
            }
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

            string      server;
            string      account;
            string      password;

            // Validate the dialog entries.

            server   = serverName.Text.Trim();
            account  = adminAccount.Text.Trim();
            password = adminPassword.Text.Trim();

            if (server == string.Empty)
            {
                MessageBox.Show("Please enter the database server name or IP address.", wizard.SetupTitle,
                                MessageBoxButtons.OK, MessageBoxIcon.Error);

                serverName.Focus();
                serverName.SelectAll();
                return false;
            }

            if (account == string.Empty)
            {
                MessageBox.Show("Please enter the database administrator account.", wizard.SetupTitle,
                                MessageBoxButtons.OK, MessageBoxIcon.Error);

                adminAccount.Focus();
                adminAccount.SelectAll();
                return false;
            }

            // Verify that the database server exists and that the account information
            // is valid by connecting to the database executing sp_helpsrvrolemember
            // and verifying that the account is returned as one of the accounts having
            // the sysadmin role.
            //
            // Note that sp_helpsrvrolemember does not exist on SQL Azure, so we're going
            // to first determine whether we're running on Azure and then skip this call
            // if we are.

            string          conString;
            SqlContext      ctx;
            SqlCommand      cmd;
            DataTable       dt;
            bool            found;
            WaitForm        waitForm;

            conString = string.Format("server={0};database={1};uid={2};pwd={3}",
                                      server, "master", account, password);

            wizard.Enabled = false;
            this.Update();

            waitForm = new WaitForm("Verifying administrator credentials...");
            waitForm.TopLevel = true;
            waitForm.Show();
            waitForm.Update();
            Thread.Sleep(2000);

            ctx = new SqlContext(conString);
            try
            {
                ctx.Open();

                // SQL Azure detection

                wizard.IsSqlAzure = ctx.IsSqlAzure;

                // Verify that the account is an admin

                if (!wizard.IsSqlAzure)
                {
                    cmd = ctx.CreateSPCall("sp_helpsrvrolemember");
                    cmd.Parameters.Add("@srvrolename", SqlDbType.NVarChar).Value = "sysadmin";

                    dt = ctx.ExecuteTable(cmd);
                    found = false;
                    foreach (DataRow row in dt.Rows)
                        if (String.Compare(account, SqlHelper.AsString(row["MemberName"]), true) == 0)
                        {
                            found = true;
                            break;
                        }

                    if (!found)
                    {
                        wizard.Enabled = true;
                        waitForm.Close();

                        MessageBox.Show(string.Format("Account [{0}] is not a system administrator.", account.ToUpper()),
                                        wizard.SetupTitle,
                                        MessageBoxButtons.OK, MessageBoxIcon.Error);

                        adminAccount.Focus();
                        adminAccount.SelectAll();
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

            wizard.SetupState["connectionString"] = conString;
            wizard.SetupState["server"] = server;
            wizard.SetupState["adminAccount"] = account;
            wizard.SetupState["adminPassword"] = password;

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
            this.serverName = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.adminAccount = new System.Windows.Forms.TextBox();
            this.adminPassword = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(16, 216);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(352, 32);
            this.label1.TabIndex = 0;
            this.label1.Text = "Enter the host name or IP address of the database server as well as the database " +
    "administrator account information.";
            // 
            // serverName
            // 
            this.serverName.Location = new System.Drawing.Point(135, 64);
            this.serverName.Name = "serverName";
            this.serverName.Size = new System.Drawing.Size(199, 20);
            this.serverName.TabIndex = 100;
            // 
            // label2
            // 
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(33, 68);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(96, 16);
            this.label2.TabIndex = 2;
            this.label2.Text = "Database Server:";
            this.label2.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label3
            // 
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(49, 100);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(80, 16);
            this.label3.TabIndex = 3;
            this.label3.Text = "Administrator:";
            this.label3.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // adminAccount
            // 
            this.adminAccount.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.adminAccount.Location = new System.Drawing.Point(116, 64);
            this.adminAccount.Name = "adminAccount";
            this.adminAccount.Size = new System.Drawing.Size(100, 20);
            this.adminAccount.TabIndex = 101;
            // 
            // adminPassword
            // 
            this.adminPassword.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.adminPassword.Location = new System.Drawing.Point(116, 96);
            this.adminPassword.Name = "adminPassword";
            this.adminPassword.Size = new System.Drawing.Size(100, 20);
            this.adminPassword.TabIndex = 102;
            // 
            // label5
            // 
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(65, 132);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(64, 16);
            this.label5.TabIndex = 103;
            this.label5.Text = "Password:";
            this.label5.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.adminAccount);
            this.groupBox1.Controls.Add(this.adminPassword);
            this.groupBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox1.Location = new System.Drawing.Point(19, 32);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(334, 144);
            this.groupBox1.TabIndex = 104;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Server Information";
            // 
            // ServerAdminStep
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(376, 256);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.serverName);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "ServerAdminStep";
            this.ShowInTaskbar = false;
            this.Text = "GetServer";
            this.Load += new System.EventHandler(this.ServerAdminStep_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion

        private void ServerAdminStep_Load(object sender, System.EventArgs args)
        {
            serverName.Text = (string)wizard.SetupState["server"];

            adminPassword.PasswordChar = Helper.PasswordChar;
        }
    }
}
