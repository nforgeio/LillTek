//-----------------------------------------------------------------------------
// FILE:        FormServiceHost.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Hosts a service in a Windows Forms application.

using System;
using System.Drawing;
using System.Threading;
using System.ServiceProcess;
using System.Windows.Forms;

using LillTek.Client;
using LillTek.Common;

namespace LillTek.Service
{
    /// <summary>
    /// Hosts a service in a Windows Forms application.
    /// </summary>
    public class FormServiceHost : System.Windows.Forms.Form, IServiceHost
    {
        private delegate void StringParamDelegate(string s);
        private delegate void UITimerDelegate(ServiceState state, string status);

        private ISysLogProvider         logProvider;
        private string[]                args;
        private bool                    autoStart;
        private IService                service;
        private StringParamDelegate     onStatus;
        private StringParamDelegate     onLog;
        private int                     rightBorder;
        private int                     bottomBorder;
        private GatedTimer              timer;
        private UITimerDelegate         onUITimer;
        private ConsoleUI               console;

        private System.Windows.Forms.Button startButton;
        private System.Windows.Forms.Button stopButton;
        private System.Windows.Forms.StatusBar statusBar;
        private System.Windows.Forms.CheckBox freezeCheck;
        private System.Windows.Forms.Button clearButton;
        private System.ComponentModel.Container components = null;
        private System.Windows.Forms.Button shutDownButton;
        private System.Windows.Forms.Button reconfigButton;
        private System.Windows.Forms.TextBox displayStatus;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Splitter splitter1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.RichTextBox logBox;

