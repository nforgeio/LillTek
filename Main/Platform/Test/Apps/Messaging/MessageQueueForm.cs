//-----------------------------------------------------------------------------
// FILE:        MessageQueueForm.cs
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
using System.Transactions;
using System.Windows.Forms;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Queuing;

namespace LillTek.Test.Messaging
{
    /// <summary>
    /// The Query Client test form
    /// </summary>
    public partial class MessageQueueForm : Form, ITestForm
    {
        private bool        running = false;
        private DateTime    startTime;
        private long        startTimer;
        private long        cTotal;
        private bool        status;
        private Thread      bkThread;
        private string      queueEP;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MessageQueueForm()
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
            startButton.Enabled  = !running;
            stopButton.Enabled   = running;
            queueEPLabel.Enabled = !running;
            queueEPBox.Enabled   = !running;
        }

        /// <summary>
        /// Called periodically by the main form allowing statistics to
        /// be rendered.  This will be called on the UI thread.
        /// </summary>
        public void OnTimer()
        {
            if (!running)
            {
                statusBox.Text = string.Empty;
                return;
            }

            StringBuilder   sb   = new StringBuilder();
            TimeSpan        time = HiResTimer.CalcTimeSpan(startTimer);
            long            cTotal;

            cTotal = Interlocked.Read(ref this.cTotal);

            sb.AppendFormat("Messages: {0}\r\n", cTotal);
            sb.AppendFormat("Status:   {0}\r\n", status ? "OK" : "FAILURE");

            statusBox.Text = sb.ToString();

            startTimer = HiResTimer.Count;
        }

        //---------------------------------------------------------------------
        // Background thread

        /// <summary>
        /// The background thread.
        /// </summary>
        private void BkThread()
        {
            long startCount;

            while (true)
            {
                startCount = HiResTimer.Count;

                try
                {
                    // Sent 100 messages to a queue.

                    using (var queue = new MsgQueue(MainForm.Router))
                    {
                        for (int i = 0; i < 100; i++)
                        {
                            queue.EnqueueTo(queueEP, new QueuedMsg(i));
                            Interlocked.Increment(ref cTotal);
                        }
                    }

                    // Read the 100 messages back from the queue within a transaction
                    // and verify that we got all of them.

                    using (var queue = new MsgQueue(MainForm.Router))
                    {
                        var found = new bool[100];

                        for (int i = 0; i < 50; i++)
                        {
                            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew))
                            {
                                found[(int)queue.DequeueFrom(queueEP).Body] = true;
                                Interlocked.Increment(ref cTotal);

                                found[(int)queue.DequeueFrom(queueEP).Body] = true;
                                Interlocked.Increment(ref cTotal);

                                scope.Complete();
                            }
                        }

                        for (int i = 0; i < found.Length; i++)
                            if (!found[i])
                                status = false;
                    }
                }
                catch
                {
                    status = false;
                }
            }
        }

        //---------------------------------------------------------------------
        // Form event handlers

        private void MessageQueueForm_Load(object sender, EventArgs args)
        {
            SetUIState();
            queueEPBox.Text = Helper.NewGuid().ToString("D");
        }

        private void startButton_Click(object sender, EventArgs args)
        {
            if (running || !MainForm.Running)
                return;

            startTime  = SysTime.Now;
            startTimer = HiResTimer.Count;
            cTotal     = 0;
            status     = true;
            queueEP    = queueEPBox.Text.Trim();

            running = true;
            SetUIState();

            bkThread = new Thread(new ThreadStart(BkThread));
            bkThread.Start();
        }

        private void stopButton_Click(object sender, EventArgs args)
        {
            if (!running)
                return;

            bkThread.Abort();
            bkThread.Join();
            bkThread = null;
            Thread.Sleep(1000);

            MainForm.Router.Dispatcher.RemoveTarget(this);
            Thread.Sleep(1000);

            running = false;
            statusBox.Text = string.Empty;
            SetUIState();
        }
    }
}