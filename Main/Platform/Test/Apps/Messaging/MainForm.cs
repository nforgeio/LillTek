//-----------------------------------------------------------------------------
// FILE:        MainForm.cs
// OWNER:       Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Main application form.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Queuing;

namespace LillTek.Test.Messaging
{
    /// <summary>
    /// Main application form.
    /// </summary>
    public partial class MainForm : Form
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The message router instance or null.
        /// </summary>
        public static LeafRouter Router = null;

        /// <summary>
        /// Returns <c>true</c> if the main form is running (MainForm.Router has been started).
        /// </summary>
        public static bool Running
        {
            get { return Router != null; }
        }

        //---------------------------------------------------------------------
        // Instance members

        private List<ITestForm>     testForms = new List<ITestForm>();
        private bool                running   = false;
        private GatedTimer          timer;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Sets the UI state.
        /// </summary>
        private void SetUIState()
        {
            startButton.Enabled = !running;
            stopButton.Enabled  = running;
        }

        /// <summary>
        /// Handles timer callbacks.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void OnTimer(object state)
        {
            Invoke((MethodDelegate)delegate()
            {
                foreach (ITestForm test in testForms)
                    test.OnTimer();
            });
        }

        //---------------------------------------------------------------------
        // Form event handlers

        private void MainForm_Load(object sender, EventArgs args)
        {
            TabPage     tab;
            Form        form;

            // QueryServerForm

            tabBox.TabPages.Add(tab = new TabPage());

            form = new QueryServerForm();
            tab.Text = form.Text;
            tab.Controls.Add(form);
            testForms.Add((ITestForm)form);

            // QueryClientForm

            tabBox.TabPages.Add(tab = new TabPage());

            form = new QueryClientForm();
            tab.Text = form.Text;
            tab.Controls.Add(form);
            testForms.Add((ITestForm)form);

            // MessageQueueForm

            tabBox.TabPages.Add(tab = new TabPage());

            form = new MessageQueueForm();
            tab.Text = form.Text;
            tab.Controls.Add(form);
            testForms.Add((ITestForm)form);

            // ReceiveMsgForm

            tabBox.TabPages.Add(tab = new TabPage());

            form = new MsgReceiveForm();
            tab.Text = form.Text;
            tab.Controls.Add(form);
            testForms.Add((ITestForm)form);

            // Initialize

            SetUIState();
            startButton_Click(null, null);

            timer = new GatedTimer(new TimerCallback(OnTimer), null, TimeSpan.FromSeconds(1));
        }

        private void startButton_Click(object sender, EventArgs args)
        {
            if (running)
                return;

            Router = new LeafRouter();
            Router.Start();

            running = true;
            SetUIState();
        }

        private void stopButton_Click(object sender, EventArgs args)
        {
            if (!running)
                return;

            foreach (ITestForm test in testForms)
                test.Stop();

            Router.Stop();
            Router = null;

            running = false;
            SetUIState();
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs args)
        {
            timer.Dispose();
            stopButton_Click(null, null);
        }
    }
}