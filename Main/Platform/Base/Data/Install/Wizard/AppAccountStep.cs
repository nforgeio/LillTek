//-----------------------------------------------------------------------------
// FILE:        AppAccountStep.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Prompts for the application database account information.

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
    /// Prompts for the application database account information.
    /// </summary>
    internal class ServiceAccountStep : System.Windows.Forms.Form, IWizardStep
    {
        private InstallWizard   wizard;
        private string          conString;

        private System.Windows.Forms.Button createAccountButton;
        private System.Windows.Forms.Button refreshButton;
        private System.Windows.Forms.ListBox accountList;
        private System.Windows.Forms.Label label1;
        private System.ComponentModel.Container components = null;

        public ServiceAccountStep(InstallWizard wizard)
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
            get { return "Application Account"; }
        }

        /// <summary>
        /// Called when the step is activated.
        /// </summary>
        /// <param name="steps">The step list.</param>
        public void OnStepIn(WizardStepList steps)
        {
            this.Show();
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

            var account = (string)accountList.SelectedItem;

            if (account == null)
            {

                MessageBox.Show("Select an account from the list or click Create\r\nto create a new one.", wizard.SetupTitle,
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (account.ToUpper() == "SA")
            {

                MessageBox.Show("The SA account cannot be used.  Please select another account.", wizard.SetupTitle,
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (account.ToUpper() != wizard.SetupState["account"].ToUpper())
            {

                wizard.SetupState["account"] = account;
                wizard.SetupState["password"] = string.Empty;
            }

            // Prompt for the account password.

            var form = new PasswordForm(wizard, (string)wizard.SetupState["account"], (string)wizard.SetupState["password"]);

            if (form.ShowDialog(wizard) == DialogResult.Cancel)
                return false;

            wizard.SetupState["password"] = form.Password;

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
            this.createAccountButton = new System.Windows.Forms.Button();
            this.refreshButton = new System.Windows.Forms.Button();
            this.accountList = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // createAccountButton
            // 
            this.createAccountButton.Location = new System.Drawing.Point(256, 80);
            this.createAccountButton.Name = "createAccountButton";
            this.createAccountButton.Size = new System.Drawing.Size(75, 23);
            this.createAccountButton.TabIndex = 107;
            this.createAccountButton.Text = "Create";
            this.createAccountButton.Click += new System.EventHandler(this.createAccountButton_Click);
            // 
            // refreshButton
            // 
            this.refreshButton.Location = new System.Drawing.Point(256, 112);
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.Size = new System.Drawing.Size(75, 23);
            this.refreshButton.TabIndex = 108;
            this.refreshButton.Text = "Refresh";
            this.refreshButton.Click += new System.EventHandler(this.refreshButton_Click);
            // 
            // accountList
            // 
            this.accountList.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.accountList.Location = new System.Drawing.Point(56, 72);
            this.accountList.Name = "accountList";
            this.accountList.Size = new System.Drawing.Size(184, 134);
            this.accountList.Sorted = true;
            this.accountList.TabIndex = 106;
            this.accountList.DoubleClick += new System.EventHandler(this.accountList_DoubleClick);
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(16, 16);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(344, 53);
            this.label1.TabIndex = 105;
            this.label1.Text = "Select or create the database account to be used by the application being install" +
    "ed. Click the Create button to create a new database account.  Note that the SA " +
    "account cannot be used.";
            this.label1.Click += new System.EventHandler(this.label1_Click);
            // 
            // ServiceAccountStep
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(376, 256);
            this.Controls.Add(this.createAccountButton);
            this.Controls.Add(this.refreshButton);
            this.Controls.Add(this.accountList);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "ServiceAccountStep";
            this.Text = "ServiceAccountStep";
            this.Load += new System.EventHandler(this.ServiceAccountStep_Load);
            this.ResumeLayout(false);

        }
        #endregion

        private void LoadAccounts()
        {
            // Load the listbox with the list of existing server logins, filtering
            // out the SA, Windows, or server role accounts as well as any built-in
            // accounts that begin with "##".

            SqlConnectionInfo   conInfo;
            SqlContext          ctx;
            SqlCommand          cmd;
            DataSet             ds;
            DataTable           dt;
            WaitForm            waitForm;
            string              login;

            wizard.Enabled = false;
            this.Update();

            waitForm          = new WaitForm("Scanning accounts...");
            waitForm.TopLevel = true;
            waitForm.Show();
            waitForm.Update();
            Thread.Sleep(2000);

            // Connect to the master database.

            conInfo = new SqlConnectionInfo(conString);
            conInfo.Database = "master";

            ctx = new SqlContext(conInfo.ToString());
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

                accountList.Items.Clear();

                foreach (DataRow row in dt.Rows)
                {
                    login = SqlHelper.AsString(row["LoginName"]);

                    // Append the account, skipping any that are empty or
                    // appear to be a server role, a Windows domain account,
                    // or a built-in account.

                    if (login == null)
                        continue;   // Empty

                    if (login.IndexOf('\\') != -1)
                        continue;   // Windows account

                    if (String.Compare(login, "sa", true) == 0)
                        continue;   // SA account

                    if (login.StartsWith("##"))
                        continue;   // Built-in account

                    accountList.Items.Add(login);
                    if (String.Compare(login, (string)wizard.SetupState["account"], true) == 0)
                        accountList.SelectedIndex = accountList.Items.Count - 1;
                }
            }
            catch (Exception e)
            {
                wizard.Enabled = true;
                waitForm.Close();
                MessageBox.Show("Cannot load database accounts.\r\n\r\n" + e.Message,
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

        private void ServiceAccountStep_Load(object sender, System.EventArgs args)
        {

            conString = (string)wizard.SetupState["connectionString"];
            LoadAccounts();
        }

        private void createAccountButton_Click(object sender, System.EventArgs args)
        {
            CreateAccountForm form;

            wizard.SetupState["account"] = string.Empty;

            form = new CreateAccountForm(wizard);
            if (form.ShowDialog(wizard) == DialogResult.OK)
            {

                wizard.SetupState["account"] = form.Account;
                wizard.SetupState["password"] = form.Password;
                LoadAccounts();
            }

            wizard.SetFocusToNext();
        }

        private void refreshButton_Click(object sender, System.EventArgs args)
        {
            LoadAccounts();
            wizard.SetFocusToNext();
        }

        private void accountList_DoubleClick(object sender, System.EventArgs args)
        {
            wizard.Steps.StepNext();
        }

        private void label1_Click(object sender, EventArgs e)
        {
        }
    }
}