        /// <summary>
        /// Constructs a Windows Form based service host instance.
        /// </summary>
        public FormServiceHost()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormServiceHost));
            this.startButton = new System.Windows.Forms.Button();
            this.stopButton = new System.Windows.Forms.Button();
            this.statusBar = new System.Windows.Forms.StatusBar();
            this.freezeCheck = new System.Windows.Forms.CheckBox();
            this.clearButton = new System.Windows.Forms.Button();
            this.displayStatus = new System.Windows.Forms.TextBox();
            this.shutDownButton = new System.Windows.Forms.Button();
            this.reconfigButton = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.logBox = new System.Windows.Forms.RichTextBox();
            this.splitter1 = new System.Windows.Forms.Splitter();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // startButton
            // 
            this.startButton.Location = new System.Drawing.Point(8, 8);
            this.startButton.Name = "startButton";
            this.startButton.Size = new System.Drawing.Size(75, 23);
            this.startButton.TabIndex = 0;
            this.startButton.Text = "Start";
            this.startButton.Click += new System.EventHandler(this.startButton_Click);
            // 
            // stopButton
            // 
            this.stopButton.Location = new System.Drawing.Point(168, 8);
            this.stopButton.Name = "stopButton";
            this.stopButton.Size = new System.Drawing.Size(75, 23);
            this.stopButton.TabIndex = 2;
            this.stopButton.Text = "Stop";
            this.stopButton.Click += new System.EventHandler(this.stopButton_Click);
            // 
            // statusBar
            // 
            this.statusBar.Location = new System.Drawing.Point(0, 333);
            this.statusBar.Name = "statusBar";
            this.statusBar.Size = new System.Drawing.Size(492, 22);
            this.statusBar.TabIndex = 8;
            // 
            // freezeCheck
            // 
            this.freezeCheck.Location = new System.Drawing.Point(416, 8);
            this.freezeCheck.Name = "freezeCheck";
            this.freezeCheck.Size = new System.Drawing.Size(72, 24);
            this.freezeCheck.TabIndex = 5;
            this.freezeCheck.Text = "Freeze";
            this.freezeCheck.CheckedChanged += new System.EventHandler(this.freezeCheck_CheckedChanged);
            // 
            // clearButton
            // 
            this.clearButton.Location = new System.Drawing.Point(328, 8);
            this.clearButton.Name = "clearButton";
            this.clearButton.Size = new System.Drawing.Size(75, 23);
            this.clearButton.TabIndex = 4;
            this.clearButton.Text = "Clear";
            this.clearButton.Click += new System.EventHandler(this.clearButton_Click);
            // 
            // displayStatus
            // 
            this.displayStatus.Dock = System.Windows.Forms.DockStyle.Top;
            this.displayStatus.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.displayStatus.Location = new System.Drawing.Point(0, 0);
            this.displayStatus.Multiline = true;
            this.displayStatus.Name = "displayStatus";
            this.displayStatus.Size = new System.Drawing.Size(478, 94);
            this.displayStatus.TabIndex = 6;
            // 
            // shutDownButton
            // 
            this.shutDownButton.Location = new System.Drawing.Point(88, 8);
            this.shutDownButton.Name = "shutDownButton";
            this.shutDownButton.Size = new System.Drawing.Size(75, 23);
            this.shutDownButton.TabIndex = 1;
            this.shutDownButton.Text = "Shutdown";
            this.shutDownButton.Click += new System.EventHandler(this.shutDownButton_Click);
            // 
            // reconfigButton
            // 
            this.reconfigButton.Location = new System.Drawing.Point(248, 8);
            this.reconfigButton.Name = "reconfigButton";
            this.reconfigButton.Size = new System.Drawing.Size(75, 23);
            this.reconfigButton.TabIndex = 3;
            this.reconfigButton.Text = "Reconfig";
            this.reconfigButton.Click += new System.EventHandler(this.reconfigButton_Click);
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.panel2);
            this.panel1.Controls.Add(this.splitter1);
            this.panel1.Controls.Add(this.displayStatus);
            this.panel1.Location = new System.Drawing.Point(8, 40);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(478, 290);
            this.panel1.TabIndex = 9;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.logBox);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(0, 97);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(478, 193);
            this.panel2.TabIndex = 8;
            // 
            // logBox
            // 
            this.logBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logBox.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.logBox.Location = new System.Drawing.Point(0, 0);
            this.logBox.Name = "logBox";
            this.logBox.ReadOnly = true;
            this.logBox.Size = new System.Drawing.Size(478, 193);
            this.logBox.TabIndex = 0;
            this.logBox.Text = "";
            // 
            // splitter1
            // 
            this.splitter1.Dock = System.Windows.Forms.DockStyle.Top;
            this.splitter1.Location = new System.Drawing.Point(0, 94);
            this.splitter1.Name = "splitter1";
            this.splitter1.Size = new System.Drawing.Size(478, 3);
            this.splitter1.TabIndex = 7;
            this.splitter1.TabStop = false;
            // 
            // FormServiceHost
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(492, 355);
            this.Controls.Add(this.reconfigButton);
            this.Controls.Add(this.shutDownButton);
            this.Controls.Add(this.clearButton);
            this.Controls.Add(this.freezeCheck);
            this.Controls.Add(this.statusBar);
            this.Controls.Add(this.stopButton);
            this.Controls.Add(this.startButton);
            this.Controls.Add(this.panel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(150, 150);
            this.Name = "FormServiceHost";
            this.Text = "FormServiceHost";
            this.Closing += new System.ComponentModel.CancelEventHandler(this.FormServiceHost_Closing);
            this.Load += new System.EventHandler(this.FormServiceHost_Load);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.ResumeLayout(false);

        }
        #endregion

        //---------------------------------------------------------------------
        // IServiceHost implementations

        /// <summary>
        /// Initializes the service user interface.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <param name="service">The service to associate with this instance.</param>
        /// <param name="logProvider">The optional system log provider (or <c>null</c>).</param>
        /// <param name="start"><c>true</c> to start the service.</param>
        public void Initialize(string[] args, IService service, ISysLogProvider logProvider, bool start)
        {
            this.logProvider = logProvider;
            this.args        = args;
            this.service     = service;
            this.autoStart   = start;
        }

        /// <summary>
        /// Returns a value indicating how the service will be started.
        /// </summary>
        public StartAs StartedAs
        {
            get { return StartAs.Form; }
        }

        /// <summary>
        /// Returns the service instance managed by the host.
        /// </summary>
        public IService Service
        {
            get { return service; }
        }

        /// <summary>
        /// Handles marshalled <see cref="IService.State" /> calls.
        /// </summary>
        /// <param name="message"></param>
        private void OnStatus(string message)
        {

            statusBar.Text = message;
        }

        /// <summary>
        /// Sets the status text to the message passed.
        /// </summary>
        /// <param name="message">The message.</param>
        public void SetStatus(string message)
        {
            ApplicationHost.Invoke(this, onStatus, new object[] { message });
        }

        /// <summary>
        /// Sets the status text to the message built by formatting the arguments
        /// passed.  This uses the same formatting conventions as string.Format().
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        public void SetStatus(string format, params object[] args)
        {
            SetStatus(string.Format(format, args));
        }

        /// <summary>
        /// Handles marshalled <see cref="Log(string)" /> calls.
        /// </summary>
        /// <param name="message"></param>
        private void OnLog(string message)
        {
            console.Write(message);
        }

        /// <summary>
        /// Writes the message passed to a log area of the user interface.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Log(string message)
        {
            ApplicationHost.Invoke(this, onLog, new object[] { message });
        }

        /// <summary>
        /// Writes the  to the message built by formatting the arguments
        /// passed to the log area of the user interface.  This uses the same 
        /// formatting conventions as string.Format().
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        public void Log(string format, params object[] args)
        {
            console.Write(format, args);
        }

        /// <summary>
        /// Called by a service in ServiceState.Shutdown mode when the last
        /// user disconnects from the service.  The service host will then
        /// typically call the service's <see cref="IService.Stop" /> method.
        /// </summary>
        /// <param name="service">The service completing shutdown.</param>
        public void OnShutdown(IService service)
        {
            SysLog.LogInformation("Shutdown complete.  Now stopping...");

            try
            {
                service.Stop();
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }

            SysLog.LogInformation("Status: {0}", service.State);
        }

        //---------------------------------------------------------------------
        // Event handlers

        private void FormServiceHost_Load(object sender, System.EventArgs args)
        {
            if (logProvider != null)
            {
                SysLog.LogProvider = logProvider;
            }
            else
            {
                SysLog.LogProvider = new ServiceSysLogProvider(this);
            }

            this.console                = new ConsoleUI(logBox);
            this.console.Frozen         = false;
            this.onStatus               = new StringParamDelegate(OnStatus);
            this.onLog                  = new StringParamDelegate(OnLog);
            this.Text                   = service.DisplayName;
            this.startButton.Enabled    = true;
            this.shutDownButton.Enabled = false;
            this.stopButton.Enabled     = false;
            this.reconfigButton.Enabled = service.IsConfigureImplemented;

            if (autoStart)
                startButton_Click(null, null);
            else
                SetStatus(service.State.ToString());

            var rc       = this.ClientRectangle;
            var rLog     = logBox.Bounds;

            rightBorder  = rc.Right - rLog.Right;
            bottomBorder = rc.Bottom - rLog.Bottom;

            onUITimer    = new UITimerDelegate(OnUITimer);
            timer        = new GatedTimer(new TimerCallback(OnTimer), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        private void startButton_Click(object sender, System.EventArgs args)
        {
            try
            {
                SysLog.LogInformation("Starting...");

                try
                {
                    service.Start(this, this.args);
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }

                SetStatus(service.State.ToString());
                SysLog.LogInformation("Status: {0}", service.State);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }

            startButton.Enabled    = false;
            shutDownButton.Enabled = true;
            stopButton.Enabled     = true;
        }

        private void shutDownButton_Click(object sender, System.EventArgs args)
        {
            try
            {
                SysLog.LogInformation("Shutdown...");
                service.Shutdown();
                SetStatus(service.State.ToString());
                SysLog.LogInformation("Status: {0}", service.State);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        private void stopButton_Click(object sender, System.EventArgs args)
        {
            try
            {
                SysLog.LogInformation("Stopping...");
                SetStatus("Stopping...");
                service.Stop();
                SysLog.LogInformation("Status: {0}", service.State);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }

            SetStatus(service.State.ToString());

            startButton.Enabled    = true;
            shutDownButton.Enabled = false;
            stopButton.Enabled     = false;
        }

        private void reconfigButton_Click(object sender, System.EventArgs args)
        {
            try
            {
                service.Configure();
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        private void clearButton_Click(object sender, System.EventArgs args)
        {
            console.Clear();
        }

        private void FormServiceHost_Closing(object sender, System.ComponentModel.CancelEventArgs args)
        {
            switch (service.State)
            {
                case ServiceState.Starting:
                case ServiceState.Running:
                case ServiceState.Shutdown:

                    try
                    {
                        service.Stop();
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e);
                    }
                    break;
            }

            SysLog.LogProvider = null;

            if (timer != null)
            {
                timer.Dispose();
                timer = null;
            }

            Environment.Exit(0);
        }

        /// <summary>
        /// Handles the timer event on the thread pool thread by marshalling
        /// a call to the UI thread.
        /// </summary>
        /// <param name="state"></param>
        private void OnTimer(object state)
        {
            ServiceState    serviceState = service.State;
            string          status       = service.DisplayStatus;

            ApplicationHost.Invoke(this, onUITimer, new object[] { serviceState, status });
        }

        /// <summary>
        /// Handles the UI timer events.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="status"></param>
        private void OnUITimer(ServiceState state, string status)
        {
            bool enableStart    = false;
            bool enableStop     = false;
            bool enableShutdown = false;

            SetStatus(state.ToString());

            switch (state)
            {
                case ServiceState.Stopped:

                    enableStart = true;
                    SysLog.Flush();
                    break;

                case ServiceState.Running:

                    enableStop     = true;
                    enableShutdown = true;
                    break;

                case ServiceState.Shutdown:

                    enableStop = true;
                    break;
            }

            startButton.Enabled    = enableStart;
            stopButton.Enabled     = enableStop;
            shutDownButton.Enabled = enableShutdown;
            displayStatus.Text     = status;
        }

        private void freezeCheck_CheckedChanged(object sender, System.EventArgs args)
        {
            console.Frozen = freezeCheck.Checked;
        }
    }
}
