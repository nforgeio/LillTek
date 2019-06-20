//-----------------------------------------------------------------------------
// FILE:        StartForm.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Handles the starting of a Windows service.

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.ServiceProcess;

namespace LillTek.Tools.InstallHelper
{
    /// <summary>
    /// Summary description for StartForm.
    /// </summary>
    internal class StartForm : System.Windows.Forms.Form
    {
        private static string   service;
        private static bool     starting;
        private DateTime        closeTime;

        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.Timer Timer;
        private System.Windows.Forms.Label Message;


        /// <summary>
        /// Shows the form and starts the named services.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        public static void Show(string serviceName)
        {
            service  = serviceName;
            starting = false;

            Application.Run(new StartForm());
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public StartForm()
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
            this.components = new System.ComponentModel.Container();
            this.Message = new System.Windows.Forms.Label();
            this.Timer = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // Message
            // 
            this.Message.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Message.Location = new System.Drawing.Point(8, 0);
            this.Message.Name = "Message";
            this.Message.Size = new System.Drawing.Size(280, 56);
            this.Message.TabIndex = 0;
            this.Message.Text = "Starting: {0}...";
            this.Message.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // Timer
            // 
            this.Timer.Interval = 60000;
            this.Timer.Tick += new System.EventHandler(this.Timer_Tick);
            // 
            // StartForm
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(292, 53);
            this.Controls.Add(this.Message);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "StartForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Load += new System.EventHandler(this.StartForm_Load);
            this.ResumeLayout(false);

        }
        #endregion

        private void StartForm_Load(object sender, System.EventArgs args)
        {
            ServiceController           controller;
            ServiceControllerStatus     status;

            this.Text = Program.Title;

            try
            {
                controller   = new ServiceController(service);
                status       = controller.Status;   // Forces initialization of the display name
                starting     = true;
                Message.Text = string.Format(this.Message.Text, controller.DisplayName);
            }
            catch
            {
                this.Message.Text = string.Format(this.Message.Text, service);
                MessageBox.Show(string.Format("Cannot start: {0}.", service), Program.Title);

                starting  = false;
                closeTime = DateTime.UtcNow;
            }

            Timer.Interval = 100;
            Timer.Start();

            Activate();
            this.Update();
        }

        private void Timer_Tick(object sender, System.EventArgs args)
        {
            if (starting)
            {
                ServiceController       controller;
                ServiceControllerStatus status;

                try
                {
                    controller = new ServiceController(service);
                    status     = controller.Status;
                    starting   = false;

                    if (status != ServiceControllerStatus.Running)
                        controller.Start();
                }
                catch
                {
                    this.Message.Text = string.Format(this.Message.Text, service);
                    MessageBox.Show(string.Format("Cannot start: {0}.", service), Program.Title);
                }

                closeTime = DateTime.UtcNow + TimeSpan.FromSeconds(2.0);
                return;
            }

            if (DateTime.UtcNow >= closeTime)
                Close();
        }
    }
}
