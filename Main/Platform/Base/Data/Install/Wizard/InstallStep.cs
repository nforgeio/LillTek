//-----------------------------------------------------------------------------
// FILE:        InstallStep.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Handles the actual installation.

using System;
using System.IO;
using System.Data;
using System.Data.SqlClient;
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
    /// Handles the actual installation.
    /// </summary>
    internal class InstallStep : System.Windows.Forms.Form, IWizardStep
    {

        private InstallWizard   wizard;
        private string          conString;
        private bool            error;

        private System.Windows.Forms.RichTextBox infoBox;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.ComponentModel.Container components = null;

        public InstallStep(InstallWizard wizard)
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
            get { return "Installing..."; }
        }

        /// <summary>
        /// Called when the step is activated.
        /// </summary>
        /// <param name="steps">The step list.</param>
        public void OnStepIn(WizardStepList steps)
        {
            wizard.SetButtonMode(InstallWizard.ButtonMode.DisableAll);
            this.Show();
            this.Update();

            DoInstall();
        }

        /// <summary>
        /// Called when the step is deactivated.
        /// </summary>
        /// <param name="steps">The step list.</param>
        /// <param name="forward"><c>true</c> if we're stepping forward in the wizard.</param>
        /// <returns><c>true</c> if the transition can proceed.</returns>
        public bool OnStepOut(WizardStepList steps, bool forward)
        {
            this.Hide();
            wizard.SetButtonMode(InstallWizard.ButtonMode.Normal);
            return true;
        }

        /// <summary>
        /// Returns <c>true</c> if installation failed.
        /// </summary>
        public bool Error
        {
            get { return error; }
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.infoBox = new System.Windows.Forms.RichTextBox();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.SuspendLayout();
            // 
            // infoBox
            // 
            this.infoBox.Font = new System.Drawing.Font("Verdana", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
            this.infoBox.Location = new System.Drawing.Point(0, 0);
            this.infoBox.Name = "infoBox";
            this.infoBox.ReadOnly = true;
            this.infoBox.Size = new System.Drawing.Size(376, 232);
            this.infoBox.TabIndex = 0;
            this.infoBox.Text = "";
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(0, 240);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(376, 16);
            this.progressBar.TabIndex = 1;
            // 
            // InstallStep
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(376, 256);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.infoBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "InstallStep";
            this.Text = "InstallStep";
            this.ResumeLayout(false);

        }
        #endregion

        private Font        normal;
        private Font        bold;
        private Font        boldUnderline;

        private void Append(string text, params object[] args)
        {
            if (args.Length == 0)
                infoBox.AppendText(text + "\r\n");
            else
                infoBox.AppendText(string.Format(text, args) + "\r\n");

            infoBox.Focus();
            infoBox.ScrollToCaret();
            infoBox.Update();
        }

        private void AppendBold(string text, params object[] args)
        {
            infoBox.SelectionFont = bold;

            if (args.Length == 0)
                infoBox.AppendText(text + "\r\n");
            else
                infoBox.AppendText(string.Format(text, args) + "\r\n");

            infoBox.SelectionFont = normal;
            infoBox.Focus();
            infoBox.ScrollToCaret();
            infoBox.Update();
        }

        private void AppendBoldUnderline(string text, params object[] args)
        {
            infoBox.SelectionFont = boldUnderline;

            if (args.Length == 0)
                infoBox.AppendText(text + "\r\n");
            else
                infoBox.AppendText(string.Format(text, args) + "\r\n");

            infoBox.SelectionFont = normal;
            infoBox.Focus();
            infoBox.ScrollToCaret();
            infoBox.Update();
        }

        private void DoInstall()
        {
            string action = (string)wizard.SetupState["action"];

            error = false;
            conString = wizard.SetupState["connectionString"];

            // Initialize the log control

            normal = infoBox.SelectionFont;
            bold = new Font(normal.FontFamily, normal.Size, FontStyle.Bold);
            boldUnderline = new Font(normal.FontFamily, normal.Size, FontStyle.Bold | FontStyle.Underline);

            // Perform the installation

            infoBox.Clear();
            infoBox.AppendText("\r\n");
            infoBox.SelectionAlignment = HorizontalAlignment.Center;
            AppendBold("Configuring [{0}]\r\n", wizard.SetupState["database"]);
            infoBox.SelectionAlignment = HorizontalAlignment.Left;

            switch (action)
            {
                case "Install":

                    error = !Install();
                    break;

                case "Upgrade":

                    error = !Upgrade();
                    break;

                case "GrantOnly":

                    error = !GrantOnly();
                    break;
            }

            infoBox.AppendText("\r\n");
            infoBox.SelectionAlignment = HorizontalAlignment.Center;

            if (error)
            {
                AppendBold("Database configuration failed.");
                wizard.SetButtonMode(InstallWizard.ButtonMode.NoFinish);
            }
            else
            {
                AppendBold("Database configured successfully.");
                wizard.SetButtonMode(InstallWizard.ButtonMode.FinishOnly);
            }

            infoBox.SelectionAlignment = HorizontalAlignment.Left;
            infoBox.AppendText("");
        }

        /// <summary>
        /// Installs the package returning true on success.
        /// </summary>
        private bool Install()
        {

            Package             package     = wizard.Package;
            PackageEntry        schemaFile  = package["/Schema/Schema.sql"];
            PackageEntry        delProcFile = package["/Schema/DeleteProcs.sql"];
            PackageEntry        grantFile   = package["/Schema/GrantAccess.sql"];
            PackageEntry        funcFolder  = package["/Funcs"];
            PackageEntry        procFolder  = package["/Procs"];
            int                 opCount;
            string              script;
            SqlConnection       sqlCon;
            QueryDisposition[]  qd;

            if (schemaFile == null || !schemaFile.IsFile)
            {
                MessageBox.Show("Invalid Database Package: /Schema/Schema.sql missing.",
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (delProcFile == null || !delProcFile.IsFile)
            {
                MessageBox.Show("Invalid Database Package: /Schema/DeleteProcs.sql missing.",
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (grantFile == null || !grantFile.IsFile)
            {
                MessageBox.Show("Invalid Database Package: /Schema/GrantAccess.sql missing.",
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // Count the number of operations we're going to perform and
            // initialize the progress bar.

            opCount = 0;
            opCount++;      // Delete functions and stored procedures
            opCount++;      // Run the schema script
            opCount++;      // Create database user
            opCount++;      // Grant access

            if (funcFolder != null)
                opCount += funcFolder.Children.Length;

            if (procFolder != null)
                opCount += procFolder.Children.Length;

            progressBar.Minimum = 0;
            progressBar.Maximum = opCount;
            progressBar.Step    = 1;

            sqlCon = new SqlConnection(conString);

            try
            {
                sqlCon.Open();
            }
            catch (Exception e)
            {
                Append("Error: " + e.Message);
                return false;
            }

            try
            {

                // Remove the functions and procedures

                Append("Removing functions and procedures");
                script = Helper.FromAnsi(delProcFile.GetContents());
                qd     = new SqlScriptRunner(script).Run(sqlCon, true);

                for (int i = 0; i < qd.Length; i++)
                    if (qd[i].Message != null)
                    {
                        Append(qd[i].Message);
                        return false;
                    }

                progressBar.Value++;

                // Create the schema

                Append("Creating the database schema");

                script = Helper.FromAnsi(schemaFile.GetContents());
                qd     = new SqlScriptRunner(script).Run(sqlCon, true);

                for (int i = 0; i < qd.Length; i++)
                    if (qd[i].Message != null)
                    {
                        Append(qd[i].Message);
                        return false;
                    }

                progressBar.Value++;

                // Add the functions

                if (funcFolder != null)
                {
                    foreach (var file in funcFolder.Children)
                    {
                        if (!file.IsFile)
                            continue;

                        Append("Adding: {0}", file.Name);

                        script = Helper.FromAnsi(file.GetContents());
                        qd     = new SqlScriptRunner(script).Run(sqlCon, true);

                        for (int i = 0; i < qd.Length; i++)
                            if (qd[i].Message != null)
                            {
                                Append(qd[i].Message);
                                return false;
                            }

                        progressBar.Value++;
                    }
                }

                // Add the procedures

                if (procFolder != null)
                {
                    foreach (var file in procFolder.Children)
                    {
                        if (!file.IsFile)
                            continue;

                        Append("Adding: {0}", file.Name);

                        script = Helper.FromAnsi(file.GetContents());
                        qd     = new SqlScriptRunner(script).Run(sqlCon, true);

                        for (int i = 0; i < qd.Length; i++)
                            if (qd[i].Message != null)
                            {

                                Append(qd[i].Message);
                                return false;
                            }

                        progressBar.Value++;
                    }
                }

                // Create a database user for the login if necessary

                SqlContext  ctx = new SqlContext(conString);
                DataTable   dt;

                try
                {
                    ctx.Open();

                    dt = ctx.ExecuteTable(ctx.CreateCommand("select name from sysusers where name='{0}'", wizard.SetupState["account"]));
                    if (dt.Rows.Count == 0)
                    {
                        // The database user doesn't already exist, so create one.

                        Append("Creating database user: {0}", wizard.SetupState["account"]);
                        ctx.Execute(ctx.CreateCommand("create user {0} from login {0}", wizard.SetupState["account"]));
                    }
                }
                catch (Exception e)
                {
                    ctx.Close();

                    Append("Error: " + e.Message);
                    return false;
                }

                progressBar.Value++;

                // Grant access to the application account

                Append("Granting access to: {0}", wizard.SetupState["account"]);

                script = Helper.FromAnsi(grantFile.GetContents());
                script = script.Replace("%account%", wizard.SetupState["account"]);

                qd = new SqlScriptRunner(script).Run(sqlCon, true);

                for (int i = 0; i < qd.Length; i++)
                    if (qd[i].Message != null)
                    {
                        Append(qd[i].Message);
                        return false;
                    }

                progressBar.Value++;
            }
            catch (Exception e)
            {
                Append("Error: " + e.Message);
                return false;
            }
            finally
            {
                sqlCon.Close();
            }

            return true;
        }

        private sealed class UpgradeScript
        {
            public Version  Version;
            public string   Script;

            public UpgradeScript(Version version, string script)
            {

                this.Version = version;
                this.Script = script;
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
        /// Upgrades the database returning true on success.
        /// </summary>
        private bool Upgrade()
        {

            Package                 package       = wizard.Package;
            PackageEntry            schemaFile    = package["/Schema/Schema.sql"];
            PackageEntry            delProcFile   = package["/Schema/DeleteProcs.sql"];
            PackageEntry            grantFile     = package["/Schema/GrantAccess.sql"];
            PackageEntry            funcFolder    = package["/Funcs"];
            PackageEntry            procFolder    = package["/Procs"];
            PackageEntry            upgradeFolder = package["/Upgrade"];
            Version                 curVersion    = new Version(wizard.SetupState["CurSchemaVersion"]);
            UpgradeScript[]         upgradeScripts;
            int                     opCount;
            string                  script;
            SqlConnection           sqlCon;
            QueryDisposition[]      qd;
            int                     pos;

            if (schemaFile == null || !schemaFile.IsFile)
            {
                MessageBox.Show("Invalid Database Package: /Schema/Schema.sql missing.",
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (delProcFile == null || !delProcFile.IsFile)
            {
                MessageBox.Show("Invalid Database Package: /Schema/DeleteProcs.sql missing.",
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (grantFile == null || !grantFile.IsFile)
            {
                MessageBox.Show("Invalid Database Package: /Schema/GrantAccess.sql missing.",
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (upgradeFolder == null || !upgradeFolder.IsFolder || upgradeFolder.Children.Length == 0)
            {
                MessageBox.Show("Invalid Database Package: There are no upgrade scripts.",
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

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

                list.Add(new UpgradeScript(ver, Helper.FromAnsi(file.GetContents())));
            }

            if (list.Count == 0)
            {
                MessageBox.Show("Invalid Database Package: There are no upgrade scripts.",
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            list.Sort(new UpgradeComparer());

            for (pos = 0; pos < list.Count; pos++)
                if (((UpgradeScript)list[pos]).Version > curVersion)
                    break;

            if (pos >= list.Count)
            {
                MessageBox.Show("Invalid Database Package: There are no upgrade scripts.",
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            upgradeScripts = new UpgradeScript[list.Count - pos];
            list.CopyTo(pos, upgradeScripts, 0, upgradeScripts.Length);

            // Count the number of operations we're going to perform and
            // initialize the progress bar.

            opCount = 0;
            opCount++;      // Delete functions and stored procedures
            opCount++;      // Run the schema script
            opCount++;      // Grant access

            opCount += upgradeScripts.Length;

            if (funcFolder != null)
                opCount += funcFolder.Children.Length;

            if (procFolder != null)
                opCount += procFolder.Children.Length;

            progressBar.Minimum = 0;
            progressBar.Maximum = opCount;
            progressBar.Step = 1;

            sqlCon = new SqlConnection(conString);

            try
            {
                sqlCon.Open();
            }
            catch (Exception e)
            {
                Append("Error: " + e.Message);
                return false;
            }

            try
            {

                // Remove the functions and procedures

                Append("Removing functions and procedures");
                script = Helper.FromAnsi(delProcFile.GetContents());
                qd     = new SqlScriptRunner(script).Run(sqlCon, true);

                for (int i = 0; i < qd.Length; i++)
                    if (qd[i].Message != null)
                    {

                        Append(qd[i].Message);
                        return false;
                    }

                progressBar.Value++;

                // Run the update scripts

                foreach (UpgradeScript us in upgradeScripts)
                {
                    script = us.Script;
                    Append("Upgrading schema to version {0}", us.Version);

                    qd = new SqlScriptRunner(script).Run(sqlCon, true);

                    for (int i = 0; i < qd.Length; i++)
                        if (qd[i].Message != null)
                        {

                            Append(qd[i].Message);
                            return false;
                        }

                    progressBar.Value++;
                }

                // Add the functions

                if (funcFolder != null)
                {
                    foreach (PackageEntry file in funcFolder.Children)
                    {
                        if (!file.IsFile)
                            continue;

                        Append("Adding: {0}", file.Name);

                        script = Helper.FromAnsi(file.GetContents());
                        qd     = new SqlScriptRunner(script).Run(sqlCon, true);

                        for (int i = 0; i < qd.Length; i++)
                            if (qd[i].Message != null)
                            {
                                Append(qd[i].Message);
                                return false;
                            }

                        progressBar.Value++;
                    }
                }

                // Add the procedures

                if (procFolder != null)
                {
                    foreach (var file in procFolder.Children)
                    {
                        if (!file.IsFile)
                            continue;

                        Append("Adding: {0}", file.Name);

                        script = Helper.FromAnsi(file.GetContents());
                        qd     = new SqlScriptRunner(script).Run(sqlCon, true);

                        for (int i = 0; i < qd.Length; i++)
                            if (qd[i].Message != null)
                            {
                                Append(qd[i].Message);
                                return false;
                            }

                        progressBar.Value++;
                    }
                }

                // Grant access to the application account

                Append("Granting access to: {0}", wizard.SetupState["account"]);

                script = Helper.FromAnsi(grantFile.GetContents());
                script = script.Replace("%account%", wizard.SetupState["account"]);

                qd = new SqlScriptRunner(script).Run(sqlCon, true);

                for (int i = 0; i < qd.Length; i++)
                    if (qd[i].Message != null)
                    {
                        Append(qd[i].Message);
                        return false;
                    }

                progressBar.Value++;
            }
            catch (Exception e)
            {
                Append("Error: " + e.Message);
                return false;
            }
            finally
            {
                sqlCon.Close();
            }

            return true;
        }

        /// <summary>
        /// Grants access to the database returning true on success.
        /// </summary>
        private bool GrantOnly()
        {

            Package             package     = wizard.Package;
            PackageEntry        schemaFile  = package["/Schema/Schema.sql"];
            PackageEntry        delProcFile = package["/Schema/DeleteProcs.sql"];
            PackageEntry        grantFile   = package["/Schema/GrantAccess.sql"];
            PackageEntry        funcFolder  = package["/Funcs"];
            PackageEntry        procFolder  = package["/Procs"];
            int                 opCount;
            string              script;
            SqlConnection       sqlCon;
            QueryDisposition[]  qd;

            if (schemaFile == null || !schemaFile.IsFile)
            {
                MessageBox.Show("Invalid Database Package: /Schema/Schema.sql missing.",
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (delProcFile == null || !delProcFile.IsFile)
            {
                MessageBox.Show("Invalid Database Package: /Schema/DeleteProcs.sql missing.",
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (grantFile == null || !grantFile.IsFile)
            {
                MessageBox.Show("Invalid Database Package: /Schema/GrantAccess.sql missing.",
                                wizard.SetupTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // Count the number of operations we're going to perform and
            // initialize the progress bar.

            opCount = 0;
            opCount++;      // Grant access

            progressBar.Minimum = 0;
            progressBar.Maximum = opCount;
            progressBar.Step    = 1;

            sqlCon = new SqlConnection(conString);

            try
            {
                sqlCon.Open();
            }
            catch (Exception e)
            {
                Append("Error: " + e.Message);
                return false;
            }

            try
            {
                // Grant access to the application account

                Append("Granting access to: {0}", wizard.SetupState["account"]);

                script = Helper.FromAnsi(grantFile.GetContents());
                script = script.Replace("%account%", wizard.SetupState["account"]);

                qd = new SqlScriptRunner(script).Run(sqlCon, true);

                for (int i = 0; i < qd.Length; i++)
                    if (qd[i].Message != null)
                    {
                        Append(qd[i].Message);
                        return false;
                    }

                progressBar.Value++;
            }
            catch (Exception e)
            {
                Append("Error: " + e.Message);
                return false;
            }
            finally
            {
                sqlCon.Close();
            }

            Append("\r\n\r\n\r\n\r\n");
            return true;
        }
    }
}
