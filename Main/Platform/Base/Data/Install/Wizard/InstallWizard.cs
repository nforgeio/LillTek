//-----------------------------------------------------------------------------
// FILE:        InstallWizard.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the database package installation user interface.

using System;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Forms;

using LillTek.Common;
using LillTek.Install;
using LillTek.Data;

namespace LillTek.Data.Install
{
    /// <summary>
    /// Implements the database package installation user interface.
    /// </summary>
    internal class InstallWizard : System.Windows.Forms.Form, IWizardStep
    {
        //---------------------------------------------------------------------
        // Static members

        private static DBInstallResult result;

        /// <summary>
        /// Runs the installation wizard.
        /// </summary>
        /// <param name="installer">The package installer.</param>
        /// <returns>A DBInstallResult indicating what happened.</returns>
        public static DBInstallResult Install(DBPackageInstaller installer)
        {
            result = DBInstallResult.Unknown;
            Application.Run(new InstallWizard(installer));
            return result;
        }

        //---------------------------------------------------------------------
        // Instance members

        public enum ButtonMode
        {
            Normal,
            DisableAll,
            FinishOnly,
            NoFinish,
        };

        private DBPackageInstaller      installer;
        private WizardStepList          steps;
        private bool                    closePromptedAlready = false;
        private StringDictionary        setupState;
        private ButtonMode              buttonMode = ButtonMode.Normal;

        private ServerAdminStep         serverAdminStep;
        private SelectDatabaseStep      selectDatabaseStep;
        private ServiceAccountStep      appAccountStep;
        private ActionRequestStep       actionRequestStep;
        private InstallStep             installStep;

        private System.Windows.Forms.Button nextButton;
        private System.Windows.Forms.Button backButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RichTextBox welcomeBox;
        private System.ComponentModel.Container components = null;

