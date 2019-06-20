//-----------------------------------------------------------------------------
// FILE:        ActionRequestStep.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Shows the user what needs to be done to the database
//              (setup or upgrade) and asks whether the install should
//              proceed.

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
    /// Shows the user what needs to be done to the database (setup or upgrade)
    /// and asks whether the install should proceed.
    /// </summary>
    internal class ActionRequestStep : System.Windows.Forms.Form, IWizardStep
    {
        private InstallWizard    wizard;

        private System.Windows.Forms.RichTextBox infoBox;
        private System.ComponentModel.Container components = null;

        public ActionRequestStep(InstallWizard wizard)
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
            get { return "Ready"; }
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
            this.infoBox = new System.Windows.Forms.RichTextBox();
            this.SuspendLayout();
            // 
            // infoBox
            // 
            this.infoBox.Font = new System.Drawing.Font("Verdana", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
            this.infoBox.Location = new System.Drawing.Point(0, 0);
            this.infoBox.Name = "infoBox";
            this.infoBox.ReadOnly = true;
            this.infoBox.ShowSelectionMargin = true;
            this.infoBox.Size = new System.Drawing.Size(376, 256);
            this.infoBox.TabIndex = 0;
            this.infoBox.Text = "";
            // 
            // ActionRequestStep
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(376, 256);
            this.Controls.Add(this.infoBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "ActionRequestStep";
            this.Text = "ActionRequestStep";
            this.Load += new System.EventHandler(this.ActionRequestStep_Load);
            this.ResumeLayout(false);

        }
        #endregion

        private Font normal;
        private Font bold;
        private Font boldUnderline;

        private void Append(string text, params object[] args)
        {
            if (args.Length == 0)
                infoBox.AppendText(text);
            else
                infoBox.AppendText(string.Format(text, args));
        }

        private void AppendBold(string text, params object[] args)
        {
            infoBox.SelectionFont = bold;

            if (args.Length == 0)
                infoBox.AppendText(text);
            else
                infoBox.AppendText(string.Format(text, args));

            infoBox.SelectionFont = normal;
        }

        private void AppendBoldUnderline(string text, params object[] args)
        {
            infoBox.SelectionFont = boldUnderline;

            if (args.Length == 0)
                infoBox.AppendText(text);
            else
                infoBox.AppendText(string.Format(text, args));

            infoBox.SelectionFont = normal;
        }

        private void ActionRequestStep_Load(object sender, System.EventArgs args)
        {
            normal        = infoBox.SelectionFont;
            bold          = new Font(normal.FontFamily, normal.Size, FontStyle.Bold);
            boldUnderline = new Font(normal.FontFamily, normal.Size, FontStyle.Bold | FontStyle.Underline);

            // Generate the RTF describing what the installation is going do.

            infoBox.AppendText("\r\n\r\n\r\n\r\n");
            infoBox.SelectionAlignment = HorizontalAlignment.Center;
            AppendBoldUnderline("Ready to begin database configuration\r\n\r\n");

            infoBox.SelectionAlignment = HorizontalAlignment.Left;
            Append("Server: ");
            AppendBold("[{0}]\r\n\r\n", (string)wizard.SetupState["server"]);

            if (wizard.SetupState["Action"] == "Install")
            {
                Append("The database ");
                AppendBold("[{0}]", (string)wizard.SetupState["database"]);
                Append(" is currently empty. Setup will configure a new ");
                AppendBold("[{0}]", (string)wizard.SetupState["productName"]);
                Append(" database and then grant the application account ");
                AppendBold("[{0}]", (string)wizard.SetupState["account"]);
                Append(" access to it.");
            }
            else if (wizard.SetupState["Action"] == "Upgrade")
            {
                var curSchemaVersion = (string)wizard.SetupState["CurSchemaVersion"];
                var curVer           = new Version(curSchemaVersion);
                var setupVer         = new Version((string)wizard.SetupState["schemaVersion"]);

                if (curVer < setupVer)
                {

                    Append("The database ");
                    AppendBold("[{0}]", (string)wizard.SetupState["database"]);
                    Append(" is already configured as a ");
                    AppendBold("[{0}]", (string)wizard.SetupState["productName"]);
                    Append(" database.  Setup will upgade the database from schema version ");
                    AppendBold("[{0}]", Helper.GetVersionString(curVer));
                    Append(" to ");
                    AppendBold("[{0}]", Helper.GetVersionString(setupVer));
                    Append(" and then grant the application account ");
                    AppendBold("[{0}]", (string)wizard.SetupState["account"]);
                    Append(" access to it.  No information will be lost during the upgrade.");
                }
                else if (curVer == setupVer)
                {
                    wizard.SetupState["Action"] = "GrantOnly";

                    Append("The database ");
                    AppendBold("[{0}]", (string)wizard.SetupState["database"]);
                    Append(" is already configured and is up to date as a ");
                    AppendBold("[{0}]", (string)wizard.SetupState["productName"]);
                    Append(" database.  Setup will grant the application account ");
                    AppendBold("[{0}]", (string)wizard.SetupState["account"]);
                    Append(" access to it.");
                }
                else
                {
                    wizard.SetupState["Action"] = "GrantOnly";

                    Append("The database ");
                    AppendBold("[{0}]", (string)wizard.SetupState["database"]);
                    Append(" appears to have been configured by a more recent version of ");
                    AppendBold("[{0}]", (string)wizard.SetupState["productName"]);
                    Append(" setup.  Setup will grant the application account ");
                    AppendBold("[{0}]", (string)wizard.SetupState["account"]);
                    Append(" access to it.");
                }
            }
            else
            {
                MessageBox.Show("Unexpected setup action: [" + (string)wizard.SetupState["Action"] + "]");
            }

            Append("\r\n\r\nClick ");
            AppendBold("Next");
            Append(" to proceed with the database installation.");
        }
    }
}
