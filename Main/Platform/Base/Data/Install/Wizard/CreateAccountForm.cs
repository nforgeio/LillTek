//-----------------------------------------------------------------------------
// FILE:        CreateAccountForm.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Handles the creation of a new database account.

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
    /// Handles the creation of a new database account.
    /// </summary>
    internal class CreateAccountForm : System.Windows.Forms.Form
    {
        private InstallWizard   wizard;
        private string          account;
        private string          password;

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox accountBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox passwordBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox confirmBox;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.GroupBox groupBox1;

        private System.ComponentModel.Container components = null;

        public CreateAccountForm(InstallWizard wizard)
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

        public string Account
        {
            get { return account; }
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
            this.label3 = new System.Windows.Forms.Label();
            this.confirmBox = new System.Windows.Forms.TextBox();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(32, 21);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(48, 16);
            this.label1.TabIndex = 0;
            this.label1.Text = "Account:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // accountBox
            // 
            this.accountBox.Location = new System.Drawing.Point(88, 16);
            this.accountBox.Name = "accountBox";
            this.accountBox.Size = new System.Drawing.Size(117, 20);
            this.accountBox.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(16, 51);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(64, 16);
            this.label2.TabIndex = 2;
            this.label2.Text = "Password:";
            this.label2.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // passwordBox
            // 
            this.passwordBox.Location = new System.Drawing.Point(88, 48);
            this.passwordBox.Name = "passwordBox";
            this.passwordBox.Size = new System.Drawing.Size(117, 20);
            this.passwordBox.TabIndex = 3;
            // 
            // label3
            // 
            this.label3.Location = new System.Drawing.Point(32, 83);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(48, 16);
            this.label3.TabIndex = 4;
            this.label3.Text = "Confirm:";
            this.label3.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // confirmBox
            // 
            this.confirmBox.Location = new System.Drawing.Point(88, 80);
            this.confirmBox.Name = "confirmBox";
            this.confirmBox.Size = new System.Drawing.Size(117, 20);
            this.confirmBox.TabIndex = 5;
            // 
            // okButton
            // 
            this.okButton.Location = new System.Drawing.Point(224, 8);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 23);
            this.okButton.TabIndex = 6;
            this.okButton.Text = "OK";
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(224, 40);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 7;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // label4
            // 
            this.label4.Location = new System.Drawing.Point(8, 128);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(296, 48);
            this.label4.TabIndex = 8;
            this.label4.Text = "To create a new database account, enter the new account name, password, and passw" +
    "ord confirmation and then click OK.";
            // 
            // groupBox1
            // 
            this.groupBox1.Location = new System.Drawing.Point(-8, 112);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(328, 100);
            this.groupBox1.TabIndex = 9;
            this.groupBox1.TabStop = false;
            // 
            // CreateAccountForm
            // 
            this.AcceptButton = this.okButton;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(306, 175);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.confirmBox);
            this.Controls.Add(this.passwordBox);
            this.Controls.Add(this.accountBox);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CreateAccountForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Create Database Account";
            this.Load += new System.EventHandler(this.CreateAccountForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion

        private void CreateAccountForm_Load(object sender, System.EventArgs args)
        {
            passwordBox.PasswordChar = Helper.PasswordChar;
            confirmBox.PasswordChar  = Helper.PasswordChar;
        }

        private void okButton_Click(object sender, System.EventArgs args)
        {
            account = accountBox.Text.Trim();
            if (account.Length == 0)
            {
                MessageBox.Show("Please enter an account name.",
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

                accountBox.Focus();
                accountBox.SelectAll();
                return;
            }

            password = passwordBox.Text.Trim();
            if (password != confirmBox.Text.Trim())
            {
                MessageBox.Show("The password and confirmation are not the same.",
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

                passwordBox.Text = string.Empty;
                confirmBox.Text = string.Empty;
                passwordBox.Focus();
                passwordBox.SelectAll();
                return;
            }

            // Create the account

            SqlConnectionInfo   conInfo;
            SqlContext          ctx;
            SqlCommand          cmd;
            WaitForm            waitForm;

            wizard.Enabled = false;
            this.Update();

            waitForm          = new WaitForm("Creating account [" + account + "]...");
            waitForm.TopLevel = true;
            waitForm.Show();
            waitForm.Update();
            Thread.Sleep(2000);

            conInfo = new SqlConnectionInfo((string)wizard.SetupState["connectionString"]);
            conInfo.Database = "master";

            ctx = new SqlContext(conInfo.ToString());
            try
            {
                ctx.Open();

                if (ctx.IsSqlAzure)
                    cmd = ctx.CreateCommand("create login {0} with password='{1}'", account, password);
                else
                {
                    cmd = ctx.CreateSPCall("sp_addlogin");
                    cmd.Parameters.Add("@loginame", SqlDbType.VarChar).Value = account;
                    cmd.Parameters.Add("@passwd", SqlDbType.VarChar).Value = password;
                }

                ctx.Execute(cmd);
            }
            catch (Exception e)
            {
                passwordBox.Text = string.Empty;
                confirmBox.Text  = string.Empty;

                wizard.Enabled = true;
                waitForm.Close();
                MessageBox.Show("Cannot create the new database account.\r\n\r\n" + e.Message,
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