        /// <summary>
        /// Constructs the install wizard user interface.
        /// </summary>
        /// <param name="installer">The package installer.</param>
        public InstallWizard(DBPackageInstaller installer)
        {
            this.installer               = installer;
            this.setupState              = new StringDictionary();

            setupState["server"]         = installer.Server;
            setupState["database"]       = installer.Database;
            setupState["account"]        = installer.Account;
            setupState["password"]       = installer.Password;

            setupState["setupTitle"]     = installer.setupTitle;
            setupState["productName"]    = installer.productName;
            setupState["productID"]      = installer.productID;
            setupState["productVersion"] = installer.productVersion.ToString();
            setupState["databaseType"]   = installer.databaseType;
            setupState["schemaVersion"]  = installer.schemaVersion.ToString();

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
        /// Indicates whether the SQL database is hosted on Azure.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This is initialized in the <b>ServerAdminStep</b>.
        /// </note>
        /// </remarks>
        public bool IsSqlAzure { get; set; }

        /// <summary>
        /// The installation install result.
        /// </summary>
        public DBInstallResult Result
        {
            get { return result; }
            set { result = value; }
        }

        /// <summary>
        /// Returns the collection of wizard steps.
        /// </summary>
        public WizardStepList Steps
        {
            get { return steps; }
        }

        /// <summary>
        /// Returns the database install package.
        /// </summary>
        public DBPackageInstaller Installer
        {
            get { return installer; }
        }

        /// <summary>
        /// Returns the actual package containing the 
        /// database scripts.
        /// </summary>
        public Package Package
        {
            get { return installer.package; }
        }

        /// <summary>
        /// Returns a dictionary to be used for holding install
        /// state between wizard steps. 
        /// </summary>
        public StringDictionary SetupState
        {
            get { return setupState; }
        }

        /// <summary>
        /// Returns the title of the step.
        /// </summary>
        public string Title
        {
            get { return "Welcome"; }
        }

        /// <summary>
        /// Called when the step is activated.
        /// </summary>
        /// <param name="steps">The step list.</param>
        public void OnStepIn(WizardStepList steps)
        {
            welcomeBox.Show();
        }

        /// <summary>
        /// Called when the step is deactivated.
        /// </summary>
        /// <param name="steps">The step list.</param>
        /// <param name="forward"><c>true</c> if we're stepping forward in the wizard.</param>
        /// <returns><c>true</c> if the transition can proceed.</returns>
        public bool OnStepOut(WizardStepList steps, bool forward)
        {
            welcomeBox.Hide();
            return true;
        }

        /// <summary>
        /// Sets the focus to the Next button.
        /// </summary>
        public void SetFocusToNext()
        {
            nextButton.Focus();
        }

        /// <summary>
        /// Called whenever the current step has changed.
        /// </summary>
        /// <param name="steps">The step list.</param>
        /// <param name="step">The new current step.</param>
        public void OnStep(WizardStepList steps, IWizardStep step)
        {
            switch (buttonMode)
            {
                case ButtonMode.Normal:

                    cancelButton.Enabled = true;
                    backButton.Enabled = step != steps.FirstStep;
                    nextButton.Enabled = true;
                    break;

                case ButtonMode.DisableAll:

                    cancelButton.Enabled = false;
                    backButton.Enabled = false;
                    nextButton.Enabled = false;
                    break;

                case ButtonMode.FinishOnly:

                    cancelButton.Enabled = false;
                    backButton.Enabled = false;
                    nextButton.Enabled = true;
                    break;

                case ButtonMode.NoFinish:

                    cancelButton.Enabled = true;
                    backButton.Enabled = step != steps.FirstStep;
                    nextButton.Enabled = false;
                    break;
            }

            if (step != steps.LastStep)
                nextButton.Text = "Next >";
            else
                nextButton.Text = "Finish";

            this.Text = installer.setupTitle + ": " + step.Title;
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.nextButton = new System.Windows.Forms.Button();
            this.backButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.welcomeBox = new System.Windows.Forms.RichTextBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // nextButton
            // 
            this.nextButton.Location = new System.Drawing.Point(304, 16);
            this.nextButton.Name = "nextButton";
            this.nextButton.Size = new System.Drawing.Size(88, 23);
            this.nextButton.TabIndex = 1;
            this.nextButton.Text = "Next >";
            this.nextButton.Click += new System.EventHandler(this.nextButton_Click);
            // 
            // backButton
            // 
            this.backButton.Location = new System.Drawing.Point(208, 16);
            this.backButton.Name = "backButton";
            this.backButton.Size = new System.Drawing.Size(88, 23);
            this.backButton.TabIndex = 3;
            this.backButton.Text = "< Back";
            this.backButton.Click += new System.EventHandler(this.backButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(112, 16);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(88, 23);
            this.cancelButton.TabIndex = 2;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.cancelButton);
            this.groupBox1.Controls.Add(this.nextButton);
            this.groupBox1.Controls.Add(this.backButton);
            this.groupBox1.Location = new System.Drawing.Point(-8, 272);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(544, 100);
            this.groupBox1.TabIndex = 3;
            this.groupBox1.TabStop = false;
            // 
            // welcomeBox
            // 
            this.welcomeBox.AccessibleRole = System.Windows.Forms.AccessibleRole.MenuBar;
            this.welcomeBox.Location = new System.Drawing.Point(8, 8);
            this.welcomeBox.Name = "welcomeBox";
            this.welcomeBox.ReadOnly = true;
            this.welcomeBox.ShowSelectionMargin = true;
            this.welcomeBox.Size = new System.Drawing.Size(376, 256);
            this.welcomeBox.TabIndex = 4;
            this.welcomeBox.Text = "";
            // 
            // InstallWizard
            // 
            this.AcceptButton = this.nextButton;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(394, 319);
            this.Controls.Add(this.welcomeBox);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "InstallWizard";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "InstallWizard";
            this.Closing += new System.ComponentModel.CancelEventHandler(this.InstallWizard_Closing);
            this.Load += new System.EventHandler(this.InstallWizard_Load);
            this.groupBox1.ResumeLayout(false);
            this.ResumeLayout(false);

        }
        #endregion

        public string SetupTitle
        {
            get { return (string)SetupState["setupTitle"]; }
        }

        public void SetButtonMode(ButtonMode mode)
        {
            buttonMode = mode;
        }

        private void Center(Form form)
        {
            // Center the form over the welcome box.

            int cx, cy;

            cx = welcomeBox.Left + welcomeBox.Width / 2;
            cy = welcomeBox.Top + welcomeBox.Height / 2;

            form.Left = cx - form.Width / 2;
            form.Top = cy - form.Height / 2;
        }

        private void AddStep(Form form)
        {
            form.TopLevel = false;
            form.Hide();
            Controls.Add(form);
            Center(form);
            steps.Add(form as IWizardStep);
        }

        private void InstallWizard_Load(object sender, System.EventArgs args)
        {
            this.TopMost = true;
            this.Activate();
            this.TopMost = false;

            welcomeBox.Rtf = Helper.ToRtf(installer.package["/Welcome.rtf"].GetContents());
            welcomeBox.Hide();

            // Initialize the step list

            steps = new WizardStepList(this);
            steps.Add(this);

            serverAdminStep = new ServerAdminStep(this);
            AddStep(serverAdminStep);

            selectDatabaseStep = new SelectDatabaseStep(this);
            AddStep(selectDatabaseStep);

            appAccountStep = new ServiceAccountStep(this);
            AddStep(appAccountStep);

            actionRequestStep = new ActionRequestStep(this);
            AddStep(actionRequestStep);

            installStep = new InstallStep(this);
            AddStep(installStep);

            steps.StepTo(steps.FirstStep, true);
        }

        private bool PromptClose()
        {
            DialogResult dr;

            if (closePromptedAlready)
                return true;

            dr = MessageBox.Show("Are you sure you want to cancel database setup?", installer.setupTitle,
                                 MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);

            if (dr == DialogResult.Yes)
            {
                closePromptedAlready = true;
                result = DBInstallResult.Cancelled;
            }

            return closePromptedAlready;
        }

        private void cancelButton_Click(object sender, System.EventArgs args)
        {
            if (PromptClose())
                Close();
        }

        private void backButton_Click(object sender, System.EventArgs args)
        {
            steps.StepBack();
        }

        private void nextButton_Click(object sender, System.EventArgs args)
        {
            if (steps.Current == steps.LastStep)
            {

                // Wizard completed successfully.

                closePromptedAlready = true;
                result = DBInstallResult.Installed;

                installer.server = (string)setupState["server"];
                installer.database = (string)setupState["database"];
                installer.account = (string)setupState["account"];
                installer.password = (string)setupState["password"];

                Close();
                return;
            }

            steps.StepNext();
        }

        private void InstallWizard_Closing(object sender, System.ComponentModel.CancelEventArgs args)
        {
            if (!PromptClose())
                args.Cancel = true;
        }
    }
}
