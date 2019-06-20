//-----------------------------------------------------------------------------
// FILE:        UsageForm.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Displays usage information for the tool.

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace LillTek.Tools.InstallHelper
{
    /// <summary>
    /// Summary description for UsageForm.
    /// </summary>
    internal class UsageForm : System.Windows.Forms.Form
    {
        private System.ComponentModel.Container     components = null;
        private System.Windows.Forms.Button         OKButton;

        /// <summary>
        /// Displays the form.
        /// </summary>
        public static new void Show()
        {
            Application.Run(new UsageForm());
        }

        private System.Windows.Forms.RichTextBox Message;

        /// <summary>
        /// Constructor;
        /// </summary>
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
            this.OKButton = new System.Windows.Forms.Button();
            this.Message = new System.Windows.Forms.RichTextBox();
            this.SuspendLayout();
            // 
            // OKButton
            // 
            this.OKButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.OKButton.Location = new System.Drawing.Point(168, 248);
            this.OKButton.Name = "OKButton";
            this.OKButton.TabIndex = 1;
            this.OKButton.Text = "OK";
            this.OKButton.Click += new System.EventHandler(this.OKButton_Click);
            // 
            // Message
            // 
            this.Message.Location = new System.Drawing.Point(8, 8);
            this.Message.Name = "Message";
            this.Message.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.Message.Size = new System.Drawing.Size(400, 232);
            this.Message.TabIndex = 2;
            this.Message.Text = "";
            // 
            // UsageForm
            // 
            this.AcceptButton = this.OKButton;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.CancelButton = this.OKButton;
            this.ClientSize = new System.Drawing.Size(418, 279);
            this.Controls.Add(this.Message);
            this.Controls.Add(this.OKButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "UsageForm";
            this.Text = "Install Helper";
            this.Load += new System.EventHandler(this.UsageForm_Load);
            this.ResumeLayout(false);

        }
        #endregion

        private void UsageForm_Load(object sender, System.EventArgs args)
        {
            this.Text    = Program.Name;
            Message.Text =

@"Install Helper Usage:

    InstallHelper.exe [-wait:<processID>] [-start:<service>]

where:

    -wait:<processID> 

    specifies that the tool should wait for the process whose
    ID is passed as an unsigned decimal integer to terminate 
    before executing any other commands.

    -start:<service>

    starts the named Windows service.
";
        }

        private void OKButton_Click(object sender, System.EventArgs args)
        {
            this.Close();
        }
    }
}
