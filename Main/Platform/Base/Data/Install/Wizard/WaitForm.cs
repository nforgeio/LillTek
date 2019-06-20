//-----------------------------------------------------------------------------
// FILE:        WaitForm.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A modeless dialog used to display progress while a
//              backgound task is being performed.

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
    /// A modeless dialog used to display progress while a backgound 
    /// task is being performed.
    /// </summary>
    internal class WaitForm : System.Windows.Forms.Form
    {
        private string      message;

        private System.Windows.Forms.Label waitMessage;
        private System.ComponentModel.Container components = null;

        public WaitForm(string message)
        {
            this.message = message;
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
            this.waitMessage = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // waitMessage
            // 
            this.waitMessage.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.waitMessage.Location = new System.Drawing.Point(8, 9);
            this.waitMessage.Name = "waitMessage";
            this.waitMessage.Size = new System.Drawing.Size(240, 53);
            this.waitMessage.TabIndex = 0;
            this.waitMessage.Text = "Verifying database credentials...";
            this.waitMessage.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // WaitForm
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(258, 71);
            this.Controls.Add(this.waitMessage);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "WaitForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Please wait a moment";
            this.Load += new System.EventHandler(this.WaitForm_Load);
            this.ResumeLayout(false);

        }
        #endregion

        private void WaitForm_Load(object sender, System.EventArgs args)
        {
            waitMessage.Text = message;
        }
    }
}
