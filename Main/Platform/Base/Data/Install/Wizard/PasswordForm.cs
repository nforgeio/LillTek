//-----------------------------------------------------------------------------
// FILE:        PasswordForm.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Confirms the application account password.

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
    /// Summary description for PasswordForm.
    /// </summary>
    internal class PasswordForm : System.Windows.Forms.Form
    {

        private InstallWizard   wizard;
        private string          account;
        private string          password;

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox accountBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox passwordBox;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.ComponentModel.Container components = null;

        public PasswordForm(InstallWizard wizard, string account, string password)
        {
            this.wizard   = wizard;
            this.account  = account;
            this.password = password;

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

        public string Password
        {
            get { return password; }
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.accountBox = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.passwordBox = new System.Windows.Forms.TextBox();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(16, 11);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(48, 16);
            this.label1.TabIndex = 0;
            this.label1.Text = "Account:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // accountBox
            // 
            this.accountBox.Enabled = false;
            this.accountBox.Location = new System.Drawing.Point(72, 8);
            this.accountBox.Name = "accountBox";
            this.accountBox.TabIndex = 1;
            this.accountBox.Text = "";
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(0, 43);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(64, 16);
            this.label2.TabIndex = 2;
            this.label2.Text = "Password:";
            this.label2.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // passwordBox
            // 
            this.passwordBox.Location = new System.Drawing.Point(72, 40);
            this.passwordBox.Name = "passwordBox";
            this.passwordBox.TabIndex = 3;
            this.passwordBox.Text = "";
            // 
            // okButton
            // 
            this.okButton.Location = new System.Drawing.Point(224, 8);
            this.okButton.Name = "okButton";
            this.okButton.TabIndex = 4;
            this.okButton.Text = "OK";
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(224, 40);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.TabIndex = 5;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Location = new System.Drawing.Point(-24, 72);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(352, 100);
            this.groupBox1.TabIndex = 6;
            this.groupBox1.TabStop = false;
            // 
            // label4
            // 
            this.label4.Location = new System.Drawing.Point(32, 48);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(288, 23);
            this.label4.TabIndex = 1;
            this.label4.Text = "Please enter the account password and click OK.";
            // 
            // label3
            // 
            this.label3.Location = new System.Drawing.Point(32, 16);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(288, 32);
            this.label3.TabIndex = 0;
            this.label3.Text = "Setup needs to verify the application\'s database account password.";
            // 
            // PasswordForm
            // 
            this.AcceptButton = this.okButton;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(306, 143);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.passwordBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.accountBox);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PasswordForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Verify the account password";
            this.Load += new System.EventHandler(this.PasswordForm_Load);
            this.groupBox1.ResumeLayout(false);
            this.ResumeLayout(false);

        }
        #endregion

        private void PasswordForm_Load(object sender, System.EventArgs args)
        {
            accountBox.Text          = account;
            passwordBox.Text         = password;
            passwordBox.PasswordChar = Helper.PasswordChar;
        }

        private void okButton_Click(object sender, System.EventArgs args)
        {
            password = passwordBox.Text.Trim();

            // Validate the password by attempting to log into to the server.
            // Note that login SQL Azure works a little differently from 
            // standard SQL Server and it looks like normal accounts are
            // not able to login to the MASTER database.  So we're not
            // going to validate credentials on SQL Azure.

            if (wizard.IsSqlAzure)
            {
                DialogResult = DialogResult.OK;
                return;
            }

            string      conString;
            SqlContext  ctx;
            WaitForm    waitForm;

            wizard.Enabled = false;
            this.Update();

            waitForm = new WaitForm("Verifying account credentials...");
            waitForm.TopLevel = true;
            waitForm.Show();
            waitForm.Update();
            Thread.Sleep(2000);

            conString = string.Format("server={0};database=master;uid={1};pwd={2}", wizard.SetupState["server"], account, password);
            ctx = new SqlContext(conString);
            try
            {
                ctx.Open();
            }
            catch (Exception e)
            {
                wizard.Enabled = true;
                waitForm.Close();
                MessageBox.Show("Cannot validate the account. Check the password.\r\n\r\n" + e.Message,
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
