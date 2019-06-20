//-----------------------------------------------------------------------------
// FILE:        UsageForm.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the DBPackage's administration form.

using System;
using System.Drawing;
using System.Collections;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows.Forms;

namespace LillTek.Tools.DBPackage
{
    /// <summary>
    /// Displays application usage information.
    /// </summary>
    internal class UsageForm : System.Windows.Forms.Form
    {
        //---------------------------------------------------------------------
        // Static members

        public static void Display()
        {
            Application.Run(new UsageForm());
        }

        //---------------------------------------------------------------------
        // Instance members

        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.RichTextBox usageText;
        private System.ComponentModel.Container components = null;

        public UsageForm()
        {
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

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.okButton = new System.Windows.Forms.Button();
            this.usageText = new System.Windows.Forms.RichTextBox();
            this.SuspendLayout();
            // 
            // okButton
            // 
            this.okButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.okButton.Location = new System.Drawing.Point(248, 424);
            this.okButton.Name = "okButton";
            this.okButton.TabIndex = 0;
            this.okButton.Text = "OK";
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // usageText
            // 
            this.usageText.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
            this.usageText.Location = new System.Drawing.Point(8, 8);
            this.usageText.Name = "usageText";
            this.usageText.ReadOnly = true;
            this.usageText.Size = new System.Drawing.Size(560, 408);
            this.usageText.TabIndex = 1;
            this.usageText.Text = "usageText";
            // 
            // UsageForm
            // 
            this.AcceptButton = this.okButton;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.CancelButton = this.okButton;
            this.ClientSize = new System.Drawing.Size(578, 455);
            this.Controls.Add(this.usageText);
            this.Controls.Add(this.okButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "UsageForm";
            this.Text = "DBPackage Usage";
            this.Load += new System.EventHandler(this.UsageForm_Load);
            this.ResumeLayout(false);

        }
        #endregion

        private void UsageForm_Load(object sender, System.EventArgs args)
        {
            usageText.Text =

@"The DBPackage tool provides a way to create and install database packages.

A database package is an archive holding files describing the
database schema creation, schema upgrade, function and stored
procedure scripts.  Database packages provide a clean and unified
way of installing and upgrading databases.

This application has two basic mode: create mode and install mode.
In create mode, the tool creates a database package.  Install mode
is used to apply schema packages to a database during application
install.

The application mode is selected by passing either the -create
or -install option on the command line.  If neither are present
then -create will be assumed.  Each mode defines additional
command line parameters.

Command Line Option     Description
---------------------------------------------------------------
-create                 Puts the tool into Create mode
-install:[package]      Puts the tool into Install mode,
                        installing the database package whose
                        file path is specified.
                        
Create Options:
---------------

-setup:[file]           Names the file holding the database
                        package setup.ini information.
                        
-welcome:[RTF file]     Names the RTF file with the welcome
                        text (optional).

-upgrade:[directory]    Specifies the directory holding the SQL
                        database schema upgrade scripts.

-schema:[directory]     Specifies the directory holding the
                        schema related scripts.

-funcs:[directory]      Specifies the directory holding the
                        database function creation scripts.

-procs:[directory]      Specifies the directory holding the
                        database stored procedure creation
                        scripts.
                        
-out:[package]          Specifies where the new package should
                        be saved.

Install Options
---------------

-config:[file]          Names the configuration file to be updated
                        with the new database connection string
                        
-setting:[key]          The fully qualified configuration key for
                        the database connection string.

-defdb:[name]           The default database name to use if not
                        already specified in the configuration
                        file.
";
        }

        private void okButton_Click(object sender, System.EventArgs args)
        {
            Close();
        }
    }
}
