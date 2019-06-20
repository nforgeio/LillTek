//-----------------------------------------------------------------------------
// FILE:        MsgReceiveForm.cs
// OWNER:       Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The Query Client test form

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using LillTek.Client;
using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.Test.Messaging
{
    /// <summary>
    /// The Query Client test form
    /// </summary>
    public partial class MsgReceiveForm : Form, ITestForm
    {
        private bool        running = false;
        private ConsoleUI   console;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MsgReceiveForm()
        {
            InitializeComponent();

            TopLevel = false;
            Visible  = true;
            Dock     = DockStyle.Fill;
        }

        /// <summary>
        /// Stops the execution of a test if one is running.
        /// </summary>
        public void Stop()
        {
            stopButton_Click(null, null);
        }

        /// <summary>
        /// Sets the UI state.
        /// </summary>
        private void SetUIState()
        {
            startButton.Enabled   = !running;
            stopButton.Enabled    = running;
            endPointLabel.Enabled = !running;
            endPointBox.Enabled   = !running;
        }

        /// <summary>
        /// Called periodically by the main form allowing statistics to
        /// be rendered.  This will be called on the UI thread.
        /// </summary>
        public void OnTimer()
        {
        }

        private void OnMsg(Msg msg)
        {
            var propMsg = msg as PropertyMsg;

            if (propMsg == null)
                console.Write("Received: {0}", msg.GetType().Name);
            else
                console.Write("Received: {0}: {1}", propMsg.GetType().Name, propMsg._Get("text", "<missing text>"));
        }

        //---------------------------------------------------------------------
        // Form event handlers

        private void QueryClientForm_Load(object sender, EventArgs args)
        {
            console = new ConsoleUI(statusBox);
            SetUIState();
        }

        private void startButton_Click(object sender, EventArgs args)
        {
            if (running || !MainForm.Running)
                return;

            MainForm.Router.Dispatcher.AddLogical(new MsgHandlerDelegate(OnMsg), endPointBox.Text, typeof(Msg), true, null);
            running = true;
            SetUIState();
        }

        private void stopButton_Click(object sender, EventArgs args)
        {
            if (!running)
                return;

            MainForm.Router.Dispatcher.RemoveTarget(this);
            Thread.Sleep(1000);

            running = false;
            statusBox.Text = string.Empty;
            SetUIState();
        }
    }
}